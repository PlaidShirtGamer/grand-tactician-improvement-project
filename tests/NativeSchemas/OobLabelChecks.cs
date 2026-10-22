using Mono.Cecil;
using Mono.Cecil.Cil;

static class OobLabelChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var regiment=game.MainModule.Types.Single(t=>t.Name=="Regiment");
        var oob=game.MainModule.Types.Single(t=>t.Name=="OOB");
        var campaign=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        bool Calls(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.DeclaringType.Name==owner && c.Name==name);
        bool Reads(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.DeclaringType.Name==owner && f.Name==name);
        bool Literal(MethodDefinition method,string text)=>method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,text));
        int checks=0;
        void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;}
        var add=oob.Methods.Single(m=>m.Name=="AddUnitIcon");
        Check(Calls(add,"Regiment","GetUnitSymbolData") && Literal(add,"Background/UnitName") && Calls(add,"TMP_Text","set_text"),
            "Native OOB icon labels no longer use the audited native symbol name / TextMeshPro child.");
        var registered=oob.NestedTypes.Single(t=>t.Name=="UnitsRegistered");
        Check(registered.Fields.Any(f=>f.Name=="reg" && f.FieldType.Name=="Regiment") && registered.Fields.Any(f=>f.Name=="multiplepanelid" && f.FieldType.FullName=="System.Int32") &&
            oob.Fields.Any(f=>f.Name=="unitsregistered"),"Native OOB panel registrations changed.");
        var symbol=regiment.Methods.Single(m=>m.Name=="GetUnitSymbolData");
        Check(Reads(symbol,"Regiment","abrvname") && Reads(symbol,"Commander","name") && Literal(symbol,"(L)") && Literal(symbol,"(D)"),
            "Native OOB label rules changed; review abbreviation, officer and detached artillery handling.");
        Check(symbol.Body.Instructions.Where(i=>i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld).All(i=>i.Operand is FieldReference f &&
            f.DeclaringType.Name=="Regiment" && f.Name=="groupunittype"),"Native symbol lookup gained an unpreserved native state write.");
        foreach(string getter in new[]{"GetMaxUnitType","GetSymbolColor"})
            Check(!regiment.Methods.Single(m=>m.Name==getter).Body.Instructions.Any(i=>(i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld) && i.Operand is FieldReference f &&
                new[]{"Regiment","Commander","Alliance","Nation","BattleUnits"}.Contains(f.DeclaringType.Name)),"Native symbol dependency gained a campaign state write: "+getter);
        var label=campaign.Methods.Single(m=>m.Name=="ReplicaOobLabel");
        var cleanup=label.Body.ExceptionHandlers.Single(h=>h.HandlerType==ExceptionHandlerType.Finally);
        Check(Calls(label,"Regiment","GetUnitSymbolData") && label.Body.Instructions.Any(i=>i.Offset>=cleanup.HandlerStart.Offset &&
            (cleanup.HandlerEnd==null || i.Offset<cleanup.HandlerEnd.Offset) && i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="groupunittype"),
            "Replica OOB labels must reuse native formatting and restore its campaign cache in finally.");
        var refresh=campaign.Methods.Single(m=>m.Name=="RefreshReplicaOobLabels");
        string[] rebuilds={"UpdateOOB","UpdateOOBs","ClearOOB","ForceCheckFilterSelectionChange","SelectUnit","SetParent","UpdateGroupStrength","UpdateUnitList"};
        Check(!refresh.Body.Instructions.Any(i=>i.Operand is MethodReference c && rebuilds.Contains(c.Name)) && Calls(refresh,"MultiplayerCampaign","ReplicaOobLabel"),
            "In-place OOB labels must not rebuild hierarchy, filters or selection.");
        Check(Calls(campaign.Methods.Single(m=>m.Name=="RefreshReplicaPresentation"),"MultiplayerCampaign","RefreshReplicaOobLabels"),
            "Authoritative nonstructural batches no longer refresh existing OOB labels.");
        Console.WriteLine($"Native OOB labels: {checks} checks; native label contract, preserved cache and in-place authoritative refresh verified.");
    }
}
