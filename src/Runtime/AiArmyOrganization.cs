using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public static class AiArmyOrganization
    {
        public const int MaximumCorps = 5;

        static bool AiAlliance(int alliance) => alliance >= 0 && alliance < 2 &&
            (alliance != GameVars.playeralliance || GameVars.ai_vs_ai);

        static bool Available(Regiment unit, object faction)
        {
            if (unit == null || !AiAlliance(unit.alliance)) return false;
            var members = unit.gameObject.GetComponentsInChildren<Regiment>(true);
            foreach (var member in members)
            {
                if (member.inbattle || member.onretreat || member.garrisonreference != null ||
                    member.isgarrisonbasicunit || member.transfertime > 0 || member.regimentpaths > 0 ||
                    member.embarkmentduration > 0 || AICampaign.RaidForce.IsRaidUnit(member) ||
                    AICampaign.SeaInvasionForce.GetSeaInvasionForceReference(member) != null ||
                    AICampaign.FortConstructionOrder.UnitAlreadyConstructing(member)) return false;
                foreach (string field in new[] { "unitsindefensiveoperations", "unitsinoffensiveoperations", "groupstodefendcapital", "unitsconstructingsupplydepots" })
                {
                    var assigned = NativeAccess.Get<List<Regiment>>(faction, field);
                    if (assigned != null && assigned.Contains(member)) return false;
                }
            }
            return true;
        }

        static bool Compatible(Regiment corps, Regiment other, object faction) =>
            corps != other && other.alliance == corps.alliance && Available(other, faction) &&
            BattleUnits.GetHighestParent(other) == other && corps.assignedarmygroup == other.assignedarmygroup &&
            Vector3.Distance(corps.transform.position, other.transform.position) <= GamePrefs.aimaximumdistancetosearchforunitrelocations &&
            (AICampaign.IsWithinOperationsTheater(corps, other.transform.position) &&
             AICampaign.IsWithinOperationsTheater(other, corps.transform.position));

        static Regiment FindArmy(object faction, Regiment corps)
        {
            return NativeAccess.Get<List<Regiment>>(faction, "ownunits")
                .Where(unit => unit != null && unit.unittyp == HierarchyModel.Army && Compatible(corps, unit, faction) &&
                    unit.GetAttachedUnitsReg(false, true, HierarchyModel.Corps, true).Length < MaximumCorps)
                .OrderBy(unit => Vector3.Distance(corps.transform.position, unit.transform.position))
                .ThenBy(unit => unit.GetInstanceID()).FirstOrDefault();
        }

        static void Attach(BattleUnits engine, Regiment corps, Regiment army)
        {
            engine.MoveUnit(corps.gameObject, army.gameObject, skipdlccheck: true);
            if (corps.transform.parent != army.transform) return;
            // Corps remain independently movable, just as in ArmyInsertion.
            corps.permanentlydetached = true;
            BattleUnits.UpdateAllParentUnits(corps.gameObject, true);
            army.UpdateGroupStrength(overridecalc: true, ignoreactivestate: true);
            BattleUnits.UpdateUnitList();
            ToolsUnitList.SetDirty();
        }

        public static void IntegrateRaisedCorps(object faction, Regiment corps, bool forceNew)
        {
            if (!SixLevelRuntime.Active || corps == null || corps.unittyp != HierarchyModel.Corps ||
                !AiAlliance(corps.alliance) || BattleUnits.GetHighestParent(corps) != corps ||
                corps.assignedarmygroup != null || !Available(corps, faction)) return;
            var engine = NativeAccess.Get<BattleUnits>(typeof(AICampaign), "bunits");
            var army = forceNew ? null : FindArmy(faction, corps);
            if (army != null) Attach(engine, corps, army);
            else
            {
                army = ArmyInsertion.Insert(engine, corps);
                var own = NativeAccess.Get<List<Regiment>>(faction, "ownunits");
                if (army != null && !own.Contains(army)) own.Add(army);
            }
        }

        // Run after native Division-to-Corps consolidation. At most one attachment
        // per pass; never dismantle existing Armies or move their Corps away.
        public static void Consolidate(int factionIndex)
        {
            if (!SixLevelRuntime.Active) return;
            var factions = NativeAccess.Get<IList>(typeof(AICampaign), "aifaction");
            if (factions == null || factionIndex < 0 || factionIndex >= factions.Count) return;
            var faction = factions[factionIndex];
            int alliance = NativeAccess.Get<int>(faction, "allianceid");
            if (!AiAlliance(alliance) || !AICampaign.GrandArmyStructure(alliance)) return;
            var own = NativeAccess.Get<List<Regiment>>(faction, "ownunits");
            var independent = own.Where(unit => unit != null && unit.unittyp == HierarchyModel.Corps &&
                BattleUnits.GetHighestParent(unit) == unit && Available(unit, faction)).ToArray();
            var engine = NativeAccess.Get<BattleUnits>(typeof(AICampaign), "bunits");
            foreach (var corps in independent)
            {
                var army = FindArmy(faction, corps);
                if (army == null)
                {
                    var partner = independent.Where(unit => Compatible(corps, unit, faction))
                        .OrderBy(unit => Vector3.Distance(corps.transform.position, unit.transform.position)).FirstOrDefault();
                    if (partner == null) continue;
                    army = ArmyInsertion.Insert(engine, partner);
                    if (army == null) return;
                    if (!own.Contains(army)) own.Add(army);
                }
                Attach(engine, corps, army);
                return;
            }
        }
    }
}
