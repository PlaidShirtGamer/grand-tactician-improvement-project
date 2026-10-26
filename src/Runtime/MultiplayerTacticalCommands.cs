using System;
using System.Linq;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        // UpdateSiegeButtons also changes assault/retreat buttons and tooltips.
        // Keep its manual-battle admission rule without changing the host's panel.
        static bool TacticalManualEligible(Autocalc battle, int side)
        {
            if (battle == null || !battle.isActiveAndEnabled || battle.type != 2 || !battle.hasplayercontrolledunits ||
                battle.alliance == null || battle.alliance.Length != 2 || !battle.alliance.Contains(side) ||
                battle.envolvedunits == null || battle.envolvedunits.Length != 2 ||
                battle.envolvedunits.Any(s => s == null || s.topunits == null || s.topunits.Count == 0 || s.topunits.Any(u => u == null)) ||
                battle.IsFortEngagement()) return false;
            var playerUnits = battle.GetPlayerUnits();
            return playerUnits.Count > 0 && playerUnits.Sum(CampaignArmyPanel.GetReadinessStep) / playerUnits.Count > 0;
        }

        bool GuestTacticalManualButton(ButtonTooltip button)
        {
            var panel = AutoresolvePanel.autoresolvepanel;
            if (panel == null || button == null || button != panel.BT_PlayManually) return false;
            // Consume this exact control even when disabled; it must never fall
            // through to a native guest callback or an unrelated unit action.
            if (panel.isActiveAndEnabled && button.state != 2)
                RequestTacticalManual(AutoresolvePanel.autocalcrefopen);
            return true;
        }

        void RequestTacticalManual(Autocalc battle)
        {
            if (host || !authenticated || phase != "live" || TacticalHolding || battle == null) return;
            var panel = AutoresolvePanel.autoresolvepanel;
            if (panel == null || !panel.isActiveAndEnabled || AutoresolvePanel.autocalcrefopen != battle ||
                NativeAccess.Get<Autocalc>(panel, "autocalcref") != battle || panel.BT_PlayManually == null || panel.BT_PlayManually.state == 2) return;
            var monument = battle.GetComponent<BattleMonument>();
            int id = BattleReference(monument);
            if (id <= 0 || monument.autocalcref != battle || !TacticalManualEligible(battle, guestSide)) return;
            RequestNative("tactical-manual", id);
        }

        bool ExecuteTacticalManual(string action, MpMessage request, string detail)
        {
            if (action != "tactical-manual") return false;
            if (!host || !authenticated || phase != "live" || !tacticalProofEnabled || TacticalHolding || !Ready ||
                mode != "coop" || guestSide != hostSide || GameVars.playeralliance != hostSide ||
                GamePrefs.debug_loaddebugarmysetup ||
                SceneManagement.currentoperation != 2 || SceneManagement.batchoperation == null || SceneManagement.batchoperation.Count != 0 ||
                request.unit <= 0 || request.x != 0 || !string.IsNullOrEmpty(detail))
                throw new InvalidOperationException("Shared manual battle launch is unavailable.");
            var monument = BattleById(request.unit);
            var battle = monument == null ? null : monument.autocalcref;
            if (monument == null || !monument.gameObject.activeInHierarchy || BattleReference(monument) != request.unit ||
                battle == null || battle.GetComponent<BattleMonument>() != monument || !TacticalManualEligible(battle, hostSide))
                throw new InvalidOperationException("The active battle no longer permits manual play.");
            // Preserve native unit-file and battlefield selection. Its patched
            // PrepareLaunchingBattleFromCampaign starts the shared loading barrier.
            battle.StartManualBattleFromAutocalc(true);
            return true;
        }
    }
}
