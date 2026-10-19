using Mono.Cecil;
using Mono.Cecil.Cil;

static class PoseStreamChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var campaign=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        var regiment=game.MainModule.Types.Single(t=>t.Name=="Regiment");
        MethodDefinition Method(string name)=>campaign.Methods.Single(m=>m.Name==name);
        bool Calls(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.DeclaringType.Name==owner && call.Name==name);
        string[] FieldNames(MethodDefinition method,OpCode opcode,string owner)=>method.Body.Instructions.Where(i=>i.OpCode==opcode && i.Operand is FieldReference f && f.DeclaringType.Name==owner).Select(i=>((FieldReference)i.Operand).Name).Distinct().ToArray();
        void Require(bool valid,string failure){if(!valid)throw new Exception(failure);}

        // These compiled integration checks complement the pure history/rules tests:
        // incoming poses may update display histories, never native logical positions.
        var forbiddenCalls=new[]{"set_position","set_rotation","SetPosition","SetRotation","SetBlockobjectPosition","SetBlockobjectRotation","ApplyPose","ApplyReplica","RegimentSetPath","SetGameSpeed"}.ToHashSet();
        foreach(string name in new[]{"ReceiveMotion","RecordPose","RecordReliableMotion","VisualOffset","ReplicaDrawPosition","ReplicaDrawBlock"}) {
            var method=Method(name);
            Require(!method.Body.Instructions.Any(i=>i.Operand is MethodReference call && forbiddenCalls.Contains(call.Name)),"Motion presentation mutates native logical positions: "+name);
            Require(!method.Body.Instructions.Any(i=>(i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld) && i.Operand is FieldReference f && (f.DeclaringType.Name=="Regiment" || f.DeclaringType.Name=="GameVars")),"Motion presentation writes native campaign state: "+name);
        }
        Require(regiment.Methods.Any(m=>m.Name=="GetBlockobjectPosition"),"Native visual block-position reader changed.");
        var send=Method("SendMotion");var receive=Method("ReceiveMotion");
        Require(Calls(send,"MpSnapshotRoster","Contains") && Calls(send,"MultiplayerCampaign","KnownMotionParent") && Calls(send,"MultiplayerCampaign","Visible"),"Motion samples bypass acknowledged lifecycle identities or visibility.");
        Require(new[]{"date","baseRevision","units","sequence"}.All(FieldNames(send,OpCodes.Stfld,"MpMessage").Contains),"Motion frame omitted its capture time or lifecycle generation.");
        Require(Calls(receive,"MpMotionRules","Validate") && Calls(receive,"MultiplayerCampaign","KnownMotionParent") &&
            new[]{"alliance","unittyp"}.All(FieldNames(receive,OpCodes.Ldfld,"Regiment").Contains) &&
            FieldNames(receive,OpCodes.Ldfld,"MultiplayerCampaign").Contains("replicaVisibility"),"Received motion lacks generation, parent, side, tier or visibility validation.");
        Require(Calls(Method("LateUpdate"),"MultiplayerCampaign","SendMotion"),"Host visual samples are not captured after native Update.");
        Require(Calls(Method("Handle"),"MultiplayerCampaign","ReceiveMotion"),"Live session does not route motion messages.");
        Require(Calls(Method("VisualOffset"),"MpPoseClock","RenderTime") && Calls(Method("VisualOffset"),"MpPoseHistory","Evaluate"),"Native drawing bypasses timestamped pose interpolation.");

        // A lifecycle snapshot's timestamp must survive asynchronous preparation and
        // transport; guest apply time cannot stand in for the host capture time.
        var dataTick=Method("DataTick");var dataMessage=Method("DataMessage");
        Require(Calls(dataTick,"MultiplayerCampaign","RecordReliableMotion") && FieldNames(dataTick,OpCodes.Ldfld,"MultiplayerCampaign").Contains("incomingCaptureTime"),"Reliable pose history does not use the transmitted host capture time.");
        Require(FieldNames(dataMessage,OpCodes.Stfld,"MultiplayerCampaign").Contains("incomingCaptureTime") && FieldNames(dataMessage,OpCodes.Ldfld,"MpMessage").Contains("date"),"Campaign data offer loses the host capture time.");
        var captured=campaign.NestedTypes.Single(t=>t.Name=="PreparedData");
        Require(captured.Fields.Any(f=>f.Name=="CapturedAt" && f.FieldType.FullName=="System.Single"),"Prepared campaign data does not retain capture time.");
        Require(FieldNames(dataTick,OpCodes.Ldfld,"PreparedData").Contains("CapturedAt") && FieldNames(dataTick,OpCodes.Stfld,"MpMessage").Contains("date"),"Host campaign offer does not publish the captured snapshot time.");
        Require(FieldNames(Method("ClearData"),OpCodes.Stfld,"MultiplayerCampaign").Contains("incomingCaptureTime"),"Campaign timestamp survives session reset.");

        // Picking uses the same temporary symbol positions as rendering, then always
        // restores their exact local positions even when a native input callback fails.
        var update=Method("Update");var instructions=update.Body.Instructions;
        int pick=instructions.ToList().FindIndex(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="MultiplayerCampaign" && m.Name=="GuestMapInput");
        Require(pick>=0 && instructions.Take(pick).Any(i=>i.Operand is MethodReference m && m.Name=="DrawMotionSymbols"),"Guest picking ignores rendered symbol positions.");
        bool restored=update.Body.ExceptionHandlers.Any(h=>h.HandlerType==ExceptionHandlerType.Finally &&
            instructions.IndexOf(h.TryStart)<=pick && (h.TryEnd==null || pick<instructions.IndexOf(h.TryEnd)) &&
            instructions.Skip(instructions.IndexOf(h.HandlerStart)).Take((h.HandlerEnd==null?instructions.Count:instructions.IndexOf(h.HandlerEnd))-instructions.IndexOf(h.HandlerStart))
                .Any(i=>i.Operand is MethodReference m && m.Name=="RestoreMotionSymbols"));
        Require(restored,"Guest picking can leave visual offsets installed after a native callback failure.");
        var draw=Method("DrawMotionSymbols");
        var roots=draw.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr).Select(i=>(string)i.Operand).ToHashSet();
        Require(new[]{"UnitSymbol","Flag","Flag2","unitnametext"}.All(roots.Contains) && !draw.Body.Instructions.Any(i=>i.Operand is MethodReference call && new[]{"SetPosition","SetRotation","SetParent","ApplyPose"}.Contains(call.Name)),"Visual symbol offsets escape their explicit drawing children.");

        Require(Calls(Method("End"),"MultiplayerCampaign","ClearWorld") && Calls(Method("ClearWorld"),"MultiplayerCampaign","ClearMotion") && Calls(Method("ClearMotion"),"MultiplayerCampaign","ClearPoseStream"),"Ending a session leaves pose histories or rendered offsets behind.");
        var reset=Method("ClearPoseStream");
        Require(new[]{"lastMotionSend","sentMotion","previousMotionPaused","motionSequence","receivedMotionSequence","appliedMotionRevision"}.All(FieldNames(reset,OpCodes.Stfld,"MultiplayerCampaign").Contains) && Calls(reset,"MpPoseClock","Clear"),"Session reset leaves a motion generation, timer or clock anchor behind.");
        Require(reset.Body.Instructions.Count(i=>i.Operand is MethodReference m && m.Name=="Clear" && m.DeclaringType.Name=="Dictionary`2")>=2,"Session reset retains old sent or received pose identities.");
        Console.WriteLine("Native pose stream: host capture timestamps, acknowledged visible identities, display-only histories, exception-safe rendered-symbol picking and session cleanup verified in compiled integration. Logical transforms remain authoritative.");
    }
}
