using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        static readonly MpStateSchema[] RecruitSchema={MpStateSchema.Scalars(typeof(MpRecruitDraft))};
        Regiment nativePreview;
        CampaignMainPanel previewPanel;
        int previewCommander=-1;
        bool guestFileMenu,buildingPreview;
        bool NativeRecruitOpen => previewPanel!=null && previewPanel.gameObject.activeInHierarchy && (previewPanel.contentselection==10 || nativePreview!=null);
        bool GuestDialogOpen => guestFileMenu;
        public static void ReplicaRecruitDialog(CampaignMainPanel target,Regiment parent)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaRecruitDialog",parent);return;}
            if(!LiveSession || parent==null || parent.alliance!=instance.guestSide || !instance.ids.ContainsKey(parent))return;
            instance.previewPanel=target;instance.previewCommander=-1;
            NativeAccess.Call(target,"GTIP_Original_ReplicaRecruitDialog",parent);
        }
        public static void ReplicaRecruitPreview(CampaignMainPanel target,int type,bool horse,Color coat,Color trousers,int uniform,int recruitType)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaRecruitPreview",type,horse,coat,trousers,uniform,recruitType);return;}
            var mp=instance;if(mp==null || target!=mp.previewPanel)return;
            mp.DestroyRecruitPreview();
            // Native preview creation assigns commanders, updates counters and roster caches.
            // Preserve the authoritative records around that UI-only transaction.
            var objects=new List<Tuple<object,int,object[]>>();
            foreach(var a in GameVars.alliance.Where(a=>a!=null))objects.Add(Tuple.Create((object)a,0,DataSchemas[0].Capture(a)));
            foreach(var n in GameVars.nation.Where(n=>n!=null))objects.Add(Tuple.Create((object)n,1,DataSchemas[1].Capture(n)));
            var officers=GameVars.commander.ToArray();var links=officers.Select(c=>new[]{c.currentcommand,c.lastcommand}).ToArray();var dates=officers.Select(OfficerDates).ToArray();
            var attached=mp.units.Values.ToDictionary(u=>u,u=>u.allattachedunits);
            foreach(var c in officers)objects.Add(Tuple.Create((object)c,2,DataSchemas[2].Capture(c)));
            foreach(var u in mp.units.Values)objects.Add(Tuple.Create((object)u,3,DataSchemas[3].Capture(u)));
            var complete=BattleUnits.completeunitlist.ToArray();var campaign=BattleUnits.campaignunitlist.ToArray();
            var roster=GameVars.alliance.Select(a=>a==null?null:a.unitlist.ToArray()).ToArray();var formed=GameVars.alliance.Select(a=>a==null?null:a.lastmonthunitsformed.ToArray()).ToArray();
            mp.buildingPreview=true;
            try {NativeAccess.Call(target,"GTIP_Original_ReplicaRecruitPreview",type,horse,coat,trousers,uniform,recruitType);mp.nativePreview=CampaignMainPanel.dummyregiment;}
            finally {
                mp.buildingPreview=false;
                mp.nativePreview=CampaignMainPanel.dummyregiment;
                foreach(var pair in attached)if(pair.Key!=null)pair.Key.allattachedunits=pair.Value;
                foreach(var item in objects)if(!(item.Item1 is UnityEngine.Object obj) || obj!=null)DataSchemas[item.Item2].Apply(item.Item1,item.Item3);
                GameVars.commander.Clear();GameVars.commander.AddRange(officers);
                for(int i=0;i<officers.Length;i++){officers[i].currentcommand=links[i][0];officers[i].lastcommand=links[i][1];ApplyOfficerDates(officers[i],dates[i]);}
                BattleUnits.completeunitlist=complete.ToList();BattleUnits.campaignunitlist=campaign.ToList();
                for(int i=0;i<roster.Length;i++)if(roster[i]!=null){GameVars.alliance[i].unitlist.Clear();GameVars.alliance[i].unitlist.AddRange(roster[i]);GameVars.alliance[i].lastmonthunitsformed.Clear();GameVars.alliance[i].lastmonthunitsformed.AddRange(formed[i]);}
                var preview=mp.nativePreview;
                if(preview!=null) {
                    IsolateRecruitPreview(preview);
                    foreach(var line in UnityEngine.Object.FindObjectsOfType<Frontline2>())line.RemoveObjectFromFrontline(preview.gameObject);
                    foreach(var renderer in preview.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                    foreach(var collider in preview.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                    if(mp.previewCommander>=0 && mp.previewCommander<GameVars.commander.Count)preview.commander=mp.previewCommander;
                    if(preview.commander<0 || preview.commander>=GameVars.commander.Count)preview.commander=0;
                    NativeAccess.Set(target,"currentcommanderchosen",preview.commander);
                    RestoreRecruitPreviewPanel(target,preview);
                }
            }
        }
        public static void ReplicaMainSelection(CampaignMainPanel target,int selection,bool reset,GameObject unit)
        {
            if(instance!=null && instance.host && instance.remoteIntent)return;
            if(IsReplica && instance!=null && instance.nativePreview!=null && target==instance.previewPanel && selection==0)selection=10;
            NativeAccess.Call(target,"GTIP_Original_ReplicaMainSelection",selection,reset,unit);
        }
        void DestroyRecruitPreview()
        {
            var preview=nativePreview;nativePreview=null;
            if(preview==null)return;
            if(CampaignMainPanel.dummyregiment==preview)CampaignMainPanel.dummyregiment=null;
            if(ui!=null && ui.selectedunitreg==preview)ui.SelectUnit(null);
            UnityEngine.Object.DestroyImmediate(preview.gameObject);
        }
        public static void ReplicaRecruitCancel(CampaignMainPanel target,bool updatePanels)
        {
            if(IsReplica && instance!=null) {instance.DestroyRecruitPreview();instance.previewCommander=-1;instance.previewPanel=null;}
            NativeAccess.Call(target,"GTIP_Original_ReplicaRecruitCancel",updatePanels);
        }
        public static void ReplicaRecruitCommit(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaRecruitCommit");return;}
            var mp=instance;var u=mp?.nativePreview;if(u==null || !LiveSession)return;
            if(mp.recruitPendingSequence!=0)return;
            var parent=NativeAccess.Get<Regiment>(target,"unittorecruitfor");
            if(parent==null || !mp.ids.TryGetValue(parent,out int id))return;
            var draft=new MpRecruitDraft{Parent=id,State=u.state,Type=u.unittyp,Horse=u.ishorseartillery,SizeLevel=NativeAccess.Get<int>(target,"recruitingsizelevel"),RecruitType=u.recruitingtype,
                Commander=mp.previewCommander,Name=u.name,Uniform=u.uniformset,Colors=ColorData(u.componentcolor[1]).Concat(ColorData(u.componentcolor[0])).ToArray()};
            draft.Validate();
            var request=new MpMessage{kind="recruit",session=mp.session,sequence=++mp.guestSequence,blob=MpStateCodec.Encode(RecruitSchema,new[]{new MpStateRow{Values=RecruitSchema[0].Capture(draft)}}),text=draft.Commander<0?"":OfficerCommandIdentity(draft.Commander)};
            if(mp.BeginRecruitRequest(target,u,request)) {
                mp.orderStatus="Recruitment request sent to host.";
                mp.LogDiagnostic(mp.orderStatus+" sequence="+request.sequence+", state="+draft.State+", type="+draft.Type+", sizeLevel="+draft.SizeLevel+", recruits="+u.strength+", recruitType="+draft.RecruitType);
            }
        }
        public static void ReplicaCommanderAssign(CommandersList target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaCommanderAssign");return;}
            var mp=instance;var unit=NativeAccess.Get<Regiment>(target,"chosenunit");int commander=target.selectedcommanderid;
            if(mp==null || unit==null || commander<0 || commander>=GameVars.commander.Count)return;
            if(unit==mp.nativePreview){mp.previewCommander=commander;unit.commander=commander;RestoreRecruitPreviewPanel(mp.previewPanel,unit);return;}
            if(mp.ids.ContainsKey(unit)){mp.guestSelection=unit;mp.SubmitGuestOrder("commander",new Vector3(commander,0,0));}
        }
        public static void ReplicaCommanderPreview(CampaignMainPanel target,int changeChosen)
        {
            var preview=IsReplica && instance!=null && target==instance.previewPanel?instance.nativePreview:null;
            int id=changeChosen>=0?changeChosen:NativeAccess.Get<int>(target,"currentcommanderchosen");
            if(preview==null || id!=preview.commander || id<0 || id>=GameVars.commander.Count){NativeAccess.Call(target,"GTIP_Original_ReplicaCommanderPreview",changeChosen);return;}
            var officer=GameVars.commander[id];
            WithRecruitPreviewCommander(target,preview,officer,()=>NativeAccess.Call(target,"GTIP_Original_ReplicaCommanderPreview",changeChosen));
        }
        public static void ReplicaMainPanelClose(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaMainPanelClose");return;}
            if(instance!=null && instance.previewPanel==target)ReplicaRecruitCancel(target,false);
            var cam=UnityEngine.Object.FindObjectOfType<Camera_Battlefield>();if(cam!=null)cam.preventmanualzoom=false;
            GameVars.deactivateshortcuts=false;var h=NativeAccess.Get<PanelHandler>(target,"panelhandler");h?.ClearDragAndDrop();
        }
        void ExecuteRecruit(MpMessage request)
        {
            var rows=MpStateCodec.Decode(RecruitSchema,request.blob);
            if(rows.Length!=1 || rows[0].Deleted || rows[0].Kind!=0 || rows[0].Id!=0)throw new InvalidDataException("Invalid recruitment request.");
            var d=new MpRecruitDraft();RecruitSchema[0].Apply(d,rows[0].Values);d.Validate();
            if(!units.TryGetValue(d.Parent,out var parent) || parent==null || parent.alliance!=guestSide || parent.unittyp<14 || parent.unittyp==17 || parent.inbattle || parent.onretreat)throw new InvalidOperationException("Recruitment parent is unavailable.");
            if(d.State>=GameVars.nation.Length)throw new InvalidOperationException("Unknown state.");
            var nation=GameVars.nation[d.State];var alliance=GameVars.alliance[guestSide];int size=BattleUnits.GetMaxUnitSize(guestSide,d.Type,d.SizeLevel,d.Horse);
            LogDiagnostic("Guest recruitment draft: sequence="+request.sequence+", parent="+d.Parent+", state="+d.State+"/"+nation.name+", type="+d.Type+", sizeLevel="+d.SizeLevel+", recruits="+size+", recruitType="+d.RecruitType+", pool="+nation.GetRecruitAmounts(d.RecruitType,guestSide)+", rating="+alliance.currentrating);
            // Single-brigade native recruitment deliberately includes off-map home states.
            // averageposition==zero is not an eligibility failure (RecruitingList.Initialize
            // defaults showstatesoffmap:true); retain the same native pool/rating checks.
            if(!nation.recruitable)throw new InvalidOperationException("The selected home state cannot recruit units.");
            if(size<=0)throw new InvalidOperationException("The selected unit type has no available recruitment size.");
            if(!nation.HasEnoughRecruits(size,d.RecruitType,guestSide))throw new InvalidOperationException("Not enough "+(d.RecruitType==0?"volunteers":"draftees")+" in "+nation.name+": "+nation.GetRecruitAmounts(d.RecruitType,guestSide)+" available, "+size+" required.");
            if(!alliance.IsRatingOkForRecruitment())throw new InvalidOperationException("The nation's credit rating is too low to recruit new units.");
            if(d.Horse && !alliance.IsHorseArtilleryAvailable())throw new InvalidOperationException("Horse artillery is not available under current policies.");
            if(d.Commander>=0 && (OfficerCommandIdentity(d.Commander)!=request.text || !EditorFormationOperations.Commanders(guestSide,d.Type,null).Contains(d.Commander)))throw new InvalidOperationException("Selected officer is no longer eligible.");
            var created=EditorFormationOperations.Engine.CreateUnitDefault(parent,d.State,d.Type,d.Horse,d.Colors==null?default(Color):FromColor(d.Colors),d.Colors==null?default(Color):FromColor(d.Colors,4),d.Uniform,d.RecruitType,guestSide,sizeoverride:size);
            if(created==null)throw new InvalidOperationException("Native recruitment failed.");
            if(d.Commander>=0)GameVars.commander[d.Commander].AssignCommando(created);
            if(!string.IsNullOrWhiteSpace(d.Name))created.name=d.Name;
            BattleUnits.RecruitUnit(created);RefreshHostIds();
            // Creation is already committed. A local host-panel repaint cannot turn its
            // receipt into a refusal and invite a second recruitment of the same draft.
            try {RefreshReplicaRosterUi();}catch(Exception error){LogDiagnostic("Host recruitment UI refresh deferred: "+error);}
            LogDiagnostic("Native guest recruitment: formation="+Id(created)+", parent="+d.Parent+", recruits="+size);
        }
        void DrawGuestDialogs()
        {
            if(!IsReplica)return;
            string notice=RecruitNotice;
            if(NativeRecruitOpen && notice.Length>0)GUI.Box(new Rect((Screen.width-560)/2,110,560,64),notice);
            if(!guestFileMenu)return;
            var rect=new Rect((Screen.width-520)/2,(Screen.height-230)/2,520,230);GUI.Box(rect,"");GUILayout.BeginArea(new Rect(rect.x+12,rect.y+12,496,206));
            GUILayout.Label("Multiplayer campaign files");GUILayout.Label("Save a new checkpoint on the host, or disconnect to load another campaign locally.");
            if(GUILayout.Button("Save a new checkpoint on the host",GUILayout.Height(32))){SubmitGlobal("save-checkpoint",0);guestFileMenu=false;}
            if(GUILayout.Button("Disconnect and return to main menu",GUILayout.Height(32))){guestFileMenu=false;End("Disconnected to select another campaign.");}
            if(GUILayout.Button("Cancel",GUILayout.Height(32)))guestFileMenu=false;GUILayout.EndArea();
        }
    }
}
