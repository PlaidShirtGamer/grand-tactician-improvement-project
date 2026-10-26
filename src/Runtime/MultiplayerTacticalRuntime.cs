using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        // Opt-in scene proof. It has no combat phase and never applies battle results.
        bool tacticalProofEnabled, tacticalHolding, tacticalFailureHeld, tacticalNativeWrite;
        bool tacticalReceiptSent, tacticalSceneReady;
        bool tacticalFenceSent;
        MpMessage tacticalFence;
        string tacticalStage, tacticalScenario;
        float tacticalStarted;
        MpTacticalSession tacticalSession;
        MpTacticalDrain tacticalDrain;
        MpTacticalNativeTicket tacticalTicket;
        GameObject tacticalOldRoot;
        byte[] tacticalReturnArchive;
        string tacticalScene, tacticalSublevel;
        GameObject tacticalMonument;
        public static bool TacticalHolding => instance!=null && (instance.tacticalHolding || instance.tacticalFailureHeld);
        public static bool TacticalInBattle => TacticalHolding && instance.ui!=null && !instance.ui.IsCampaign && !instance.ui.IsMainMenu;
        public static bool TacticalNativeWrite => instance!=null && instance.tacticalNativeWrite;
        static bool TacticalLoading => TacticalHolding && instance.tacticalStage!="paused";
        public static bool TacticalLoadingInputBlocked => TacticalLoading || instance==null || instance.panel || CampaignEditor.BlocksInput;

        public static void TacticalPrepare(string bfsceneid,string sublevel,GameObject monumentobjref)
        {
            var mp=instance;
            if(TacticalHolding)return;
            if(mp==null || mp.transport==null || !mp.authenticated || !mp.tacticalProofEnabled) {
                NativeAccess.Call(typeof(SceneManagement),"GTIP_Original_TacticalPrepare",bfsceneid,sublevel,monumentobjref);return;
            }
            // Guest manual-fight buttons already send an engagement intent. Only
            // the host's validated native decision can choose the shared battle.
            if(!mp.host)return;
            if(mp.phase!="live" || !mp.Ready || SceneManagement.currentoperation!=2 || SceneManagement.batchoperation.Count!=0)
                throw new InvalidOperationException("Wait for the campaign to finish loading before entering battle.");
            if(GamePrefs.debug_loaddebugarmysetup)throw new InvalidOperationException("Disable debug army setup before testing shared battle loading.");
            mp.EnsureTacticalSession();
            if(!mp.tacticalSession.Lifecycle.CanUseCampaign)throw new InvalidOperationException("A tactical transition is already active.");
            mp.tacticalScene=bfsceneid;mp.tacticalSublevel=sublevel;mp.tacticalMonument=monumentobjref;
            mp.tacticalScenario=GamePrefs.leveltoload+"/"+GamePrefs.subleveltoload;
            MpTacticalLoadPlan.ValidateScenario(mp.tacticalScenario);
            mp.tacticalDrain=new MpTacticalDrain(mp.session,checked(mp.tacticalSession.Lifecycle.Generation+1));
            mp.BeginTacticalHold("drain");
            mp.Send(mp.tacticalDrain.Offer());
            mp.LogDiagnostic("Shared tactical entry requested; holding both peers before campaign drain.");
        }
        void EnsureTacticalSession()
        {if(tacticalSession==null)tacticalSession=new MpTacticalSession(host,session,Send,LoadTacticalCheckpoint);}
        void BeginTacticalHold(string stage)
        {
            tacticalHolding=true;tacticalStage=stage;tacticalStarted=Time.realtimeSinceStartup;
            tacticalReceiptSent=false;tacticalSceneReady=false;
            tacticalFenceSent=false;tacticalFence=null;
            HoldTacticalClock();BlockInput();ClosePanel();
            message="Both players are loading the shared battle. Waiting for the campaign handoff...";
        }
        bool TacticalDataQuiet => outgoingData==null && incomingData==null && prepareTask==null && receiveTask==null && commitTask==null;
        int TacticalGeneration => tacticalSession==null?0:tacticalSession.Lifecycle.Generation;
        bool TacticalMessage(MpMessage value)
        {
            if(!MpTacticalSession.IsTactical(value))return false;
            if(value.kind=="tactical-hold") {
                if(host || phase!="live")throw new InvalidDataException("Unexpected tactical hold.");
                EnsureTacticalSession();
                MpTacticalDrain.ValidateOffer(value,session,checked(TacticalGeneration+1));
                if(tacticalHolding) {
                    if(tacticalStage!="drain")throw new InvalidDataException("A tactical transition is already active.");
                    return true;
                }
                if(!tacticalSession.Lifecycle.CanUseCampaign)throw new InvalidDataException("Campaign handoff is unavailable.");
                tacticalScenario=GamePrefs.leveltoload+"/"+GamePrefs.subleveltoload;
                BeginTacticalHold("drain");return true;
            }
            if(value.kind=="tactical-held") {
                if(!host || !tacticalHolding || tacticalStage!="drain" || tacticalDrain==null || !tacticalFenceSent)throw new InvalidDataException("Unexpected campaign drain receipt.");
                tacticalDrain.Receive(value);return true;
            }
            if(value.kind=="tactical-drained") {
                if(host || !tacticalHolding || tacticalStage!="drain")throw new InvalidDataException("Unexpected campaign drain fence.");
                MpTacticalDrain.ValidateFence(value,session,checked(TacticalGeneration+1),value.baseRevision,value.stateHash);
                if(tacticalFence!=null && (tacticalFence.baseRevision!=value.baseRevision || tacticalFence.stateHash!=value.stateHash))
                    throw new InvalidDataException("Conflicting campaign drain fence.");
                tacticalFence=value;return true;
            }
            if(!tacticalHolding || tacticalSession==null)throw new InvalidDataException("Tactical transfer has no shared loading barrier.");
            if(!host && value.kind=="tactical-offer" && value.baseRevision==1 && !tacticalReceiptSent)
                throw new InvalidDataException("Battle offered before the campaign drain completed.");
            if(!host && value.kind=="tactical-offer" && value.baseRevision==2 && tacticalStage=="paused") {
                tacticalStage="returning";ClosePanel();BlockInput();
            }
            tacticalSession.Receive(value,Time.realtimeSinceStartup);
            CompleteTacticalBarrier(); // Before another queued campaign frame is handled.
            return true;
        }
        bool TacticalTick()
        {
            if(!tacticalHolding)return false;
            HoldTacticalClock();BlockInput();
            if(tacticalStage=="drain") {
                DataTick();
                if(TacticalDataQuiet && Baseline.Revision>0 && transport.PendingOutgoing==0) {
                    if(host && !tacticalFenceSent) {
                        // This FIFO fence follows every campaign frame, including
                        // preparations that were still running when hold was sent.
                        Send(MpTacticalDrain.Fence(session,checked(TacticalGeneration+1),Baseline.Revision,Baseline.Hash));
                        tacticalFenceSent=true;
                    } else if(!host && !tacticalReceiptSent && tacticalFence!=null) {
                        MpTacticalDrain.ValidateFence(tacticalFence,session,checked(TacticalGeneration+1),Baseline.Revision,Baseline.Hash);
                        tacticalReceiptSent=true;tacticalStarted=Time.realtimeSinceStartup;
                        Send(MpTacticalDrain.Receipt(session,checked(TacticalGeneration+1),Baseline.Revision,Baseline.Hash));
                        tacticalStage="waiting-battle";
                    } else if(host && tacticalDrain.Commit(false,Baseline.Revision,Baseline.Hash))StartNativeBattle();
                }
            } else if(tacticalStage=="host-loading" && NativeTacticalReady(false)) {
                string parent="SaveGTIPBattleHost"+Guid.NewGuid().ToString("N");
                string target=Path.Combine("Campaigns",tacticalScenario,parent);
                // SaveBattle requires its parent campaign files. Keep a copy of the
                // native battle-entry autosave as well as the pre-battle return copy.
                var parentBytes=MpCheckpoint.Pack(Path.Combine("Campaigns",tacticalScenario,Saving.latestfolder));
                MpCheckpoint.Unpack(parentBytes,MpCheckpoint.Hash(parentBytes),target);
                string prior=Saving.latestfolder;
                try {Saving.latestfolder=parent;ImportExportUnitData.SaveBattle(false,"GTIP shared paused battle");}
                finally {Saving.latestfolder=prior;}
                var checkpoint=MpTacticalCheckpoint.Pack(target);
                tacticalSession.BeginBattle(checkpoint,Time.realtimeSinceStartup);
            } else if((tacticalStage=="load-battle" || tacticalStage=="load-campaign") && !tacticalSceneReady && NativeTacticalReady(tacticalTicket.Returning)) {
                // Read the initialized native graph, rather than hashing only the
                // bytes that arrived. Local camera and UI preferences are excluded.
                string world=MpTacticalNativeState.Capture(tacticalTicket.Returning);
                string digest=MpCheckpoint.Hash(Encoding.UTF8.GetBytes(world+"|"+tacticalTicket.Plan.ContentHash));
                tacticalSceneReady=true;
                LogDiagnostic("Native tactical scene ready: generation="+TacticalGeneration+", return="+tacticalTicket.Returning+", digest="+digest);
                tacticalSession.SceneLoaded(tacticalTicket.Request,digest,Time.realtimeSinceStartup);
            }
            if(tacticalSession!=null && tacticalSession.Lifecycle.MustPause && tacticalSession.Lifecycle.Tick(Time.realtimeSinceStartup,180))
                throw new TimeoutException(tacticalSession.Lifecycle.Failure);
            if((tacticalStage=="drain" || tacticalStage=="waiting-battle" || tacticalStage=="host-loading") && Time.realtimeSinceStartup-tacticalStarted>180)
                throw new TimeoutException("Shared tactical entry did not finish. Both clients remain held.");
            CompleteTacticalBarrier();return true;
        }
        void StartNativeBattle()
        {
            if(!host || !tacticalDrain.Committed || !TacticalDataQuiet)throw new InvalidOperationException("Campaign handoff has not drained.");
            HoldTacticalClock();
            string prior=Saving.latestfolder,folder="SaveGTIPBattleReturn"+Guid.NewGuid().ToString("N");
            try {
                tacticalNativeWrite=true;Saving.latestfolder=folder;
                SceneManagement.SaveAll(true,true,true,true,false,"GTIP pre-battle recovery");
                tacticalReturnArchive=MpCheckpoint.Pack(Path.Combine("Campaigns",tacticalScenario,folder));
            } finally {tacticalNativeWrite=false;Saving.latestfolder=prior;}
            ClearTacticalCampaignState();tacticalOldRoot=BattleUnits.unitfolder;
            tacticalStage="host-loading";tacticalStarted=Time.realtimeSinceStartup;
            try {
                tacticalNativeWrite=true;
                NativeAccess.Call(typeof(SceneManagement),"GTIP_Original_TacticalPrepare",tacticalScene,tacticalSublevel,tacticalMonument);
            } finally {tacticalNativeWrite=false;}
        }
        void LoadTacticalCheckpoint(MpTacticalLoadRequest request)
        {
            HoldTacticalClock();ClearTacticalCampaignState();tacticalOldRoot=BattleUnits.unitfolder;
            tacticalSceneReady=false;tacticalStage=request.ReturningToCampaign?"load-campaign":"load-battle";
            tacticalStarted=Time.realtimeSinceStartup;
            tacticalTicket=MpTacticalNativeScene.Load(request,tacticalScenario,host?hostSide:guestSide);
        }
        bool NativeTacticalReady(bool campaign)
        {
            if(ui==null || ui.IsMainMenu || ui.IsCampaign!=campaign || GameVars.frame<=60 || !GameVars.allregimentsinitialized ||
                BattleUnits.unitfolder==null || BattleUnits.unitfolder==tacticalOldRoot || SceneManagement.currentoperation!=(campaign?2:3) || SceneManagement.batchoperation.Count!=0)return false;
            var controller=GameObject.Find("GameController");var setup=controller==null?null:controller.GetComponent<BattlefieldSetup>();
            if(setup==null || setup.initialized!=4 || BattleUnits.completeunitlist.Any(u=>u!=null && u.gameObject.activeInHierarchy && !NativeAccess.Get<bool>(u,"initialized")))return false;
            if(campaign)return GamePrefs.leveltoload+"/"+GamePrefs.subleveltoload==tacticalScenario;
            string expected=tacticalTicket!=null && !tacticalTicket.Returning?tacticalTicket.Battlefield:tacticalScene;
            return SceneManagement.currentbattlescenario==expected && GamePrefs.leveltoload==expected;
        }
        void CompleteTacticalBarrier()
        {
            if(!tacticalHolding || tacticalSession==null)return;
            if(tacticalSession.Lifecycle.Phase==MpTacticalPhase.Faulted)throw new InvalidOperationException(tacticalSession.Lifecycle.Failure);
            if(tacticalSession.Lifecycle.Phase==MpTacticalPhase.PausedBattle && tacticalStage!="paused") {
                tacticalStage="paused";HoldTacticalClock();
                CloseTacticalStartupPanels();
                NativeAccess.Call(typeof(LoadingScreen),"GTIP_Original_TacticalHideBlack");
                message="Both players loaded the same paused battle. Combat is disabled in this scene test. F9 returns both players to the pre-battle campaign.";
                LogDiagnostic("Shared paused battlefield barrier complete: generation="+TacticalGeneration);
            } else if(tacticalSession.Lifecycle.Phase==MpTacticalPhase.Campaign && tacticalStage=="load-campaign") {
                RestoreTacticalCampaign();
            }
        }
        void ReturnTacticalCampaign()
        {
            if(!host || tacticalSession==null || tacticalSession.Lifecycle.Phase!=MpTacticalPhase.PausedBattle || tacticalReturnArchive==null)
                throw new InvalidOperationException("Both players must finish loading the paused battle before returning.");
            ClosePanel();tacticalStage="returning";
            tacticalSession.BeginReturn(tacticalReturnArchive,Time.realtimeSinceStartup);
        }
        void RestoreTacticalCampaign()
        {
            ClearTacticalCampaignState();BuildManifest();root=BattleUnits.unitfolder;officers=GameVars.commander;
            if(!host){SaveReplicaState();replicaReady=true;BlockInput();}
            tacticalHolding=false;tacticalFailureHeld=false;tacticalStage=null;tacticalTicket=null;tacticalDrain=null;
            tacticalReturnArchive=null;tacticalMonument=null;HoldTacticalClock();
            NativeAccess.Call(typeof(LoadingScreen),"GTIP_Original_TacticalHideBlack");
            if(host)RestoreInput();phase="live";lastDataSend=0;lastSend=0;
            message="Both players returned to the pre-battle campaign. Campaign remains paused.";
            LogDiagnostic("Shared campaign return verified: generation="+TacticalGeneration);
        }
        public static void TacticalHideBlack()
        {if(!TacticalLoading)NativeAccess.Call(typeof(LoadingScreen),"GTIP_Original_TacticalHideBlack");}
        public static void TacticalExit()
        {
            if(TacticalHolding) {
                if(instance.host && instance.transport!=null && instance.tacticalStage=="paused")instance.ReturnTacticalCampaign();
                return;
            }
            NativeAccess.Call(typeof(SceneManagement),"GTIP_Original_TacticalExit");
        }
        void ClearTacticalCampaignState()
        {
            DestroyRecruitPreview();previewPanel=null;previewCommander=-1;ClearRecruitRequest();pendingUniforms.Clear();ClearCommanderPortraits();
            ClearData(false);ClearMovement();panelPause.Clear();blockPanelResume=false;pendingPromotions.Clear();officerViewDirty=false;
            newsBarrier.Clear();newspaperState=new MpNewspaperState();ClearNewspaperPresentation();guestFileMenu=false;
            financeViews.Clear();replicaAlerts.Clear();alertPositions.Clear();lastAlertCounts="";ClearMapNotices();replicaVisibility.Clear();ClearDispatchState();ClearWorld();
            clockSequence=receivedClockSequence=0;replicaMutated=false;hasLiveData=false;replicaReady=false;snapshot=null;
            originalPositions.Clear();originalRotations.Clear();originalActive.Clear();originalParents.Clear();originalSiblingIndices.Clear();
            restoreClock=null;hostClock=null;replicaTimePanel=null;replicaTopPanel=null;campaignClockLabels=null;
            ids.Clear();units.Clear();groupIds.Clear();groups.Clear();nextId=0;
            guestSelection=null;choosingCommander=false;orderStatus="";
        }
        // A dropped connection cannot turn a partially loaded guest into a simulator.
        // Hosts retain an explicit pre-battle recovery action instead of random results.
        bool EndTactical()
        {
            bool held=TacticalHolding,guest=held && !host;
            tacticalSession?.Dispose();tacticalSession=null;tacticalDrain=null;tacticalTicket=null;
            // A host that has not replaced its campaign needs only a normal pause.
            bool recover=held && (!host || tacticalReturnArchive!=null || SceneManagement.currentoperation!=2 || root!=BattleUnits.unitfolder || SceneManagement.batchoperation.Count!=0);
            tacticalHolding=false;tacticalFailureHeld=recover;tacticalStage=recover?"failed":null;
            tacticalNativeWrite=false;
            if(!held){tacticalReturnArchive=null;return false;}
            HoldTacticalClock();
            if(guest)SceneManagement.batchoperation.Clear();
            return guest;
        }
        void CloseTacticalStartupPanels()
        {
            var ph=ui.panelhandler;
            foreach(int index in new[]{ph.panel_choosepaper,ph.panel_briefing,ph.panel_objectivespaper,ph.panel_statistics})
                ph.HidePanel(ph.panel,index,hideimmediately:true);
            ph.cambf.PreventCamMovement(false,false);GameVars.showallobjectivenames=false;
        }
        void RecoverTacticalCampaign()
        {
            if(!host || !tacticalFailureHeld || transport!=null || tacticalReturnArchive==null || SceneManagement.batchoperation.Count!=0)
                throw new InvalidOperationException("Wait for native loading to finish before restoring the pre-battle campaign.");
            var request=new MpTacticalLoadRequest(1,true,MpCheckpoint.Hash(tacticalReturnArchive),tacticalReturnArchive);
            ClosePanel();LoadTacticalCheckpoint(request);tacticalStage="offline-return";
        }
        void TacticalOfflineTick()
        {
            if(!tacticalFailureHeld)return;
            HoldTacticalClock();
            if(!host && ui!=null && ui.IsMainMenu) {
                tacticalFailureHeld=false;tacticalStage=null;NativeAccess.Call(typeof(LoadingScreen),"GTIP_Original_TacticalHideBlack");RestoreInput();
            } else if(host && tacticalStage=="offline-return" && NativeTacticalReady(true)) {
                // Still validate native loading before releasing offline authority.
                MpTacticalNativeState.Capture(true);ClearTacticalCampaignState();root=BattleUnits.unitfolder;officers=GameVars.commander;
                tacticalFailureHeld=false;tacticalStage=null;tacticalReturnArchive=null;HoldTacticalClock();
                NativeAccess.Call(typeof(LoadingScreen),"GTIP_Original_TacticalHideBlack");RestoreInput();
                message="Pre-battle campaign restored offline and paused.";
            }
        }
        void DrawTacticalLoading()
        {
            if(!TacticalLoading)return;
            if(white==null){white=new Texture2D(1,1);white.SetPixel(0,0,Color.white);white.Apply();}
            var color=GUI.color;GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),white);GUI.color=color;
            GUI.Label(new Rect(Screen.width/2-250,Screen.height/2-45,500,120),
                (tacticalFailureHeld?"Battle transition stopped. Open F9 for recovery.":"Loading the shared battlefield / campaign...\nWaiting until both players are ready.")+"\nF9: session menu");
        }
    }
}
