using UnityEngine;
namespace GTIP { public static class SixLevelRuntime { public static bool Active = true; internal static void EnsureArmyData() { } } }
public class Regiment
{
    static int next;
    readonly int id = ++next;
    public int GetInstanceID() => id;
    public string name = "Formation";
    public int unittyp, alliance = 1, commander, strength, groupstrengthactive, regimentpaths;
    public float transfertime, embarkmentduration;
    public bool inbattle, onretreat, istopunit = true, permanentlydetached, isgarrisonbasicunit, raiding;
    public object garrisonreference;
    public Vector3 theaterposition;
    public ArmyGroup assignedarmygroup;
    public Transform transform;
    public GameObject gameObject;
    public Regiment() { transform = new Transform { Unit = this }; gameObject = new GameObject { Unit = this }; BattleUnits.completeunitlist.Add(this); }
    public Regiment[] GetAttachedUnitsReg(bool a, bool b, int type, bool direct = false) => BattleUnits.completeunitlist.Where(u => u != this && u.unittyp == type && (direct ? u.transform.parent == transform : u.IsBelow(this))).ToArray();
    public bool IsBelow(Regiment root) { for(var t = transform.parent; t != null; t = t.parent) if(t.Unit == root) return true; return false; }
    public void UpdateGroupStrength(bool overridecalc, bool ignoreactivestate) { }
}
public class ArmyGroup
{
    public string name = "Group";
    public int GetInstanceID() => GetHashCode();
    public List<Regiment> Units = new();
    public bool Destroyed;
    public void GTIP_Original_AddArmyGroupUnit(Regiment u, bool load, bool rename) { if(Destroyed) throw new Exception(); Units.Add(u); u.assignedarmygroup = this; }
    public void RemoveUnit(Regiment u) { Units.Remove(u); u.assignedarmygroup = null; Destroyed = Units.Count == 0; }
}
public class BattleUnits
{
    public static object unitfolder = new();
    public static List<Regiment> completeunitlist = new();
    public bool FailCreation;
    public static void UpdateUnitList() { }
    public static void UpdateAllParentUnits(GameObject obj, bool self) { }
    public static Regiment GetHighestParent(Regiment u) { while(u.transform.parent != null) u = u.transform.parent.Unit; return u; }
    public Regiment CreateUnitDefault(Regiment p, int state, int type, int _alliance, Vector3 forcedcreationposition, int overridelev)
        => FailCreation ? null : new Regiment { unittyp = type, alliance = _alliance, transform = { position = forcedcreationposition } };
    public void MoveUnit(GameObject source, GameObject destination, bool skipdlccheck) { var u = source.Unit; u.assignedarmygroup?.RemoveUnit(u); u.transform.SetParent(destination.Unit.transform, true); u.istopunit = false; }
}
public static class ToolsUnitList { public static void SetDirty() { } }
public static class GameVars
{
    public static int playeralliance;
    public static bool ai_vs_ai;
    public static object uniStormSystem = new Clock();
    public static float GetCampaignFloatingDate() => 1863.1f;
    public class Clock { public int monthCounter = 2, dayCounter = 10; }
}
public static class GamePrefs { public static float aimaximumdistancetosearchforunitrelocations = 100; }
public class AICampaign
{
    public static BattleUnits bunits = new();
    public static List<Faction> aifaction = new();
    public class Faction
    {
        public int allianceid = 1;
        public List<Regiment> ownunits = new(), unitsindefensiveoperations = new(), unitsinoffensiveoperations = new(), groupstodefendcapital = new(), unitsconstructingsupplydepots = new();
    }
    public static bool GrandArmyStructure(int alliance) => true;
    public static bool IsWithinOperationsTheater(Regiment u, Vector3 p) => u.theaterposition == default(Vector3) || Vector3.Distance(u.theaterposition, p) < 100;
    public class RaidForce { public static bool IsRaidUnit(Regiment u) => u.raiding; }
    public class SeaInvasionForce { public static object GetSeaInvasionForceReference(Regiment u) => null; }
    public class FortConstructionOrder { public static bool UnitAlreadyConstructing(Regiment u) => false; }
    public static int Calls;
    public static Action NativeAction;
    public static void GTIP_Original_Recruit(int f) { Calls++; NativeAction?.Invoke(); }
    public void GTIP_Original_Combine(int f) { Calls++; NativeAction?.Invoke(); }
    public void GTIP_Original_Transfer(int f) { Calls++; NativeAction?.Invoke(); }
    public static void GTIP_Original_CombineCombat(int f) { Calls++; NativeAction?.Invoke(); }
    public void GTIP_Original_ManageGroups(int f) { Calls++; NativeAction?.Invoke(); }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x;
        public static float Distance(Vector3 a, Vector3 b) => Math.Abs(a.x-b.x);
        public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x;
        public static bool operator !=(Vector3 a, Vector3 b) => a.x != b.x;
        public override bool Equals(object b) => b is Vector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode();
    }
    public class GameObject
    {
        public Regiment Unit;
        public T[] GetComponentsInChildren<T>(bool active) => BattleUnits.completeunitlist.Where(u => u == Unit || u.IsBelow(Unit)).Cast<T>().ToArray();
    }
    public class Transform
    {
        public Regiment Unit;
        public Transform parent;
        public Vector3 position;
        public void SetParent(Transform t, bool preserve) { parent = t; }
        public T GetComponent<T>() => (T)(object)Unit;
    }
    public static class Application { public static string dataPath; }
    public static class Debug { public static int Warnings; public static void LogWarning(string text) { Warnings++; } }
}
