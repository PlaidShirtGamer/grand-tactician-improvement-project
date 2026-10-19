using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class BattleChecks
{
    public static void Run(string gamePath,string runtimePath,Array schemas)
    {
        int checks=0;
        void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var mp=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        TypeDefinition Native(string name)=>game.MainModule.Types.Single(t=>t.Name==name);
        MethodDefinition NativeMethod(string type,string name)=>Native(type).Methods.Single(m=>m.Name==name);
        MethodDefinition Bridge(string name)=>mp.Methods.Single(m=>m.Name==name);
        string[] Calls(MethodDefinition method)=>method.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>((MethodReference)i.Operand).Name).ToArray();
        string[] Strings(MethodDefinition method)=>method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr).Select(i=>(string)i.Operand).ToArray();
        IEnumerable<MethodDefinition> Methods(TypeDefinition type)=>type.Methods.Concat(type.NestedTypes.SelectMany(Methods));
        string[] Fields(int kind)=>((FieldInfo[])schemas.GetValue(kind)!.GetType().GetField("Fields")!.GetValue(schemas.GetValue(kind))!).Select(f=>f.Name).ToArray();
        Check(Calls(NativeMethod("BattleUI","SelectUnit")).Contains("SearchAutocalcFromMonument"),"Native campaign selection must resolve a live battle record");
        Check(Calls(NativeMethod("BattleUI","SelectUnit")).Contains("OpenAutoResolvePanel"),"Native campaign selection opens the autoresolve panel from that battle");
        Check(Calls(NativeMethod("BattlefieldSetup","SearchAutocalcFromMonument")).Contains("ContainsUnit"),"Native selection lookup follows battle participation");
        Check(Calls(NativeMethod("Autocalc","Update")).Contains("InitSounds"),"Native combat Update also owns sound initialization");
        Check(Calls(Bridge("ReplicaAutocalc")).Contains("ReplicaAutocalcPresentation"),"Replica combat Update invokes the separate presentation bridge");
        Check(Strings(Bridge("ReplicaAutocalcPresentation")).Contains("InitSounds"),"Replica presentation restores native ambient sound setup");
        Check(!Calls(Bridge("ReplicaAutocalcPresentation")).Any(c=>c.StartsWith("Update") || c.StartsWith("StartLand")),"Replica presentation cannot enter a combat cycle");
        var forbidden=new[]{"StartLandBattle","StartSiegeCombat","StartNavalBattle","UpdateLandBattleCycle","UpdateSiegeCombat","UpdateNavalBattle","UpdateRaiding","UpdateFortNavyEngagement","UpdateTransportInterception","UpdateSkirmishing","FinishLandBattle","FinishNavyBattle","SufferCasualties","AddTopUnit"};
        foreach(string name in new[]{"ApplyBattles","ApplyBattleAudio","ApplyBattleSide","ApplyBattlePresentation","ReplicaAutocalcPresentation"})
            Check(!Calls(Bridge(name)).Concat(Strings(Bridge(name))).Any(forbidden.Contains),"Guest battle apply may only rebuild native presentation records: "+name);
        Check(Calls(Bridge("ReplicaBattleMonument")).Contains("get_IsReplica") && Strings(Bridge("ReplicaBattleMonument")).Contains("UpdateBalanceBar") && !Strings(Bridge("ReplicaBattleMonument")).Contains("CheckDisappearance"),"Guest monument update cannot independently expire host records");
        foreach(string name in new[]{"ReplicaBattleStart","ReplicaAutocalcRetreat","ReplicaAutocalcStance","ReplicaAutocalcManual"}) {
            Check(Calls(Bridge(name)).Contains("get_IsReplica"),"Native mutation callbacks retain replica guard: "+name);
            Check(Strings(Bridge(name)).Single()=="GTIP_Original_"+name,"Only the preserved host/offline callback is reachable: "+name);
        }
        var init=NativeMethod("Autocalc","InitSounds");var explosion=NativeMethod("Autocalc","SingleExplosion");
        Check(!Calls(init).Concat(Calls(explosion)).Any(forbidden.Contains),"Native sound setup and explosion visuals contain no campaign combat operation");
        Check(Calls(explosion).Contains("Instantiate") && Calls(explosion).Contains("ActivateArray"),"Explosion replay preserves native effect prefab plus sound");
        Check(new[]{"points","forcebalance","totalstrength","guns","totallosses","alliance","commanderinchief","currentfirepower","readiness","usedstance","fightingdistance","isnight","hasplayercontrolledunits","participatingunits"}.All(Fields(36).Contains),"Native battle panel public scalar inputs are host replicated");
        Check(!Fields(35).Contains("ismouseover"),"Monument hover stays local");
        Check(new HashSet<string>(Fields(39)).SetEquals(new[]{"Type","X","Y","Z"}),"Minor combat sources contain only presentation type and position");
        Check(Calls(Bridge("ApplyBattleAudio")).Contains("AddComponent") && Calls(Bridge("ApplyBattleAudio")).Contains("Destroy"),"Standalone sound sources are constructed and removed through their own lifecycle");
        Check(!Bridge("ApplyBattleAudio").Body.Instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="BattlefieldSetup" && f.Name=="battlemonument"),"Standalone raids and skirmishes cannot add fake battle monuments");
        Check(Calls(Bridge("ClearBattles")).Contains("Clear") && Calls(Bridge("ClearBattles")).Contains("Destroy") && Calls(Bridge("ClearBattles")).Contains("StopReplicaBattleAudio"),"Session cleanup clears identity receipts and removes temporary sound sources");
        Check(new[]{"LoopPurpose","LoopActive"}.All(Fields(38).Contains),"Current battle loops accompany checkpoint presentation without replaying one shots");
        Check(Methods(mp).Any(method=>method.HasBody && Strings(method).Contains("permanentlyactive")),"Loop snapshots retain the native requested activation state");
        Check(Calls(Bridge("ApplyBattlePresentation")).Contains("PlayBattleSound"),"Applying presentation restores ongoing loop state after native sound setup");
        Check(Calls(Bridge("ReplicaAutocalcPresentation")).Contains("AddPurposeArray"),"Minor raid explosion sound arrays never reuse the unrelated default array index");
        foreach(var item in new[]{("Autocalc","soundengine"),("Autocalc","soundobjects"),("Autocalc","log"),("SoundEngine","soundobject"),("SoundEngine","soundobjectarray")})
            Check(Native(item.Item1).Fields.Any(f=>f.Name==item.Item2),"Native battle presentation reflection field remains available: "+item);
        Console.WriteLine($"Native battle boundary: {checks} checks passed against actual native and compiled bridge metadata; no Unity simulation was run.");
    }
}
