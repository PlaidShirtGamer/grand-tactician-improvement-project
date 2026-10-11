using GTIP;

var units = new BattleUnits();
// Synthetic levels exercise traversal depth; they are not a proposed game ID map.
Regiment[] chain = Enumerable.Range(0, 6).Select(i => new Regiment(30 - i)).ToArray();
for (int i = 0; i < chain.Length - 1; i++) chain[i].Children.Add(chain[i + 1]);
void Dispatch(int level = -1, bool inactive = false, bool routed = false, bool sameAi = false)
    => HierarchyCommands.Dispatch(units, chain[0], level, "Test", 42, inactive, routed, sameAi);
void Reset() { foreach (var r in chain) r.gameObject.Messages.Clear(); }
void Check(bool result, string description) { if (!result) throw new Exception(description); Console.WriteLine("PASS " + description); }
Dispatch();
Check(chain.All(r => r.gameObject.Messages.SequenceEqual(new[] { "Test:42" })), "Six tiers each receive exactly one order");
Reset(); Dispatch(25);
Check(chain.Take(5).All(r => r.gameObject.Messages.Count == 0) && chain[5].gameObject.Messages.Count == 1, "Level filter reaches deepest tier");
Reset(); chain[2].gameObject.activeInHierarchy = false; Dispatch();
Check(chain[2].gameObject.Messages.Count == 0 && chain[5].gameObject.Messages.Count == 1, "Inactive parent does not suppress eligible descendants");
Reset(); Dispatch(inactive: true);
Check(chain[2].gameObject.Messages.Count == 1, "Include inactive units flag");
chain[2].gameObject.activeInHierarchy = true;
Reset(); chain[3].isrouted = true; chain[4].onretreat = true; Dispatch(routed: true);
Check(chain[3].gameObject.Messages.Count == 0 && chain[4].gameObject.Messages.Count == 0 && chain[5].gameObject.Messages.Count == 1, "Routed and retreating filters");
chain[3].isrouted = false; chain[4].onretreat = false;
Reset(); chain[2].permanentlydetached = true; Dispatch();
Check(chain[2].gameObject.Messages.Count == 0 && chain[5].gameObject.Messages.Count == 1, "Detached recipient filter");
chain[2].permanentlydetached = false;
Reset(); chain[4].groupaiobject = new object(); Dispatch(sameAi: true);
Check(chain[4].gameObject.Messages.Count == 0 && chain[5].gameObject.Messages.Count == 1, "AI group filter");
chain[4].groupaiobject = null;
Reset(); chain[5].Children.Add(chain[0]); chain[0].Children.Add(chain[1]); Dispatch();
Check(chain.All(r => r.gameObject.Messages.Count == 1), "Cycles and duplicate references terminate without duplicate orders");
HierarchyCommands.Dispatch(units, null, -1, "Test", null, false, false, false);
DLC_WL.dlc_scenarioactive = true; Dispatch();
Check(units.FallbackCalls == 1, "DLC delegates to original game dispatcher");
DLC_WL.dlc_scenarioactive = false; GamePrefs.commandhierarchyshift = 0; Dispatch();
Check(units.FallbackCalls == 2, "Unshifted hierarchy delegates to original game dispatcher");
Console.WriteLine("All traversal checks passed. These use stubs and do not validate Unity gameplay.");

public static class DLC_WL { public static bool dlc_scenarioactive; }
public static class GamePrefs { public static int commandhierarchyshift = -1; }
public class Regiment
{
    static int nextId;
    readonly int id = ++nextId;
    public int Level;
    public UnityEngine.GameObject gameObject;
    public bool permanentlydetached, isrouted, onretreat;
    public object groupaiobject;
    public List<Regiment> Children = new();
    public Regiment(int level) { Level = level; gameObject = new(this); }
    public int GetInstanceID() => id;
}
public class BattleUnits
{
    public int FallbackCalls;
    public class HierarchyTree
    {
        public int hierarchylevel, attachedunits;
        public UnityEngine.GameObject[] attachedunit;
    }
    public HierarchyTree GetHierarchyTree(Regiment r) => new()
    {
        hierarchylevel = r.Level, attachedunits = r.Children.Count,
        attachedunit = r.Children.Select(c => c.gameObject).ToArray()
    };
    public void GTIP_Original_Dispatch(Regiment r, int l, string f, object v, bool a, bool e, bool s) => FallbackCalls++;
}
namespace UnityEngine
{
    public class GameObject
    {
        readonly Regiment owner;
        public bool activeInHierarchy = true;
        public List<string> Messages = new();
        public GameObject(Regiment r) { owner = r; }
        public T GetComponent<T>() where T : class => owner as T;
        public void SendMessage(string f, object value) => Messages.Add(f + ":" + value);
    }
}
