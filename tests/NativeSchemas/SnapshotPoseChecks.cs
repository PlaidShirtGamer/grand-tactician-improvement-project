using Mono.Cecil;
using Mono.Cecil.Cil;

static class SnapshotPoseChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var mp=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Method(string name)=>mp.Methods.Single(m=>m.Name==name);
        bool Calls(MethodDefinition method,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.Name==name);
        var view=Method("ApplyReplica");
        var forbidden=new[]{"ApplyPose","SetPosition","SetRotation","SetBlockobjectPosition","SetBlockobjectRotation","set_position","set_rotation"}.ToHashSet();
        if(view.Body.Instructions.Any(i=>i.Operand is MethodReference call && forbidden.Contains(call.Name)))throw new Exception("Legacy map view acquired an independent logical-pose writer.");
        if(!Calls(view,"SetActive") || !view.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="replicaVisibility"))throw new Exception("Removing snapshot pose writes must retain native activation and map visibility.");
        if(!view.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="hasLiveData"))throw new Exception("Map view can arrive before authoritative campaign data.");
        var data=Method("DataTick");
        if(!Calls(data,"ApplyMapPoses") || !Calls(data,"AuditNativeMap") || !Calls(Method("ApplyMapPoses"),"ApplyPose"))throw new Exception("Authoritative campaign data lost logical-pose application or readback.");
        var audit=Method("AuditNativeMap");
        if(audit.Body.Instructions.Count(i=>i.OpCode==OpCodes.Ldc_R4 && (float)i.Operand==.1f)!=2 || !Calls(audit,"WriteMapPoseFailure"))throw new Exception("Native map readback tolerance changed or position failure diagnostics are absent.");
        var cache=game.MainModule.Types.Single(t=>t.Name=="Regiment").Fields.Single(f=>f.Name=="currentgameobjectposition");
        if(cache.FieldType.FullName!="UnityEngine.Vector3")throw new Exception("Native position-cache diagnostic bridge changed.");
        Console.WriteLine("Native snapshot pose ownership: legacy map views retain activation/visibility but cannot write logical poses; authoritative data application and unchanged0.1 X/Z readback remain enabled, with expected/actual/ancestor failure diagnostics.");
    }
}
