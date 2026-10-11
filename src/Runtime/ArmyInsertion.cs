using UnityEngine;

namespace GTIP
{
    public static class ArmyInsertion
    {
        // Insert an HQ; changing the Corps' type would merely rename the missing tier.
        public static Regiment Insert(BattleUnits engine, Regiment corps)
        {
            if (corps == null || corps.unittyp != HierarchyModel.Corps || corps.inbattle ||
                BattleUnits.GetHighestParent(corps) != corps) return null;
            SixLevelRuntime.EnsureArmyData();
            var army = engine.CreateUnitDefault(null, -1, HierarchyModel.Army, _alliance: corps.alliance,
                forcedcreationposition: corps.transform.position, overridelev: HierarchyModel.Army);
            if (army == null) return null;
            var group = corps.assignedarmygroup;
            // Add the replacement first: removing the last Corps destroys its group.
            if (group != null)
            {
                NativeAccess.Call(group, "GTIP_Original_AddArmyGroupUnit", army, false, false);
                group.RemoveUnit(corps);
            }
            corps.transform.SetParent(army.transform, true);
            corps.istopunit = false;
            corps.permanentlydetached = true;
            BattleUnits.UpdateAllParentUnits(corps.gameObject, true);
            army.UpdateGroupStrength(overridecalc: true, ignoreactivestate: true);
            BattleUnits.UpdateUnitList();
            ToolsUnitList.SetDirty();
            return army;
        }

    }
}
