using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GTIP
{
    // Native Unity UI. It does not launch WPF or expose a network listener.
    public sealed partial class CampaignEditor : MonoBehaviour
    {
        static CampaignEditor instance;
        static int suppressUntilFrame = -1;
        public static bool BlocksInput => (instance != null && instance.open) || Time.frameCount <= suppressUntilFrame;
        bool open;
        BattleUI ui;
        GameObject campaignRoot;
        object officers;
        float campaignDate;
        EventSystem eventSystem;
        BaseInputModule[] disabledInputModules = new BaseInputModule[0];
        LiveEditSession session;
        List<CampaignEditorRecord> rows = new List<CampaignEditorRecord>();
        CampaignEditorRecord selected;
        string[] values = new string[0];
        object[] expected = new object[0];
        string search = "", status = "", pending;
        int domain, side;
        Vector2 listScroll, editScroll;
        GUISkin skin;
        Texture2D background;
        static readonly string[] Domains = { "Armies", "Garrisons", "Fleets", "Officers", "Weapons", "Nation", "Diagnostics" };

        public static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("GTIP_AideDeCamp");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CampaignEditor>();
        }
        public static void MenuOpened() { if (instance != null) { instance.Close(); instance.ResetReveal(); } }
        public static void EditorBattleUI(BattleUI target)
        {
            MultiplayerCampaign.Observe(target);
            MultiplayerCampaign.RepairShortcutFocus();
            if (MultiplayerCampaign.BlocksInput) return;
            if (MultiplayerCampaign.IsReplica) { MultiplayerCampaign.ReplicaBattleUi(target); return; }
            if (instance != null) instance.InputTick(target);
            if (!BlocksInput) NativeAccess.Call(target, "GTIP_Original_EditorBattleUI");
        }
        public static void EditorCamera(Camera_Battlefield target)
        { MultiplayerCampaign.RepairShortcutFocus(); if (!BlocksInput && !MultiplayerCampaign.BlocksInput) NativeAccess.Call(target, "GTIP_Original_EditorCamera"); }
        public static void EditorCampaignAi(AICampaign target)
        { if (!BlocksInput && !MultiplayerCampaign.HoldsCampaignAi && !MultiplayerCampaign.IsReplica) NativeAccess.Call(target, "GTIP_Original_EditorCampaignAi"); }

        void InputTick(BattleUI target)
        {
            try
            {
                ValidateReveal(target);
                if (open)
                {
                    if (!target.IsCampaign || target.IsMainMenu || BattleUnits.unitfolder != campaignRoot ||
                        !ReferenceEquals(officers, GameVars.commander) || !GameVars.gamepaused ||
                        GameVars.GetCampaignFloatingDate() != campaignDate) { Close(); return; }
                    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F8)) { Close(); return; }
                }
                else if (Input.GetKeyDown(KeyCode.F8) && target.IsCampaign && !target.IsMainMenu &&
                    GameVars.frame > 50 && GameVars.allregimentsinitialized && SixLevelRuntime.Active && BattleUnits.unitfolder != null)
                {
                    ui = target;
                    GameVars.SetGameSpeed(0, manualset: true);
                    if (!GameVars.gamepaused) throw new InvalidOperationException("Pause the campaign before opening Aide-de-Camp.");
                    campaignRoot = BattleUnits.unitfolder;
                    officers = GameVars.commander;
                    campaignDate = GameVars.GetCampaignFloatingDate();
                    eventSystem = EventSystem.current;
                    disabledInputModules = eventSystem == null ? new BaseInputModule[0] : eventSystem.GetComponents<BaseInputModule>().Where(module => module.enabled).ToArray();
                    foreach (var module in disabledInputModules) module.enabled = false;
                    open = true;
                    session = new LiveEditSession(Guard, RefreshEditedData);
                    status = "Campaign paused. Changes apply to this session; use the game's Save As after closing.";
                    side = Math.Max(0, Math.Min(1, GameVars.playeralliance));
                    Reload();
                }
            }
            catch (Exception error) { Debug.LogWarning("[GTIP] Aide-de-Camp: " + error); Close(); }
        }
        void Guard()
        {
            if (!open || ui == null || !ui.IsCampaign || ui.IsMainMenu || !GameVars.gamepaused ||
                BattleUnits.unitfolder != campaignRoot || !ReferenceEquals(officers, GameVars.commander) ||
                GameVars.GetCampaignFloatingDate() != campaignDate)
                throw new InvalidOperationException("Campaign changed. Close and reopen Aide-de-Camp.");
            if (selected != null && selected.Target is Regiment unit && (unit == null || !BattleUnits.completeunitlist.Contains(unit)))
                throw new InvalidOperationException("This unit is unavailable.");
        }
        void Close()
        {
            if (!open) return;
            open = false;
            suppressUntilFrame = Time.frameCount + 1; // Closing click must not issue a map order.
            foreach (var module in disabledInputModules) if (module != null) module.enabled = true;
            disabledInputModules = new BaseInputModule[0];
            eventSystem = null;
            session?.Clear(); session = null;
            selected = null; pending = null; rows.Clear(); editedOfficers.Clear(); ResetOperation();
            CancelTreeDrag(); ClearPortraitPreview(); treeRows.Clear(); treeNodes.Clear(); collapsedTargets.Clear(); treeInitialized = false;
            // Deliberately remain paused; never resume a campaign behind the user's back.
        }
        void OnDisable() { Close(); ResetReveal(); }
        void OnDestroy()
        {
            Close(); if (skin != null) Destroy(skin); if (background != null) Destroy(background);
            if (instance == this) instance = null;
        }
        void RefreshEditedData()
        {
            CampaignEditorData.Refresh();
            // Name and commander lookup caches must follow edits and their undo.
            foreach (var officer in GameVars.commander)
                if (officer != null && editedOfficers.Contains(officer))
                { officer.combinedname = officer.firstname + " " + officer.name; officer.refreshcommand = true; }
        }
        readonly HashSet<GameVars.Commander> editedOfficers = new HashSet<GameVars.Commander>();
        void Reload()
        {
            ResetOperation();
            selected = null; pending = null; values = new string[0]; expected = new object[0];
            rows.Clear();
            rows = CampaignEditorData.Read(domain, side, search);
            RebuildTree();
            editedOfficers.RemoveWhere(o => !GameVars.commander.Contains(o));
        }
        void Select(CampaignEditorRecord row)
        {
            ResetOperation();
            selected = row; pending = null; editScroll = Vector2.zero;
            expected = row.Fields.Select(field => field.Read()).ToArray();
            values = row.Fields.Select((field, i) => field.Format(expected[i])).ToArray();
        }
        void Run(Action action)
        {
            try { Guard(); action(); }
            catch (Exception error) { status = error.Message; Debug.LogWarning("[GTIP] Editor operation: " + error.Message); }
        }
        void MakeSkin()
        {
            if (skin != null) return;
            skin = Instantiate(GUI.skin);
            background = new Texture2D(1, 1); background.SetPixel(0, 0, new Color(.07f, .10f, .13f)); background.Apply();
            skin.box.normal.background = background;
            foreach (var style in new[] { skin.label, skin.button, skin.textField, skin.box, skin.toggle })
            { style.fontSize = 16; style.normal.textColor = Color.white; style.hover.textColor = Color.white; style.active.textColor = Color.white; style.focused.textColor = Color.white; }
            skin.label.wordWrap = true; skin.button.wordWrap = true;
            skin.button.fixedHeight = 32; skin.textField.fixedHeight = 30;
        }
        void OnGUI()
        {
            if (!open) { DrawReveal(); return; }
            MakeSkin();
            var oldSkin = GUI.skin; var oldMatrix = GUI.matrix;
            try
            {
                GUI.skin = skin;
                float scale = Math.Min(1f, Math.Min(Screen.width / 1100f, Screen.height / 750f));
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
                float width = Screen.width / scale, height = Screen.height / scale;
                GUI.Box(new Rect(0, 0, width, height), "");
                GUILayout.BeginArea(new Rect(20, 15, width - 40, height - 30));
                GUILayout.BeginHorizontal();
                GUILayout.Label("Aide-de-Camp | " + MpBuild.Label + " | Integration preview", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Naming schemes", GUILayout.Width(145))) Run(BeginNaming);
                if (GUILayout.Button("Create HQ", GUILayout.Width(120))) Run(() => BeginOperation("create"));
                if (GUILayout.Button("New officer", GUILayout.Width(120))) Run(() => BeginOperation("officer"));
                if (GUILayout.Button("Close (F8 / Esc)", GUILayout.Width(165))) Close();
                GUILayout.EndHorizontal();
                int newSide = GUILayout.Toolbar(side, new[] { "Union", "Confederacy" });
                int newDomain = GUILayout.Toolbar(domain, Domains);
                GUILayout.BeginHorizontal(); GUILayout.Label("Search", GUILayout.Width(60));
                string newSearch = GUILayout.TextField(search, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Refresh", GUILayout.Width(95))) Run(Reload);
                GUILayout.EndHorizontal();
                if (newSide != side || newDomain != domain || newSearch != search)
                { side = newSide; domain = newDomain; search = newSearch; Run(Reload); }
                GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
                if (domain == 6) DrawDiagnostics();
                else if (graphicalTree && domain <= 1) DrawGraphicalTree((width - 55) * .65f);
                else
                {
                    GUILayout.BeginVertical(GUILayout.Width((width - 55) * .55f));
                    if (domain <= 1 && GUILayout.Button("Graphical tree")) { graphicalTree = true; RebuildTree(); }
                    listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Width((width - 55) * .55f));
                    foreach (var row in rows)
                    {
                        GUILayout.BeginHorizontal(); GUILayout.Space(Math.Min(6, row.Depth) * 18);
                        string label = domain == 5 ? row.Title : row.Title + "\n" + string.Join("\n", row.Detail.Split('\n').Take(2));
                        float rowHeight = Math.Max(48, skin.label.CalcHeight(new GUIContent(label), (width - 55) * .55f - 45 - Math.Min(6, row.Depth) * 18) + 12);
                        if (GUILayout.Button(label, new GUIStyle(skin.button) { fixedHeight = 0 }, GUILayout.Height(domain == 5 ? 36 : rowHeight))) Select(row);
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                    GUILayout.EndVertical();
                }
                editScroll = GUILayout.BeginScrollView(editScroll, GUILayout.ExpandWidth(true));
                if (domain == 6) GUILayout.Label("Diagnostic markers are read-only. Native fog, spotting, targeting and AI settings remain unchanged. Close Aide-de-Camp to observe the map, then resume time manually.\n\nThis mode resets on leaving/reloading the campaign and is not saved.");
                else if (operation != null) DrawOperation();
                else if (selected == null) GUILayout.Label("Select a formation or record to inspect it.\n\nUse Create HQ for a new command, or select a land formation for transfers, commander replacement and deletion. Other standalone features are tracked in the branch's parity checklist.");
                else
                {
                    GUILayout.Label(selected.Title); GUILayout.Label(selected.Detail);
                    GUI.enabled = pending == null;
                    for (int i = 0; i < selected.Fields.Count; i++)
                    { GUILayout.Label(selected.Fields[i].Label); values[i] = GUILayout.TextField(values[i]); }
                    GUI.enabled = true;
                    if (selected.Fields.Count > 0 && pending == null && GUILayout.Button("Review changes"))
                        Run(() => {
                            for (int i = 0; i < values.Length; i++)
                                if (values[i] != selected.Fields[i].Format(expected[i])) selected.Fields[i].Parse(values[i]);
                            pending = string.Join("\n", selected.Fields.Select((field, i) => field.Label + ": " + field.Format(expected[i]) + " → " + values[i]));
                        });
                    if (pending != null)
                    {
                        GUILayout.Label(pending);
                        if (GUILayout.Button("Apply to paused campaign")) Run(() => {
                            for (int i = 0; i < expected.Length; i++)
                                if (!Equals(expected[i], selected.Fields[i].Read())) throw new InvalidOperationException("Record changed. Refresh and review it again.");
                            if (selected.Target is GameVars.Commander officer) editedOfficers.Add(officer);
                            session.Apply(selected.Fields, values);
                            status = "Applied. Undo is available until you close the editor. Save through the game's menu.";
                            var target = selected.Target;
                            Reload();
                            var updated = rows.FirstOrDefault(row => ReferenceEquals(row.Target, target));
                            if (updated != null) Select(updated);
                        });
                        if (GUILayout.Button("Cancel review")) pending = null;
                    }
                    if (pending == null && (selected.Target is Regiment || selected.Target is ArmyGroup)) DrawFormationButtons();
                }
                GUILayout.EndScrollView(); GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUI.enabled = session != null && session.UndoCount > 0;
                if (GUILayout.Button("Undo", GUILayout.Width(95))) Run(() => { session.Undo(); Reload(); status = "Undone."; });
                GUI.enabled = session != null && session.RedoCount > 0;
                if (GUILayout.Button("Redo", GUILayout.Width(95))) Run(() => { session.Redo(); Reload(); status = "Redone."; });
                GUI.enabled = true;
                GUILayout.Label(rows.Count + " records | Paused"); GUILayout.EndHorizontal();
                GUILayout.Label(status, GUILayout.Height(55));
                GUILayout.EndArea();
            }
            finally { GUI.enabled = true; GUI.skin = oldSkin; GUI.matrix = oldMatrix; }
        }
    }
}
