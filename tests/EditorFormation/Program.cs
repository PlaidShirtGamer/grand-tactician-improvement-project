using GTIP;
using UnityEngine;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
void Reject(Action action, string label) { try { action(); } catch (InvalidOperationException) { Console.WriteLine("PASS " + label); return; } throw new Exception(label); }
Regiment Unit(int type, Regiment parent=null) {
    var u=new Regiment {unittyp=type,name="Unit "+type,commander=GameVars.commander.Count};
    u.transform.SetParent(parent?.transform ?? BattleUnits.unitfolder.transform,true);
    GameVars.commander.Add(new GameVars.Commander {currentcommand=u,rank=7}); BattleUnits.completeunitlist.Add(u); return u;
}
Check(EditorFormationRules.CanParent(16,18) && !EditorFormationRules.CanParent(18,16) && !EditorFormationRules.CanParent(16,17),"Army accepts Corps; inverse and Fleet links rejected");
var army=Unit(18); var otherArmy=Unit(18); var corps=Unit(16,army); var division=Unit(15,corps);
EditorFormationOperations.Move(corps,otherArmy);
Check(corps.transform.parent==otherArmy.transform && division.transform.parent==corps.transform && corps.permanentlydetached,"Existing Corps transfer preserves children and independent movement");
Check(BattleUnits.Refreshed.Contains(army),"Former Army cache is refreshed");
Reject(()=>EditorFormationOperations.Move(otherArmy,division),"Descendant cycles rejected");
var enemy=Unit(18);enemy.alliance=1;
Reject(()=>EditorFormationOperations.Move(corps,enemy),"Cross-faction transfer rejected");
division.inbattle=true;
Reject(()=>EditorFormationOperations.Move(corps,army),"In-battle descendant blocks whole transfer"); division.inbattle=false;
var first=new ArmyGroup(); var second=new ArmyGroup(); BattleUnits.armygroups.AddRange(new[]{first,second});
first.GTIP_Original_AddArmyGroupUnit(otherArmy,false,false);
EditorFormationOperations.Move(otherArmy,second);
Check(first.attachedunits.Count==0 && !first.Destroyed && second.attachedunits.Single()==otherArmy,"Moving last Army preserves empty source Army Group");
EditorFormationOperations.Delete(corps);
Check(!BattleUnits.completeunitlist.Contains(corps) && BattleUnits.completeunitlist.Contains(division) && division.transform.parent==otherArmy.transform,"Deleting Corps preserves Division and reparents to Army");
Check(!BattleUnits.ReturnedRecruits,"HQ deletion does not return phantom recruits or weapons");
EditorFormationOperations.Delete(otherArmy);
Check(division.transform.parent==BattleUnits.unitfolder.transform && second.attachedunits.Single()==division,"Deleting Army promotes its HQ children into original Army Group");
EditorFormationOperations.Delete(second);
Check(division.assignedarmygroup==null && BattleUnits.completeunitlist.Contains(division),"Deleting Army Group preserves member formations");
var brigade=Unit(14);var infantry=Unit(0,brigade);
Reject(()=>EditorFormationOperations.Delete(brigade),"Root HQ with combat children needs a replacement parent before deletion");
EditorFormationOperations.Delete(infantry);
Check(BattleUnits.ReturnedRecruits,"Combat deletion delegates recruitment/equipment returns to native disbanding");
int eligible=GameVars.commander.Count;
GameVars.commander.Add(new GameVars.Commander {rank=6,exp=0});
Check(EditorFormationOperations.Commanders(0,18).Contains(eligible) && !EditorFormationOperations.Commanders(0,18,armyGroup:true).Contains(eligible),"Army and Army Group experience requirements remain distinct");
GameVars.commander[eligible].exp=1;
Check(EditorFormationOperations.Commanders(0,18,armyGroup:true).Contains(eligible),"Experienced available general eligible for Army Group");
var town=new Town {Owner=0,state=0,CityName="Town"};BattleUnits.towns.Add(town);
var created=(Regiment)EditorFormationOperations.Create(0,18,"Created Army",null,town,eligible);
Check(created.name=="Created Army" && created.commander==eligible && GameVars.commander[eligible].currentcommand==created,"Creation assigns requested commander and name");
Reject(()=>EditorFormationOperations.Create(0,18,"Duplicate",null,town,eligible),"Assigned commander cannot be reused");
var busy=Unit(16); AICampaign.aifaction[0].unitsinoffensiveoperations.Add(busy);
Reject(()=>EditorFormationOperations.Move(busy,army),"Active AI operations block structural edits");
Console.WriteLine("Structural adapter checks use native-operation doubles; in-game save/reload verification remains necessary.");

public class Regiment {
    public int unittyp,alliance,commander;public string name; public bool inbattle,onretreat,isgarrisonbasicunit,istopunit,permanentlydetached;
    public float transfertime,embarkmentduration;public int regimentpaths;public object garrisonreference,parentregiment,detachedregiment;
    public ArmyGroup assignedarmygroup;public GameObject gameObject;public Transform transform=>gameObject.transform;
    public Regiment(){gameObject=new GameObject {Unit=this};}
    public T[] GetComponentsInChildren<T>(bool inactive)=>BattleUnits.completeunitlist.Where(u=>u==this || u.transform.IsChildOf(transform)).Cast<T>().ToArray();
    public void AssignArmyGroup(ArmyGroup group){assignedarmygroup=group;}
    public void UpdateUnitIconCampaign(bool forcehiding=false){}
}
public class ArmyGroup {
    public string name="Group";public int alliance; public bool Destroyed;public List<Regiment> attachedunits=new();
    public int GetAlliance()=>alliance;public bool CanAddFurtherUnits()=>true;public void UpdateHierarchyLevel(){}
    public void GTIP_Original_AddArmyGroupUnit(Regiment unit,bool load,bool rename){attachedunits.Add(unit);unit.AssignArmyGroup(this);}
    public void RemoveArmyGroup(){foreach(var u in attachedunits)u.AssignArmyGroup(null);BattleUnits.armygroups.Remove(this);Destroyed=true;}
    public static void AppointCommander(ArmyGroup group,int id){}
    public static ArmyGroup CreateNewArmyGroup(GameObject prefab,string name,int commander,List<Regiment> units,bool excludeplayerunits){var g=new ArmyGroup{name=name};BattleUnits.armygroups.Add(g);return g;}
}
public class BattleUnits {
    public static BattleUnits Instance=new();public GameObject ArmyGroupPrefab=new();
    public static GameObject unitfolder=new();public static List<Regiment> completeunitlist=new();public static List<ArmyGroup> armygroups=new();public static List<Town> towns=new();
    public static HashSet<Regiment> Refreshed=new();public static bool ReturnedRecruits;
    public void MoveUnit(GameObject child,GameObject parent,bool skipdlccheck){child.transform.SetParent(parent.transform,true);child.Unit.istopunit=false;}
    public static void UpdateAllParentUnits(GameObject unit,bool self){Refreshed.Add(unit.Unit);}
    public static void UpdateUnitList(){}
    public Regiment CreateUnitDefault(Regiment parent,int state,int type,int _alliance,Vector3 forcedcreationposition,int overridelev){var u=new Regiment {unittyp=type,alliance=_alliance,commander=-1};u.transform.SetParent(parent?.transform??unitfolder.transform,true);completeunitlist.Add(u);return u;}
    public static void DeleteUnit(GameObject unit,bool transferrecruitstopool){ReturnedRecruits=transferrecruitstopool;GameVars.commander[unit.Unit.commander].currentcommand=null;completeunitlist.RemoveAll(u=>u==unit.Unit||u.transform.IsChildOf(unit.transform));}
}
public class Town {public int Owner,state;public string CityName;public Transform transform=new();}
public static class GameVars {
    public static List<Commander> commander=new();public static bool allowarmygroups=true;public static object[] nation={new()};public static Alliance[] alliance={new(),new()};
    public static int GetMinRankForUnittyp(int type,int side)=>type==18?6:1;public static int GetMaxRankForUnittyp(int type,int side,bool check)=>8;
    public class Commander {public int alliance,rank,historicalbranch;public bool activeincampaign=true,refreshcommand;public float exp;public Regiment currentcommand;
        public bool IsAvailable()=>true;public bool HasCommand()=>currentcommand!=null;public bool CheckSuitableRank(int type,Regiment unit)=>type!=18||exp>=.5f;
        public void AssignCommando(Regiment unit,bool updatename){currentcommand=unit;unit.commander=commander.IndexOf(this);}}
    public class Alliance {public List<Regiment> unitlist=new(),lastmonthunitsformed=new();}
}
public static class AICampaign {
    public static List<Faction> aifaction=new(){new()};
    public class Faction {public List<Regiment> unitsindefensiveoperations=new(),unitsinoffensiveoperations=new(),groupstodefendcapital=new(),unitsconstructingsupplydepots=new();public void SeparateUnits(List<Regiment> units){}}
    public static class RaidForce {public static bool IsRaidUnit(Regiment u)=>false;}
    public static class SeaInvasionForce {public static object GetSeaInvasionForceReference(Regiment u)=>null;}
    public static class FortConstructionOrder {public static bool UnitAlreadyConstructing(Regiment u)=>false;}
}
namespace GTIP {internal static class SixLevelRuntime {internal static void EnsureArmyData(){}}internal static class CampaignEditorData {internal static void Refresh(){}}}
namespace UnityEngine {
    public struct Vector3{}
    public class GameObject {public Regiment Unit;public Transform transform;public GameObject(){transform=new Transform{Object=this};}public static GameObject Find(string name)=>new();public T GetComponent<T>()=>(T)(object)BattleUnits.Instance;public void SetActive(bool active){}}
    public class Transform {public GameObject Object;public Transform parent;public Vector3 position;public T GetComponent<T>()=>(T)(object)Object?.Unit;public void SetParent(Transform p,bool world){parent=p;}public bool IsChildOf(Transform p){for(var t=parent;t!=null;t=t.parent)if(t==p)return true;return false;}}
}
