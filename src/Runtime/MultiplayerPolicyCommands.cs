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
            if(Input.GetMouseButtonDown(0) && target.ButtonTooltipRef.mouseonobject && target.ButtonTooltipRef.state!=2)
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
            if(request.kind!="policy" && request.kind!="speed")return false;
            if(mode!="coop" || guestSide!=hostSide || request.sequence<=lastSequence || request.sequence<=0 || !MpRules.Finite(request.x))
                throw new InvalidOperationException("Invalid co-op request.");
            lastSequence=request.sequence;
            if(request.kind=="speed") {
                if(request.x!=0 && !GameVars.gamespeedcampaigns.Contains(request.x))throw new InvalidOperationException("Unsupported campaign speed.");
                GameVars.SetGameSpeed(request.x,manualset:true);return true;
            }
            if(request.x!=(int)request.x)throw new InvalidOperationException("Invalid policy ID.");
            var p=Policy.GetPolicyFromID((int)request.x);
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
            if(changed)Policies.UpdateActivations();
        }
        static void SetPolicyMembership(List<Policy> list,Policy p,bool present)
        {if(present){if(!list.Contains(p))list.Add(p);}else list.Remove(p);}
        void GuestTimeKeys()
        {
            if(host || !authenticated || panel || choosingCommander || GameVars.deactivateshortcuts)return;
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
