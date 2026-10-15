using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        static readonly MpStateSchema[] RecruitSchema={MpStateSchema.Scalars(typeof(MpRecruitDraft))};
        MpRecruitDraft recruitDraft;
        Vector2 recruitScroll;
        bool guestFileMenu;
        public static void ReplicaRecruitDialog(CampaignMainPanel target,Regiment parent)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaRecruitDialog",parent);return;}
            if(instance==null || !instance.authenticated || parent==null || parent.alliance!=instance.guestSide || !instance.ids.TryGetValue(parent,out int id))return;
            instance.recruitDraft=new MpRecruitDraft{Parent=id,State=-1,SizeLevel=2};
            instance.recruitScroll=Vector2.zero;
        }
        void ExecuteRecruit(MpMessage request)
        {
            var rows=MpStateCodec.Decode(RecruitSchema,request.blob);
            if(rows.Length!=1 || rows[0].Deleted || rows[0].Kind!=0 || rows[0].Id!=0)throw new InvalidDataException("Invalid recruitment request.");
            var draft=new MpRecruitDraft();RecruitSchema[0].Apply(draft,rows[0].Values);draft.Validate();
            if(!units.TryGetValue(draft.Parent,out var parent) || parent==null || parent.alliance!=guestSide || parent.unittyp<14 || parent.unittyp>18 || parent.unittyp==17 || parent.inbattle || parent.onretreat)
                throw new InvalidOperationException("The parent command is no longer available for recruitment.");
            if(draft.State>=GameVars.nation.Length)throw new InvalidOperationException("Unknown recruiting state.");
            var nation=GameVars.nation[draft.State];var alliance=GameVars.alliance[guestSide];
            int size=BattleUnits.GetMaxUnitSize(guestSide,draft.Type,draft.SizeLevel,draft.Horse);
            if(!nation.recruitable || nation.averageposition==Vector3.zero || size<=0 || !nation.HasEnoughRecruits(size,draft.RecruitType,guestSide) || !alliance.IsRatingOkForRecruitment())
                throw new InvalidOperationException("Recruitment requires a recruiting state, enough recruits, and a suitable debt rating.");
            if(draft.Horse && !alliance.IsHorseArtilleryAvailable())throw new InvalidOperationException("Horse artillery is not available.");
            var created=EditorFormationOperations.Engine.CreateUnitDefault(parent,draft.State,draft.Type,draft.Horse,recruitingtype:draft.RecruitType,_alliance:guestSide,sizeoverride:size);
            if(created==null)throw new InvalidOperationException("Native recruitment failed to create the unit.");
            BattleUnits.RecruitUnit(created);
            RefreshHostIds();RefreshReplicaRosterUi();
            LogDiagnostic("Guest recruitment executed: formation="+Id(created)+", parent="+draft.Parent+", recruits="+size);
        }
        bool GuestDialogOpen => recruitDraft!=null || guestFileMenu;
        void DrawGuestDialogs()
        {
            if(!IsReplica || !GuestDialogOpen)return;
            float width=Math.Min(640,Screen.width-30),height=Math.Min(620,Screen.height-50);
            var area=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
            GUI.Box(area,"");GUILayout.BeginArea(new Rect(area.x+12,area.y+12,width-24,height-24));
            if(guestFileMenu) {
                GUILayout.Label("Multiplayer campaign files");
                GUILayout.Label("The host owns this campaign. Request a checkpoint on the host, or disconnect to load a different game locally.");
                if(GUILayout.Button("Save a new checkpoint on the host",GUILayout.Height(32))) {SubmitGlobal("save-checkpoint",0);guestFileMenu=false;}
                if(GUILayout.Button("Disconnect and return to main menu",GUILayout.Height(32))){guestFileMenu=false;End("Disconnected to select another campaign.");}
                if(GUILayout.Button("Cancel",GUILayout.Height(32)))guestFileMenu=false;
            } else {
                var d=recruitDraft;
                GUILayout.Label("Recruit into "+(units.TryGetValue(d.Parent,out var p)&&p!=null?p.name:"unavailable command"));
                GUILayout.Label("The host checks recruitment and pays the native cost. The unit arrives through the normal transfer process.");
                int type=GUILayout.Toolbar(d.Horse?3:d.Type,new[]{"Infantry","Cavalry","Artillery","Horse artillery"});d.Type=Math.Min(type,2);d.Horse=type==3;
                d.SizeLevel=GUILayout.Toolbar(d.SizeLevel,new[]{"Small","Medium","Large"});
                d.RecruitType=GUILayout.Toolbar(d.RecruitType,new[]{"Volunteers","Draftees"});
                int size=BattleUnits.GetMaxUnitSize(guestSide,d.Type,d.SizeLevel,d.Horse);
                GUILayout.Label("Recruits needed: "+size+". Select a recruiting state:");
                recruitScroll=GUILayout.BeginScrollView(recruitScroll);
                foreach(var id in Enumerable.Range(0,GameVars.nation.Length).Where(i=>GameVars.nation[i]!=null && GameVars.nation[i].recruitable && GameVars.nation[i].averageposition!=Vector3.zero).OrderBy(i=>GameVars.nation[i].name)) {
                    var n=GameVars.nation[id];
                    if(GUILayout.Button((d.State==id?"> ":"")+n.name+" | volunteers "+Math.Max(0,n.volunteers[guestSide])+" | draftees "+Math.Max(0,n.drafts[guestSide]),GUILayout.Height(30)))d.State=id;
                }
                GUILayout.EndScrollView();
                GUI.enabled=d.State>=0 && GameVars.nation[d.State].HasEnoughRecruits(size,d.RecruitType,guestSide) && GameVars.alliance[guestSide].IsRatingOkForRecruitment() && (!d.Horse || GameVars.alliance[guestSide].IsHorseArtilleryAvailable());
                if(GUILayout.Button("Recruit — submit to host",GUILayout.Height(32))) {
                    d.Validate();var blob=MpStateCodec.Encode(RecruitSchema,new[]{new MpStateRow{Values=RecruitSchema[0].Capture(d)}});
                    Send(new MpMessage{kind="recruit",session=session,sequence=++guestSequence,blob=blob});
                    orderStatus="Recruitment request sent to host.";recruitDraft=null;
                }
                GUI.enabled=true;
                if(GUILayout.Button("Cancel",GUILayout.Height(32)))recruitDraft=null;
            }
            GUILayout.EndArea();
        }
    }
}
