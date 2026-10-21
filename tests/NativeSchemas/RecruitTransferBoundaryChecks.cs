using Mono.Cecil;
using Mono.Cecil.Cil;

static class RecruitTransferBoundaryChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var native=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
        var nativeTypes=native.MainModule.Types.SelectMany(Types).ToArray();
        MethodDefinition Native(string owner,string name)=>nativeTypes.Single(t=>t.Name==owner).Methods.Single(m=>m.Name==name);
        var mp=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Bridge(string name)=>mp.Methods.Single(m=>m.Name==name);
        bool Calls(MethodDefinition m,string owner,string name)=>m.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.DeclaringType.Name==owner && c.Name==name);
        bool Field(MethodDefinition m,string name)=>m.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name==name);
        int checks=0;
        void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;}

        Check(Calls(Native("BattleUnits","CreateUnitDefault"),"ToolsUnitList","ChangeSelection") && Calls(Native("ToolsUnitList","ChangeSelection"),"ToolsUnitList","CreateList"),
            "Native draft creation no longer rebuilds the shared military list; review the recruiting simulation boundary.");
        Check(Calls(Native("ToolsUnitList","CreateList"),"ToolsUnitList","AddListLine") && Calls(Native("OOB","GetUnitIcons"),"Regiment","GetUnitTooltipInfo"),
            "Native military list/icon drawing no longer uses the audited full unit-tooltip path.");
        Check(Calls(Native("ToolsUnitList","AddListLine"),"ToolsListLine",".ctor") && Calls(Native("ToolsListLine",".ctor"),"ToolsListLine","UpdateContent") &&
            Calls(Native("ToolsListLine","UpdateContent"),"OOB","GetUnitIcons"),
            "Shared military list reconstruction no longer paints every line through its native full-tooltip icon lookup.");
        Check(Calls(Native("CampaignMainPanel","UpdateRegimentDummy"),"CampaignMainPanel","UpdateUnitData") && Calls(Native("CampaignMainPanel","UpdateUnitData"),"CampaignMainPanel","UpdateOOBs") &&
            Calls(Native("OOB","AddUnitIcon"),"Regiment","GetUnitTooltipInfo"),
            "Native draft panel redraw no longer reaches existing formation tooltips through its OOB refresh.");
        var tooltip=Native("Regiment","GetUnitTooltipInfo");
        var aggregateCalls=tooltip.Body.Instructions.Where(i=>i.Operand is MethodReference c && c.Name=="UpdateGroupStrength").ToArray();
        Check(aggregateCalls.Length>0 && aggregateCalls.All(i=>i.Previous.OpCode==OpCodes.Ldnull && i.Previous.Previous.OpCode==OpCodes.Ldc_I4_1 && i.Previous.Previous.Previous.OpCode==OpCodes.Ldc_I4_1),
            "Native full tooltips no longer invoke aggregate calculation with inactive subordinates included.");
        var aggregate=Native("Regiment","UpdateGroupStrength");
        Check(Calls(aggregate,"Regiment","UpdateCampaignUnitStats") && Calls(aggregate,"Regiment","UpdateTransferTime"),
            "Native aggregate refresh no longer reaches campaign statistics and recruitment transfers.");
        Check(Calls(Native("Regiment","UpdateCampaignUnitStats"),"Regiment","UpdateTransferTime"),
            "Native subordinate campaign-stat refresh no longer advances recruitment transfer progress.");
        var transfer=Native("Regiment","UpdateTransferTime");
        Check(Calls(transfer,"Vector3","MoveTowards") && Calls(transfer,"BattleUnits","GetUnitVisibilityStateOnCampaignMap") && Calls(transfer,"Regiment","SetPosition"),
            "Native hidden-unit transfer no longer moves the logical position after advancing its transfer position.");
        Check(Calls(Native("Regiment","SetPosition"),"Transform","set_position"),
            "Native transfer SetPosition no longer writes the logical Unity transform.");
        var identity=Bridge("IsRecruitPreview");
        Check(Field(identity,"ids") && Field(identity,"nativePreview") && Field(identity,"buildingPreview") &&
            identity.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.Name=="ContainsKey"),
            "Compiled recruiting identity must exclude registered authoritative units in both creation and assigned-preview branches.");
        var guard=Bridge("ReplicaGroupStats");
        Check(Calls(guard,"MultiplayerCampaign","get_IsReplica") && Calls(guard,"MultiplayerCampaign","IsRecruitPreview") &&
            guard.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,"GTIP_Original_ReplicaGroupStats")),
            "The native aggregate bridge must enforce the production recruiting identity before invoking its preserved implementation.");
        var restore=Bridge("RestoreRecruitPreviewPanel");
        Check(Field(restore,"ObjectsToDeactivateForRecruiting") && Calls(restore,"GameObject","SetActive") &&
            restore.Body.ExceptionHandlers.Any(e=>e.HandlerType==ExceptionHandlerType.Finally),
            "Recruiting pane redraw must reapply native existing-unit control visibility even after a display failure.");
        Check(Calls(restore,"Regiment","UpdateCommanderPicture") && Calls(Bridge("ReplicaCommanderAssign"),"MultiplayerCampaign","RestoreRecruitPreviewPanel"),
            "Initial and reselected recruiting commanders must refresh their local preview portrait.");
        Console.WriteLine($"Native recruiting transfer boundary: {checks} checks; shared UI aggregate-to-hidden-unit movement chain, registered-unit exclusion, recruiting visibility and preview portraits verified.");
    }
}
