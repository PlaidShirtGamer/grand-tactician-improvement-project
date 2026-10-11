using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GTIP
{
    public static class HierarchySidebar
    {
        public static void Sidebar(ToolsUnitList view)
        {
            if (!SixLevelRuntime.Active || view.selectedview != 0)
            { NativeAccess.Call(view, "GTIP_Original_Sidebar"); return; }
            NativeAccess.Call(view, "ClearList");
            var field = typeof(ToolsUnitList).GetField("toolslistline", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rows = (IList)Activator.CreateInstance(field.FieldType);
            field.SetValue(view, rows);
            view.FilterSelectionText.text = "Army";
            var displayed = new List<Regiment>();
            var seen = new HashSet<Regiment>();
            void Add(Regiment unit, int depth)
            {
                if (unit == null || !seen.Add(unit) || unit.unittyp == HierarchyModel.Fleet ||
                    unit.alliance != GameVars.playeralliance || unit.garrisonreference != null) return;
                bool visible = unit.gameObject.activeInHierarchy;
                if (visible)
                {
                    NativeAccess.Call(view, "AddListLine", unit, displayed.Count * view.YStep, depth > 0);
                    var row = NativeAccess.Get<GameObject>(rows[rows.Count - 1], "lineobject");
                    var spacer = row.transform.Find("SpacerL10");
                    if (spacer != null && depth > 0)
                    {
                        var layout = spacer.GetComponent<LayoutElement>() ?? spacer.gameObject.AddComponent<LayoutElement>();
                        layout.minWidth = layout.preferredWidth = depth * 10;
                    }
                    displayed.Add(unit);
                }
                foreach (var child in unit.GetAttachedUnitsReg(false, true, -1, true, true)) Add(child, depth + (visible ? 1 : 0));
            }
            foreach (var unit in BattleUnits.campaignunitlist)
                if (unit != null) Add(BattleUnits.GetHighestParent(unit), 0);
            NativeAccess.Set(view, "usedunits", displayed);
        }
    }
}
