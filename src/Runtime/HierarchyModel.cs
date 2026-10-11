namespace GTIP
{
    // Fleet stays 17. Army Group is a separate ArmyGroup object, not a Regiment.
    public static class HierarchyModel
    {
        public static float CommanderExperienceMinimum(float nativeMinimum, bool active, bool actualArmy)
            => active && actualArmy ? 0f : nativeMinimum;

        public static int SafeArmyRank(int current, int minimum, int count)
        {
            if (count <= 0) return current;
            return current >= 0 && current < count ? current : System.Math.Max(0, System.Math.Min(count - 1, minimum));
        }

        public const int Brigade = 14, Division = 15, Corps = 16, Fleet = 17, Army = 18;
        public static int ChildHeadquarters(int type)
        {
            switch (type) { case Army: return Corps; case Corps: return Division;
                case Division: return Brigade; default: return -1; }
        }
        public static int LoadType(int inferred, int savedSymbol)
        {
            // groups.dat persists the actual HQ type in overridesymbol. A four-HQ
            // tree otherwise infers 13 + 4 = 17 and silently turns into a fleet.
            if (savedSymbol == Fleet) return Fleet;
            if (savedSymbol == Army) return Army;
            return inferred;
        }
        public static int ParentHeadquarters(int type)
        {
            if (type <= 13) return Brigade;
            switch (type) { case Brigade: return Division; case Division: return Corps;
                case Corps: return Army; default: return -1; }
        }
    }
}
