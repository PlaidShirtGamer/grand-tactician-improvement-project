using System;
using System.IO;
using UnityEngine;

namespace GTIP
{
    public sealed class MpTacticalNativeTicket
    {
        public readonly MpTacticalLoadRequest Request;
        public readonly MpTacticalLoadPlan Plan;
        public string Folder=>Plan.Folder;
        public string Scenario=>Plan.Scenario;
        public string Battlefield=>Plan.Battlefield;
        public bool Returning=>Plan.Returning;
        internal MpTacticalNativeTicket(MpTacticalLoadRequest request,MpTacticalLoadPlan plan){Request=request;Plan=plan;}
    }
    public static class MpTacticalNativeScene
    {
        // The owner must hold all simulation/input before calling this method and
        // retain the exact returned Request for readiness attestation after load.
        public static MpTacticalNativeTicket Load(MpTacticalLoadRequest request,string expectedCampaign,int side)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            MpTacticalLoadPlan.ValidateScenario(expectedCampaign);
            if(side<0 || side>1 || SceneManagement.batchoperation==null || SceneManagement.batchoperation.Count!=0)
                throw new InvalidOperationException("Wait for existing native scene operations to finish before tactical loading.");
            string install=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            // Native imports use relative paths; an unexpected process directory
            // must not load or save another copy's campaign data.
            if(!string.Equals(Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar),install.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))
                throw new IOException("Native tactical loading requires the game installation as its working directory.");
            string folder="SaveGTIPBattle"+Guid.NewGuid().ToString("N");
            string relative=Path.Combine("Campaigns",expectedCampaign.Replace('/',Path.DirectorySeparatorChar),folder),target=Path.Combine(install,relative);
            // The older campaign checkpoint decoder does not inspect junctions;
            // verify the destination ancestors before either decoder writes.
            MpTacticalLoadPlan.CheckPath(target);
            if(request.ReturningToCampaign)MpCheckpoint.Unpack(request.Checkpoint,request.Hash,target);
            else MpTacticalCheckpoint.Unpack(request.Checkpoint,request.Hash,target);
            var plan=MpTacticalLoadPlan.Read(install,target,expectedCampaign,side,request.ReturningToCampaign,MainMenuControls.currentversion);
            string scene=plan.Returning?plan.Level+"C":plan.Battlefield;
            if(!Application.CanStreamedLevelBeLoaded(scene) || !Application.CanStreamedLevelBeLoaded(plan.Returning?"CampaignBase":"BattlefieldBase"))
                throw new IOException("Required native Unity scene is not installed: "+scene);
            // Build every operation before changing native state; all imports and
            // queued loads use the verified isolated save, never battle results.
            var batches=new System.Collections.Generic.List<SceneManagement.BatchOperation>();
            batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_showblackscreen));
            batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_rebuildscene,1));
            if(plan.Returning) {
                batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_resetloadingscreen,2,122,-2,plan.President,null,null,null,"",LoadingScreen.sound_loadingcampaign[side]));
                batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_loadcampaignscenario,2,plan.Level,plan.Sublevel,plan.President));
                batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_hideblackscreen,2));
            } else {
                batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_resetloadingscreen,2,115,-1,-2,null,null,null,"",LoadingScreen.sound_loadingbattle[side]));
                batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_loadcampaignbattlefromsave,3,plan.Battlefield,plan.BattleSublevel,plan.Scenario));
                batches.Add(new SceneManagement.BatchOperation(SceneManagement.bo_hideblackscreen,3));
            }
            // Saving.LoadSave performs old-scene cleanup before rebuild. Call only
            // ClearBattlefield: native ExitCampaignBattleAndRebuildCampaign would
            // calculate random results; Exit*Scenario also writes local options.
            var controller=GameObject.Find("GameController");
            var setup=controller==null?null:controller.GetComponent<BattlefieldSetup>();
            if(setup!=null)setup.ClearBattlefield();
            Saving.latestfolder=folder;Saving.priorloadedfolder=folder;
            SceneManagement.currentcampaignscenario=plan.Scenario;
            SceneManagement.currentoperation=plan.Returning?6:8;
            ImportExportUnitData.LoadScenarioFile(Path.Combine(relative,plan.Returning?"":"Battle")+Path.DirectorySeparatorChar,true);
            GameVars.playeralliance=side;
            BattleController.quitbattlescene=false;CampaignController.quitcampaignscene=false;
            SceneManagement.batchoperation.AddRange(batches);
            return new MpTacticalNativeTicket(request,plan);
        }
    }
}
