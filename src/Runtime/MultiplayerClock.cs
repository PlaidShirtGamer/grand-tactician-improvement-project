using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        MpClock hostClock;
        Action restoreClock;
        TimePanel replicaTimePanel;
        CampaignTopPanel replicaTopPanel;
        TextMeshProUGUI[] campaignClockLabels;
        float lastClockSend, lastHostSpeed;
        bool lastHostPaused;
        int clockSequence,receivedClockSequence;
        MpClock CaptureClock()
        {
            var controller=GameObject.Find("GameController").GetComponent<BattleUnits>();
            var storm=controller.uniStormSystem;
            if(storm==null) throw new InvalidOperationException("Campaign clock is unavailable.");
            var value=new MpClock { year=controller.year, month=storm.monthCounter, day=storm.dayCounter,
                hour=storm.Hour, elapsed=GameVars.currenttimefromstart, speed=GameVars.gamespeed, paused=GameVars.gamepaused };
            value.Validate(); return value;
        }
        void SaveClock()
        {
            var controller=GameObject.Find("GameController").GetComponent<BattleUnits>();
            var storm=controller.uniStormSystem;
            var clock=CaptureClock();
            int weatherYear=storm.yearCounter, oldMonth=GameVars.currentmonth;
            float oldHour=GameVars.currenthourofday, start=storm.startTime, startHour=storm.startTimeHour;
            int startMinute=storm.startTimeMinute, minute=storm.minuteCounter, hour=storm.hourCounter;
            bool stopped=storm.timeStopped;
            restoreClock=()=> {
                if(controller==null || storm==null) return;
                SetClockFields(controller,storm,clock);
                storm.yearCounter=weatherYear; storm.startTime=start; storm.startTimeHour=startHour;
                storm.startTimeMinute=startMinute; storm.minuteCounter=minute; storm.hourCounter=hour; storm.timeStopped=stopped;
                GameVars.currentmonth=oldMonth; GameVars.currenthourofday=oldHour;
                NativeAccess.Call(typeof(GameVars),"GTIP_Original_ReplicaSpeed",clock.paused?0f:clock.speed,false,true);
                RenderClock(clock);
            };
        }
        static void SetClockFields(BattleUnits controller,UniStormWeatherSystem_C storm,MpClock c)
        {
            controller.year=c.year; storm.yearCounter=c.year; storm.monthCounter=c.month; storm.dayCounter=c.day;
            storm.Hour=c.hour; storm.startTime=c.hour/24f; storm.startTimeHour=(int)c.hour;
            storm.hourCounter=(int)c.hour; storm.minuteCounter=(int)(c.hour*60f)%60; storm.startTimeMinute=storm.minuteCounter;
            GameVars.currenttimefromstart=c.elapsed; GameVars.currentmonth=c.month-1; GameVars.currenthourofday=c.hour;
            GameVars.gamespeed=c.speed; GameVars.gamepaused=c.paused;
            // The authoritative clock advances through network updates, never a second local timer.
            storm.timeStopped=true;
        }
        void ApplyClock(MpClock clock)
        {
            if(clock==null) throw new InvalidOperationException("Missing host campaign clock.");
            clock.Validate();
            var controller=GameObject.Find("GameController").GetComponent<BattleUnits>();
            SetClockFields(controller,controller.uniStormSystem,clock);
            ModelAnimator.SetOverallSpeed(clock.paused?0f:clock.speed);
            hostClock=clock;
        }
        void RenderClock(MpClock clock)
        {
            if(replicaTimePanel==null) replicaTimePanel=UnityEngine.Object.FindObjectOfType<TimePanel>();
            if(replicaTimePanel!=null && replicaTimePanel.text!=null) replicaTimePanel.text.text=Tools.ConvertFloatToTimestamp(clock.hour);
            if(replicaTopPanel==null) replicaTopPanel=UnityEngine.Object.FindObjectOfType<CampaignTopPanel>();
            if(replicaTopPanel!=null) {
                var date=NativeAccess.Get<TextMeshProUGUI>(replicaTopPanel,"date");
                if(date!=null) date.text=Tools.GetDateString(clock.day,clock.month,clock.year);
            }
            if(ui==null || ui.panelhandler==null) return;
            var handler=ui.panelhandler; int index=handler.cpanel_timerpanelcampaign;
            if(index<0 || index>=handler.panel.Count) return;
            if(campaignClockLabels==null) {
                campaignClockLabels=handler.panel[index].go.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Where(t=>t.name=="BattleTime").ToArray();
                LogDiagnostic("Native campaign clock labels: "+string.Join(",",campaignClockLabels.Select(t=>t.name).ToArray()));
            }
            foreach(var label in campaignClockLabels) if(label!=null) label.text=Tools.ConvertFloatToTimestamp(clock.hour);
            int button=72;
            if(!clock.paused) {
                button=73;
                for(int i=1;i<GameVars.gamespeedcampaigns.Length && i<=3;i++) if(clock.speed>=GameVars.gamespeedcampaigns[i]) button=73+i;
            }
            handler.ClearButtonStates(handler.panel[index],skipdeactivated:true,updateactivation:false);
            handler.SetButtonsCondition(handler.panel[index],1,button);
        }
        void LateUpdate()
        {
            cursorGuard.Repair("late update");
            if(transport==null || !authenticated || phase!="live")return;
            RefreshEconomyViews();
            if(host) {
                RefreshOfficerView();
                // Sample after native simulation/UI Update. A pause must publish the final
                // clock, and the host's throttled native label must show that same sample.
                hostClock=CaptureClock();RenderClock(hostClock);
                if(MpRules.ClockDue(hostClock.paused,lastHostPaused!=hostClock.paused || lastHostSpeed!=hostClock.speed,Time.realtimeSinceStartup,lastClockSend)) {
                    lastClockSend=Time.realtimeSinceStartup;lastHostPaused=hostClock.paused;lastHostSpeed=hostClock.speed;
                    Send(new MpMessage{kind="clock",session=session,sequence=++clockSequence,clock=hostClock});
                }
            } else if(IsReplica && hostClock!=null) {
                SetClockFields(EditorFormationOperations.Engine,EditorFormationOperations.Engine.uniStormSystem,hostClock);RenderClock(hostClock);
                // Native panel/tooltip refreshes run independently of the replicated clock.
                // Reassert host-owned alerts after those callbacks, including while paused.
                RenderAlerts();
                DrawMotionSymbols();
            }
        }
        void RestoreClock()
        {
            var restore=restoreClock; restoreClock=null; hostClock=null;
            restore?.Invoke(); replicaTimePanel=null; replicaTopPanel=null; campaignClockLabels=null;
        }
    }
}
