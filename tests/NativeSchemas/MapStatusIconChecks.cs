using Mono.Cecil;
using Mono.Cecil.Cil;

static class MapStatusIconChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var regiment=game.MainModule.Types.Single(t=>t.Name=="Regiment");
        var campaign=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Native(string type,string name)=>game.MainModule.Types.Single(t=>t.Name==type).Methods.Single(m=>m.Name==name);
        MethodDefinition Runtime(string name)=>campaign.Methods.Single(m=>m.Name==name);
        bool Calls(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name==owner && m.Name==name);
        bool Text(MethodDefinition method,string text)=>method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,text));
        int checks=0;
        void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;}
        var status=Native("Regiment","UpdateUnitIconTexture");
        Check(status.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="entrenchment") &&
            status.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldsfld && i.Operand is FieldReference f && f.Name=="entrenchmentmodellevel1"),
            "Native entrenchment status must still depend on both its transient building reference and level.");
        var readOnly=new[]{Native("Regiment","IsLowOnSupply"),Native("Regiment","GetSiegeAutocalc"),Native("BattlefieldSetup","SearchAutocalcFromMonument"),Native("Autocalc","ContainsUnit")};
        Check(readOnly.All(m=>!m.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld)),
            "Native map status getter dependencies gained campaign or cache writes.");
        Check(Calls(readOnly[1],"BattlefieldSetup","SearchAutocalcFromMonument") && Calls(readOnly[2],"Autocalc","ContainsUnit"),
            "Native status siege lookup no longer follows the audited read-only monument membership path.");
        var dto=runtime.MainModule.Types.Single(t=>t.Name=="MpFormationState");
        Check(dto.Fields.Any(f=>f.Name=="StatusTexture" && f.FieldType.FullName=="System.Int32") && Calls(Runtime("FormationState"),"MultiplayerCampaign","NativeMapStatusIndex"),
            "Formation records no longer capture host-selected native map status indices.");
        var capture=Runtime("NativeMapStatusIndex");
        Check(Text(capture,"GTIP_NativeMapStatusIndex") && capture.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="mapStatusIcons") &&
            Calls(capture,"MultiplayerCampaign","get_IsReplica"),"Guest map-status readback must retain the received host index rather than re-evaluate missing local references.");
        var render=Runtime("ReplicaMapStatusIcon");
        Check(Text(render,"GTIP_Original_ReplicaMapStatusIcon") && Calls(render,"PanelHandler","UpdateButtonPic") &&
            render.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_I4 && Equals(i.Operand,183)),
            "Replica status refresh must retain offline behavior and native map button183 sprite channels.");
        string[] forbidden={"Place","UpdateCampsAndEntrenchments","UpdateGroupStrength","UpdateCampaignUnitStats","SetPosition","SetRotation","SetActive","Initialize"};
        Check(!render.Body.Instructions.Any(i=>i.Operand is MethodReference m && forbidden.Contains(m.Name)) &&
            !render.Body.Instructions.Any(i=>(i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld) && i.Operand is FieldReference f && f.DeclaringType.Name=="Regiment"),
            "Map status presentation must not fabricate transient buildings or mutate native simulation/visibility state.");
        Check(Calls(Runtime("DataTick"),"MultiplayerCampaign","ApplyMapStatus") && Calls(Runtime("ClearWorld"),"MultiplayerCampaign","ClearMapStatus"),
            "Map status selection must follow authoritative application and clear at session teardown.");
        Console.WriteLine($"Native map status icons: {checks} checks; transient-reference dependency, read-only selector graph, host identity capture and presentation-only refresh verified.");
    }
}
