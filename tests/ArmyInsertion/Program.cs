using GTIP;
using UnityEngine;

void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
var engine = new BattleUnits();
var corps = new Regiment { unittyp = 16 };
var division = new Regiment { unittyp = 15 };
division.transform.SetParent(corps.transform, true);
var army = ArmyInsertion.Insert(engine, corps);
Check(army.unittyp == 18 && corps.unittyp == 16 && division.transform.parent == corps.transform && corps.transform.parent == army.transform,
    "Inserting Army preserves the existing Corps and its Division instead of renaming Corps");
Check(!corps.istopunit && corps.permanentlydetached && army.Refreshed && BattleUnits.Refreshed == corps,
    "Insertion refreshes hierarchy and preserves independent Corps movement");
corps = new Regiment { unittyp = 16 };
var group = new ArmyGroup(); group.GTIP_Original_AddArmyGroupUnit(corps, false, false);
army = ArmyInsertion.Insert(engine, corps);
Check(!group.Destroyed && group.Units.SequenceEqual(new[] { army }) && army.assignedarmygroup == group && corps.assignedarmygroup == null,
    "Replacing the last Corps in an Army Group preserves the group and assigns the Army");
Check(ArmyInsertion.Insert(engine, corps) == null && corps.transform.parent == army.transform,
    "Already subordinate Corps cannot acquire a duplicate Army HQ");
corps = new Regiment { unittyp = 16, inbattle = true };
Check(ArmyInsertion.Insert(engine, corps) == null && corps.transform.parent == null,
    "In-battle Corps is left untouched");
corps = new Regiment { unittyp = 16 };
engine.FailCreation = true;
Check(ArmyInsertion.Insert(engine, corps) == null && corps.istopunit && corps.transform.parent == null,
    "Failed Army creation leaves the existing Corps unchanged");
Console.WriteLine("Army insertion checks use stubs; live UI and campaign save behavior remain unverified.");

namespace GTIP { public static class SixLevelRuntime { internal static void EnsureArmyData() { } } }
public class Regiment
{
    public int unittyp, alliance;
    public bool inbattle, istopunit = true, permanentlydetached, Refreshed;
    public ArmyGroup assignedarmygroup;
    public Transform transform;
    public GameObject gameObject;
    public Regiment() { transform = new Transform { Unit = this }; gameObject = new GameObject { Unit = this }; }
    public void UpdateGroupStrength(bool overridecalc, bool ignoreactivestate) { Refreshed = true; }
}
public class ArmyGroup
{
    public bool Destroyed;
    public List<Regiment> Units = new();
    public void GTIP_Original_AddArmyGroupUnit(Regiment unit, bool load, bool rename)
    { if (Destroyed) throw new Exception("Group was destroyed"); Units.Add(unit); unit.assignedarmygroup = this; }
    public void RemoveUnit(Regiment unit) { Units.Remove(unit); unit.assignedarmygroup = null; if (Units.Count == 0) Destroyed = true; }
}
public class BattleUnits
{
    public static void UpdateUnitList() { }
    public bool FailCreation;
    public static Regiment Refreshed;
    public static Regiment GetHighestParent(Regiment unit)
    { while(unit.transform.parent != null) unit = unit.transform.parent.Unit; return unit; }
    public Regiment CreateUnitDefault(Regiment parent, int state, int type, int _alliance, Vector3 forcedcreationposition, int overridelev)
        => FailCreation ? null : new Regiment { unittyp = type, alliance = _alliance };
    public static void UpdateAllParentUnits(GameObject obj, bool self) { Refreshed = obj.Unit; }
}
public static class ToolsUnitList { public static void SetDirty() { } }
namespace UnityEngine
{
    public struct Vector3 { }
    public class GameObject { public Regiment Unit; }
    public class Transform
    {
        public Regiment Unit;
        public Transform parent;
        public Vector3 position;
        public void SetParent(Transform target, bool preservePosition) { parent = target; }
    }
}
