using System;

namespace GTIP
{
    internal static class EditorFormationRules
    {
        internal static bool Land(int type) => type >= 0 && type <= 2 || type >= 14 && type <= 16 || type == 18;
        internal static int Level(int type) => type == 18 ? 4 : type >= 14 && type <= 16 ? type - 13 : type >= 0 && type <= 2 ? 0 : -1;
        internal static bool CanParent(int child, int parent) => Land(child) && Land(parent) && Level(parent) > Level(child);
        internal static void Transfer(int child, int parent, int side, int targetSide, bool same, bool descendant)
        {
            if (same || descendant) throw new InvalidOperationException("A unit cannot be attached to itself or its descendants.");
            if (side != targetSide) throw new InvalidOperationException("Choose a command from the same faction.");
            if (!CanParent(child, parent)) throw new InvalidOperationException("Choose a higher land headquarters.");
        }
    }
}
