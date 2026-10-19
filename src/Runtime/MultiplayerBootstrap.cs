using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        string phase="idle", checkpointHash, remoteScenario;
        byte[] outgoingCheckpoint;
        MemoryStream incomingCheckpoint;
        int checkpointSize;
        float bootstrapStarted;
        bool replicaReady;
        void CreateHostCheckpoint()
        {
            if(SceneManagement.currentoperation!=2) throw new InvalidOperationException("Wait for campaign loading or saving to finish before joining.");
            GameVars.SetGameSpeed(0,manualset:true);
            string prior=Saving.latestfolder;
            string folder="SaveGTIPHost"+Guid.NewGuid().ToString("N");
            string scenario=GamePrefs.leveltoload+"/"+GamePrefs.subleveltoload;
            CheckScenario(scenario);
            try {
                Saving.latestfolder=folder;
                SceneManagement.SaveAll(true,true,true,true,false,"GTIP MP session checkpoint");
                outgoingCheckpoint=MpCheckpoint.Pack(Path.Combine("Campaigns",scenario,folder));
            } finally { Saving.latestfolder=prior; }
            BuildManifest();
            ClearData(false);
            try {
                var rows=CaptureData();var issues=MpStateCodec.InvalidNumbers(DataSchemas,rows);
                if(issues.Length>0) {
                    foreach(string issue in issues)LogDiagnostic("Host state preflight rejected: "+issue);
                    throw new InvalidDataException("Invalid numeric campaign fields (up to 64): "+string.Join("; ",issues));
                }
                var encoded=MpStateCodec.Encode(DataSchemas,rows,false);
                LogDiagnostic("Host state preflight passed: records="+rows.Length+", bytes="+encoded.Length);
            } catch(Exception error) {
                LogDiagnostic("Host state preflight failed: "+error);
                RejectGuest("Host campaign state validation failed before transfer. See the host multiplayer log.");return;
            }
            checkpointHash=MpCheckpoint.Hash(outgoingCheckpoint);
            phase="sending"; bootstrapStarted=Time.realtimeSinceStartup;
            Send(new MpMessage {kind="checkpoint-offer",session=session,campaign=scenario,text=checkpointHash,unit=outgoingCheckpoint.Length,side=guestSide});
            LogDiagnostic("Host checkpoint prepared: "+outgoingCheckpoint.Length+" bytes; hash="+checkpointHash);
        }
        static void CheckScenario(string value)
        {
            if(value==null || !Regex.IsMatch(value,@"\A[0-9]{3}/[A-Za-z0-9]\z")) throw new InvalidDataException("Invalid campaign scenario identifier.");
        }
        bool BootstrapMessage(MpMessage value)
        {
            if(value.kind=="keepalive") {
                if(session!=null && !string.IsNullOrEmpty(value.session) && value.session!=session) throw new InvalidDataException("Session mismatch.");
                return true;
            }
            if(host && value.kind=="join") {
                if(phase!="waiting" || value.token!=token || value.text!=MpBuild.Version) { RejectGuest("Session key or MP build mismatch."); return true; }
                CreateHostCheckpoint(); return true;
            }
            if(!host && value.kind=="checkpoint-offer") {
                if(phase!="joining" || value.unit<1 || value.unit>MpCheckpoint.MaxBytes || string.IsNullOrEmpty(value.session) || value.side<0 || value.side>1)
                    throw new InvalidDataException("Invalid checkpoint offer.");
                CheckScenario(value.campaign);
                session=value.session; guestSide=value.side; remoteScenario=value.campaign; checkpointHash=value.text;
                checkpointSize=value.unit; incomingCheckpoint=new MemoryStream(); phase="receiving";
                Send(new MpMessage{kind="checkpoint-next",session=session,sequence=0}); return true;
            }
            if(host && value.kind=="checkpoint-next") {
                if(phase!="sending" || value.session!=session || value.sequence<0 || value.sequence>=outgoingCheckpoint.Length) throw new InvalidDataException("Invalid checkpoint request.");
                int count=Math.Min(MpCheckpoint.ChunkBytes,outgoingCheckpoint.Length-value.sequence);
                var bytes=new byte[count]; Buffer.BlockCopy(outgoingCheckpoint,value.sequence,bytes,0,count);
                Send(new MpMessage{kind="checkpoint-data",session=session,sequence=value.sequence,blob=bytes}); return true;
            }
            if(!host && value.kind=="checkpoint-data") {
                if(phase!="receiving" || value.session!=session || value.sequence!=incomingCheckpoint.Length || value.blob==null || value.blob.Length==0 || incomingCheckpoint.Length+value.blob.Length>checkpointSize)
                    throw new InvalidDataException("Out-of-order or oversized checkpoint data.");
                incomingCheckpoint.Write(value.blob,0,value.blob.Length);
                message="Receiving host campaign: "+(incomingCheckpoint.Length*100/checkpointSize)+"%";
                if(incomingCheckpoint.Length==checkpointSize) LoadHostCheckpoint();
                else Send(new MpMessage{kind="checkpoint-next",session=session,sequence=(int)incomingCheckpoint.Length});
                return true;
            }
            if(host && value.kind=="ready") {
                if(phase!="sending" || value.session!=session) throw new InvalidDataException("Unexpected guest readiness.");
                var diff=MpIdentity.Difference(manifestKeys,Array.ConvertAll(value.units,u=>u.name));
                if(diff!=null) { RejectGuest(diff); return true; }
                authenticated=true; phase="live"; outgoingCheckpoint=null;
                Send(new MpMessage {kind="welcome",session=session,side=guestSide,mode=mode,text=MpBuild.Version,campaign=manifest,units=HierarchyManifest()});
                message="Guest loaded the host campaign. Close F9 and resume when ready.";
                LogDiagnostic("Guest loaded and matched host checkpoint."); return true;
            }
            return false;
        }
        void LoadHostCheckpoint()
        {
            string folder="SaveGTIPGuest"+Guid.NewGuid().ToString("N");
            string target=Path.Combine("Campaigns",remoteScenario,folder);
            MpCheckpoint.Unpack(incomingCheckpoint.ToArray(),checkpointHash,target);
            incomingCheckpoint.Dispose(); incomingCheckpoint=null;
            var parts=remoteScenario.Split('/');
            var scenarioLines=File.ReadAllLines(Path.Combine(target,"scenario.dat"));
            if(scenarioLines.Length<12 || !int.TryParse(scenarioLines[11],out int president)) throw new InvalidDataException("Missing checkpoint president.");
            Saving.latestfolder=folder; Saving.priorloadedfolder=folder;
            SceneManagement.currentoperation=6;
            ImportExportUnitData.LoadScenarioFile(target+"/",true);
            GameVars.playeralliance=guestSide;
            SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_showblackscreen));
            SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_rebuildscene,1));
            SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_resetloadingscreen,2,122,-2,president,null,null,null,"",LoadingScreen.sound_loadingcampaign[guestSide]));
            SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_loadcampaignscenario,2,parts[0],parts[1],president));
            SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(SceneManagement.bo_hideblackscreen,2));
            phase="loading"; message="Loading host campaign..."; ClosePanel();
            LogDiagnostic("Checkpoint verified; loading isolated session folder "+target);
        }
        void BootstrapTick()
        {
            // Waiting for a player is not a campaign transfer. Start the deadline on join.
            if(host && phase=="waiting") return;
            if(Time.realtimeSinceStartup-bootstrapStarted>300) throw new TimeoutException("Campaign transfer/load timed out.");
            // The transport worker supplies liveness even while Unity loading
            // blocks Update. These main-thread deadlines still require progress.
            if(!host && phase=="loading" && Ready && GameVars.frame>60 && SceneManagement.batchoperation.Count==0) {
                GameVars.SetGameSpeed(0,manualset:true);
                BuildManifest(); SaveReplicaState(); root=BattleUnits.unitfolder; officers=GameVars.commander;
                replicaReady=true; BlockInput(); phase="ready";
                var avatar=UnityEngine.Object.FindObjectOfType<AvatarButton>();
                if(avatar!=null)avatar.UpdateVisibility();
                ModularUIFunctions.UpdateWindowOperations();
                Send(new MpMessage{kind="ready",session=session,units=HierarchyManifest()});
            }
        }
        void ClearBootstrap()
        {
            incomingCheckpoint?.Dispose(); incomingCheckpoint=null; outgoingCheckpoint=null;
            phase="idle"; replicaReady=false;
        }
    }
}
