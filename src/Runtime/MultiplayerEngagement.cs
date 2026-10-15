using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly MpFirstDecision engagementDecision=new MpFirstDecision();
        readonly Dictionary<int,GameObject> engagementLines=new Dictionary<int,GameObject>();
        readonly Dictionary<int,CheckBox> engagementChecks=new Dictionary<int,CheckBox>();
        MpEngagement guestEngagement;
        int shownEngagement;
        BattlePanel NativeBattlePanel => ui==null?null:NativeAccess.Get<BattlePanel>(ui,"battlepanel");
        static ButtonTooltip[] EngagementButtons(BattlePanel p) => new[]{p.Button_Play,p.Button_Autoresolve,p.Button_DeployDefend,p.Button_Withdraw,p.Button_Retreat};
        static int DecisionButton(int state) => state==3?0:state==5?1:state==6?2:state==7?3:state==8?4:-1;
        public static void ReplicaBattleDecision(BattleUI target,int newstate)
        {
            var mp=instance;int button=DecisionButton(newstate);
            if(mp!=null && mp.transport!=null && mp.phase=="live" && mp.authenticated) {
                if(IsReplica) {
                    if(button>=0 && mp.guestEngagement?.Active==true)mp.RequestNative("engagement-choice",mp.guestEngagement.Serial,newstate);
                    return;
                }
                if(newstate==1)mp.engagementDecision.Begin(mp.engagementDecision.Serial+1);
                if(button>=0 && !mp.engagementDecision.Claim(mp.engagementDecision.Serial))return;
            }
            NativeAccess.Call(target,"GTIP_Original_ReplicaBattleDecision",newstate);
        }
        public static void ReplicaBattlePanelUpdate(BattlePanel target)
        {if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaBattlePanelUpdate");}
        static string RelativePath(Transform root,Transform child)
        {var names=new List<string>();for(var t=child;t!=null && t!=root;t=t.parent)names.Add(t.name);names.Reverse();return string.Join("/",names);}
        void CaptureEngagement(List<MpStateRow> rows)
        {
            var p=NativeBattlePanel;bool active=p!=null && (ui.battlepanelshown==1 || ui.battlepanelshown==2) && p.callingunit!=null && p.callingunit.Length==2 && p.callingunit.All(u=>u!=null);
            if(active && engagementDecision.Serial==0)engagementDecision.Begin(1);
            var s=new MpEngagement{Serial=engagementDecision.Serial,Active=active};
            if(active) {
                s.X=ui.battlepanellocation.x;s.Y=ui.battlepanellocation.y;s.Z=ui.battlepanellocation.z;
                s.Callers=p.callingunit.Select(DispatchReference).ToArray();s.Commanders=p.commanderid.ToArray();s.Buttons=EngagementButtons(p).Select(b=>b.state).ToArray();
                var root=ui.panelhandler.panel[ui.panelhandler.cpanel_battle].go.transform;
                var labels=root.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t=>!p.UnitLine.Any(l=>t.transform.IsChildOf(l.transform.parent))).ToArray();
                s.Paths=labels.Select(t=>RelativePath(root,t.transform)).ToArray();s.Labels=labels.Select(t=>t.text).ToArray();
                s.Totals=p.totalinf.Concat(p.totalcav).Concat(p.totalart).Select(t=>t.text).ToArray();
                var entries=NativeAccess.Get<IEnumerable>(typeof(BattlePanel),"bpunitentry");int index=0;
                if(entries!=null)foreach(var entry in entries) {
                    var unit=NativeAccess.Get<Regiment>(entry,"regref");int id=DispatchReference(unit);if(id==0)continue;
                    var check=NativeAccess.Get<CheckBox>(entry,"checkbox");
                    rows.Add(DataRow(25,id,new MpEngagementUnit{Side=NativeAccess.Get<int>(entry,"side"),Order=index++,Checked=check.isactive,Editable=!NativeAccess.Get<bool>(check,"frozen"),
                        Name=NativeAccess.Get<TextMeshProUGUI>(entry,"unitname").text,Men=NativeAccess.Get<TextMeshProUGUI>(entry,"men").text,Guns=NativeAccess.Get<TextMeshProUGUI>(entry,"guns").text,Delay=NativeAccess.Get<TextMeshProUGUI>(entry,"delay").text}));
                }
            }
            rows.Add(DataRow(24,0,s));
        }
        void ApplyEngagement(MpStateRow[] rows,MpStateRow[] changes)
        {
            if(!changes.Any(r=>r.Kind==24 || r.Kind==25))return;
            var row=rows.SingleOrDefault(r=>r.Kind==24);if(row==null)return;var s=ReadData<MpEngagement>(row);var p=NativeBattlePanel;
            if(p==null)throw new InvalidOperationException("Native engagement panel unavailable.");
            guestEngagement=s;
            if(!s.Active) {
                if(shownEngagement!=0){ui.panelhandler.HidePanel(ui.panelhandler.panel[ui.panelhandler.cpanel_battle]);ClearEngagementLines();shownEngagement=0;}
                ui.battlepanelshown=0;return;
            }
            if(s.Serial<=0 || s.Callers==null || s.Callers.Length!=2 || s.Callers.Any(id=>!units.ContainsKey(id)) || s.Commanders==null || s.Commanders.Length!=2 || s.Buttons==null || s.Buttons.Length!=5 || s.Paths==null || s.Labels==null || s.Paths.Length!=s.Labels.Length || s.Paths.Length>500 || s.Totals==null || s.Totals.Length!=6)throw new InvalidDataException("Invalid engagement panel state.");
            if(shownEngagement!=s.Serial) {ClearEngagementLines();p.ClearUnitLines();ui.panelhandler.ShowPanel(ui.panelhandler.cpanel_battle);shownEngagement=s.Serial;LogDiagnostic("Native engagement shown: "+s.Serial);}
            ui.battlepanelshown=2;ui.battlepanellocation=new Vector3(s.X,s.Y,s.Z);p.callingunit=s.Callers.Select(id=>units[id]).ToArray();p.commanderid=s.Commanders.ToArray();
            var root=ui.panelhandler.panel[ui.panelhandler.cpanel_battle].go.transform;
            for(int i=0;i<s.Paths.Length;i++) {var t=root.Find(s.Paths[i])?.GetComponent<TextMeshProUGUI>();if(t!=null)t.text=s.Labels[i];}
            var totals=p.totalinf.Concat(p.totalcav).Concat(p.totalart).ToArray();for(int i=0;i<totals.Length;i++)totals[i].text=s.Totals[i];
            for(int i=0;i<2;i++)ui.UpdateCommanderPanel(ui.panelhandler.panel[i==0?ui.panelhandler.cpanel_battlepanelcommanderleft:ui.panelhandler.cpanel_battlepanelcommanderright],null,s.Commanders[i],1,null,null,6);
            var buttons=EngagementButtons(p);for(int i=0;i<buttons.Length;i++)buttons[i].SetState(s.Buttons[i]);
            var incoming=rows.Where(r=>r.Kind==25).ToDictionary(r=>r.Id,ReadData<MpEngagementUnit>);
            foreach(var id in engagementLines.Keys.ToArray())if(!incoming.ContainsKey(id)){Destroy(engagementLines[id]);engagementLines.Remove(id);engagementChecks.Remove(id);}
            foreach(var pair in incoming.OrderBy(p1=>p1.Value.Order)) {
                var d=pair.Value;if(d.Side<0 || d.Side>1 || !units.TryGetValue(pair.Key,out var unit))throw new InvalidDataException("Invalid engagement unit.");
                if(!engagementLines.TryGetValue(pair.Key,out var line)) {
                    line=Instantiate(p.UnitLine[d.Side],p.UnitLine[d.Side].transform.parent);line.transform.localScale=p.UnitLine[d.Side].transform.localScale;line.SetActive(true);engagementLines[pair.Key]=line;
                    var icons=p.OOBObject.GetUnitIcons(unit);line.transform.Find("Icon/Type").GetComponent<Image>().sprite=icons[0];line.transform.Find("Icon/Size").GetComponent<Image>().sprite=icons[2];
                    line.transform.Find("Icon/Background").GetComponent<Image>().color=p.OOBObject.GetUnitColor(unit);
                    engagementChecks[pair.Key]=line.transform.Find("CheckBox").GetComponent<CheckBox>();
                }
                line.transform.SetSiblingIndex(d.Order);
                line.transform.Find("UnitInfo/UnitName").GetComponent<TextMeshProUGUI>().text=d.Name;line.transform.Find("UnitInfo/Men").GetComponent<TextMeshProUGUI>().text=d.Men;
                line.transform.Find("UnitInfo/Guns").GetComponent<TextMeshProUGUI>().text=d.Guns;line.transform.Find("Delay").GetComponent<TextMeshProUGUI>().text=d.Delay;
                var check=engagementChecks[pair.Key];check.Check(d.Checked);check.Freeze(!d.Editable);check.HasChanged();
            }
        }
        void GuestEngagementChecks()
        {
            if(guestEngagement?.Active!=true)return;
            foreach(var pair in engagementChecks)if(pair.Value!=null && pair.Value.HasChanged())RequestNative("engagement-reinforcement",guestEngagement.Serial,pair.Key,pair.Value.isactive?"1":"0");
        }
        bool ExecuteEngagementAction(string action,MpMessage request,string detail)
        {
            if(action!="engagement-choice" && action!="engagement-reinforcement")return false;
            if(request.unit!=engagementDecision.Serial || engagementDecision.Resolved || (ui.battlepanelshown!=1 && ui.battlepanelshown!=2))throw new InvalidOperationException("Engagement already resolved or replaced.");
            var p=NativeBattlePanel;
            if(action=="engagement-choice") {
                int choice=(int)request.x,index=DecisionButton(choice);
                if(choice!=request.x || index<0 || EngagementButtons(p)[index].state==2)throw new InvalidOperationException("Native engagement choice unavailable.");
                ui.SetBattlePanelShownState(choice);LogDiagnostic("Engagement first decision: "+request.unit+", choice="+choice);return true;
            }
            if(request.x!=(int)request.x || (detail!="0" && detail!="1"))throw new InvalidOperationException("Invalid reinforcement selection.");
            var entries=NativeAccess.Get<IEnumerable>(typeof(BattlePanel),"bpunitentry");
            foreach(var entry in entries)if(DispatchReference(NativeAccess.Get<Regiment>(entry,"regref"))==(int)request.x) {
                var check=NativeAccess.Get<CheckBox>(entry,"checkbox");if(NativeAccess.Get<bool>(check,"frozen"))throw new InvalidOperationException("Reinforcement is required or unavailable.");
                check.Check(detail=="1");NativeAccess.Call(p,"CheckCheckboxes");return true;
            }
            throw new InvalidOperationException("Reinforcement no longer exists.");
        }
        void ClearEngagementLines(){foreach(var line in engagementLines.Values)if(line!=null)Destroy(line);engagementLines.Clear();engagementChecks.Clear();}
        void ClearEngagement(){ClearEngagementLines();engagementDecision.Clear();guestEngagement=null;shownEngagement=0;}
    }
}
