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
        Newspaper guestNewspaper;
        int guestNewsPrepared,guestNewsDisplayed;
        bool guestNewsPresenting,guestNewsDeferred,guestNewsRepairLogged;
        float guestNewsRetryAt;
        void ClearNewspaperPresentation()
        {
            guestNewspaper=null;guestNewsSerial=guestNewsPrepared=guestNewsDisplayed=0;
            guestNewsClosed=guestNewsPresenting=guestNewsDeferred=guestNewsRepairLogged=false;guestNewsRetryAt=0;
        }
        static string NewspaperHeadline(MpNewspaperState state)=>string.Join(" | ",state.Headlines??Array.Empty<string>());
        public static void ReplicaNewspaperShow(Newspaper target,string[] headlines,string prosa,int imgid,string imageheadline,string location,Tools.Date datefrom,bool dontcontinue)
        {
            var mp=instance;
            if(mp!=null && mp.host && mp.transport!=null && mp.phase=="live" && mp.authenticated) {
                // UpdateShow silently returns for this local option. Such a call
                // cannot create a multiplayer dismissal barrier for an unseen issue.
                if(GameVars.debug_deactivatenewspapermessages) {
                    NativeAccess.Call(target,"GTIP_Original_ReplicaNewspaperShow",headlines,prosa,imgid,imageheadline,location,datefrom,dontcontinue);return;
                }
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
                mp.LogDiagnostic("Newspaper opened: issue="+serial+"; headline="+NewspaperHeadline(mp.newspaperState)+"; storedSpeed="+mp.newsResumeSpeed+"; canResume="+mp.newsCanResume+"; waiting for both players.");return;
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
                    // OnDisable also calls Close, including when another native
                    // panel hides this one. Only a close of the active newspaper
                    // is a player's dismissal; an implicit hide is recoverable.
                    if(!mp.guestNewsPresenting && target.gameObject.activeInHierarchy)mp.AcknowledgeGuestNewspaper("dismissed");
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
            if(state.Serial<guestNewsSerial || state.Serial<0 || (state.Active && (state.Serial==0 || state.Headlines==null || state.Headlines.Length>8 || state.Headlines.Any(h=>h==null) || state.Prosa==null || state.ImageHeadline==null || state.Location==null || state.Day<1 || state.Month<1 || state.Month>12 || state.Year<1 || state.Year>9999 || state.Day>DateTime.DaysInMonth(state.Year,state.Month))))
                throw new InvalidDataException("Invalid newspaper state.");
            newspaperState=state;
            if(!state.Active)return;
            if(guestNewsSerial!=state.Serial) {
                guestNewsSerial=state.Serial;guestNewsClosed=false;guestNewsDeferred=guestNewsRepairLogged=false;guestNewsRetryAt=0;
                LogDiagnostic("Host newspaper received: issue="+state.Serial+"; headline="+NewspaperHeadline(state)+"; localDisabled="+GameVars.debug_deactivatenewspapermessages);
            }
            RefreshGuestNewspaper();
        }
        void AcknowledgeGuestNewspaper(string reason)
        {
            if(guestNewsClosed || guestNewsSerial<=0 || !newspaperState.Active || newspaperState.Serial!=guestNewsSerial)return;
            guestNewsClosed=true;
            Send(new MpMessage{kind="news-ack",session=session,unit=guestNewsSerial});
            orderStatus="Newspaper dismissed. Waiting for the host to dismiss it.";
            LogDiagnostic("Host newspaper acknowledged: issue="+guestNewsSerial+"; reason="+reason);
        }
        bool GuestNewspaperCloseIntent(PanelHandler handler,GameObject target)
        {
            if(!IsReplica || host || !authenticated || phase!="live" || target==null || handler==null ||
                handler.cpanel_newspaper<0 || handler.cpanel_newspaper>=handler.panel.Count || handler.panel[handler.cpanel_newspaper].go!=target)return false;
            var paper=target.GetComponent<Newspaper>();
            if(paper==null)return false;
            // The native X can close through ButtonTooltip/ModularUIFunctions
            // instead of Newspaper.Close. Record that explicit user intent now,
            // before its generic canvas fade can be mistaken for a passive hide.
            AcknowledgeGuestNewspaper("Close Panel control");
            ReplicaNewspaperClose(paper);return true;
        }
        void DeferGuestNewspaper(string reason)
        {
            if(guestNewsDeferred)return;guestNewsDeferred=true;
            LogDiagnostic("Host newspaper presentation pending: issue="+guestNewsSerial+"; "+reason);
        }
        static bool NewspaperContentPrepared(Newspaper target,MpNewspaperState state)
        {
            if(!Newspaper.newspapershown || target.ProsaT.text!=state.Prosa || target.ImageTitleT.text!=state.ImageHeadline || target.HeadlineT.Length<state.Headlines.Length)return false;
            for(int i=0;i<target.HeadlineT.Length;i++)
                if(target.HeadlineT[i]==null || target.HeadlineT[i].text!=(i<state.Headlines.Length?state.Headlines[i]:""))return false;
            return true;
        }
        static bool NewspaperPanelOpen(PanelHandler handler,int id)=>id>=0 && id<handler.panel.Count && handler.panel[id].state>0;
        static string NewspaperViewDiagnostic(PanelHandler handler,PanelHandler.Panel paper)
        {
            string path=paper.go.name;
            for(var parent=paper.go.transform.parent;parent!=null;parent=parent.parent)path=parent.name+"/"+path;
            var main=handler.cpanel_mainpanel>=0 && handler.cpanel_mainpanel<handler.panel.Count?handler.panel[handler.cpanel_mainpanel]:null;
            string Order(PanelHandler.Panel p)=>p?.canvas==null?"none":p.canvas.sortingOrder+"/override="+p.canvas.overrideSorting;
            return "view="+path+"; active="+paper.go.activeInHierarchy+"; panelState="+paper.state+"; alpha="+paper.alpha+
                "; canvasAlpha="+(paper.canvasgroup==null?"none":paper.canvasgroup.alpha.ToString())+"; nativeShown="+Newspaper.newspapershown+
                "; canvasOrder="+Order(paper)+"; militaryState="+(main==null?-1:main.state)+"; militaryCanvasOrder="+Order(main);
        }
        void RefreshGuestNewspaper()
        {
            if(!IsReplica || host || !authenticated || phase!="live" || !newspaperState.Active || guestNewsClosed || guestNewsSerial<=0)return;
            if(GameVars.debug_deactivatenewspapermessages) {
                AcknowledgeGuestNewspaper("local newspaper option disabled");return;
            }
            // A session/options menu may intentionally cover the native campaign
            // UI. Resume presentation after it closes, without fighting that menu.
            if(BlocksInput || CampaignEditor.BlocksInput || GuestDialogOpen)return;
            var handler=ui?.panelhandler;
            if(handler==null || handler.panel==null || handler.cpanel_newspaper<0 || handler.cpanel_newspaper>=handler.panel.Count) {
                DeferGuestNewspaper("native panel controller unavailable");return;
            }
            if(NewspaperPanelOpen(handler,handler.cpanel_optionspanel) || NewspaperPanelOpen(handler,handler.panel_mainmenusmall))return;
            var paper=handler.panel[handler.cpanel_newspaper];
            if(paper.go==null) {DeferGuestNewspaper("native newspaper panel unavailable");return;}
            // The panel owns the view to render. A scene-wide Resources search can
            // also return an inactive/template controller that is not this panel.
            if(guestNewspaper==null || guestNewspaper.gameObject!=paper.go)guestNewspaper=paper.go.GetComponent<Newspaper>();
            if(guestNewspaper==null || paper.go==null) {DeferGuestNewspaper("native newspaper view unavailable");return;}
            if(guestNewsPrepared!=guestNewsSerial) {
                if(guestNewspaper.ProsaT==null || guestNewspaper.HeadlineT==null || guestNewspaper.DateT==null || guestNewspaper.VolumeT==null ||
                    guestNewspaper.ProsaLocationT==null || guestNewspaper.ImageTitleT==null) {DeferGuestNewspaper("native newspaper text references unavailable");return;}
                if(Time.realtimeSinceStartup<guestNewsRetryAt)return;
                guestNewsRetryAt=Time.realtimeSinceStartup+1;
                int number=NativeAccess.Get<int>(guestNewspaper,"runningnumber");
                try {
                    guestNewsPresenting=true;
                    var state=newspaperState;
                    NativeAccess.Call(guestNewspaper,"GTIP_Original_ReplicaNewspaperShow",state.Headlines,state.Prosa,state.Image,state.ImageHeadline,state.Location,new Tools.Date(state.Day,state.Month,state.Year),true);
                    if(NativeAccess.Get<int>(guestNewspaper,"runningnumber")!=unchecked(number+1) || !NewspaperContentPrepared(guestNewspaper,state)) {
                        NativeAccess.Set(guestNewspaper,"runningnumber",number);
                        DeferGuestNewspaper("native UpdateShow did not prepare the received content");return;
                    }
                    guestNewsPrepared=guestNewsSerial;
                } catch(Exception error) {
                    NativeAccess.Set(guestNewspaper,"runningnumber",number);
                    DeferGuestNewspaper("native view initialization failed: "+error.GetType().Name+": "+error.Message);return;
                } finally {guestNewsPresenting=false;}
            }
            // Content is prepared once per serial. A native panel can be hidden by
            // deferred window operations while its issue remains unacknowledged.
            // Re-show that panel, never UpdateShow/UpdateCommon: the native volume
            // number and article body must not advance for a visibility repair.
            bool open=paper.go.activeInHierarchy && (paper.state==1 || paper.state==2) && paper.timeremaining>0;
            bool transparent=paper.state==2 && (paper.alpha<=0 || paper.canvasgroup!=null && paper.canvasgroup.alpha<=0);
            if(!open || !Newspaper.newspapershown || transparent) {
                try {
                    guestNewsPresenting=true;
                    int previous=paper.state;bool active=paper.go.activeInHierarchy;
                    handler.ShowPanel(handler.cpanel_newspaper,100000000f,skipbuttonstatusupdate:true,skipmouseoverclearing:true);
                    NativeAccess.Set(guestNewspaper,"dontcontinuespeed",true);Newspaper.newspapershown=true;
                    if(!guestNewsRepairLogged) {
                        guestNewsRepairLogged=true;
                        LogDiagnostic("Host newspaper visibility restored: issue="+guestNewsSerial+"; previousState="+previous+"; previouslyActive="+active);
                    }
                } catch(Exception error) {DeferGuestNewspaper("native panel restore failed: "+error.GetType().Name+": "+error.Message);return;}
                finally {guestNewsPresenting=false;}
            }
            if(paper.go.activeInHierarchy && (paper.state==1 || paper.state==2) && paper.timeremaining>0 && Newspaper.newspapershown &&
                paper.alpha>0 && (paper.canvasgroup==null || paper.canvasgroup.alpha>0)) {
                if(guestNewsDisplayed!=guestNewsSerial) {
                    guestNewsDisplayed=guestNewsSerial;
                    LogDiagnostic("Host newspaper displayed: issue="+guestNewsSerial+"; headline="+NewspaperHeadline(newspaperState)+"; "+NewspaperViewDiagnostic(handler,paper));
                }
            } else DeferGuestNewspaper("waiting for native panel visibility; "+NewspaperViewDiagnostic(handler,paper));
        }
    }
}
