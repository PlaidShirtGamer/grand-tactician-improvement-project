using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace GTIP
{
    // This is deliberately limited to traversal. Adding a new HQ type requires
    // separate recruitment, layout, formation, AI, and persistence work.
    public static class HierarchyCommands
    {
        public static void Dispatch(BattleUnits units, Regiment root, int level,
            string functiontocall, object value, bool alsononactive,
            bool excluderoutedandretreatingunits, bool sameaigrouponly)
        {
            if (DLC_WL.dlc_scenarioactive || GamePrefs.commandhierarchyshift != -1)
            {
                var original = typeof(BattleUnits).GetMethod("GTIP_Original_Dispatch",
                    BindingFlags.Public | BindingFlags.Instance);
                try
                {
                    original.Invoke(units, new object[] { root, level, functiontocall,
                        value, alsononactive, excluderoutedandretreatingunits, sameaigrouponly });
                }
                catch (TargetInvocationException error)
                {
                    ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                }
                return;
            }
            if (root == null) return;
            var visited = new HashSet<int>();
            var pending = new Stack<Regiment>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                if (current == null || !visited.Add(current.GetInstanceID())) continue;
                var tree = units.GetHierarchyTree(current);
                if (tree == null) continue;
                bool isRoot = current == root;
                bool send = (tree.hierarchylevel == level || level == -1)
                    && (current.gameObject.activeInHierarchy || alsononactive);
                if (!isRoot)
                {
                    send = send && !current.permanentlydetached
                        && (!excluderoutedandretreatingunits || (!current.isrouted && !current.onretreat))
                        && (!sameaigrouponly || root.groupaiobject == current.groupaiobject);
                }
                if (send) current.gameObject.SendMessage(functiontocall, value);
                // Reverse push preserves the original child order and sends once.
                for (int i = tree.attachedunits - 1; i >= 0; i--)
                    if (tree.attachedunit[i] != null)
                        pending.Push(tree.attachedunit[i].GetComponent<Regiment>());
            }
        }
    }
}
