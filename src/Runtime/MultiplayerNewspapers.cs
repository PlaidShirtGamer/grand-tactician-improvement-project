using System;
using System.Linq;
using System.IO;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly MpNewsBarrier newsBarrier=new MpNewsBarrier();
        MpNewspaperState newspaperState=new MpNewspaperState();
        float newsResumeSpeed;
        bool newsCanResume;
        int guestNewsSerial;
        bool guestNewsClosed;
        public static void ReplicaNewspaperShow(Newspaper target,string[] headlines,string prosa,int imgid,string imageheadline,string location,Tools.Date datefrom,bool dontcontinue)
        {
            var mp=instance;
            if(mp!=null && mp.host && mp.transport!=null && mp.phase=="live" && mp.authenticated) {
                if(mp.newsBarrier.Waiting)throw new InvalidOperationException("Newspaper queue advanced before both acknowledgements.");
                // Native Close resumes the stored speed, not a fixed speed, and observes
                // dontcontinue, the stay-paused preference and other open modal panels.
                mp.newsResumeSpeed=GameVars.gamespeed;
                mp.newsCanResume=!dontcontinue;
                var clock=mp.CaptureClock();
                int serial=checked(mp.newsBarrier.Serial+1);mp.newsBarrier.Begin(serial);
                mp.newspaperState=new MpNewspaperState{Serial=serial,Active=true,Headlines=headlines?.ToArray(),Prosa=prosa,Image=imgid,ImageHeadline=imageheadline??"",Location=location??"",
                    Day=datefrom==null?clock.day:(int)datefrom.day,Month=datefrom==null?clock.month:(int)datefrom.month,Year=datefrom==null?clock.year:(int)datefrom.year};
                NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperShow",headlines,prosa,imgid,imageheadline,location,datefrom,true);
                mp.LogDiagnostic("Newspaper opened: issue="+serial+"; storedSpeed="+mp.newsResumeSpeed+"; canResume="+mp.newsCanResume+"; waiting for both players.");return;
            }
            if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperShow",headlines,prosa,imgid,imageheadline,location,datefrom,dontcontinue);
        }
        public static void ReplicaNewspaperQueue(Newspaper target)
        {
            if(IsReplica || (instance!=null && instance.host && instance.transport!=null && instance.newsBarrier.Waiting))return;
            NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperQueue");
        }
        public static void ReplicaNewspaperClose(Newspaper target)
        {
            var mp=instance;
            if(mp!=null && mp.transport!=null && mp.phase=="live" && mp.authenticated) {
                if(mp.host && mp.newsBarrier.Waiting) {
                    NativeAccess.Set(target,"dontcontinuespeed",true);
                    mp.newsBarrier.Close(mp.newsBarrier.Serial,true);mp.newspaperState.HostClosed=true;
                    NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperClose");
                    mp.FinishNewspaper();return;
                }
                if(IsReplica) {
                    NativeAccess.Set(target,"dontcontinuespeed",true);
                    if(!mp.guestNewsClosed && mp.guestNewsSerial>0) {
                        mp.guestNewsClosed=true;
                        mp.Send(new MpMessage{kind="news-ack",session=mp.session,unit=mp.guestNewsSerial});
                        mp.orderStatus="Newspaper dismissed. Waiting for the host to dismiss it.";
                    }
                    NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperClose");return;
                }
            }
            NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperClose");
        }
        void FinishNewspaper()
        {
            if(!newspaperState.Active || newsBarrier.Waiting)return;
            newspaperState.Active=false;
            var paper=Resources.FindObjectsOfTypeAll<Newspaper>().FirstOrDefault(n=>n.gameObject.scene.IsValid());
            bool otherPanel=false;
            if(paper!=null)foreach(var go in NativeAccess.Get<GameObject[]>(paper,"PanelsWhenOpenedNotResetSpeed") ?? new GameObject[0]) {
                var p=ui.panelhandler.GetPanelFromGameObject(go);
                if(p!=null && p.state>0){otherPanel=true;break;}
            }
            bool resume=MpPresentationRules.ResumeNewspaper(newsCanResume,GameVars.remainspausedpanelclosed,otherPanel,newsResumeSpeed);
            // Preserve the stored speed even when a native panel paused with manualset=true.
            // Zero is a deliberate pause and must never fall back to the highest speed.
            GameVars.gamespeed=newsResumeSpeed;
            GameVars.SetGameSpeed(resume?newsResumeSpeed:0);
            LogDiagnostic("Newspaper acknowledged by both players: issue="+newsBarrier.Serial+"; resumed="+resume+"; speed="+GameVars.gamespeed+"; paused="+GameVars.gamepaused);
        }
        bool NewspaperMessage(MpMessage value)
        {
            if(value.kind!="news-ack")return false;
            if(!host)throw new InvalidDataException("Unexpected newspaper acknowledgement.");
            if(value.unit!=newsBarrier.Serial || !newspaperState.Active)return true; // delayed duplicate cannot release a later issue
            newsBarrier.Close(value.unit,false);FinishNewspaper();return true;
        }
        void ApplyNewspaper(MpStateRow[] rows)
        {
            var row=rows.SingleOrDefault(r=>r.Kind==11);
            if(row==null)return;
            var state=ReadData<MpNewspaperState>(row);
            if(state.Serial<guestNewsSerial || state.Serial<0 || (state.Active && (state.Serial==0 || state.Headlines==null || state.Headlines.Length>8 || state.Headlines.Any(h=>h==null) || state.Prosa==null || state.Day<1 || state.Month<1 || state.Month>12 || state.Year<1 || state.Year>9999 || state.Day>DateTime.DaysInMonth(state.Year,state.Month))))
                throw new InvalidDataException("Invalid newspaper state.");
            newspaperState=state;
            if(!state.Active || guestNewsSerial==state.Serial)return;
            var target=Resources.FindObjectsOfTypeAll<Newspaper>().FirstOrDefault(n=>n.gameObject.scene.IsValid());
            if(target==null)throw new InvalidOperationException("Native newspaper controller unavailable.");
            guestNewsSerial=state.Serial;guestNewsClosed=false;
            NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperShow",state.Headlines,state.Prosa,state.Image,state.ImageHeadline,state.Location,new Tools.Date(state.Day,state.Month,state.Year),true);
            LogDiagnostic("Host newspaper displayed: issue="+state.Serial);
        }
    }
}
