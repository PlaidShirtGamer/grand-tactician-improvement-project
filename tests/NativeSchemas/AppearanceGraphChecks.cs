using Mono.Cecil;
using Mono.Cecil.Cil;

static class AppearanceGraphChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var native=AssemblyDefinition.ReadAssembly(gamePath);
        using var compiled=AssemblyDefinition.ReadAssembly(runtimePath);
        var mp=compiled.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Native(string type,string name)=>native.MainModule.Types.Single(t=>t.Name==type).Methods.Single(m=>m.Name==name);
        MethodDefinition Bridge(string name)=>mp.Methods.Single(m=>m.Name==name);
        bool IsCall(Instruction instruction,string owner,string name)=>instruction.Operand is MethodReference call && call.DeclaringType.Name==owner && call.Name==name;
        bool Calls(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>IsCall(i,owner,name));
        bool Text(MethodDefinition method,string value)=>method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand==value);
        int checks=0;
        void Check(bool valid,string failure){if(!valid)throw new Exception(failure);checks++;}
        var dispatch=Native("PanelHandler","UpdatePanels");
        var instructions=dispatch.Body.Instructions.ToArray();
        foreach(var control in new[]{("476_PrevSoldier","477_NextSoldier"),("477_NextSoldier","475_SwitchRecruitType")}) {
            int start=Array.FindIndex(instructions,i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand==control.Item1);
            int end=Array.FindIndex(instructions,start+1,i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand==control.Item2);
            var body=instructions.Skip(start).Take(end-start).ToArray();
            Check(start>=0 && end>start && body.Any(i=>IsCall(i,"Regiment","AdjustUniformSet")) && body.Any(i=>IsCall(i,"CampaignMainPanel","UpdateUnitInfoPanel")) &&
                body.All(i=>!IsCall(i,"GameObject","get_activeInHierarchy")),"Native existing-unit uniform callback eligibility changed: "+control.Item1);
        }
        var adjust=Native("Regiment","AdjustUniformSet");
        Check(adjust.Body.Instructions.Any(i=>i.OpCode==OpCodes.Newobj && i.Operand is MethodReference ctor && ctor.DeclaringType.Name=="UniformSet") &&
            adjust.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="availableuniformsets"),"Native uniform cycling must retain its native eligibility list.");
        var uniforms=native.MainModule.Types.Single(t=>t.Name=="CampaignMainPanel").NestedTypes.Single(t=>t.Name=="UniformSet").Methods.Single(m=>m.IsConstructor);
        Check(Calls(uniforms,"Policy","GetStatusByPolicyID") && uniforms.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="state"),
            "Native uniform eligibility no longer includes policy and state rules.");
        var panelLookup=uniforms.Body.Instructions.SingleOrDefault(i=>IsCall(i,"GameObject","Find") && i.Previous.OpCode==OpCodes.Ldstr && (string)i.Previous.Operand=="UI/Panel_Main");
        Check(panelLookup!=null && panelLookup.Next.Operand is GenericInstanceMethod getPanel && getPanel.Name=="GetComponent" && getPanel.GenericArguments.Single().Name=="CampaignMainPanel",
            "Native UniformSet no longer unconditionally resolves its panel through the active-only GameObject.Find lookup.");
        Check(new[]{"InfantryImagesUnion","CavalryImagesUnion","ArtilleryImagesUnion","InfantryImagesConfederacy","CavalryImagesConfederacy","ArtilleryImagesConfederacy"}.All(name=>
            uniforms.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name==name)),
            "Native UniformSet no longer uses the resolved panel's original per-side sprite arrays.");
        var panelFallback=Bridge("ReplicaUniformPanel");
        Check(panelFallback.Parameters.Count==1 && panelFallback.Parameters[0].ParameterType.FullName=="UnityEngine.GameObject" && panelFallback.ReturnType.FullName=="UnityEngine.GameObject" &&
            Calls(panelFallback,"MultiplayerCampaign","get_LiveSession") && Text(panelFallback,"campaignmainpanel") && Text(panelFallback,"Panel_Main") && Calls(panelFallback,"NativeAccess","Get"),
            "Inactive native uniform panel resolution must retain native results and use only the live session's correctly named cached panel.");
        Check(!Calls(panelFallback,"GameObject","Find") && !Calls(panelFallback,"GameObject","SetActive") && !Calls(panelFallback,"CampaignMainPanel","OnEnable") &&
            panelFallback.Body.Instructions.All(i=>i.OpCode!=OpCodes.Stfld && i.OpCode!=OpCodes.Stsfld && i.OpCode!=OpCodes.Newobj),
            "Uniform panel fallback must not search for another UI, activate a hidden host panel, or modify native selection state.");
        var attachments=Native("Regiment","UpdateAllAttachedUnits");
        Check(attachments.Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod call && call.Name=="GetComponentsInChildren" && call.GenericArguments.Single().Name=="Regiment" && i.Previous.OpCode==OpCodes.Ldc_I4_1),
            "Native managed regiment attachment lists no longer include inactive subordinates.");
        var picture=Native("CampaignMainPanel","UpdateSoldierPicture");
        Check(Calls(picture,"Palette","GetSelectedColor") && picture.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="paletteactive") &&
            picture.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stelem_Any && i.Operand is TypeReference type && type.Name=="Color"),
            "Native soldier redraw palette-to-unit feedback contract changed.");
        var info=Native("CampaignMainPanel","UpdateUnitInfoPanel");
        foreach(var state in new[]{OpCodes.Ldc_I4_0,OpCodes.Ldc_I4_1})
            Check(info.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="UniformTextObjUnitPanel" &&
                i.Next?.OpCode==state && IsCall(i.Next.Next,"GameObject","SetActive")),"Native uniform label visibility branch changed: "+state);
        Check(info.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_I4 && Equals(i.Operand,476)) && info.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_I4 && Equals(i.Operand,477)) &&
            Calls(info,"PanelHandler","SetButtonsCondition"),"Native info refresh no longer owns the uniform arrow state.");

        var execute=Bridge("ExecuteAppearanceAction");
        Check(execute.Body.Instructions.Where(i=>IsCall(i,"GameObject","get_activeInHierarchy")).All(i=>i.Next.OpCode.FlowControl!=FlowControl.Cond_Branch),
            "Host uniform eligibility incorrectly uses campaign-map activity instead of native management eligibility.");
        Check(Calls(execute,"Regiment","AdjustUniformSet") && Calls(execute,"Palette","GetColorFromIndex") && Calls(execute,"MultiplayerCampaign","RefreshHostAppearance"),
            "Appearance intents must use native eligibility/palette operations and refresh the matching host panel.");
        var feedback=Bridge("RefreshHostAppearance");
        int colorWrite=feedback.Body.Instructions.ToList().FindIndex(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="currentcolorchosen");
        int redraw=feedback.Body.Instructions.ToList().FindIndex(i=>IsCall(i,"CampaignMainPanel","UpdateUnitInfoPanel"));
        Check(colorWrite>=0 && redraw>colorWrite && Text(feedback,"lastusedunit") && !Text(feedback,"GetUsedUnit") &&
            feedback.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="selectedunitreg"),
            "Accepted host color must replace the matching open palette's stale absolute selection before redraw without changing host selection.");
        var render=Bridge("ReplicaSoldierPicture");
        var restore=render.Body.ExceptionHandlers.SingleOrDefault(e=>e.HandlerType==ExceptionHandlerType.Finally);
        Check(Calls(render,"MultiplayerCampaign","PreviewUniformValue") && Calls(render,"Array","Clone") && restore!=null &&
            new[]{"componentcolor","uniformset"}.All(name=>render.Body.Instructions.Any(i=>i.Offset>=restore.HandlerStart.Offset && (restore.HandlerEnd==null || i.Offset<restore.HandlerEnd.Offset) &&
                i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name==name)),"Replica appearance rendering must restore exact authoritative state even if native drawing fails.");
        var preview=Bridge("PreviewExistingUniformStep");
        Check(Calls(preview,"Regiment","AdjustUniformSet") && preview.Body.ExceptionHandlers.Any(e=>e.HandlerType==ExceptionHandlerType.Finally) &&
            Calls(Bridge("GuestRecruitPreviewAction"),"MultiplayerCampaign","PreviewExistingUniformStep"),"Existing-unit visual prediction must use native eligible cycling and leave replica state restored.");
        Check(Text(Bridge("GuestRecruitPreviewAction"),", id=") && Text(execute,"Formation uniform unavailable: id="),
            "Appearance routing/rejection diagnostics lack the actual formation identity.");
        Check(!Calls(Native("WeaponList","AssignWeapon"),"GameObject","get_activeInHierarchy") && Calls(Native("WeaponList","AssignWeapon"),"Weapon","IsWeaponInStock") && Calls(Native("CampaignMainPanel","UpdateUnitNameInputs"),"CampaignMainPanel","GetUsedUnit"),
            "Native equipment retains stock eligibility and naming selects the managed unit independently of its map activity.");
        var management=Bridge("ExecuteNative");
        int commandGate=management.Body.Instructions.ToList().FindIndex(i=>IsCall(i,"MultiplayerCampaign","Controllable"));
        Check(commandGate>0 && !Calls(management,"GameObject","get_activeInHierarchy") &&
            new[]{"unit-name","weapon-assign"}.All(name=>management.Body.Instructions.Take(commandGate).Any(i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand==name)),
            "Inactive managed-unit naming/equipment must precede the separate movement eligibility gate.");
        Check(Calls(Bridge("Controllable"),"GameObject","get_activeInHierarchy"),"Campaign movement must still require an active map unit.");
        Console.WriteLine($"Native appearance graph: {checks} checks; inactive panel constructor dependency, cached lookup isolation, managed regiments, native uniform/label eligibility, palette feedback and rendering restoration verified. No Unity gameplay exercised.");
    }
}
