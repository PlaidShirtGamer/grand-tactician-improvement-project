using System;
using System.Collections.Generic;
using System.Linq;

namespace GTIP
{
    // Pure layout, independent of Unity objects and save-format IDs.
    internal sealed class EditorTreeNode
    {
        internal int Index, Parent = -1, Depth;
        internal float X, Y;
        internal readonly List<int> Children = new List<int>();
    }
    internal static class EditorTreeLayout
    {
        internal static List<EditorTreeNode> Build(int[] depths, bool[] matches, ISet<int> collapsed)
        {
            var nodes = depths.Select((d, i) => new EditorTreeNode { Index = i }).ToArray();
            var stack = new List<int>();
            for (int i = 0; i < nodes.Length; i++)
            {
                while (stack.Count > 0 && depths[stack[stack.Count - 1]] >= depths[i]) stack.RemoveAt(stack.Count - 1);
                if (stack.Count > 0) { nodes[i].Parent = stack[stack.Count - 1]; nodes[nodes[i].Parent].Children.Add(i); }
                stack.Add(i);
            }
            var visible = new HashSet<int>();
            for (int i = 0; i < nodes.Length; i++) if (matches == null || matches[i])
                for (int n = i; n >= 0 && visible.Add(n); n = nodes[n].Parent) { }
            var result = new List<EditorTreeNode>(); float nextX = 0;
            Action<int, int> place = null;
            place = (id, depth) => {
                var node = nodes[id]; node.Depth = depth; node.Y = depth * 200;
                var children = node.Children.Where(visible.Contains).ToArray();
                if (collapsed.Contains(id) && matches == null) children = new int[0];
                if (children.Length == 0) { node.X = nextX; nextX += 250; }
                else { foreach (var child in children) place(child, depth + 1); node.X = (nodes[children[0]].X + nodes[children[children.Length - 1]].X) / 2; }
                result.Add(node);
            };
            foreach (var root in nodes.Where(n => n.Parent < 0 && visible.Contains(n.Index))) { place(root.Index, 0); nextX += 60; }
            return result;
        }
    }
}
