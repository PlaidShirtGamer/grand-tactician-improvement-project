using Mono.Cecil;
using Mono.Cecil.Cil;

static class NewspaperPresentationChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var native=game.MainModule.Types.Single(t=>t.Name=="Newspaper");
        var bridge=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        bool Calls(MethodDefinition method,string name)=>method.HasBody && method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name==name);
        bool Literal(MethodDefinition method,string value)=>method.HasBody && method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,value));
        int checks=0;
        void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
        var show=native.Methods.Single(m=>m.Name=="UpdateShow");
        var suppress=show.Body.Instructions.Single(i=>i.Operand is FieldReference f && f.Name=="debug_deactivatenewspapermessages");
        var showPanel=show.Body.Instructions.First(i=>i.Operand is MethodReference m && m.Name=="ShowPanel");
        Check(suppress.Offset<showPanel.Offset && show.Body.Instructions.Any(i=>i.Offset>suppress.Offset && i.Offset<showPanel.Offset && i.OpCode==OpCodes.Ret),
            "Native UpdateShow no longer silently skips a local disabled newspaper before displaying a panel.");
        var common=native.Methods.Single(m=>m.Name=="UpdateCommon");
        Check(Calls(show,"UpdateCommon") && common.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="runningnumber"),
            "Native edition counter no longer advances inside content preparation; review delivery retries.");
        Check(Calls(native.Methods.Single(m=>m.Name=="OnDisable"),"Close"),
            "Native newspaper deactivation no longer calls the shared Close path; review implicit-hide handling.");
        var tooltipClose=game.MainModule.Types.Single(t=>t.Name=="ButtonTooltip").Methods.Single(m=>m.Name=="CheckButtonClose");
        Check(tooltipClose.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="CloseThisPanelIfPressed") && Calls(tooltipClose,"OpenClosePanelStatic"),
            "Native close-toolbutton no longer uses the generic window-close intent rather than Newspaper.Close.");
        var closeIntent=bridge.Methods.Single(m=>m.Name=="GuestNewspaperCloseIntent");
        Check(Calls(bridge.Methods.Single(m=>m.Name=="ReplicaLocalClose"),"GuestNewspaperCloseIntent") &&
            Calls(closeIntent,"AcknowledgeGuestNewspaper") && Calls(closeIntent,"ReplicaNewspaperClose"),
            "The actual guest close-control router must acknowledge the newspaper before native cleanup rather than only fade its window.");
        var handler=game.MainModule.Types.Single(t=>t.Name=="PanelHandler");
        Check(handler.Methods.Any(m=>Literal(m,"UI/Newspaper") && Calls(m,"GetComponent")),
            "Native PanelHandler no longer binds the canonical UI/Newspaper component.");
        Check(native.Fields.Single(f=>f.Name=="runningnumber").FieldType.FullName=="System.Int32" &&
            native.Fields.Single(f=>f.Name=="newspapershown").IsStatic,
            "Native newspaper completion fields changed ABI.");
        var policy=game.MainModule.Types.Single(t=>t.Name=="Policies").Methods.Single(m=>m.Name=="LaunchNewspaperMessage");
        Check(Calls(policy,"Show") && Calls(native.Methods.Single(m=>m.Name=="CheckHistoricalEvents"),"Show") &&
            Calls(native.Methods.Single(m=>m.Name=="UpdateNewspaperQueue"),"UpdateShow"),
            "Native policy and historical event producers no longer share the generic newspaper display boundary.");
        var refresh=bridge.Methods.Single(m=>m.Name=="RefreshGuestNewspaper");
        Check(Calls(refresh,"GetComponent") && !Calls(refresh,"FindObjectsOfTypeAll") &&
            refresh.Body.Instructions.Any(i=>i.Operand is MethodReference m && m is GenericInstanceMethod g && g.GenericArguments.Any(a=>a.Name=="Newspaper")),
            "Guest newspaper rendering must bind the component owned by its actual panel.");
        Check(Calls(bridge.Methods.Single(m=>m.Name=="ApplyNewspaper"),"RefreshGuestNewspaper") &&
            Calls(bridge.Methods.Single(m=>m.Name=="ReplicaUiLate"),"RefreshGuestNewspaper") &&
            Calls(bridge.Methods.Single(m=>m.Name=="End"),"ClearNewspaperPresentation"),
            "Newspaper receipt, deferred visibility check and session cleanup must all remain integrated.");
        Console.WriteLine($"Native newspaper contract: {checks} checks; generic producers, local suppression, edition counting, implicit closes and panel-owned delivery verified.");
    }
}
