using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    // Structural changes deliberately use native campaign operations. Unlike field
    // transactions, creation/deletion and native transfer delays are not undoable.
    internal static class EditorFormationOperations
    {
        internal static BattleUnits Engine => GameObject.Find("GameController").GetComponent<BattleUnits>();
        internal static Regiment Parent(Regiment unit) => unit.transform.parent == null ? null : unit.transform.parent.GetComponent<Regiment>();
        internal static Regiment[] Children(Regiment unit) => BattleUnits.completeunitlist.Where(u => u != null && Parent(u) == unit).ToArray();
        internal static void Check(Regiment unit)
        {
            if (unit == null || !BattleUnits.completeunitlist.Contains(unit) || !EditorFormationRules.Land(unit.unittyp))
                throw new InvalidOperationException("Select an existing land formation.");
            foreach (var member in unit.GetComponentsInChildren<Regiment>(true))
            {
                if (member.inbattle || member.onretreat || member.transfertime > 0 || member.regimentpaths > 0 ||
                    member.embarkmentduration > 0 || member.garrisonreference != null || member.isgarrisonbasicunit ||
                    member.parentregiment != null || member.detachedregiment != null ||
                    AICampaign.RaidForce.IsRaidUnit(member) || AICampaign.SeaInvasionForce.GetSeaInvasionForceReference(member) != null ||
                    AICampaign.FortConstructionOrder.UnitAlreadyConstructing(member))
                    throw new InvalidOperationException("Formation must be stationary, outside battle, transfers, garrisons, raids and construction, with no detached companies.");
                var factions = NativeAccess.Get<IList>(typeof(AICampaign), "aifaction");
                if (factions != null) foreach (var faction in factions)
                    foreach (string field in new[] { "unitsindefensiveoperations", "unitsinoffensiveoperations", "groupstodefendcapital", "unitsconstructingsupplydepots" })
                        if (NativeAccess.Get<List<Regiment>>(faction, field)?.Contains(member) == true)
                            throw new InvalidOperationException("This formation is assigned to an AI operation. Wait until that operation finishes.");
            }
        }
        internal static void CheckGroup(ArmyGroup group)
        {
            if (group == null || !BattleUnits.armygroups.Contains(group)) throw new InvalidOperationException("Army Group no longer exists.");
            foreach (var unit in group.attachedunits) Check(unit);
        }
        // Native RemoveUnit destroys a group when its last member leaves. Preserve
        // an empty editor group, its commander and identity instead.
        static void DetachGroup(Regiment unit)
        {
            var old = unit.assignedarmygroup;
            if (old == null) return;
            old.attachedunits.Remove(unit);
            unit.AssignArmyGroup(null);
            NativeAccess.Call(old, "UpdateHierarchyLevel");
            unit.UpdateUnitIconCampaign(forcehiding: true);
            unit.UpdateUnitIconCampaign();
        }
        internal static void CheckMove(Regiment unit, object destination)
        {
            Check(unit);
            if (destination is Regiment parent)
            {
                Check(parent);
                EditorFormationRules.Transfer(unit.unittyp, parent.unittyp, unit.alliance, parent.alliance,
                    unit == parent, parent.transform.IsChildOf(unit.transform));
                if (Parent(unit) == parent) throw new InvalidOperationException("Already attached to that headquarters.");
            }
            else if (destination is ArmyGroup group)
            {
                CheckGroup(group);
                if (unit.unittyp < 14 || group.GetAlliance() != unit.alliance || unit.assignedarmygroup == group)
                    throw new InvalidOperationException("Choose a different Army Group of the same faction for a headquarters.");
                if (!group.CanAddFurtherUnits()) throw new InvalidOperationException("Army Group has reached its native command capacity.");
            }
            else if (destination != null || unit.unittyp < 14 || Parent(unit) == null && unit.assignedarmygroup == null)
                throw new InvalidOperationException("Only an attached headquarters can become an independent command.");
        }
        internal static void Move(Regiment unit, object destination)
        {
            CheckMove(unit, destination);
            var oldParent = Parent(unit);
            DetachGroup(unit);
            if (destination is Regiment parent)
            {
                Engine.MoveUnit(unit.gameObject, parent.gameObject, skipdlccheck: true);
                if (Parent(unit) != parent) throw new InvalidOperationException("The game rejected this transfer.");
                if (unit.unittyp >= 16) unit.permanentlydetached = true;
            }
            else
            {
                unit.transform.SetParent(BattleUnits.unitfolder.transform, true);
                unit.istopunit = true;
                unit.permanentlydetached = true;
                unit.gameObject.SetActive(true);
                if (destination is ArmyGroup group)
                    NativeAccess.Call(group, "GTIP_Original_AddArmyGroupUnit", unit, false, false);
            }
            if (oldParent != null) BattleUnits.UpdateAllParentUnits(oldParent.gameObject, true);
            BattleUnits.UpdateAllParentUnits(unit.gameObject, true);
            Refresh();
        }
        internal static List<int> Commanders(int side, int type, Regiment unit = null, bool armyGroup = false)
        {
            return Enumerable.Range(0, GameVars.commander.Count).Where(id => {
                var c = GameVars.commander[id];
                if (c == null || c.alliance != side || !c.activeincampaign || !c.IsAvailable() || c.HasCommand() ||
                    c.refreshcommand || c.historicalbranch == 4 || c.rank < GameVars.GetMinRankForUnittyp(type, side)) return false;
                // Type 18 is shared by GTIP Armies and native Army Groups. Only
                // Army Groups retain the native minimum-experience requirement.
                return type == 18 && !armyGroup ? c.rank <= GameVars.GetMaxRankForUnittyp(type, side, false) : c.CheckSuitableRank(type, unit);
            }).ToList();
        }
        internal static void ReplaceCommander(object target, int id)
        {
            var unit = target as Regiment; var group = target as ArmyGroup;
            if (unit != null) Check(unit); else CheckGroup(group);
            int side = unit != null ? unit.alliance : group.GetAlliance();
            if (!Commanders(side, unit != null ? unit.unittyp : 18, unit, group != null).Contains(id))
                throw new InvalidOperationException("Commander is no longer available or eligible.");
            if (unit != null) GameVars.commander[id].AssignCommando(unit, updatename: false);
            else ArmyGroup.AppointCommander(group, id);
            Refresh();
        }
        internal static void CheckCreate(int side, int type, Regiment parent, Town town, int commander)
        {
            if (type != 14 && type != 15 && type != 16 && type != 18 && type != 19)
                throw new InvalidOperationException("Choose a headquarters tier.");
            if (!Commanders(side, type == 19 ? 18 : type, null, type == 19).Contains(commander))
                throw new InvalidOperationException("Select an available commander of suitable rank and experience.");
            if (type == 19)
            {
                if (!GameVars.allowarmygroups || Engine.ArmyGroupPrefab == null) throw new InvalidOperationException("Army Groups are not available in this campaign.");
                if (parent != null) throw new InvalidOperationException("Army Groups are independent commands.");
            }
            else if (parent != null)
            {
                Check(parent); EditorFormationRules.Transfer(type, parent.unittyp, side, parent.alliance, false, false);
            }
            else if (town == null || !BattleUnits.towns.Contains(town) || town.Owner != side || town.state < 0 || town.state >= GameVars.nation.Length)
                throw new InvalidOperationException("Choose an owned town with a valid state.");
        }
        internal static object Create(int side, int type, string name, Regiment parent, Town town, int commander)
        {
            name = (string)LiveEditSession.Name(name);
            CheckCreate(side, type, parent, town, commander);
            if (type == 19)
            {
                var group = ArmyGroup.CreateNewArmyGroup(Engine.ArmyGroupPrefab, name, commander, new List<Regiment>(), excludeplayerunits: false);
                if (group == null) throw new InvalidOperationException("The game could not create an Army Group.");
                Refresh(); return group;
            }
            SixLevelRuntime.EnsureArmyData();
            var created = Engine.CreateUnitDefault(parent, parent == null ? town.state : -1, type, _alliance: side,
                forcedcreationposition: parent == null ? town.transform.position : parent.transform.position, overridelev: type);
            if (created == null) throw new InvalidOperationException("The game could not create this headquarters.");
            GameVars.commander[commander].AssignCommando(created, updatename: false);
            created.name = name;
            if (type >= 16) created.permanentlydetached = true;
            Refresh(); return created;
        }
        internal static void CheckDelete(object target)
        {
            if (target is ArmyGroup group) { CheckGroup(group); return; }
            var unit = target as Regiment; Check(unit);
            if (unit.commander < 0 || unit.commander >= GameVars.commander.Count)
                throw new InvalidOperationException("Assign a valid commander before deleting this formation.");
            if (GameVars.commander[unit.commander].currentcommand != unit)
                throw new InvalidOperationException("Commander ownership is inconsistent. Replace its commander before deleting this formation.");
            if (Parent(unit) == null && Children(unit).Any(c => c.unittyp < 14))
                throw new InvalidOperationException("Attach this headquarters beneath another HQ before deleting it, so its combat units retain a parent.");
        }
        internal static void Delete(object target)
        {
            CheckDelete(target);
            if (target is ArmyGroup group)
            {
                // Members remain independent commands. Native removal preserves them.
                group.RemoveArmyGroup(); Refresh(); return;
            }
            var unit = (Regiment)target; var parent = Parent(unit); var armyGroup = unit.assignedarmygroup;
            foreach (var child in Children(unit))
            {
                child.transform.SetParent(parent != null ? parent.transform : BattleUnits.unitfolder.transform, true);
                child.istopunit = parent == null;
                if (parent == null || child.unittyp >= 16) { child.permanentlydetached = true; child.gameObject.SetActive(true); }
                if (parent == null && armyGroup != null)
                    NativeAccess.Call(armyGroup, "GTIP_Original_AddArmyGroupUnit", child, false, false);
                BattleUnits.UpdateAllParentUnits(child.gameObject, true);
            }
            DetachGroup(unit);
            BattleUnits.DeleteUnit(unit.gameObject, transferrecruitstopool: unit.unittyp <= 2);
            if (parent != null) BattleUnits.UpdateAllParentUnits(parent.gameObject, true);
            Refresh();
        }
        internal static void Refresh()
        {
            BattleUnits.UpdateUnitList();
            foreach (var alliance in GameVars.alliance)
                if (alliance != null) { alliance.unitlist?.RemoveAll(u => u == null); alliance.lastmonthunitsformed?.RemoveAll(u => u == null); }
            var factions = NativeAccess.Get<IList>(typeof(AICampaign), "aifaction");
            if (factions != null) foreach (var faction in factions)
                NativeAccess.Call(faction, "SeparateUnits", BattleUnits.completeunitlist);
            CampaignEditorData.Refresh();
        }
    }
}
