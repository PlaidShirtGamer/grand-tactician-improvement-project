using GTIP;
using UnityEngine;

void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
Regiment[] Chain()
{
    var nodes = new[] { 18, 16, 15, 14, 0 }.Select(type => new Regiment { unittyp = type }).ToArray();
    for (int i = 0; i < nodes.Length - 1; i++) nodes[i].Children.Add(nodes[i + 1]);
    return nodes;
}
void Order(BattleUnits engine, Regiment root, bool immediate, Vector3 target)
    => HierarchyFormation.Formation(engine, root, 0, 90, target, immediate, true, false, 2, -1, false, false, true, false, false);
var units = Chain();
var engine = new BattleUnits();
Order(engine, units[0], true, new Vector3(100, 0, 100));
Check(engine.Measured.SequenceEqual(units.Reverse()), "Army formation measures deepest regiment before all four HQs");
Check(units.All(r => r.Placements == 1), "Immediate Army placement reaches every HQ and regiment once");
units = Chain(); engine = new BattleUnits();
var second = new Regiment { unittyp = 0 };
units[3].Children.Add(second);
Order(engine, units[0], false, new Vector3(100, 0, 100));
Check(units.All(r => r.Paths == 1 && r.OrderRoot == units[0]) && second.Paths == 1, "Army movement issues paths to both deepest branches with the same order source");
Check(units[4].LastPosition.x != second.LastPosition.x, "Sibling regiments receive distinct formation positions");
units = Chain(); engine = new BattleUnits { IsCampaign = true };
units[0].transform.position = new Vector3(80, 0, 70);
Order(engine, units[0], true, default);
Check(units[0].LastPosition == units[0].transform.position, "Campaign formation without target retains root position");
SixLevelRuntime.Active = false;
engine = new BattleUnits(); Order(engine, units[0], true, default);
Check(engine.Fallbacks == 1 && engine.Measured.Count == 0, "Inactive mod uses original formation implementation");
SixLevelRuntime.Active = true;
engine = new BattleUnits(); Order(engine, units[1], true, default);
Check(engine.Fallbacks == 1 && engine.Measured.Count == 0, "Existing Corps formations use original implementation");
BattleUnits.Restores = 0;
engine = new BattleUnits { ThrowOnMeasure = true };
try { Order(engine, units[0], true, default); throw new Exception("Expected measurement error"); }
catch (InvalidOperationException) { }
Check(BattleUnits.Restores == 1, "Detached units are restored even when layout fails");
Console.WriteLine("Formation checks use stubs; Unity terrain, pathfinding, and live save transitions require gameplay testing.");

namespace GTIP { public static class SixLevelRuntime { public static bool Active = true; } }
public static class GameVars
{
    public static bool useorderdelays;
    public static object SetFormationParam(int a, bool b, int c, float d, bool e) => null;
}
public static class GamePrefs { public static float groupunitdistancetoback = 5, refuseflankdepth = 1, refuseflankskew = 2; }
public class Regiment
{
    public int unittyp, groupformation, formationordered, Placements, Paths;
    public bool pathinterrupted, withinrotationprocess, permanentlydetached, isrouted, immediateunitplacement;
    public float lastsetwaypointrotation;
    public Vector3 lastsetwaypointposition, LastPosition;
    public Regiment OrderRoot;
    public Transform transform = new();
    public List<Regiment> Children = new();
    public BattleUnits.GroupPosition groupposition;
    public Vector3 GetLastTransmittedPathPos(bool ignore) => transform.position;
    public float GetWidth() => 10;
    public float GetDepth() => 5;
    public void SetNewPosition(Vector3 pos, float facing, bool cover, bool ignore) { LastPosition = pos; Placements++; }
    public void RegimentSetPath(Vector3 pos, bool path, bool cover, float rotation, bool modify, int state, float speed, Regiment root, bool advance, bool options)
    { Paths++; LastPosition = pos; OrderRoot = root; }
}
public class BattleUnits
{
    public bool IsCampaign, ThrowOnMeasure;
    public int Fallbacks;
    public static int Restores;
    readonly BattlefieldSetup bfs = new();
    public List<Regiment> Measured = new();
    public class GroupPosition { public float groupwidth, groupdepth; public int attachedunits; public Regiment[] attachedunitreg; public Vector3[] unitoffsetposition; }
    public class HierarchyTree { public Regiment reg; public Regiment[] attachedunitreg; }
    public static List<Regiment> DeParentDetachedSubordinates(Regiment root) => new();
    public static void ParentDetachedSubordinates(Regiment root, List<Regiment> detached) { if (detached != null) Restores++; }
    public void PerformActionForGroupForAllUnitsOnLevel(Regiment r, int level, string action, object value) { }
    public float GetSlowestSpeedOfGroup(Regiment r) => 1;
    public HierarchyTree GetHierarchyTree(Regiment r, bool detached, bool skirmishers, bool sort, bool inactive) => new() { reg = r, attachedunitreg = r.Children.ToArray() };
    GroupPosition BuildGroupOffsetPositions(HierarchyTree tree, float rotation, int localFormation, int wholeFormation)
    {
        if (ThrowOnMeasure) throw new InvalidOperationException("Injected layout failure");
        if (tree.attachedunitreg.Any(r => r.groupposition == null)) throw new Exception("Child not measured yet");
        Measured.Add(tree.reg);
        return new() { groupwidth = 20, groupdepth = 10, attachedunits = tree.attachedunitreg.Length,
            attachedunitreg = tree.attachedunitreg, unitoffsetposition = tree.attachedunitreg.Select((r,i) => new Vector3(i * 20, 0, 10)).ToArray() };
    }
    public void GTIP_Original_Formation(Regiment r, int f, float rotation, Vector3 target, bool immediate, bool path, bool modify,
        int state, int refuse, bool ignore, bool skip, bool show, bool entrench, bool terrain) { Fallbacks++; }
}
public class BattlefieldSetup
{
    public Vector3 GetAngleTerrainPosXY(Vector3 origin, float rotation, float x, float z, Terrain terrain) => new(origin.x + x, origin.y, origin.z + z);
    public class TerrainShape
    {
        public TerrainShape(Vector3 p, float rotation, float width, List<int> ignored) { }
        public Tools.InterpolatedVector InterpolatePosition(Vector3 p) => new() { position = p, facing = 90 };
    }
}
public static class Tools { public class InterpolatedVector { public Vector3 position; public float facing; } }
namespace UnityEngine
{
    public class Transform { public Vector3 position; }
    public class Terrain { public static Terrain activeTerrain = new(); }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static bool operator ==(Vector3 a, Vector3 b) => a.x==b.x && a.y==b.y && a.z==b.z;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a==b);
        public override bool Equals(object obj) => obj is Vector3 other && this==other;
        public override int GetHashCode() => HashCode.Combine(x,y,z);
    }
}
