using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    // Reuse the game's buttons and management callbacks, but build every depth.
    public static class HierarchyOob
    {
        public static GameObject PlusIcon(OOB view, Regiment parent, float x, float y, int orientation, bool inBattle)
        {
            var result = (GameObject)NativeAccess.Call(view, "GTIP_Original_PlusIcon", parent, x, y, orientation, inBattle);
            int child = parent == null ? -1 : HierarchyModel.ChildHeadquarters(parent.unittyp);
            if (SixLevelRuntime.Active && orientation == 1 && child >= 0)
                foreach (var panel in view.panelhandler.multiplepanel)
                    if (panel.go == result)
                    {
                        string description = "LMB to create a new " + GamePrefs.unittypename[child] + " below this " + GamePrefs.unittypename[parent.unittyp] + ".";
                        view.panelhandler.ExchangeButtonTooltipText(panel, 123, "Add new group", description);
                        view.panelhandler.ExchangeButtonTooltipText(panel, 176, "Add new group", description);
                        break;
                    }
            return result;
        }

        sealed class Node
        {
            internal Regiment Unit;
            internal Node Parent;
            internal readonly List<Node> Children = new List<Node>();
            internal float Width, X, Y;
            internal int Depth, Id;
            internal object Icon;
        }

        public static void Update(OOB view, GameObject requested)
        {
            var root = requested == null ? null : requested.GetComponent<Regiment>();
            if (root != null) root = BattleUnits.GetHighestParent(root);
            if (!SixLevelRuntime.Active || root == null || root.unittyp != HierarchyModel.Army)
            { NativeAccess.Call(view, "GTIP_Original_Update", requested); return; }
            if (!view.initialized) NativeAccess.Call(view, "Initialize");
            view.unit = requested;
            view.oobtitle.text = root.name;
            Array.Clear(view.unitsinhierarchylevel, 0, view.unitsinhierarchylevel.Length);
            view.maxbrigades = 0;
            var data = NativeAccess.Get<IList>(view, "icondata");
            if (data == null)
            {
                var field = typeof(OOB).GetField("icondata", BindingFlags.Instance | BindingFlags.NonPublic);
                data = (IList)Activator.CreateInstance(field.FieldType);
                NativeAccess.Set(view, "icondata", data);
            }
            data.Clear();
            NativeAccess.Get<IList>(view, "unitsregistered").Clear();
            var folder = view.FolderToPlaceUnitIconsTo;
            foreach (Transform child in folder.transform) UnityEngine.Object.Destroy(child.gameObject);
            folder.transform.localScale = Vector3.one;
            var lines = new GameObject("LineFolder");
            lines.transform.SetParent(folder.transform, false);
            var nodes = new List<Node>();
            var seen = new HashSet<Regiment>();
            Node Build(Regiment unit, Node parent)
            {
                if (!seen.Add(unit)) return null;
                var node = new Node { Unit = unit, Parent = parent, Depth = parent == null ? 0 : parent.Depth + 1, Id = nodes.Count };
                nodes.Add(node);
                foreach (var child in unit.GetAttachedUnitsReg(view.excludedechainedunits, view.excludeskirmishers, -1, true, true, false, false, true))
                {
                    var next = Build(child, node);
                    if (next != null) node.Children.Add(next);
                }
                float width = 0;
                bool combatColumn = false;
                foreach (var child in node.Children)
                    if (child.Unit.unittyp > 13) width += child.Width + view.sparex;
                    else combatColumn = true;
                if (combatColumn) width += view.iconsizex + view.sparex;
                node.Width = Math.Max(view.iconsizex, width - view.sparex);
                return node;
            }
            void Place(Node node, float left)
            {
                node.X = left + node.Width / 2;
                node.Y = 30 + node.Depth * (view.iconsizey + view.sparey + 20);
                foreach (var child in node.Children)
                    if (child.Unit.unittyp > 13) { Place(child, left); left += child.Width + view.sparex; }
                int row = 0;
                foreach (var child in node.Children)
                    if (child.Unit.unittyp <= 13)
                    {
                        child.X = left + view.iconsizex / 2;
                        child.Y = node.Y + view.iconsizey + view.sparey + 20 + row++ * (view.iconsizey + view.spareybrigs);
                    }
            }
            Place(Build(root, null), 20);
            var iconType = typeof(OOB).GetNestedType("IconData", BindingFlags.NonPublic);
            foreach (var node in nodes)
            {
                node.Icon = Activator.CreateInstance(iconType, new object[] { node.Unit.gameObject, node.Unit,
                    node.Parent == null ? null : node.Parent.Unit.gameObject, new Vector2(node.X, node.Y),
                    new Vector2(node.Width, view.iconsizey), node.Unit.unittyp, node.Depth, node.Parent == null ? 0 : node.Parent.Id });
                data.Add(node.Icon);
                var ids = NativeAccess.Get<List<int>>(node.Icon, "subsidiaries");
                foreach (var child in node.Children) ids.Add(child.Id);
                if (node.Unit.unittyp >= 0 && node.Unit.unittyp < view.unitsinhierarchylevel.Length) view.unitsinhierarchylevel[node.Unit.unittyp]++;
                if (node.Unit.unittyp <= 13) view.maxbrigades++;
                NativeAccess.Set(node.Icon, "referencebuttonrecttf", NativeAccess.Call(view, "AddUnitIcon", node.Unit, node.X, -node.Y, false));
            }
            // Native buttons apply their own prefab scale. Draw in their actual
            // local coordinate system rather than the unscaled logical layout.
            // Shared trunks must be drawn once, not alpha-blended once per child.
            foreach (var parent in nodes.Where(n => n.Children.Count > 0))
            {
                var parentRect = NativeAccess.Get<RectTransform>(parent.Icon, "referencebuttonrecttf");
                Vector2 from = parentRect.localPosition;
                from.y -= view.iconsizey * parentRect.localScale.y / 3;
                float joint = from.y - view.sparey * parentRect.localScale.y / 2;
                var tips = parent.Children.Select(child => {
                    var rect = NativeAccess.Get<RectTransform>(child.Icon, "referencebuttonrecttf");
                    Vector2 tip = rect.localPosition; tip.y += view.iconsizey * rect.localScale.y / 3; return tip;
                }).ToArray();
                var color = new Color(.16f, .16f, .16f, .36f);
                Tools.DrawLineUI(lines, from, new Vector2(from.x, joint), 2, color);
                float left = Math.Min(from.x, tips.Min(t => t.x)), right = Math.Max(from.x, tips.Max(t => t.x));
                if (right > left) Tools.DrawLineUI(lines, new Vector2(left, joint), new Vector2(right, joint), 2, color);
                foreach (var column in tips.GroupBy(t => t.x))
                    Tools.DrawLineUI(lines, new Vector2(column.Key, joint), new Vector2(column.Key, column.Min(t => t.y)), 2, color);
            }
            view.panelhandler.ActivateDragAndDrop(new[] { 123 });
            view.UpdatePlusIcons();
            folder.transform.localScale = Vector3.one * view.panelhandler.uiscale;
            NativeAccess.Call(view, "UpdateBattleButtonStats");
            NativeAccess.Set(view, "currentshownunit", root);
        }
    }
}
