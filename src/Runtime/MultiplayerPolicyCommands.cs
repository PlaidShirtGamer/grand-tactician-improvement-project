using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed class MpPolicyState
    {
        public int Side;
        public float Progress;
        public bool Researching, Enacted, Applied;
    }
    public sealed partial class MultiplayerCampaign
    {
        public static void ReplicaPolicySelection(Policy target)
        {
            if(!IsReplica) {NativeAccess.Call(target,"GTIP_Original_ReplicaPolicySelection");return;}
            if(instance==null || BlocksInput || !instance.authenticated || target.ButtonTooltipRef==null)return;
            var handler=GameObject.Find("UI")?.GetComponent<PanelHandler>();
            if(handler==null || handler.panel[handler.cpanel_policies].state!=2)return;
            if(Input.GetMouseButtonDown(0) && ReplicaHitButton()==target.ButtonTooltipRef && target.ButtonTooltipRef.state!=2)
                instance.SubmitGlobal("policy",target.UniquePolicyID);
        }
        void SubmitGlobal(string kind,float value)
        {
            if(host || !authenticated)return;
            Send(new MpMessage{kind=kind,session=session,sequence=++guestSequence,x=value});
            orderStatus="Request "+guestSequence+" sent to host: "+kind;
        }
        bool ExecuteGlobal(MpMessage request)
        {
            if(request.kind!="policy" && request.kind!="policy-cancel" && request.kind!="speed" && request.kind!="project" && request.kind!="recruit" && request.kind!="save-checkpoint" && request.kind!="finance")return false;
            if(mode!="coop" || guestSide!=hostSide || request.sequence<=lastSequence || request.sequence<=0 || !MpRules.Finite(request.x))
                throw new InvalidOperationException("Invalid co-op request.");
            lastSequence=request.sequence;
            if(request.kind=="finance") {ExecuteFinance(request);return true;}
            if(request.kind=="recruit") { ExecuteRecruit(request);return true; }
            if(request.kind=="save-checkpoint") {
                if(SceneManagement.currentoperation!=2)throw new InvalidOperationException("Wait for the host's current load/save to finish.");
                string prior=Saving.latestfolder;
                try {
                    Saving.latestfolder="SaveGTIPRequested"+Guid.NewGuid().ToString("N");
                    SceneManagement.SaveAll(manualsavename:"GTIP MP guest checkpoint "+DateTime.Now.ToString("yyyyMMdd HHmmss"));
                } finally {Saving.latestfolder=prior;}
                return true;
            }
            if(request.kind=="speed") {
                if(newsBarrier.Waiting && request.x>0)throw new InvalidOperationException("Both players must dismiss the newspaper before resuming.");
                if(newsBarrier.Waiting && request.x==0)newsResumeSpeed=0;
                if(request.x!=0 && !GameVars.gamespeedcampaigns.Contains(request.x))throw new InvalidOperationException("Unsupported campaign speed.");
                GameVars.SetGameSpeed(request.x,manualset:true);return true;
            }
            if(request.kind=="project") {
                if(request.x!=(int)request.x)throw new InvalidOperationException("Invalid project ID.");
                var project=Projects.LoadedProjects.GetLoadedProjectFromID((int)request.x);
                if(project==null || !project.alliances.Contains(guestSide) || !Projects.IsAppointable(project,guestSide))throw new InvalidOperationException("Project is no longer available or funded.");
                Projects.AppointProject(project,guestSide,manualappointment:true);RefreshProjectsView();return true;
            }
            if(request.x!=(int)request.x)throw new InvalidOperationException("Invalid policy ID.");
            var p=Policy.GetPolicyFromID((int)request.x);
            if(request.kind=="policy-cancel") {
                if(p==null || Policy.GetAllianceFromPolicy(p)!=guestSide || !GameVars.alliance[guestSide].activatedpolicies.Contains(p) || (p.IsAct && p.researchprogress>=1))throw new InvalidOperationException("Policy is no longer cancellable.");
                Policies.RemoveResearch(guestSide,p);return true;
            }
            if(p==null || Policy.GetAllianceFromPolicy(p)!=guestSide || p.markedasgrandpolicy || !p.IsAvailableInScenario() ||
                p.ReferedChapter>Policy.CurrentChapter || !p.HasPrePolicies() || p.blocked ||
                (bool)NativeAccess.Call(typeof(Policies),"IsDeactivated",p) || !Policies.CanBeActivated(guestSide,p))
                throw new InvalidOperationException("Policy is no longer available for research.");
            Policies.AddResearch(guestSide,p);
            if(!GameVars.alliance[guestSide].activatedpolicies.Contains(p))throw new InvalidOperationException("Native policy selection failed.");
            return true;
        }
        void CapturePolicies(List<MpStateRow> rows)
        {
            foreach(var p in Policy.allpolicies) {
                int side=Policy.GetAllianceFromPolicy(p);var a=GameVars.alliance[side];
                rows.Add(DataRow(9,p.UniquePolicyID,new MpPolicyState{Side=side,Progress=p.researchprogress,Researching=a.activatedpolicies.Contains(p),Enacted=a.activatedacts.Contains(p),Applied=a.alreadyactivatedacts.Contains(p)}));
            }
        }
        void ApplyPolicies(MpStateRow[] rows)
        {
            foreach(var row in rows.Where(r=>r.Kind==9)) {
                var s=ReadData<MpPolicyState>(row);var p=Policy.GetPolicyFromID(row.Id);
                if(p==null || s.Side<0 || s.Side>=GameVars.alliance.Length || Policy.GetAllianceFromPolicy(p)!=s.Side || !MpRules.Finite(s.Progress))throw new InvalidDataException("Policy state mismatch.");
            }
            bool changed=false;
            foreach(var row in rows.Where(r=>r.Kind==9)) {
                var s=ReadData<MpPolicyState>(row);var p=Policy.GetPolicyFromID(row.Id);var a=GameVars.alliance[s.Side];
                changed|=p.researchprogress!=s.Progress || a.activatedpolicies.Contains(p)!=s.Researching || a.activatedacts.Contains(p)!=s.Enacted;
                p.researchprogress=s.Progress;
                SetPolicyMembership(a.activatedpolicies,p,s.Researching);SetPolicyMembership(a.activatedacts,p,s.Enacted);SetPolicyMembership(a.alreadyactivatedacts,p,s.Applied);
            }
            // UpdateActivations finds the active Policies/Title object. It throws when the
            // panel is closed, and also mutates subsidy effects. Refresh only the visible UI.
            if(changed)RefreshPoliciesView();
        }
        static void RefreshPoliciesView()
        {
            var view=GameObject.Find("UI/Policies");
            if(view==null || !view.activeInHierarchy)return;
            Policies.UpdateActivations();Policies.UpdateAllTooltips();Policies.UpdateCounter(GameVars.playeralliance);
        }
        public static void ReplicaProject(Projects.LoadedProjects project,int alliance,bool manualappointment)
        {
            if(!IsReplica){NativeAccess.Call(typeof(Projects),"GTIP_Original_ReplicaProject",project,alliance,manualappointment);return;}
            if(instance!=null && instance.authenticated && project!=null && alliance==instance.guestSide && !BlocksInput)
                instance.SubmitGlobal("project",project.projectid);
        }
        void CaptureProjects(List<MpStateRow> rows)
        {
            for(int i=0;i<GameVars.alliance.Length;i++)if(GameVars.alliance[i]!=null)
                rows.Add(DataRow(10,i,new MpProjectState{Projects=GameVars.alliance[i].projects.ToArray(),LastMonth=GameVars.alliance[i].lastmonthprojectsresearched.ToArray()}));
        }
        void ApplyProjects(MpStateRow[] rows)
        {
            bool changed=false;
            foreach(var row in rows.Where(r=>r.Kind==10)) {
                var s=ReadData<MpProjectState>(row);
                if(row.Id>=GameVars.alliance.Length || s.Projects==null || s.LastMonth==null || s.Projects.Any(id=>Projects.LoadedProjects.GetLoadedProjectFromID(id)==null))throw new InvalidDataException("Invalid project state.");
                var a=GameVars.alliance[row.Id];changed|=!a.projects.SequenceEqual(s.Projects);
                a.projects.Clear();a.projects.AddRange(s.Projects);a.lastmonthprojectsresearched.Clear();a.lastmonthprojectsresearched.AddRange(s.LastMonth);
            }
            if(changed) {
                // Unlock caches are derived; one-time project spending/effects run only on host.
                Projects.UpdateProjectEffects();
                foreach(var row in rows.Where(r=>r.Kind==0))DataSchemas[0].Apply(DataTarget(row),row.Values);
                RefreshProjectsView();RefreshPoliciesView();
            }
        }
        static void RefreshProjectsView()
        {
            var view=UnityEngine.Object.FindObjectOfType<Projects>();
            if(view!=null && view.isActiveAndEnabled){view.UpdateProjects();NativeAccess.Call(view,"UpdateAllProjectEffects");}
        }
        static void SetPolicyMembership(List<Policy> list,Policy p,bool present)
        {if(present){if(!list.Contains(p))list.Add(p);}else list.Remove(p);}
        void GuestTimeKeys()
        {
            RepairShortcutFocus();
            if(host || !authenticated || panel || choosingCommander || GuestDialogOpen || GameVars.deactivateshortcuts)return;
            if(Input.GetKeyDown(GameVars.keycodes[GameVars.key_showmenu].keycode) && ui!=null && ui.panelhandler!=null) {
                var h=ui.panelhandler;
                if(h.panel[h.panel_mainmenusmall].state<=0)h.ShowPanel(h.panel_mainmenusmall);
                else h.HidePanel(h.panel[h.panel_mainmenusmall]);
            }
            if(Input.GetKeyDown(GameVars.keycodes[GameVars.key_pausegame].keycode))
                SubmitGlobal("speed",GameVars.gamepaused?GameVars.gamespeedcampaigns[0]:0);
            if(Input.GetKeyDown(GameVars.keycodes[GameVars.key_speed0].keycode))SubmitGlobal("speed",0);
            int[] keys={GameVars.key_speed1,GameVars.key_speed2,GameVars.key_speed3,GameVars.key_speed4};
            for(int i=0;i<keys.Length && i<GameVars.gamespeedcampaigns.Length;i++)
                if(Input.GetKeyDown(GameVars.keycodes[keys[i]].keycode))SubmitGlobal("speed",GameVars.gamespeedcampaigns[i]);
            var speeds=new[]{0f}.Concat(GameVars.gamespeedcampaigns).Distinct().OrderBy(x=>x).ToArray();
            float current=GameVars.gamepaused?0:GameVars.gamespeed;
            if(Input.GetKeyDown(GameVars.keycodes[GameVars.key_increasespeed].keycode))
                SubmitGlobal("speed",speeds.Where(x=>x>current).DefaultIfEmpty(speeds.Last()).First());
            if(Input.GetKeyDown(GameVars.keycodes[GameVars.key_decreasespeed].keycode))
                SubmitGlobal("speed",speeds.LastOrDefault(x=>x<current));
            if(Input.GetKeyDown(GameVars.keycodes[GameVars.key_stopunits].keycode) && guestSelection!=null)
                SubmitGuestOrder("stop",Vector3.zero);
        }
    }
}
