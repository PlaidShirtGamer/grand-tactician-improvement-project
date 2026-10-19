using Mono.Cecil;
using Mono.Cecil.Cil;

static class RecruitPreviewGraphChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        IEnumerable<TypeDefinition> Types(TypeDefinition type)=>new[]{type}.Concat(type.NestedTypes.SelectMany(Types));
        var nativeTypes=game.MainModule.Types.SelectMany(Types).ToArray();
        MethodDefinition Native(string owner,string name)=>nativeTypes.Single(t=>t.Name==owner).Methods.Single(m=>m.Name==name);
        var mp=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Bridge(string name)=>mp.Methods.Single(m=>m.Name==name);
        bool IsCall(Instruction instruction,string owner,string name)=>instruction.Operand is MethodReference call && call.DeclaringType.Name==owner && call.Name==name;
        bool Calls(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>IsCall(i,owner,name));
        bool Text(MethodDefinition method,string value)=>method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand==value);
        int Local(Instruction instruction)=>instruction.OpCode.Code switch {
            Code.Ldloc_0 or Code.Stloc_0=>0,Code.Ldloc_1 or Code.Stloc_1=>1,Code.Ldloc_2 or Code.Stloc_2=>2,Code.Ldloc_3 or Code.Stloc_3=>3,
            Code.Ldloc or Code.Ldloc_S or Code.Stloc or Code.Stloc_S=>((VariableDefinition)instruction.Operand).Index,_=>-1};
        int checks=0;
        void Check(bool condition,string failure){if(!condition)throw new Exception(failure);checks++;}

        var tooltip=Native("Regiment","GetUnitTooltipInfo");
        Check(tooltip.Parameters.Count==4 && tooltip.Parameters.All(p=>p.ParameterType.MetadataType==MetadataType.Boolean) && tooltip.Parameters[0].Name=="hierarchyonly" && tooltip.ReturnType.Name=="UnitTooltipInfo",
            "Recruit preview native tooltip signature changed.");
        Check(Text(tooltip,"Units") && tooltip.Body.Instructions.Count(i=>IsCall(i,"Transform","get_parent"))>=2,
            "Native tooltip no longer walks parent transforms to its literal Units sentinel; review preview context.");
        var observedNull=tooltip.Body.Instructions.SingleOrDefault(i=>i.Offset==0x1ea);
        Check(observedNull!=null && IsCall(observedNull,"Component","get_gameObject") && IsCall(observedNull.Previous,"Transform","get_parent"),
            "Observed native tooltip null at IL_01ea no longer maps to the unguarded parent/gameObject access.");
        var hierarchy=tooltip.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldarg_1);
        var branch=hierarchy.Next;
        Check((branch.OpCode==OpCodes.Brfalse || branch.OpCode==OpCodes.Brfalse_S) && branch.Operand is Instruction end &&
            tooltip.Body.Instructions.Any(i=>i.Offset>branch.Offset && i.Offset<end.Offset && i.OpCode==OpCodes.Ret) &&
            tooltip.Body.Instructions.Where(i=>i.Offset<end.Offset).All(i=>!IsCall(i,"Regiment","UpdateGroupStrength") && !IsCall(i,"Regiment","UpdateAllAttachedUnits")),
            "Hierarchy-only native tooltip must return before aggregate or attachment calculations.");
        foreach(var caller in new[]{Native("CampaignMainPanel","UpdateUnitInfoPanel"),Native("Commander","GetTooltipInfo")}) {
            var calls=caller.Body.Instructions.Where(i=>IsCall(i,"Regiment","GetUnitTooltipInfo")).ToArray();
            Check(calls.Length==1 && calls[0].Previous?.OpCode==OpCodes.Ldc_I4_1 && calls[0].Previous.Previous?.OpCode==OpCodes.Ldc_I4_1 &&
                calls[0].Previous.Previous.Previous?.OpCode==OpCodes.Ldc_I4_0 && calls[0].Previous.Previous.Previous.Previous?.OpCode==OpCodes.Ldc_I4_1,
                "Recruiting UI no longer passes hierarchyonly:true to the native tooltip: "+caller.FullName);
        }

        bool DescendantScan(MethodDefinition method)=>method.Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod call &&
            call.Name=="GetComponentsInChildren" && call.GenericArguments.Count==1 && call.GenericArguments[0].Name=="Regiment" && i.Previous?.OpCode==OpCodes.Ldc_I4_1);
        var attachments=Native("Regiment","UpdateAllAttachedUnits");var roster=Native("BattleUnits","UpdateUnitList");
        Check(DescendantScan(attachments) && attachments.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="allattachedunits"),
            "Native attachment cache no longer derives from the inclusive descendant scan.");
        Check(DescendantScan(roster) && roster.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stsfld && i.Operand is FieldReference f && f.Name=="completeunitlist") &&
            !roster.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="dummyregiment"),
            "Native full roster scan contract changed; review dummy exclusion.");
        Check(Text(Native("CampaignMainPanel","UpdateUnitSelectionList"),"Battlemap/Units"),
            "Native military selection lists no longer scan the canonical campaign Units hierarchy.");
        Check(nativeTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).Any(m=>Text(m,"Units") && Calls(m,"GameObject","Find")),
            "Native bare Units lookup contract changed; re-evaluate duplicate-anchor ambiguity.");

        var isolate=Bridge("IsolateRecruitPreview");
        Check(Calls(isolate,"Transform","SetParent") && isolate.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldsfld && i.Operand is FieldReference f && f.Name=="instance") &&
            !isolate.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="unitfolder") && !Text(isolate,"Units") &&
            !isolate.Body.Instructions.Any(i=>i.OpCode==OpCodes.Newobj && i.Operand is MethodReference c && c.DeclaringType.Name=="GameObject"),
            "Recruit preview isolation must use the MP object, without a second global Units anchor.");
        foreach(string name in new[]{"ReplicaRecruitPreview","GuestRecruitPreviewAction","RefreshRecruitPreviewRosterUi","WithRecruitPreviewCommander"})
            Check(Calls(Bridge(name),"MultiplayerCampaign","IsolateRecruitPreview"),"Native recruiting redraw no longer preserves isolated preview ownership: "+name);

        var context=Bridge("ReplicaUnitTooltip");
        Check(context.ReturnType.FullName==tooltip.ReturnType.FullName && Text(context,"GTIP_Original_ReplicaUnitTooltip") && Text(context,"unittorecruitfor") &&
            context.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="nativePreview") &&
            Calls(context,"MultiplayerCampaign","ReachesNativeUnitRoot"),
            "Preview tooltip context must target only the local dummy and invoke the preserved native getter with a validated native ancestor.");
        var restore=context.Body.ExceptionHandlers.SingleOrDefault(e=>e.HandlerType==ExceptionHandlerType.Finally);
        Check(restore!=null && context.Body.Instructions.Any(i=>i.Offset>=restore.TryStart.Offset && i.Offset<restore.TryEnd.Offset && IsCall(i,"Transform","SetParent")) &&
            context.Body.Instructions.Any(i=>i.Offset>=restore.HandlerStart.Offset && (restore.HandlerEnd==null || i.Offset<restore.HandlerEnd.Offset) && IsCall(i,"Transform","SetParent")),
            "Temporary native tooltip parenting must restore the isolated parent in a compiled finally block.");
        var priorParent=context.Body.Instructions.Last(i=>i.Offset<restore!.TryStart.Offset && IsCall(i,"Transform","get_parent"));
        int priorLocal=Local(priorParent.Next);
        Check(priorLocal>=0 && context.Body.Instructions.Any(i=>i.Offset>=restore!.HandlerStart.Offset && (restore.HandlerEnd==null || i.Offset<restore.HandlerEnd.Offset) &&
            IsCall(i,"Transform","SetParent") && i.Previous?.OpCode==OpCodes.Ldc_I4_1 && Local(i.Previous.Previous)==priorLocal),
            "Tooltip finally must restore the exact captured prior parent, not the native parent used for rendering.");
        Check(context.Body.Instructions.Any(i=>i.Offset<restore!.TryStart.Offset && IsCall(i,"Transform","get_parent")) &&
            !context.Body.Instructions.Any(i=>i.Operand is MethodReference call && new[]{"UpdateUnitList","UpdateAllAttachedUnits","UpdateOOBs"}.Contains(call.Name)) &&
            !context.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.DeclaringType.Name=="Regiment"),
            "Tooltip context may not replace prior ownership, populate native caches or alter authoritative regiment fields.");
        var commander=Bridge("WithRecruitPreviewCommander");
        var commandRestore=commander.Body.ExceptionHandlers.SingleOrDefault(e=>e.HandlerType==ExceptionHandlerType.Finally);
        Check(commandRestore!=null && commander.Body.Instructions.Any(i=>i.Offset>=commandRestore.HandlerStart.Offset && (commandRestore.HandlerEnd==null || i.Offset<commandRestore.HandlerEnd.Offset) &&
            i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="currentcommand"),
            "Preview commander rendering must always restore the authoritative officer command reference.");
        Console.WriteLine($"Native recruiting preview graph: {checks} checks; observed parent null, hierarchy-only UI calls, inclusive native roster scans and compiled temporary-parent/commander restoration verified. No Unity gameplay exercised.");
    }
}
