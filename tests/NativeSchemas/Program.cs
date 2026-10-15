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
int fields=0;
foreach(var schema in schemas)fields+=((Array)schema!.GetType().GetField("Fields")!.GetValue(schema)!).Length;
var game=AssemblyLoadContext.Default.LoadFromAssemblyPath(gamePath);
foreach(var item in new[]{("BuildingList","showncontent",false),("CampaignMainPanel","shiptoaddto",false),("CampaignMainPanel","currentcommanderchosen",false),("FleetInfoPanel","chosenfleet",false),("RailroadList","lastchosenrailroadline",true),("ShipRecruitingList","fleetthisshipisadded",true),("BattlePanel","bpunitentry",true),("SpottedEnemy","preloadedmodels",false)}) {
    var field=game.GetType(item.Item1,true)!.GetField(item.Item2,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
    if(field==null || field.IsStatic!=item.Item3)throw new InvalidOperationException("Native field bridge mismatch: "+item.Item1+"."+item.Item2);
}
var ai=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed,"UnityEngine.AIModule.dll"));
var corners=ai.GetType("UnityEngine.AI.NavMeshPath",true)!.GetField("m_Corners",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
if(corners?.FieldType.Name!="Vector3[]")throw new InvalidOperationException("Native path corner cache layout changed.");
Console.WriteLine($"Native schema ABI: {schemas.Length} schemas, {fields} fields bound against the original game assembly. Unity gameplay is not exercised.");
