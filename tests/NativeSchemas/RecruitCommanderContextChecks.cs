using Mono.Cecil;
using Mono.Cecil.Cil;

static class RecruitCommanderContextChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
        var nativeTypes=game.MainModule.Types.SelectMany(Types).ToArray();
        MethodDefinition Native(string owner,string name)=>nativeTypes.Single(t=>t.Name==owner).Methods.Single(m=>m.Name==name);
        bool Calls(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.DeclaringType.Name==owner && c.Name==name);
        int checks=0;
        void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
        var radius=Native("Regiment","UnitIsInsideRadius");
        Check(Calls(radius,"Transform","get_parent") && radius.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,"Units")) &&
            radius.Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod c && c.Name=="GetComponent" && c.GenericArguments.Single().Name=="Regiment"),
            "Native commander radius lookup must retain its immediate Regiment-parent / Units-root contract.");
        var parentComponent=radius.Body.Instructions.Single(i=>i.Operand is GenericInstanceMethod c && c.Name=="GetComponent" && c.GenericArguments.Single().Name=="Regiment");
        int Local(Instruction i)=>i.OpCode.Code switch {
            Code.Stloc_0 or Code.Ldloc_0=>0,Code.Stloc_1 or Code.Ldloc_1=>1,Code.Stloc_2 or Code.Ldloc_2=>2,Code.Stloc_3 or Code.Ldloc_3=>3,
            Code.Stloc or Code.Stloc_S or Code.Ldloc or Code.Ldloc_S=>((VariableDefinition)i.Operand).Index,_=>-1};
        int parentLocal=Local(parentComponent.Next);
        Check(parentLocal>=0 && radius.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="commanderrange" && Local(i.Previous)==parentLocal),
            "Native radius getter must dereference commanderrange on the immediate parent's Regiment component.");
        // Mono reports the failing basic block at IL_008f (ldloc.1) in this build;
        // the actual field read is IL_0090. Assert the data flow, not a guessed offset.
        var factor=Native("Commander","GetParentCommanderInfluenceFactor");
        Check(Calls(factor,"Regiment","UnitIsInsideRadius") && Calls(factor,"BattleUnits","GrabNextParentUnit"),
            "Commander parent influence must still read the command radius and immediate native parent.");
        foreach(string stat in new[]{"GetCommanderInitiative","GetCommanderLeadership","GetCommanderAdministration","GetCommanderCunning"})
            Check(Calls(Native("Commander",stat),"Commander","GetParentCommanderInfluenceFactor"),"Commander attribute no longer uses audited parent influence: "+stat);
        var tooltip=Native("Commander","GetTooltipInfo");
        Check(Calls(tooltip,"Commander","GetCommanderInitiative") && Calls(tooltip,"Regiment","GetUnitTooltipInfo") &&
            Calls(Native("BattleUI","UpdateCommanderPanel"),"Commander","GetTooltipInfo") && Calls(Native("CampaignMainPanel","UpdateCommanderData"),"BattleUI","UpdateCommanderPanel"),
            "Native recruiting commander draw no longer follows both the unit tooltip and commander attribute paths.");
        var readOnly=new[]{factor,radius,tooltip,Native("BattleUnits","GrabNextParentUnit"),Native("Commander","GetCommanderInChiefInfluenceFactor"),
            Native("Commander","GetCommanderExperience"),Native("Commander","GetCommanderInitiative"),Native("Commander","GetCommanderLeadership"),
            Native("Commander","GetCommanderAdministration"),Native("Commander","GetCommanderCunning")};
        Check(readOnly.All(m=>!m.Body.Instructions.Any(i=>(i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld) && i.Operand is FieldReference f &&
            new[]{"Regiment","Commander","Alliance","Nation","BattleUnits"}.Contains(f.DeclaringType.Name))),
            "Audited commander stats/tooltip/radius getters unexpectedly write simulation or native roster fields.");
        string[] forbidden={"UpdateUnitList","UpdateAllAttachedUnits","UpdateAllParentUnits","UpdateGroupStrength","GetComponentsInChildren","RecruitUnit","AssignCommando"};
        var displays=readOnly.Concat(new[]{Native("CampaignMainPanel","SwitchCommanderPanel"),Native("CampaignMainPanel","UpdateCommanderData"),Native("CampaignMainPanel","UpdateUnitInfoPanel"),Native("BattleUI","UpdateCommanderPanel")});
        Check(displays.All(m=>!m.Body.Instructions.Any(i=>i.Operand is MethodReference c && forbidden.Contains(c.Name))),
            "The temporary native commander draw unexpectedly scans/rebuilds formation ownership or mutates the roster.");
        var getPosition=Native("Regiment","GetPosition");
        Check(getPosition.Body.Instructions.Where(i=>i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld).All(i=>i.Operand is FieldReference f &&
            f.DeclaringType.Name=="Regiment" && new[]{"lastpositionupdateframe","currentgameobjectposition"}.Contains(f.Name)),
            "Native position getter gained simulation writes beyond its receiver-local display position cache.");
        var context=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign").Methods.Single(m=>m.Name=="WithRecruitPreviewCommander");
        var cleanup=context.Body.ExceptionHandlers.Single(e=>e.HandlerType==ExceptionHandlerType.Finally);
        bool InCleanup(Instruction i)=>i.Offset>=cleanup.HandlerStart.Offset && (cleanup.HandlerEnd==null || i.Offset<cleanup.HandlerEnd.Offset);
        Check(Calls(context,"Transform","get_parent") && Calls(context,"Transform","SetParent") &&
            context.Body.Instructions.Any(i=>InCleanup(i) && i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="currentcommand") &&
            context.Body.Instructions.Any(i=>InCleanup(i) && i.Operand is MethodReference c && c.Name=="SetParent"),
            "Compiled preview commander scope must restore both the exact parent and authoritative command reference in finally.");
        Check(context.Body.Instructions.Any(i=>InCleanup(i) && i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="selectedunitreg") &&
            !context.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.Name=="SelectUnit"),
            "Commander context must prevent GetUsedUnit's null-selection descendant scan and restore the exact prior read reference.");
        Console.WriteLine($"Native recruiting commander context: {checks} checks; observed radius null, full attribute path, read-only native display boundaries and paired finally restoration verified.");
    }
}
