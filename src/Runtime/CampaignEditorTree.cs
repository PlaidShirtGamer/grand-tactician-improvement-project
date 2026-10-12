using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class CampaignEditor
    {
        bool graphicalTree = true;
        float treeZoom = .7f;
        Vector2 treeScroll, dragStart;
        CampaignEditorRecord dragRecord;
        bool dragging;
        int treeControl;
        List<CampaignEditorRecord> treeRows = new List<CampaignEditorRecord>();
        List<EditorTreeNode> treeNodes = new List<EditorTreeNode>();
        readonly HashSet<object> collapsedTargets = new HashSet<object>();
        bool treeInitialized;
        bool centerTree = true;
        GUIStyle treeText;
        float cardHeight = 150;
        string CardText(CampaignEditorRecord row)
        {
            string symbol = row.Target is ArmyGroup ? "XXXXX" : row.Target is Regiment u ? u.unittyp == 18 ? "XXXX" : u.unittyp == 16 ? "XXX" : u.unittyp == 15 ? "XX" : u.unittyp == 14 ? "X" : "I" : "";
            return symbol + "\n" + row.Title + "\n" + row.Detail;
        }
        void CancelTreeDrag()
        {
            if (treeControl != 0 && GUIUtility.hotControl == treeControl) GUIUtility.hotControl = 0;
            dragRecord = null; dragging = false;
        }
        void RebuildTree()
        {
            CancelTreeDrag();
            if (domain > 1) return;
            treeRows = CampaignEditorData.Read(domain, side, "");
            if (!treeInitialized)
            {
                foreach (var row in treeRows.Where(r => r.Depth >= 2)) collapsedTargets.Add(row.Target);
                treeInitialized = true;
            }
            bool[] matches = string.IsNullOrWhiteSpace(search) ? null : treeRows.Select(r =>
                (r.Title + " " + r.Detail).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            treeNodes = EditorTreeLayout.Build(treeRows.Select(r => r.Depth).ToArray(), matches,
                new HashSet<int>(Enumerable.Range(0, treeRows.Count).Where(i => collapsedTargets.Contains(treeRows[i].Target))));
            centerTree = true;
        }
        Rect TreeCard(EditorTreeNode n) => new Rect(20 + n.X * treeZoom, 20 + n.Depth * (cardHeight + 50) * treeZoom, 220 * treeZoom, cardHeight * treeZoom);
        void TreeLine(Vector2 a, Vector2 b)
        {
            var old = GUI.color; GUI.color = new Color(.45f, .62f, .68f);
            GUI.DrawTexture(new Rect(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Max(2, Math.Abs(a.x - b.x)), Math.Max(2, Math.Abs(a.y - b.y))), Texture2D.whiteTexture);
            GUI.color = old;
        }
        void DrawGraphicalTree(float width)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Focus", GUILayout.Width(65))) centerTree = true;
            if (GUILayout.Button("Collapse", GUILayout.Width(85))) { foreach (var r in treeRows.Where(r => r.Depth >= 1)) collapsedTargets.Add(r.Target); RebuildTree(); }
            if (GUILayout.Button("Expand", GUILayout.Width(75))) { collapsedTargets.Clear(); RebuildTree(); }
            GUILayout.Label("Zoom", GUILayout.Width(48));
            float zoom = GUILayout.HorizontalSlider(treeZoom, .55f, 1.2f, GUILayout.Width(95));
            if (Math.Abs(zoom - treeZoom) > .001f) { treeScroll *= zoom / treeZoom; treeZoom = zoom; CancelTreeDrag(); }
            if (GUILayout.Button("List", GUILayout.Width(60))) { graphicalTree = false; CancelTreeDrag(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Click a card to edit. Drag a unit onto an HQ or Army Group to review a transfer.");
            Rect viewport = GUILayoutUtility.GetRect(width, 300, GUILayout.ExpandHeight(true));
            if (treeText == null) treeText = new GUIStyle(skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            treeText.fontSize = Math.Max(9, (int)(15 * treeZoom));
            cardHeight = Math.Max(150, treeNodes.Count == 0 ? 150 : treeNodes.Max(n =>
                (treeText.CalcHeight(new GUIContent(CardText(treeRows[n.Index])), 220 * treeZoom - 36) + 12) / treeZoom));
            if (centerTree && Event.current.type == EventType.Repaint && treeNodes.Count > 0)
            {
                var focus = treeNodes.FirstOrDefault(n => treeRows[n.Index].Target == selected?.Target) ?? treeNodes.OrderBy(n => n.Depth).ThenBy(n => n.Index).First();
                var card = TreeCard(focus);
                treeScroll = new Vector2(Math.Max(0, card.center.x - viewport.width / 2), Math.Max(0, card.y - 30)); centerTree = false;
            }
            float contentW = Math.Max(viewport.width - 18, treeNodes.Count == 0 ? 0 : treeNodes.Max(n => TreeCard(n).xMax) + 30);
            float contentH = Math.Max(viewport.height - 18, treeNodes.Count == 0 ? 0 : treeNodes.Max(n => TreeCard(n).yMax) + 30);
            treeControl = GUIUtility.GetControlID(FocusType.Passive);
            var ev = Event.current;
            bool inside = viewport.Contains(ev.mousePosition);
            treeScroll = GUI.BeginScrollView(viewport, treeScroll, new Rect(0, 0, contentW, contentH));
            var positions = treeNodes.ToDictionary(n => n.Index);
            foreach (var n in treeNodes)
                if (positions.TryGetValue(n.Parent, out var parent))
                {
                    var p = TreeCard(parent); var c = TreeCard(n); float mid = (p.yMax + c.yMin) / 2;
                    TreeLine(new Vector2(p.center.x, p.yMax), new Vector2(p.center.x, mid));
                    TreeLine(new Vector2(p.center.x, mid), new Vector2(c.center.x, mid));
                    TreeLine(new Vector2(c.center.x, mid), new Vector2(c.center.x, c.yMin));
                }
            var view = new Rect(treeScroll.x, treeScroll.y, viewport.width, viewport.height);
            EditorTreeNode hovered = null;
            foreach (var n in treeNodes)
            {
                var card = TreeCard(n); if (!view.Overlaps(card)) continue;
                var row = treeRows[n.Index];
                if (inside && card.Contains(ev.mousePosition)) hovered = n;
                var old = GUI.color;
                GUI.color = selected?.Target == row.Target ? new Color(.35f, .65f, .85f) : new Color(.2f, .31f, .38f);
                GUI.DrawTexture(card, Texture2D.whiteTexture); GUI.color = old;
                if (dragging && inside && card.Contains(ev.mousePosition))
                {
                    GUI.color = new Color(.95f, .7f, .25f);
                    GUI.DrawTexture(new Rect(card.x, card.y, card.width, 3), Texture2D.whiteTexture); GUI.color = old;
                }
                GUI.Label(new Rect(card.x + 6, card.y + 6, card.width - 36, card.height - 12), CardText(row), treeText);
                if (n.Children.Count > 0 && GUI.Button(new Rect(card.xMax - 24, card.y, 24, 24), collapsedTargets.Contains(row.Target) ? "+" : "−"))
                { if (!collapsedTargets.Add(row.Target)) collapsedTargets.Remove(row.Target); RebuildTree(); break; }
            }
            if (ev.type == EventType.MouseDown && ev.button == 0 && hovered != null)
            {
                dragRecord = treeRows[hovered.Index]; dragStart = ev.mousePosition; dragging = false;
                GUIUtility.hotControl = treeControl; ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && GUIUtility.hotControl == treeControl && dragRecord != null)
            { dragging |= Vector2.Distance(dragStart, ev.mousePosition) > 7; ev.Use(); }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && GUIUtility.hotControl == treeControl && dragRecord != null)
            {
                var source = dragRecord; bool wasDragging = dragging; CancelTreeDrag();
                if (!wasDragging) Select(source);
                else if (hovered != null && source.Target is Regiment unit)
                {
                    var destination = treeRows[hovered.Index].Target;
                    Run(() => {
                        EditorFormationOperations.CheckMove(unit, destination);
                        Select(source); BeginOperation("move");
                        operationReview = "Move " + unit.name + " to " + DestinationName(destination) + ". Native transfer delays may apply.";
                        operationApply = () => EditorFormationOperations.Move(unit, destination);
                    });
                }
                else status = "Drop a land formation on a compatible headquarters or Army Group.";
                ev.Use();
            }
            if (dragging && dragRecord != null) GUI.Label(new Rect(ev.mousePosition.x + 15, ev.mousePosition.y + 15, 230, 55), "Move: " + dragRecord.Title);
            if (dragging && inside && ev.type == EventType.Repaint)
            {
                var point = ev.mousePosition - treeScroll;
                float step = Math.Min(.05f, Time.unscaledDeltaTime) * 500;
                treeScroll.x = Mathf.Clamp(treeScroll.x + (point.x < 35 ? -step : point.x > viewport.width - 35 ? step : 0), 0, Math.Max(0, contentW - viewport.width + 18));
                treeScroll.y = Mathf.Clamp(treeScroll.y + (point.y < 35 ? -step : point.y > viewport.height - 35 ? step : 0), 0, Math.Max(0, contentH - viewport.height + 18));
            }
            GUI.EndScrollView();
            GUILayout.EndVertical();
        }
    }
}
