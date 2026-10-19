using Mono.Cecil;
using Mono.Cecil.Cil;

static class PresentationFenceChecks
{
    public static void Run(string runtimePath)
    {
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var mp=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Method(string name)=>mp.Methods.Single(m=>m.Name==name);
        int CallIndex(MethodDefinition method,string name)=>method.Body.Instructions.ToList().FindIndex(i=>i.Operand is MethodReference m && m.Name==name);
        foreach(string name in new[]{"ApplyLifecycleData","ApplyNaval"}) {
            var method=Method(name);
            if(CallIndex(method,"RefreshReplicaRosterUi")>=0 || !method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="rosterUiPending"))
                throw new Exception("Structural state adapter must defer native roster UI: "+name);
        }
        var data=Method("DataTick");int redraw=CallIndex(data,"RefreshReplicaPresentation");
        if(redraw<CallIndex(data,"ApplyWorldAfterUnits") || redraw<CallIndex(data,"ApplyDispatches") || redraw>CallIndex(data,"VerifyAppliedScalars") || redraw>CallIndex(data,"AuditNativeMap"))
            throw new Exception("Roster redraw must follow native reference application and precede strict readback.");
        var fence=runtime.MainModule.Types.Single(t=>t.Name=="MpPresentationFence").Methods.Single(m=>m.Name=="Refresh");
        if(!fence.Body.ExceptionHandlers.Any(e=>e.HandlerType==ExceptionHandlerType.Finally) || !fence.Body.ExceptionHandlers.Any(e=>e.HandlerType==ExceptionHandlerType.Catch))
            throw new Exception("Native UI failure isolation must retain unconditional state restoration.");
        if(CallIndex(Method("RefreshReplicaPresentation"),"Refresh")<0 || CallIndex(Method("ExecuteRecruit"),"RefreshReplicaRosterUi")<0)
            throw new Exception("Guest display fence must not alter the host recruitment refresh.");
        Console.WriteLine("Native roster presentation boundary: references apply before redraw; display exceptions restore authority before strict readback; host recruitment retains native refresh.");
    }
}
