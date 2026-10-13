using System;
using UnityEngine;
namespace GTIP
{
    public sealed partial class CampaignEditor
    {
        bool revealEnemy;
        GameObject revealRoot;
        object revealOfficers;
        int revealPlayer;
        BattleUI revealUI;
        Camera revealCamera;
        GUIStyle revealLabel, revealBanner;
        void ResetReveal()
        {
            revealEnemy = false; revealRoot = null; revealOfficers = null; revealUI = null; revealCamera = null;
        }
        void ValidateReveal(BattleUI target)
        {
            if (revealEnemy && (target == null || !target.IsCampaign || target.IsMainMenu || !SixLevelRuntime.Active ||
                GameVars.frame <= 50 || BattleUnits.unitfolder != revealRoot || !ReferenceEquals(revealOfficers, GameVars.commander) ||
                GameVars.playeralliance != revealPlayer)) ResetReveal();
        }
        void DrawDiagnostics()
        {
            GUILayout.BeginVertical(GUILayout.Width(460));
            GUILayout.Label("Campaign observation");
            bool enabled = GUILayout.Toggle(revealEnemy, "Reveal enemy formations (player-only markers)");
            if (enabled != revealEnemy) Run(() => {
                ResetReveal();
                if (enabled) {
                    revealRoot = BattleUnits.unitfolder; revealOfficers = GameVars.commander;
                    revealPlayer = GameVars.playeralliance; revealUI = ui; revealEnemy = true;
                }
            });
            GUILayout.Label("Shows current enemy map-formation positions, names and whether the game considers them hidden. Amber markers identify formations hidden by native fog. Cyan markers identify visible formations.\n\nF8 returns here to disable the overlay. Markers do not select units or intercept map orders.");
            GUILayout.Label("Native fog of war: " + (GameVars.usefow ? "enabled" : "disabled") + " (unchanged by this feature)");
            GUILayout.EndVertical();
        }
        void DrawReveal()
        {
            if (!revealEnemy || Event.current.type != EventType.Repaint) return;
            ValidateReveal(revealUI);
            if (!revealEnemy || !GameVars.allregimentsinitialized) return;
            try
            {
                if (revealCamera == null) {
                    revealCamera = Camera.main;
                    if (revealCamera == null) { var go = GameObject.Find("Camera"); if (go != null) revealCamera = go.GetComponent<Camera>(); }
                }
                if (revealCamera == null || BattleUnits.completeunitlist == null) return;
                if (revealLabel == null) {
                    revealLabel = new GUIStyle(GUI.skin.box) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter, fixedHeight = 0 };
                    revealBanner = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                    revealBanner.normal.textColor = Color.white;
                }
                int drawn = 0;
                foreach (var unit in BattleUnits.completeunitlist)
                {
                    if (unit == null || !DiagnosticRevealRules.Include(revealPlayer, unit.alliance, unit.gameObject.activeInHierarchy,
                        BattleUnits.GetUnitVisibilityStateOnCampaignMap(unit))) continue;
                    var point = revealCamera.WorldToScreenPoint(unit.transform.position);
                    if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                    bool hidden = GameVars.usefow && unit.iswithinfog;
                    revealLabel.normal.textColor = hidden ? new Color(1f,.75f,.25f) : new Color(.4f,.9f,1f);
                    GUI.Label(new Rect(point.x - 4, Screen.height - point.y - 4, 8, 8), "+", revealLabel);
                    float x = Mathf.Clamp(point.x - 105, 0, Math.Max(0, Screen.width - 210));
                    float y = Mathf.Clamp(Screen.height - point.y + 8, 45, Math.Max(45, Screen.height - 65));
                    GUI.Label(new Rect(x, y, 210, 58), unit.name + "\n" + (hidden ? "Hidden by native fog" : "Visible to player"), revealLabel);
                    drawn++;
                }
                GUI.Label(new Rect(Math.Max(0, (Screen.width - 520) / 2), 6, Math.Min(520, Screen.width), 30),
                    "GTIP observer reveal | " + drawn + " markers | F8 > Diagnostics to disable", revealBanner);
            }
            catch (Exception error) { Debug.LogWarning("[GTIP] Diagnostic reveal disabled: " + error); ResetReveal(); }
        }
    }
}
