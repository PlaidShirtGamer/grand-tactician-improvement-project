using System;
using System.Collections.Generic;
using UnityEngine;

namespace GTIP
{
    public static class SixLevelRuntime
    {
        public static bool Active => !DLC_WL.dlc_scenarioactive && GamePrefs.commandhierarchyshift == -1;
        public static int ArmyType(int original) => Active ? HierarchyModel.Army : original;

        public static bool UpgradeArmy(CampaignMainPanel panel, bool checkOnly)
        {
            if (!Active) return (bool)NativeAccess.Call(panel, "GTIP_Original_UpgradeArmy", checkOnly);
            var ui = NativeAccess.Get<BattleUI>(panel, "bui");
            if (ui.selectedunitreg == null) return false;
            var top = BattleUnits.GetHighestParent(ui.selectedunitreg);
            if (top == null || top.inbattle) return false;
            // Preserve the older Division-to-Corps upgrade, then allow another tier.
            if (top.unittyp == HierarchyModel.Division)
                return (bool)NativeAccess.Call(panel, "GTIP_Original_UpgradeArmy", checkOnly);
            if (top.unittyp != HierarchyModel.Corps || top.garrisonreference != null) return false;
            if (checkOnly) return true;
            var army = ArmyInsertion.Insert(NativeAccess.Get<BattleUnits>(panel, "bunits"), top);
            if (army == null) return false;
            NativeAccess.Call(panel, "UpdateOOBAfterUnitUpdates", army);
            return true;
        }

        public static void AddArmyGroupUnit(ArmyGroup group, Regiment unit, bool fromLoad, bool rename)
        {
            if (Active && !fromLoad && unit != null && unit.unittyp == HierarchyModel.Corps &&
                !unit.inbattle && BattleUnits.GetHighestParent(unit) == unit)
            {
                var engine = GameObject.Find("GameController").GetComponent<BattleUnits>();
                unit = ArmyInsertion.Insert(engine, unit) ?? unit;
            }
            NativeAccess.Call(group, "GTIP_Original_AddArmyGroupUnit", unit, fromLoad, rename);
        }

        public static GameObject CreatedUnit(GameObject created)
        {
            var unit = created == null ? null : created.GetComponent<Regiment>();
            if (Active && unit != null && unit.unittyp == HierarchyModel.Army)
            {
                created.tag = "Army";
                unit.overridesymbol = HierarchyModel.Army;
                RepairArmyCommander(unit);
                Debug.Log("[GTIP] Army HQ created/loaded: " + created.name);
            }
            return created;
        }

        public static void RepairArmyCommander(Regiment unit)
        {
            if (!Active || unit == null || unit.unittyp != HierarchyModel.Army ||
                unit.commander < 0 || unit.commander >= GameVars.commander.Count) return;
            var commander = GameVars.commander[unit.commander];
            int count = Math.Min(GameVars.commanderranks, GameVars.rankabbrev.Length);
            if (commander.rank < 0 || commander.rank >= count)
            {
                commander.rank = HierarchyModel.SafeArmyRank(commander.rank, GameVars.GetMinRankForUnittyp(HierarchyModel.Army, unit.alliance), count);
                commander.refreshcommand = true;
            }
        }

        public static void CommanderPanel(BattleUI ui, PanelHandler.Panel panel, Regiment unit, int commander,
            int image, PanelHandler handler, Tools.Date date, int offset)
        {
            RepairArmyCommander(unit ?? ui.selectedunitreg);
            NativeAccess.Call(ui, "GTIP_Original_CommanderPanel", panel, unit, commander, image, handler, date, offset);
        }

        public static void UpgradeButtons(CampaignMainPanel panel)
        {
            NativeAccess.Call(panel, "GTIP_Original_UpgradeButtons");
            if (!Active) return;
            var handler = NativeAccess.Get<PanelHandler>(panel, "panelhandler");
            var ui = NativeAccess.Get<BattleUI>(panel, "bui");
            var root = ui.selectedunitreg == null ? null : BattleUnits.GetHighestParent(ui.selectedunitreg);
            var button = handler.GetButtonReference(handler.panel[handler.panel_oob], 566);
            if (button != null)
                button.ExchangeTooltipText(root != null && root.unittyp == HierarchyModel.Division ? "Upgrade Division to Corps" : "Upgrade Corps to Army",
                    root != null && root.unittyp == HierarchyModel.Division ? "Upgrade this independent Division to a Corps." :
                    "Create an Army headquarters above this Corps, preserving its commander and subordinate units.");
        }

        public static int ImportedType(int inferred, int savedSymbol)
        {
            if (!Active) return inferred;
            int type = HierarchyModel.LoadType(inferred, savedSymbol);
            if (type == HierarchyModel.Army) EnsureArmyData();
            return type;
        }

        internal static void EnsureArmyData()
        {
            if (GameVars.meleeweaponstandardperunittype.Length < 19)
                Array.Resize(ref GameVars.meleeweaponstandardperunittype, 19);
            var previous = GamePrefs.influenceofattributesonfightingspirit;
            if (previous != null && previous.GetLength(0) < 6)
            {
                var expanded = new float[6, previous.GetLength(1)];
                for (int row = 0; row < 6; row++)
                    for (int col = 0; col < previous.GetLength(1); col++)
                        expanded[row, col] = previous[Math.Min(row, previous.GetLength(0) - 1), col];
                GamePrefs.influenceofattributesonfightingspirit = expanded;
            }
        }

        public static void FightingSpirit(Regiment unit)
        {
            if (Active && unit.unittyp == HierarchyModel.Army) EnsureArmyData();
            NativeAccess.Call(unit, "GTIP_Original_FightingSpirit");
        }

        public static void CheckArmyStateSelection(CampaignMainPanel panel)
        {
            if (!Active) { NativeAccess.Call(panel, "GTIP_Original_CheckArmyStateSelection"); return; }
            var recruiting = NativeAccess.Get<RecruitingList>(panel, "recruitinglist");
            if (panel.contentselection != 11 || recruiting.chosenstate < 0) return;
            EnsureArmyData();
            int type = panel.raisecorps ? HierarchyModel.Corps : HierarchyModel.Army;
            var engine = NativeAccess.Get<BattleUnits>(panel, "bunits");
            var created = engine.CreateUnitDefault(null, recruiting.chosenstate, type,
                _alliance: GameVars.playeralliance, overridelev: type);
            if (created != null) NativeAccess.Call(panel, "UpdateOOBAfterUnitUpdates", created);
        }

        public static void RecruitGroup(CampaignMainPanel panel, Regiment parent)
        {
            if (!Active) { NativeAccess.Call(panel, "GTIP_Original_RecruitGroup", parent); return; }
            if (parent == null) return;
            int childType = HierarchyModel.ChildHeadquarters(parent.unittyp);
            if (childType < 0) return;
            var units = NativeAccess.Get<BattleUnits>(panel, "bunits");
            var created = units.CreateUnitDefault(parent, -1, childType);
            if (created != null) NativeAccess.Call(panel, "UpdateOOBAfterUnitUpdates", created);
        }

        public static int Visibility(Regiment unit)
        {
            if (!Active || unit == null || unit.unittyp != HierarchyModel.Army)
                return (int)NativeAccess.Call(typeof(BattleUnits), "GTIP_Original_Visibility", unit);
            // An Army with direct fighting troops needs a fighting map marker.
            // Otherwise it is a command HQ; its corps remain independent map units.
            foreach (var child in unit.GetAttachedUnitsReg(false, true, -1, true, true))
                if (child != null && child.unittyp < HierarchyModel.Corps) return 1;
            return 2;
        }

        public static Regiment.UnitSymbolData Symbol(Regiment unit)
        {
            var symbol = (Regiment.UnitSymbolData)NativeAccess.Call(unit, "GTIP_Original_Symbol");
            if (Active && unit.unittyp == HierarchyModel.Army)
            {
                symbol.sname = "Units/Symbols/Army";
                symbol.oobhierarchyid = 6;
            }
            return symbol;
        }

        public static ArmyGroup CreateArmyGroup(GameObject prefab, string name, int commander,
            List<Regiment> units, Color color, int overridelevel, bool fromLoad, bool excludePlayer)
        {
            if (Active && !fromLoad && string.IsNullOrEmpty(name))
            {
                int number = 1;
                var names = new HashSet<string>();
                if (BattleUnits.armygroups != null)
                    foreach (var group in BattleUnits.armygroups) if (group != null) names.Add(group.name);
                while (names.Contains(Tools.ConvertToRoman(number) + " Army Group")) number++;
                name = Tools.ConvertToRoman(number) + " Army Group";
            }
            return (ArmyGroup)NativeAccess.Call(typeof(ArmyGroup), "GTIP_Original_CreateArmyGroup",
                prefab, name, commander, units, color, overridelevel, fromLoad, excludePlayer);
        }

        public static bool FightingForce(Regiment unit)
        {
            if (Active && unit != null && unit.unittyp == HierarchyModel.Army)
                return unit.GetAttachedUnitsReg(false, true, HierarchyModel.Corps).Length == 0;
            return (bool)NativeAccess.Call(typeof(AICampaign), "GTIP_Original_FightingForce", unit);
        }

        public static Regiment ClosestFightingForce(Regiment unit)
        {
            if (!Active || unit == null || unit.unittyp != HierarchyModel.Army)
                return (Regiment)NativeAccess.Call(typeof(AICampaign), "GTIP_Original_ClosestFightingForce", unit);
            Regiment nearest = null;
            float distance = float.MaxValue;
            foreach (var child in unit.GetAttachedUnitsReg(false, true, HierarchyModel.Corps, false))
            {
                var force = AICampaign.GetClosestFightingForce(child);
                if (force == null) continue;
                float current = Vector3.Distance(unit.transform.position, force.transform.position);
                if (current < distance) { nearest = force; distance = current; }
            }
            return nearest ?? unit;
        }

        public static Regiment RaiseAiGroup(object faction, AICampaign.AIArea area, int state,
            bool noGrandArmy, bool forceNew, Vector3 position, Vector3 searchPosition)
        {
            var brigade = (Regiment)NativeAccess.Call(typeof(AICampaign), "GTIP_Original_RaiseAiGroup",
                faction, area, state, noGrandArmy, forceNew, position, searchPosition);
            if (!Active || noGrandArmy || brigade == null) return brigade;
            var top = BattleUnits.GetHighestParent(brigade);
            if (top == null || top.unittyp != HierarchyModel.Corps || top.assignedarmygroup != null) return brigade;
            EnsureArmyData();
            var engine = NativeAccess.Get<BattleUnits>(typeof(AICampaign), "bunits");
            var army = engine.CreateUnitDefault(null, -1, HierarchyModel.Army, _alliance: top.alliance,
                forcedcreationposition: top.transform.position, overridelev: HierarchyModel.Army);
            if (army == null) return brigade;
            top.transform.SetParent(army.transform, true);
            top.istopunit = false;
            BattleUnits.UpdateAllParentUnits(top.gameObject, true);
            var ownUnits = NativeAccess.Get<List<Regiment>>(faction, "ownunits");
            if (!ownUnits.Contains(army)) ownUnits.Add(army);
            return brigade;
        }
    }
}
