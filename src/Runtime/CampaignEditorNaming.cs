using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class CampaignEditor
    {
        [Serializable] public sealed class NamingScheme
        {
            public string name = "Regimental names", template = "{number} {state} {type}", include = "", exclude = "", state = "", skipped = "";
            public int tier, numberStyle, casing, start = 1;
            public bool stateAbbreviation, subtree;
        }
        [Serializable] public sealed class NamingCatalog { public List<NamingScheme> schemes = new List<NamingScheme>(); }
        NamingScheme naming = new NamingScheme(); NamingCatalog namingCatalog = new NamingCatalog();
        readonly List<CampaignEditorRecord> renameRows = new List<CampaignEditorRecord>();
        readonly List<string> renameOld = new List<string>(), renameNew = new List<string>();
        bool renameReviewed; Vector2 renameScroll;
        static readonly int[] RenameTypes = { -1, 0, 1, 2, 14, 15, 16, 18, 19 };
        static readonly string[] RenameLabels = { "All land", "Infantry", "Cavalry", "Artillery", "Brigade", "Division", "Corps", "Army", "Army Group" };
        string NamingPath => Path.Combine(Environment.CurrentDirectory, "Config", "GTIP-naming-schemes.json");
        void BeginNaming()
        {
            BeginOperation("naming"); renameReviewed = false;
            if (File.Exists(NamingPath)) namingCatalog = JsonUtility.FromJson<NamingCatalog>(File.ReadAllText(NamingPath)) ?? new NamingCatalog();
            if (namingCatalog.schemes == null) namingCatalog.schemes = new List<NamingScheme>();
            namingCatalog.schemes.RemoveAll(s => s == null);
            foreach (var s in namingCatalog.schemes)
            {
                s.name = s.name ?? "Unnamed scheme"; s.template = s.template ?? "{number} {type}";
                s.include = s.include ?? ""; s.exclude = s.exclude ?? ""; s.state = s.state ?? ""; s.skipped = s.skipped ?? "";
                s.tier = Math.Max(0, Math.Min(RenameTypes.Length - 1, s.tier));
                s.numberStyle = Math.Max(0, Math.Min(3, s.numberStyle)); s.casing = Math.Max(0, Math.Min(3, s.casing));
            }
        }
        void DrawNaming()
        {
            GUILayout.Label("Mass renaming / Naming schemes — " + (side == 0 ? "Union" : "Confederacy"));
            if (renameReviewed)
            {
                GUILayout.Label(renameRows.Count + " names. Apply is one undoable transaction; save through the game's menu.");
                renameScroll = GUILayout.BeginScrollView(renameScroll, GUILayout.Height(350));
                for (int i = 0; i < renameRows.Count; i++) GUILayout.Label(renameOld[i] + " → " + renameNew[i]);
                GUILayout.EndScrollView();
                if (GUILayout.Button("Apply all names")) Run(() => {
                    for (int i = 0; i < renameRows.Count; i++)
                    {
                        var target = renameRows[i].Target;
                        if ((target is Regiment u && (u == null || !BattleUnits.completeunitlist.Contains(u))) ||
                            (target is ArmyGroup g && (g == null || !BattleUnits.armygroups.Contains(g))) || !Equals(renameRows[i].Fields[0].Read(), renameOld[i]))
                            throw new InvalidOperationException("A formation changed. Build a new preview.");
                    }
                    session.Apply(renameRows.Select(r => r.Fields[0]).ToArray(), renameNew);
                    status = "Renamed " + renameRows.Count + " formations. Undo is available."; Reload();
                });
                if (GUILayout.Button("Back to scheme")) renameReviewed = false;
            }
            else
            {
                GUILayout.Label("Saved schemes (GTIP format)");
                foreach (var scheme in namingCatalog.schemes.ToArray())
                    if (GUILayout.Button(scheme.name)) naming = JsonUtility.FromJson<NamingScheme>(JsonUtility.ToJson(scheme));
                GUILayout.Label("Scheme name"); naming.name = GUILayout.TextField(naming.name);
                GUILayout.Label("Template: {number}, {state}, {type}, {tier}, {old}; other text is literal"); naming.template = GUILayout.TextField(naming.template);
                naming.tier = GUILayout.SelectionGrid(naming.tier, RenameLabels, 3);
                naming.numberStyle = GUILayout.Toolbar(naming.numberStyle, new[] { "1st", "1", "I", "1." });
                naming.casing = GUILayout.Toolbar(naming.casing, new[] { "Preserve case", "UPPER", "lower", "Title" });
                GUILayout.Label("Start number"); string start = GUILayout.TextField(naming.start.ToString()); if (int.TryParse(start, out int number)) naming.start = number;
                GUILayout.Label("Skip numbers (comma separated)"); naming.skipped = GUILayout.TextField(naming.skipped);
                GUILayout.Label("Name contains"); naming.include = GUILayout.TextField(naming.include);
                GUILayout.Label("Preserve names containing"); naming.exclude = GUILayout.TextField(naming.exclude);
                GUILayout.Label("Home state contains (blank = all)"); naming.state = GUILayout.TextField(naming.state);
                naming.stateAbbreviation = GUILayout.Toggle(naming.stateAbbreviation, "Use state abbreviations");
                naming.subtree = GUILayout.Toggle(naming.subtree, "Only selected command and its descendants");
                GUILayout.Label("Renumbers matches in displayed hierarchy order, restarting per home state when {state} is used. Excluded formations keep their names. Duplicate proposed names are rejected. This does not import standalone JSON rules.");
                if (GUILayout.Button("Save scheme")) Run(() => {
                    naming.name = (string)LiveEditSession.Name(naming.name);
                    namingCatalog.schemes.RemoveAll(s => s.name == naming.name);
                    namingCatalog.schemes.Add(JsonUtility.FromJson<NamingScheme>(JsonUtility.ToJson(naming)));
                    Directory.CreateDirectory(Path.GetDirectoryName(NamingPath));
                    File.WriteAllText(NamingPath + ".tmp", JsonUtility.ToJson(namingCatalog, true));
                    if (File.Exists(NamingPath)) File.Replace(NamingPath + ".tmp", NamingPath, NamingPath + ".bak");
                    else File.Move(NamingPath + ".tmp", NamingPath);
                    status = "Naming scheme saved.";
                });
                if (GUILayout.Button("Preview names")) Run(BuildRenamePreview);
            }
            if (GUILayout.Button("Cancel mass renaming")) ResetOperation();
        }
        void BuildRenamePreview()
        {
            if (naming.tier < 0 || naming.tier >= RenameTypes.Length) throw new InvalidOperationException("Select a formation type.");
            EditorNaming.Number(naming.start, naming.numberStyle);
            var skipped = new HashSet<int>();
            foreach (var token in naming.skipped.Split(',')) if (!string.IsNullOrWhiteSpace(token))
            { if (!int.TryParse(token.Trim(), out int value) || value < 1 || value > 99999) throw new InvalidOperationException("Skipped numbers must be 1–99,999."); skipped.Add(value); }
            var population = CampaignEditorData.Read(0, side, "").Concat(CampaignEditorData.Read(1, side, "")).GroupBy(r => r.Target).Select(g => g.First()).ToList();
            renameRows.Clear(); renameOld.Clear(); renameNew.Clear();
            var counters = new Dictionary<string, int>(); var proposed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (naming.subtree && !(operationSource is Regiment) && !(operationSource is ArmyGroup)) throw new InvalidOperationException("Select a command before opening mass renaming.");
            foreach (var row in population)
            {
                var u = row.Target as Regiment; var group = row.Target as ArmyGroup;
                int type = u != null ? u.unittyp : 19;
                if (RenameTypes[naming.tier] >= 0 && type != RenameTypes[naming.tier]) continue;
                if (row.Title.IndexOf(naming.include, StringComparison.OrdinalIgnoreCase) < 0 || naming.exclude.Length > 0 && row.Title.IndexOf(naming.exclude, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (naming.subtree && row.Target != operationSource)
                {
                    if (u == null) continue;
                    if (operationSource is Regiment root && !u.transform.IsChildOf(root.transform)) continue;
                    if (operationSource is ArmyGroup ag && !ag.attachedunits.Any(member => member == u || u.transform.IsChildOf(member.transform))) continue;
                }
                var home = u != null && u.state >= 0 && u.state < GameVars.nation.Length ? GameVars.nation[u.state] : null;
                if (naming.state.Length > 0 && (home == null || home.name.IndexOf(naming.state, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                if (naming.template.Contains("{state}") && home == null) continue;
                string stateName = home == null ? "" : naming.stateAbbreviation ? home.abbrev : home.name;
                string key = naming.template.Contains("{state}") ? stateName : "";
                int next = counters.TryGetValue(key, out int current) ? current : naming.start;
                while (skipped.Contains(next)) next++;
                string tier = group != null ? "Army Group" : CampaignEditorData.Tier(type);
                string label = type == 0 ? "Infantry" : type == 1 ? "Cavalry" : type == 2 ? "Artillery" : tier;
                string name = EditorNaming.Render(naming.template, next, naming.numberStyle, naming.casing, stateName, label, tier, row.Title);
                if (!proposed.Add(name)) throw new InvalidOperationException("Duplicate proposed name: " + name + ". Add {number} or narrow the filters.");
                counters[key] = next + 1; renameRows.Add(row); renameOld.Add(row.Title); renameNew.Add(name);
            }
            if (renameRows.Count == 0) throw new InvalidOperationException("No formations match this scheme.");
            var changed = new HashSet<object>(renameRows.Select(r => r.Target));
            if (population.Any(r => !changed.Contains(r.Target) && proposed.Contains(r.Title))) throw new InvalidOperationException("A proposed name is already used by an excluded formation. Change the start number or template.");
            renameReviewed = true; renameScroll = Vector2.zero;
        }
    }
}
