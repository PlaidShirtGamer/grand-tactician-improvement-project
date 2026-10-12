using GTIP;
void Check(bool value,string label) { if(!value) throw new Exception(label); Console.WriteLine("PASS "+label); }
var army = new Regiment { name="Army",unittyp=18 };
var corps = new Regiment { name="Corps",unittyp=16 };
var division = new Regiment { name="Division",unittyp=15 };
var brigade = new Regiment { name="Brigade",unittyp=14 };
var infantry = new Regiment { name="Regiment",unittyp=0,strength=900,training=.35f,experience=.12f,morale=.9f,conditionmen=1 };
corps.transform.parent=army.transform; division.transform.parent=corps.transform; brigade.transform.parent=division.transform; infantry.transform.parent=brigade.transform;
BattleUnits.completeunitlist.AddRange(new[]{army,corps,division,brigade,infantry});
BattleUnits.armygroups.Add(new ArmyGroup { name="Army Group", attachedunits=new(){army} });
var rows=CampaignEditorData.Read(0,0,"");
Check(rows.Count==6 && rows.Select(r=>r.Depth).SequenceEqual(new[]{0,1,2,3,4,5}),"All six hierarchy levels appear once with correct nesting");
Check(rows.Where(r=>r.Target is Regiment).All(r=>r.Detail.Contains("900 men")),"HQ totals come from descendants, independent of stale native cache");
Check(CampaignEditorData.Read(0,1,"").Count==0,"Faction selection excludes the other faction");
Check(CampaignEditorData.Read(0,0,"Regiment").Count==1,"Search locates named combat unit");
var row=rows.Last();
Check(row.Fields.Single(f=>f.Label=="Training (%)").Format(infantry.training)=="35","Training maps to native training, displayed as percent");
var session=new LiveEditSession(()=>{},CampaignEditorData.Refresh);
session.Apply(new[]{row.Fields[0]},new[]{"Renamed"});
Check(infantry.name=="Renamed","Unit rename writes selected native object"); session.Undo();
Check(infantry.name=="Regiment","Unit rename can be undone");
GameVars.commander.Add(new GameVars.Commander { name="Grant",firstname="Ulysses",combinedname="Ulysses Grant",rank=6,leadership=.7f });
row=CampaignEditorData.Read(3,0,"").Single();
Check(row.Title.StartsWith("BG.") && row.Fields.Single(f=>f.Label=="Leadership (%)").Format(.7f)=="70","Officer rank and percent labels use game data");
GameVars.weapon=new[]{new GameVars.Weapon {name="Rifle"},new GameVars.Weapon{name="Cannon"}};
GameVars.alliance[0].weaponstock=new[]{100f,200f};
rows=CampaignEditorData.Read(4,0,"");
Check(!ReferenceEquals(rows[0].Target,rows[1].Target),"Weapon selection identities remain distinct after refresh");
session.Apply(rows[1].Fields,new[]{"250"});
Check(GameVars.alliance[0].weaponstock.SequenceEqual(new[]{100f,250f}),"Stock edit changes only selected weapon"); session.Undo();
Check(GameVars.alliance[0].weaponstock[1]==200,"Stock undo restores exact value");
GameVars.nation = new[]{new GameVars.Nation{name="Ohio",population=2000000,volunteers=new[]{500,600},drafts=new[]{700,800},support=new[]{.7f,.3f}}};
var stateRow=CampaignEditorData.Read(5,0,"").Last();
Check(stateRow.Detail.Contains("500") && stateRow.Detail.Contains("700"),"Nation shows selected faction recruit pools");
session.Apply(stateRow.Fields,new[]{"2100000","75","1200","1400"});
Check(GameVars.nation[0].volunteers.SequenceEqual(new[]{1200,600}) && GameVars.nation[0].drafts.SequenceEqual(new[]{1400,800}),"Pool edits preserve other faction");
session.Undo(); Check(GameVars.nation[0].population==2000000 && GameVars.nation[0].volunteers[0]==500,"Nation undo restores population and recruits");
bool rejected=false;try{stateRow.Fields[2].Parse("1.5");}catch(InvalidOperationException){rejected=true;}Check(rejected,"Recruit pools require whole people");
Console.WriteLine("Projection checks use synthetic game objects; actual game smoke testing remains required.");

public class Transform { public Regiment Unit; public Transform parent; public T GetComponent<T>()=>(T)(object)Unit; }
public class Regiment
{
    public string name; public int unittyp,alliance,commander=-1,strength,groupstrengthactive;
    public float experience,training,morale,conditionmen;
    public bool isgarrisonbasicunit; public object garrisonreference;
    public Transform transform;
    public Regiment(){transform=new Transform{Unit=this};}
    public int GetInstanceID()=>GetHashCode();
    public void UpdateGroupStrength(bool overridecalc,bool ignoreactivestate){}
}
public class ArmyGroup { public string name; public List<Regiment> attachedunits; public int GetAlliance()=>0; public int GetCommanderID()=>-1; }
public static class BattleUnits
{
    public static List<Regiment> completeunitlist=new(); public static List<ArmyGroup> armygroups=new();
    public static Regiment GetHighestParent(Regiment u){while(u.transform.parent!=null)u=u.transform.parent.Unit;return u;}
}
public static class GameVars
{
    public static string[] rankabbrev={"","Lt.","Capt.","Maj.","Lt.Col.","Col.","BG.","MG."};
    public static List<Commander> commander=new(); public static Weapon[] weapon;
    public static Alliance[] alliance={new(),new()}; public static Nation[] nation=Array.Empty<Nation>();
    public class Commander {public string name,firstname,combinedname;public int alliance,rank,status;public bool activeincampaign=true,veteran,westpoint,political;public float exp,fame,leadership,initiative,administration,cunning;public Regiment currentcommand;}
    public class Weapon {public string name;}
    public class Alliance {public string name;public float treasury,volunteersavailable,draftsavailable;public float[] weaponstock;}
    public class Nation {public string name;public float population;public bool recruitable=true;public int alliance;public int[] volunteers=new int[2],drafts=new int[2];public float[] support=new float[2];}
}
public static class CampaignMainPanel {public static object lastregtextupdate;}
public static class ToolsUnitList {public static void SetDirty(){}}
namespace UnityEngine {public class Placeholder{}}
