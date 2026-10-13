using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class CampaignEditor
    {
        string operation, operationReview, operationName, optionSearch = "";
        object operationSource;
        Action operationApply;
        int commanderSort; bool commanderDescending = true;
        int createTier, destinationIndex, commanderIndex, townIndex;
        Vector2 destinationScroll, commanderScroll, townScroll;
        readonly List<object> destinations = new List<object>();
        readonly List<int> eligibleCommanders = new List<int>();
        readonly List<Town> ownedTowns = new List<Town>();
        static readonly int[] CreateTypes = { 14, 15, 16, 18, 19 };
        static readonly string[] CreateLabels = { "Brigade", "Division", "Corps", "Army", "Army Group" };
        void ResetOperation() { operation = null; operationSource = null; operationApply = null; operationReview = null; }
        void DrawFormationButtons()
        {
            GUILayout.Space(15); GUILayout.Label("Formation operations");
            if (selected.Target is Regiment unit && EditorFormationRules.Land(unit.unittyp) && GUILayout.Button("Transfer / regroup")) Run(() => BeginOperation("move"));
            if ((selected.Target is ArmyGroup || selected.Target is Regiment land && EditorFormationRules.Land(land.unittyp)) && GUILayout.Button("Replace commander")) Run(() => BeginOperation("commander"));
            if (GUILayout.Button("Delete formation…")) Run(() => BeginOperation("delete"));
        }
        void BeginOperation(string mode)
        {
            ResetOperation(); pending = null;
            operation = mode; operationSource = selected?.Target;
            operationName = "New " + CreateLabels[createTier]; optionSearch = "";
            if (mode == "officer") InitializeOfficer();
            PopulateOptions();
        }
        void PopulateOptions()
        {
            destinations.Clear(); eligibleCommanders.Clear(); ownedTowns.Clear();
            destinationIndex = commanderIndex = townIndex = 0;
            destinationScroll = commanderScroll = townScroll = Vector2.zero;
            var unit = operationSource as Regiment; var group = operationSource as ArmyGroup;
            if (operation == "create" || operation == "move")
            {
                destinations.Add(null);
                int childType = operation == "create" ? CreateTypes[createTier] : unit.unittyp;
                foreach (var parent in BattleUnits.completeunitlist.Where(u => u != null && u.alliance == side && EditorFormationRules.CanParent(childType, u.unittyp)))
                    if (unit == null || parent != unit && !parent.transform.IsChildOf(unit.transform)) destinations.Add(parent);
                if (operation == "move" && childType >= 14)
                    destinations.AddRange(BattleUnits.armygroups.Where(g => g != null && g.GetAlliance() == side));
                ownedTowns.AddRange(BattleUnits.towns.Where(t => t != null && t.Owner == side && t.state >= 0 && t.state < GameVars.nation.Length).OrderBy(t => t.CityName));
            }
            if (operation == "commander" || operation == "create")
            {
                int type = operation == "create" ? CreateTypes[createTier] : unit != null ? unit.unittyp : 19;
                eligibleCommanders.AddRange(EditorFormationOperations.Commanders(side, type == 19 ? 18 : type, operation == "commander" ? unit : null, type == 19));
            }
        }
        static string DestinationName(object value) => value is Regiment unit ? unit.name + " — " + CampaignEditorData.Tier(unit.unittyp) :
            value is ArmyGroup group ? group.name + " — Army Group" : "Independent command";
        int Pick(string title, string[] labels, int index, ref Vector2 scroll)
        {
            GUILayout.Label(title + (labels.Length == 0 ? " — none available" : ""));
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Math.Min(160, Math.Max(45, labels.Length * 34))));
            for (int i = 0; i < labels.Length; i++)
                if ((string.IsNullOrWhiteSpace(optionSearch) || labels[i].IndexOf(optionSearch, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    GUILayout.Button((index == i ? "✓ " : "") + labels[i])) index = i;
            GUILayout.EndScrollView(); return index;
        }
        static readonly string[] CommanderStats = { "exp", "leadership", "initiative", "administration", "cunning", "fame" };
        static readonly string[] CommanderSortLabels = { "Name", "Rank", "Experience", "Leadership", "Initiative", "Administration", "Cunning", "Fame" };
        void DrawCommanderPicker()
        {
            GUILayout.Label("Available eligible commanders - attributes out of 100");
            int selectedId = eligibleCommanders.Count > 0 ? eligibleCommanders[Math.Min(commanderIndex, eligibleCommanders.Count - 1)] : -1;
            commanderSort = GUILayout.SelectionGrid(commanderSort, CommanderSortLabels, 4);
            commanderDescending = GUILayout.Toggle(commanderDescending, "Descending order");
            Func<int, double> score = id => commanderSort == 1 ? GameVars.commander[id].rank :
                commanderSort >= 2 ? NativeAccess.Get<float>(GameVars.commander[id], CommanderStats[commanderSort - 2]) : 0;
            IEnumerable<int> sorted = commanderSort == 0 ? (commanderDescending ? eligibleCommanders.OrderByDescending(CampaignEditorData.OfficerName) : eligibleCommanders.OrderBy(CampaignEditorData.OfficerName)) :
                (commanderDescending ? eligibleCommanders.OrderByDescending(score) : eligibleCommanders.OrderBy(score)).ThenBy(CampaignEditorData.OfficerName);
            var ordered = sorted.ToArray(); eligibleCommanders.Clear(); eligibleCommanders.AddRange(ordered);
            commanderIndex = Math.Max(0, eligibleCommanders.IndexOf(selectedId));
            commanderScroll = GUILayout.BeginScrollView(commanderScroll, GUILayout.Height(260));
            var style = new GUIStyle(skin.button) { fixedHeight = 0, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            for (int i = 0; i < eligibleCommanders.Count; i++)
            {
                int id = eligibleCommanders[i]; string name = CampaignEditorData.OfficerName(id);
                if (!string.IsNullOrWhiteSpace(optionSearch) && name.IndexOf(optionSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string detail = string.Join(" | ", CommanderStats.Select((field, index) => CommanderSortLabels[index + 2] + " " + (NativeAccess.Get<float>(GameVars.commander[id], field) * 100).ToString("0.#")));
                if (GUILayout.Button((i == commanderIndex ? "> " : "") + name + "\n" + detail, style, GUILayout.MinHeight(66))) commanderIndex = i;
            }
            GUILayout.EndScrollView();
        }
        void DrawOperation()
        {
            if (operation == "naming") { DrawNaming(); return; }
            GUILayout.Label(operation == "create" ? "Create headquarters" : operation == "officer" ? "Create officer in loaded campaign" : operation + ": " + DestinationName(operationSource));
            GUILayout.Label("Structural changes use the game's native operations. They clear this editor session's undo history and cannot be undone here. Save a separate checkpoint before applying.");
            if (operationReview != null)
            {
                GUILayout.Space(12); GUILayout.Label(operationReview);
                if (GUILayout.Button("Apply reviewed operation")) Run(() => {
                    var action = operationApply;
                    session.Clear();
                    try { action(); status = "Operation applied. Use the game's Save As after closing."; }
                    catch { status = "Native operation failed. Reload the checkpoint if the campaign changed."; throw; }
                    finally { Reload(); }
                });
                if (GUILayout.Button("Back to form")) { operationReview = null; operationApply = null; }
            }
            else
            {
                if (operation == "officer") DrawNewOfficer();
                if (operation == "create")
                {
                    int next = GUILayout.Toolbar(createTier, CreateLabels);
                    if (next != createTier) { createTier = next; operationName = "New " + CreateLabels[next]; PopulateOptions(); }
                    GUILayout.Label("Name"); operationName = GUILayout.TextField(operationName);
                }
                if (operation == "move" || operation == "create" || operation == "commander")
                {
                    GUILayout.Label("Filter choices"); optionSearch = GUILayout.TextField(optionSearch);
                    if (destinations.Count > 0 && !(operation == "create" && CreateTypes[createTier] == 19))
                        destinationIndex = Pick("Attach to", destinations.Select(DestinationName).ToArray(), destinationIndex, ref destinationScroll);
                    if (operation == "create" && CreateTypes[createTier] != 19 && destinations[destinationIndex] == null)
                        townIndex = Pick("Spawn town", ownedTowns.Select(t => t.CityName + " — " + GameVars.nation[t.state].name).ToArray(), townIndex, ref townScroll);
                    if (operation != "move")
                        DrawCommanderPicker();
                }
                if (operation == "delete") GUILayout.Label("HQ children move to its parent, or become independent HQs when there is no parent. Army Group members remain independent commands. Combat-unit deletion uses native disbanding and returns recruits/equipment.");
                if (GUILayout.Button("Review operation")) Run(ReviewOperation);
            }
            if (GUILayout.Button("Cancel operation")) ResetOperation();
        }
        void ReviewOperation()
        {
            if (operation == "officer") { ReviewNewOfficer(); return; }
            var source = operationSource;
            if (operation == "move")
            {
                var unit = (Regiment)source; var destination = destinations[destinationIndex];
                EditorFormationOperations.CheckMove(unit, destination);
                operationReview = "Move " + unit.name + " to " + DestinationName(destination) + ". Native transfer delays may apply; empty Army Groups are retained.";
                operationApply = () => EditorFormationOperations.Move(unit, destination);
            }
            else if (operation == "delete")
            {
                EditorFormationOperations.CheckDelete(source);
                operationReview = "Delete " + DestinationName(source) + ". " + (source is Regiment u ? EditorFormationOperations.Children(u).Length + " immediate children will be preserved and reattached." : "All attached commands will be preserved.");
                operationApply = () => EditorFormationOperations.Delete(source);
            }
            else
            {
                if (eligibleCommanders.Count == 0) throw new InvalidOperationException("No available commander meets this command's requirements.");
                int commander = eligibleCommanders[commanderIndex];
                if (operation == "commander")
                {
                    operationReview = "Assign " + CampaignEditorData.OfficerName(commander) + " to " + DestinationName(source) + ". The current commander will be released.";
                    operationApply = () => EditorFormationOperations.ReplaceCommander(source, commander);
                }
                else
                {
                    int type = CreateTypes[createTier]; string name = (string)LiveEditSession.Name(operationName);
                    var parent = type == 19 ? null : destinations[destinationIndex] as Regiment;
                    var town = ownedTowns.Count == 0 ? null : ownedTowns[townIndex];
                    EditorFormationOperations.CheckCreate(side, type, parent, town, commander);
                    int faction = side;
                    operationReview = "Create " + name + " (" + CreateLabels[createTier] + "), commanded by " + CampaignEditorData.OfficerName(commander) + ". " +
                        (type == 19 ? "Initially empty; attach commands using Transfer / regroup." : parent == null ? "Spawn at " + town.CityName + "." : "Attach beneath " + parent.name + ".");
                    operationApply = () => EditorFormationOperations.Create(faction, type, name, parent, town, commander);
                }
            }
        }
    }
}
