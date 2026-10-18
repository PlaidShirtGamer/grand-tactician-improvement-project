using System.Reflection;
using System.Runtime.Loader;

if(args.Length!=3)throw new ArgumentException("Expected runtime, original game assembly and Managed directory.");
var runtimePath=Path.GetFullPath(args[0]);var gamePath=Path.GetFullPath(args[1]);var managed=Path.GetFullPath(args[2]);
AssemblyLoadContext.Default.Resolving+=(context,name)=> {
    var path=name.Name=="Assembly-CSharp"?gamePath:Path.Combine(managed,name.Name+".dll");
    if(!File.Exists(path))path=Path.Combine(Path.GetDirectoryName(runtimePath)!,name.Name+".dll");
    return File.Exists(path)?context.LoadFromAssemblyPath(path):null;
};
var runtime=AssemblyLoadContext.Default.LoadFromAssemblyPath(runtimePath);
var type=runtime.GetType("GTIP.MultiplayerCampaign",true)!;
// Builds the compiled, explicit field schemas against actual native metadata. No native
// object is constructed and no Unity engine function is invoked by this check.
var schemas=(Array)type.GetProperty("DataSchemas",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
SnapshotChecks.Run(runtime,schemas);
int fields=0;
foreach(var schema in schemas)fields+=((Array)schema!.GetType().GetField("Fields")!.GetValue(schema)!).Length;
var game=AssemblyLoadContext.Default.LoadFromAssemblyPath(gamePath);
foreach(var item in new[]{("CBuildingIcon","cbuildingattached"),("Production","chosenbuilding"),("Regiment","unitnametext")})
    if(game.GetType(item.Item1,true)!.GetField(item.Item2,BindingFlags.Instance|BindingFlags.NonPublic)==null)throw new Exception("Native presentation field changed: "+item);
foreach(var item in new[]{("BuildingList","showncontent",false),("CampaignMainPanel","shiptoaddto",false),("CampaignMainPanel","currentcommanderchosen",false),("FleetInfoPanel","chosenfleet",false),("RailroadList","lastchosenrailroadline",true),("ShipRecruitingList","fleetthisshipisadded",true),("BattlePanel","bpunitentry",true),("SpottedEnemy","preloadedmodels",false),("BattleUnits","currentwaypointorders",false),("BattleUnits","lastsetwaypointpos",false),("PanelHandler","moveoutdate",false)}) {
    var field=game.GetType(item.Item1,true)!.GetField(item.Item2,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
    if(field==null || field.IsStatic!=item.Item3)throw new InvalidOperationException("Native field bridge mismatch: "+item.Item1+"."+item.Item2);
}
var ai=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed,"UnityEngine.AIModule.dll"));
var corners=ai.GetType("UnityEngine.AI.NavMeshPath",true)!.GetField("m_Corners",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
if(corners?.FieldType.Name!="Vector3[]")throw new InvalidOperationException("Native path corner cache layout changed.");
using(var native=Mono.Cecil.AssemblyDefinition.ReadAssembly(gamePath)) {
    var regiment=native.MainModule.Types.Single(t=>t.Name=="Regiment");
    using(var compiled=Mono.Cecil.AssemblyDefinition.ReadAssembly(runtimePath)) {
        var fallback=compiled.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign").Methods.Single(m=>m.Name=="UpgradeWithoutMapIcon");
        var upgrade=native.MainModule.Types.Single(t=>t.Name=="CBuildingIcon").Methods.Single(m=>m.Name=="Upgrade");
        string[] Transactions(Mono.Cecil.MethodDefinition method)=>method.Body.Instructions.Where(i=>i.Operand is Mono.Cecil.MethodReference m &&
            new[]{"GetConstructionDuration","GetSubsidyCost","GetConstructionCost","GetMissingSubsidyFundingCost","AdjustTreasury","AdjustSubsidyFunding","Consume"}.Contains(m.Name))
            .Select(i=>((Mono.Cecil.MethodReference)i.Operand).FullName).OrderBy(n=>n).ToArray();
        if(!Transactions(fallback).SequenceEqual(Transactions(upgrade)))throw new Exception("Iconless upgrade transaction differs from native costs/resources operations.");
        string[] Costs(Mono.Cecil.MethodDefinition method)=>method.Body.Instructions.Where(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stfld && i.Operand is Mono.Cecil.FieldReference f &&
            new[]{"upgradetimer","constructioncosts","cumulatedspending_construction"}.Contains(f.Name)).Select(i=>((Mono.Cecil.FieldReference)i.Operand).Name).OrderBy(n=>n).ToArray();
        if(!Costs(fallback).SequenceEqual(Costs(upgrade)))throw new Exception("Iconless upgrade omits a native duration/expense write.");
        Console.WriteLine("Native upgrade ABI: iconless production upgrade retains native duration, treasury, subsidy and resource operations.");
    }
    var aggregate=regiment.Methods.Single(m=>m.Name=="UpdateGroupStrength");
    var tooltip=regiment.Methods.Single(m=>m.Name=="GetUnitTooltipInfo");
    bool Calls(Mono.Cecil.MethodDefinition method,string name)=>method.Body.Instructions.Any(i=>i.Operand is Mono.Cecil.MethodReference call && call.DeclaringType.Name=="Regiment" && call.Name==name);
    if(!Calls(tooltip,"UpdateGroupStrength") || !new[]{"UpdateMoraleCampaign","UpdateCampaignUnitStats","UpdateCondition"}.All(name=>Calls(aggregate,name)))throw new InvalidOperationException("Native tooltip simulation call graph changed; review the replica boundary.");
    var unitSchema=schemas.GetValue(3)!;
    var replicated=((FieldInfo[])unitSchema.GetType().GetField("Fields")!.GetValue(unitSchema)!).Select(f=>f.Name).ToHashSet();
    foreach(string name in new[]{"ShowPathArrows","UpdateOnMapSymbols"}) {
        var display=regiment.Methods.Single(m=>m.Name==name);
        var changed=display.Body.Instructions.Where(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stfld && i.Operand is Mono.Cecil.FieldReference f && f.DeclaringType.Name=="Regiment" && replicated.Contains(f.Name)).Select(i=>((Mono.Cecil.FieldReference)i.Operand).Name).Distinct().ToArray();
        if(changed.Any(field=>field!="totaltimeoforderdelay"))throw new InvalidOperationException("Native map display gained an unprotected campaign write: "+name+" "+string.Join(",",changed));
    }
    var lookup=native.MainModule.Types.Single(t=>t.Name=="CampaignMainPanel").Methods.Single(m=>m.Name=="GetUsedUnit");
    if(lookup.Parameters.Count!=2 || lookup.Parameters.Any(p=>!p.IsOptional || !p.HasConstant) || !Equals(lookup.Parameters[0].Constant,false) || !Equals(lookup.Parameters[1].Constant,0))throw new InvalidOperationException("Native unit lookup defaults changed.");
    Console.WriteLine("Native UI ABI: unit lookup defaults verified; path/symbol direct writes restricted to the preserved order-delay scalar.");
    var writes=aggregate.Body.Instructions.Where(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stfld && i.Operand is Mono.Cecil.FieldReference f && f.DeclaringType.Name=="Regiment").Select(i=>(Mono.Cecil.FieldReference)i.Operand).Select(f=>regiment.Fields.Single(field=>field.Name==f.Name)).Where(f=>f.IsPublic).Select(f=>f.Name).Distinct().ToArray();
    var missing=writes.Where(name=>!replicated.Contains(name)).ToArray();
    if(missing.Length>0)throw new InvalidOperationException("Host group-stat outputs missing from replication: "+string.Join(",",missing));
    Console.WriteLine($"Native tooltip boundary: simulation call chain confirmed; {writes.Length} public group-stat writes have host fields.");
}
Console.WriteLine($"Native schema ABI: {schemas.Length} schemas, {fields} fields bound against the original game assembly. Unity gameplay is not exercised.");
