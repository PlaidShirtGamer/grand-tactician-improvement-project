using Mono.Cecil;
using Mono.Cecil.Cil;

static class CommanderPortraitChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var commander=game.MainModule.Types.Single(t=>t.Name=="GameVars").NestedTypes.Single(t=>t.Name=="Commander");
        var regiment=game.MainModule.Types.Single(t=>t.Name=="Regiment");
        var campaign=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        bool Calls(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name==owner && m.Name==name);
        bool Reads(MethodDefinition method,string owner,string name)=>method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.DeclaringType.Name==owner && f.Name==name);
        int checks=0;
        void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
        var picture=regiment.Methods.Single(m=>m.Name=="UpdateCommanderPicture");
        Check(Calls(commander.Methods.Single(m=>m.Name=="AssignCommando"),"Regiment","UpdateCommanderPicture"),
            "Native commander assignment no longer refreshes the Regiment portrait cache.");
        Check(Reads(picture,"Commander","portraitfilename") && Reads(picture,"Commander","usedummyphoto") && Reads(picture,"Regiment","commander"),
            "Native portrait identity inputs changed; review replica cache identity.");
        var writes=picture.Body.Instructions.Where(i=>i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld).ToArray();
        Check(writes.Length>0 && writes.All(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="Regiment" && f.Name=="commanderpic"),
            "Native picture refresh gained writes beyond the presentation-only Regiment.commanderpic cache.");
        string[] forbidden={"AssignCommando","RemoveFromCommand","UpdateGroupStrength","UpdateUnitList","UpdateAllAttachedUnits","UpdateRankToFit","SetPosition","SetRotation"};
        Check(!picture.Body.Instructions.Any(i=>i.Operand is MethodReference m && forbidden.Contains(m.Name)),
            "Native portrait loading now invokes campaign or hierarchy mutation.");
        var panel=game.MainModule.Types.Single(t=>t.Name=="BattleUI").Methods.Single(m=>m.Name=="UpdateCommanderPanel");
        Check(Reads(panel,"Regiment","commanderpic") && Reads(panel,"Commander","combinedname"),
            "Native commander panel no longer combines cached formation portraits with live commander names.");
        var refresh=campaign.Methods.Single(m=>m.Name=="RefreshReplicaCommanderPortraits");
        Check(Calls(refresh,"Regiment","UpdateCommanderPicture") && Reads(refresh,"Regiment","commander") &&
            Reads(refresh,"Commander","portraitfilename") && Reads(refresh,"Commander","usedummyphoto"),
            "Replica portrait refresh no longer tracks every native image-identity input.");
        Check(!refresh.Body.Instructions.Any(i=>(i.OpCode==OpCodes.Stfld || i.OpCode==OpCodes.Stsfld) && i.Operand is FieldReference f &&
            new[]{"Regiment","Commander","Alliance","Nation","BattleUnits"}.Contains(f.DeclaringType.Name)),
            "Replica portrait refresh writes authoritative native state.");
        var fence=campaign.Methods.Single(m=>m.Name=="RefreshReplicaPresentation");
        int refreshIndex=fence.Body.Instructions.ToList().FindIndex(i=>i.Operand is MethodReference m && m.Name=="RefreshReplicaCommanderPortraits");
        int gateIndex=fence.Body.Instructions.ToList().FindIndex(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="rosterUiPending");
        Check(refreshIndex>=0 && gateIndex>refreshIndex,
            "Nonstructural commander changes must refresh portraits before the roster presentation early return.");
        Check(Calls(campaign.Methods.Single(m=>m.Name=="End"),"MultiplayerCampaign","ClearCommanderPortraits"),
            "Session teardown must release retained native portrait-cache identities.");
        Console.WriteLine($"Native commander portrait ABI: {checks} checks; assignment cache contract, exact image identity, presentation-only writes and teardown verified.");
    }
}
