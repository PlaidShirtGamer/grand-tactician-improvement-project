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
        var closeComponent=game.MainModule.Types.Single(t=>t.Name=="ClosePanel");
        var interaction=closeComponent.Methods.Single(m=>m.Name=="CheckInteraction");
        Check(Calls(interaction,"GetKeyDown") && Calls(interaction,"GetMouseButtonDown") &&
            interaction.Body.Instructions.Count(i=>i.Operand is MethodReference m && m.Name=="DoClosePanel")==2 &&
            interaction.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="key_showmenu") &&
            interaction.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="ThisButtonTooltip") &&
            interaction.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="mouseonobject"),
            "The shipped ClosePanel's mouse-hover and native menu-key producers must both enter DoClosePanel.");
        var nativeClose=closeComponent.Methods.Single(m=>m.Name=="DoClosePanel");
        Check(Calls(nativeClose,"GetPanelFromGameObject") && Calls(nativeClose,"HidePanel") && Calls(nativeClose,"OpenClosePanelStatic") &&
            new[]{"PanelHandlerRef","PanelsToClose","ButtonsToDeactivate"}.All(name=>nativeClose.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name==name)),
            "The actual newspaper X producer must retain its registered-panel hide and native target/button arrays.");
        Check(Calls(nativeClose,"SetState") && Calls(nativeClose,"Blink") && new[]{"mouseonobject","mouseonobject2"}.All(name=>
            nativeClose.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name==name && i.Previous.OpCode==OpCodes.Ldc_I4_0)),
            "Native ClosePanel cleanup must continue resetting its linked buttons, blinking and both hover flags.");
        var producer=bridge.Methods.Single(m=>m.Name=="ReplicaNewspaperClosePanel");
        int guard=producer.Body.Instructions.ToList().FindIndex(i=>i.Operand is MethodReference m && m.Name=="GuestNativeNewspaperCloseAllowed");
        int acknowledge=producer.Body.Instructions.ToList().FindIndex(i=>i.Operand is MethodReference m && m.Name=="AcknowledgeGuestNewspaper");
        int original=producer.Body.Instructions.ToList().FindIndex(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,"GTIP_Original_ReplicaNewspaperClosePanel"));
        Check(guard>=0 && acknowledge>guard && original>acknowledge && Calls(producer,"Call") && Calls(producer,"IsChildOf") && Calls(producer,"Contains") &&
            new[]{"PanelHandlerRef","PanelsToClose","cpanel_newspaper"}.All(name=>producer.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name==name)),
            "The native producer wrapper must validate the canonical owner and accepted input, acknowledge, then retain original ClosePanel cleanup.");
        var eligibility=bridge.Methods.Single(m=>m.Name=="GuestNativeNewspaperCloseAllowed");
        Check(Calls(eligibility,"get_BlocksInput") && Calls(eligibility,"get_GuestDialogOpen") && Calls(eligibility,"NewspaperPanelOpen") &&
            Calls(eligibility,"get_activeInHierarchy") && new[]{"cpanel_optionspanel","panel_mainmenusmall","cpanel_newspaper"}.All(name=>
                eligibility.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name==name)),
            "A hidden paper or covering session/native modal must suppress the canonical newspaper producer.");
        var graphicPredicates=eligibility.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldftn && i.Operand is MethodReference).Select(i=>((MethodReference)i.Operand).Resolve()).ToArray();
        Check(Calls(eligibility,"GetKeyDown") && Calls(eligibility,"GetMouseButtonDown") && Calls(eligibility,"RaycastAll") && Calls(eligibility,"FirstOrDefault") && Calls(eligibility,"IsChildOf") &&
            graphicPredicates.Any(m=>m.Body.Instructions.Any(i=>i.OpCode==OpCodes.Isinst && i.Operand is TypeReference t && t.FullName=="UnityEngine.UI.GraphicRaycaster")) &&
            !eligibility.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="mouseonobject"),
            "Canonical close eligibility must accept the actual native menu key or current first graphic owned by that control, never its potentially stale hover flag.");
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
