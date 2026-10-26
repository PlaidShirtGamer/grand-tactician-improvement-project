using System;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        static int tacticalInitializationDepth;

        // This permission is scoped to native scene construction, never an entire held frame.
        public static bool TacticalInitializing => tacticalInitializationDepth > 0;

        public static void HoldTacticalClock()
        {
            // SetGameSpeed also reaches scene UI and animation references that do not exist
            // during the first loading frames. The hold must work before those are created.
            GameVars.gamepaused = true;
            GameVars.gamespeed = 0f;
            var weather = NativeAccess.Get<UniStormWeatherSystem_C>(typeof(GameVars), "uniStormSystem");
            if (weather != null) weather.timeStopped = true;
        }

        static void InitializeTactical(Action initialize)
        {
            tacticalInitializationDepth++;
            try { initialize(); }
            finally
            {
                tacticalInitializationDepth--;
                HoldTacticalClock();
            }
        }

        public static bool TryTacticalRegiment(Regiment target)
        {
            if (!TacticalHolding) return false;
            if (GameVars.initializeregiments && !NativeAccess.Get<bool>(target, "initialized"))
                InitializeTactical(() => NativeAccess.Call(target, "Initialize", false));
            // Initialize builds the native model. Update mixes rendering with movement,
            // morale, ammunition, firing and recruitment even when gamepaused is true.
            return true;
        }

        public static bool TryTacticalUnits(BattleUnits target)
        {
            if (!TacticalHolding) return false;
            if (!NativeAccess.Get<bool>(target, "initiated") || !GameVars.allregimentsinitialized ||
                NativeAccess.Get<bool>(target, "formset")) return true;

            // Native saved battle state is restored at frame 29 (campaign at frame 25).
            // Do not reposition anything after that restore, including a late first Update.
            int lastPlacementFrame = target.IsCampaign ? 24 : 28;
            if (GameVars.frame < 13 || GameVars.frame > lastPlacementFrame) return true;
            InitializeTactical(() =>
            {
                if (target.IsCampaign)
                {
                    NativeAccess.Call(target, "AdjustArmyStructureToCampaign");
                    NativeAccess.Call(target, "UpdateCommanderRankingsCampaign");
                    NativeAccess.Call(typeof(BattleUnits), "UpdateCommanderInChief");
                }
                else
                {
                    NativeAccess.Call(target, "DoUnitPositioning");
                    NativeAccess.Call(target, "TransferVariablesToSubordinates");
                    NativeAccess.Call(target, "UpdateCommanderRankings", true);
                    NativeAccess.Call(target, "UpdateUnitRankings", true);
                }
                NativeAccess.Set(target, "formset", true);
            });
            return true;
        }

        public static bool TryTacticalCampaign(CampaignController target)
        {
            if (!TacticalHolding) return false;
            CampaignController.quitcampaignscene = false;
            HoldTacticalClock();
            try
            {
                if (GameVars.frame <= 51)
                    InitializeTactical(() => NativeAccess.Call(target, "GTIP_Original_ReplicaCampaign"));
            }
            finally
            {
                CampaignController.quitcampaignscene = false;
                HoldTacticalClock();
            }
            return true;
        }

        public static void TacticalBattleController(BattleController target)
        {
            if (!TacticalHolding)
            {
                NativeAccess.Call(target, "GTIP_Original_TacticalBattleController");
                return;
            }
            BattleController.quitbattlescene = false;
            HoldTacticalClock();
            try
            {
                if (GameVars.frame <= 40)
                    InitializeTactical(() => NativeAccess.Call(target, "GTIP_Original_TacticalBattleController"));
            }
            finally
            {
                BattleController.quitbattlescene = false;
                HoldTacticalClock();
            }
        }

        public static void TacticalBattleAi(AIBattle target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalBattleAi");
        }

        public static void TacticalProjectile(Projectile target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalProjectile");
        }

        public static void TacticalBuilding(Building target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalBuilding");
        }

        public static void TacticalArmyGroup(ArmyGroup target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalArmyGroup");
        }

        public static void TacticalBattleFront(BattleFront target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalBattleFront");
        }

        public static void TacticalBattlefieldSetup(BattlefieldSetup target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalBattlefieldSetup");
        }

        public static void TacticalWeather(Weather target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalWeather");
        }

        public static void TacticalStorm(UniStormWeatherSystem_C target)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalStorm");
        }

        public static bool TacticalFireBullet(Regiment target, int spriteid)
        {
            return !TacticalHolding && (bool)NativeAccess.Call(target, "GTIP_Original_TacticalFireBullet", spriteid);
        }

        public static void TacticalObjectives(Objectives target)
        {
            if (!TacticalHolding)
            {
                NativeAccess.Call(target, "GTIP_Original_TacticalObjectives");
                return;
            }
            if (GameVars.initializeterrainobjects && !NativeAccess.Get<bool>(target, "initialized"))
                InitializeTactical(() => NativeAccess.Call(target, "Initialize"));
            if (!NativeAccess.Get<bool>(target, "initialized")) return;
            NativeAccess.Call(target, "UpdateTextValues");
            NativeAccess.Call(target, "UpdateUIScale");
            if (Camera_Battlefield.IsCamUIUpdated())
            {
                NativeAccess.Call(target, "UpdateUIPosition");
                NativeAccess.Call(target, "UpdateTextVisibility");
            }
        }

        public static void TacticalCoyEnter(Coy target, Collider other)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalCoyEnter", other);
        }

        public static void TacticalCoyExit(Coy target, Collider other)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalCoyExit", other);
        }

        public static void TacticalEntryPointEnter(EntryPoint target, Collider other)
        {
            if (!TacticalHolding) NativeAccess.Call(target, "GTIP_Original_TacticalEntryPointEnter", other);
        }

        public static void TacticalCamera(Camera_Battlefield target)
        {
            if (TacticalHolding && GameVars.frame > 40)
            {
                target.preventmanualzoom = false;
                target.preventmanualrotation = false;
            }
            // The original camera loop moves the local camera, UI and fog; it does not
            // advance unit or objective simulation. Its existing preserved name is reused.
            NativeAccess.Call(target, "GTIP_Original_EditorCamera");
        }
    }
}
