using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<int,MpAlertState> replicaAlerts=new Dictionary<int,MpAlertState>();
        readonly Dictionary<int,int> alertPositions=new Dictionary<int,int>();
        string lastAlertCounts="";
        void CaptureAlerts(List<MpStateRow> rows)
        {
            // Capture a complete native refresh, not partially accumulated entries between
            // the native 25-frame refreshes (whose visible count/tooltip may still be old).
            CampaignTopPanel.AlarmInfo.UpdateButtons();
            var entries=NativeAccess.Get<Array>(typeof(CampaignTopPanel.AlarmInfo),"alarmentriesperbutton");
            if(entries==null)return;
            for(int i=0;i<entries.Length;i++) {
                var entry=entries.GetValue(i);int count=(int)NativeAccess.Call(entry,"GetEntries");
                var positions=new List<float>();
                for(int j=0;j<count;j++){var p=(Vector3)NativeAccess.Call(entry,"GetPosition",j);positions.AddRange(new[]{p.x,p.y,p.z});}
                var tooltips=NativeAccess.Get<List<string>>(entry,"tooltips");
                rows.Add(DataRow(13,841+i,new MpAlertState{Count=count,Blink=i!=10 && count>0 && (bool)NativeAccess.Call(entry,"ShallBlink"),Headline=NativeAccess.Get<string>(entry,"headline"),Tooltip=string.Join("\n",tooltips??new List<string>()),Positions=positions.ToArray()}));
            }
            LogAlertCounts(rows.Where(r=>r.Kind==13).Select(r=>ReadData<MpAlertState>(r).Count));
        }
        void ApplyAlerts(MpStateRow[] changes)
        {
            bool changed=false;
            foreach(var row in changes.Where(r=>r.Kind==13)) {
                var s=ReadData<MpAlertState>(row);
                if(row.Id<841 || row.Id>851 || s.Count<0 || s.Count>6000 || s.Positions==null || s.Positions.Length!=s.Count*3)throw new System.IO.InvalidDataException("Invalid campaign alerts.");
                replicaAlerts[row.Id]=s;changed=true;
            }
            if(changed) {RenderAlerts();LogAlertCounts(replicaAlerts.OrderBy(p=>p.Key).Select(p=>p.Value.Count));}
        }
        void LogAlertCounts(IEnumerable<int> counts)
        {
            string value=string.Join(",",counts);
            if(value==lastAlertCounts)return;
            lastAlertCounts=value;LogDiagnostic("Authoritative alerts 841-851: "+value);
        }
        public static void ReplicaAlertButtons()
        {
            if(!IsReplica){NativeAccess.Call(typeof(CampaignTopPanel.AlarmInfo),"GTIP_Original_ReplicaAlertButtons");return;}
            instance?.RenderAlerts();
        }
        void RenderAlerts()
        {
            var h=ui?.panelhandler;if(h==null || h.cpanel_campaigntopbar>=h.panel.Count)return;
            var panel=h.panel[h.cpanel_campaigntopbar];
            // Native UpdateButtons performs this lazy setup. The replica skips that
            // method's local alert calculations, but still needs its nested button refs.
            if(!CampaignTopPanel.AlarmInfo.panelhandlerinitialized) {
                panel.InitializeReferences(firstlevelonly:false,nonactive:true);
                CampaignTopPanel.AlarmInfo.panelhandlerinitialized=true;
            }
            foreach(var pair in replicaAlerts) {
                int id=pair.Key;var s=pair.Value;
                h.SetButtonsCondition(panel,s.Count>0?0:2,id,-1,hidedeactivated:true);
                h.UpdateChildText(panel,id,"Number",s.Count>1?s.Count.ToString():"");h.UpdateChildObjectState(panel,id,"Image",active:s.Count>1);
                h.SetButtonsBlinkStatus(panel,s.Blink?new Color(1,.5f,.5f):Color.white,s.Blink,id);
                h.ExchangeButtonTooltipText(panel,id,s.Headline??"",s.Tooltip??"");
            }
        }
        public static void ReplicaAlertPosition(int buttonid,int index)
        {
            if(!IsReplica){NativeAccess.Call(typeof(CampaignTopPanel.AlarmInfo),"GTIP_Original_ReplicaAlertPosition",buttonid,index);return;}
            if(instance==null || !instance.replicaAlerts.TryGetValue(buttonid,out var s) || s.Count==0)return;
            int next=instance.alertPositions.TryGetValue(buttonid,out var old)?old:0;
            for(int i=0;i<s.Count;i++) {
                next=(next+index+s.Count)%s.Count;
                var p=new Vector3(s.Positions[next*3],s.Positions[next*3+1],s.Positions[next*3+2]);
                if(p==Vector3.zero)continue;
                var cam=UnityEngine.Object.FindObjectOfType<Camera_Battlefield>();
                if(cam!=null)cam.SetMapScrollTarget(p,cam.transform.eulerAngles.y,GamePrefs.distancetounitforzoomin,GamePrefs.heighttounitforzoomin);
                break;
            }
            instance.alertPositions[buttonid]=next;
        }
    }
}
