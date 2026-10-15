using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        sealed class FinanceViewState
        {
            public float[] Taxes,Subsidies;
            public readonly Dictionary<int,float> Pending=new Dictionary<int,float>();
            public readonly Dictionary<int,float> Sent=new Dictionary<int,float>();
            public readonly Dictionary<int,float> SentValues=new Dictionary<int,float>();
        }
        readonly Dictionary<Finances,FinanceViewState> financeViews=new Dictionary<Finances,FinanceViewState>();
        static bool LiveSession => instance!=null && instance.transport!=null && instance.authenticated && instance.phase=="live";
        public static void FinanceInitial(Finances target)
        {
            NativeAccess.Call(target,"GTIP_Original_FinanceInitial");
            if(LiveSession)instance.financeViews[target]=new FinanceViewState{Taxes=target.TaxSlider.Select(s=>s.value).ToArray(),Subsidies=target.SubsidiesSlider.Select(s=>s.value).ToArray()};
        }
        public static void FinanceSliders(Finances target)
        {
            if(!LiveSession){NativeAccess.Call(target,"GTIP_Original_FinanceSliders");return;}
            instance.UpdateFinanceSliders(target);
        }
        void UpdateFinanceSliders(Finances view)
        {
            if(!financeViews.TryGetValue(view,out var state)) {
                state=new FinanceViewState{Taxes=view.TaxSlider.Select(s=>s.value).ToArray(),Subsidies=view.SubsidiesSlider.Select(s=>s.value).ToArray()};financeViews[view]=state;
            }
            var a=GameVars.alliance[GameVars.playeralliance];var taxes=(float[])a.tax.Clone();var subsidies=(float[])a.subsidies.Clone();
            void Sync(UnityEngine.UI.Slider[] sliders,float[] previous,float[] authority,int category)
            {
                for(int i=0;i<Math.Min(sliders.Length,authority.Length);i++) {
                    int key=category*100+i;float value=sliders[i].value;
                    // Only this adapter refreshes sliders from received state, and records
                    // the displayed value. A later difference is a native UI edit, including
                    // an EventSystem release processed after this component's Update.
                    bool changed=Mathf.Abs(value-previous[i])>0.00001f;
                    if(changed) {
                        value=MpFinanceRules.Normalize(category,i,value,a.tax.Length,a.subsidies.Length,GamePrefs.minimumpolicies,GamePrefs.maximumpolicies);
                        if(host)authority[i]=value;
                        else {
                            state.Pending[key]=value;
                            if(!state.Sent.TryGetValue(key,out float sent) || Time.realtimeSinceStartup-sent>.15f || Input.GetMouseButtonUp(0)) {
                                Send(new MpMessage{kind="finance",session=session,sequence=++guestSequence,unit=i,x=value,z=category});
                                state.Sent[key]=Time.realtimeSinceStartup;
                                state.SentValues[key]=value;
                            }
                        }
                    }
                    if(!host && state.Pending.TryGetValue(key,out float pending)) {
                        if(Mathf.Abs(authority[i]-pending)<0.00001f || (state.Sent.TryGetValue(key,out float sent) && Time.realtimeSinceStartup-sent>3))state.Pending.Remove(key);
                        else value=pending;
                    }
                    if(!changed && !state.Pending.ContainsKey(key))value=authority[i];
                    sliders[i].SetValueWithoutNotify(value);previous[i]=value;
                }
            }
            Sync(view.TaxSlider,state.Taxes,taxes,0);Sync(view.SubsidiesSlider,state.Subsidies,subsidies,1);
            if(!host)foreach(var pending in state.Pending)if(!state.SentValues.TryGetValue(pending.Key,out float lastValue) ||
                (lastValue!=pending.Value && (!state.Sent.TryGetValue(pending.Key,out float sent) || Time.realtimeSinceStartup-sent>.15f || Input.GetMouseButtonUp(0)))) {
                Send(new MpMessage{kind="finance",session=session,sequence=++guestSequence,unit=pending.Key%100,x=pending.Value,z=pending.Key/100});
                state.Sent[pending.Key]=Time.realtimeSinceStartup;
                state.SentValues[pending.Key]=pending.Value;
            }
            // Native routine formats slider labels. Guest edits remain requests, never local spending rules.
            try {NativeAccess.Call(view,"GTIP_Original_FinanceSliders");NativeAccess.Call(view,"GTIP_Original_FinanceInitial");}
            finally {
                if(!host){Array.Copy(taxes,a.tax,taxes.Length);Array.Copy(subsidies,a.subsidies,subsidies.Length);}
            }
        }
        public static void FinanceCheckbox(Finances target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_FinanceCheckbox");return;}
            if(!LiveSession)return;
            if(target.AutomanageCheckBox.HasChanged())instance.Send(new MpMessage{kind="finance",session=instance.session,sequence=++instance.guestSequence,unit=0,z=2,x=target.AutomanageCheckBox.isactive?1:0});
            target.AutomanageCheckBox.Check(GameVars.automanagefinances);
            target.AutomanageCheckBox.HasChanged(); // consume the programmatic refresh, never echo it as another request
        }
        void ExecuteFinance(MpMessage request)
        {
            if(request.z!=(int)request.z)throw new InvalidOperationException("Invalid financial control.");
            var a=GameVars.alliance[guestSide];int category=(int)request.z;
            float value=MpFinanceRules.Normalize(category,request.unit,request.x,a.tax.Length,a.subsidies.Length,GamePrefs.minimumpolicies,GamePrefs.maximumpolicies);
            if(category==0)a.tax[request.unit]=value;
            if(category==1){a.subsidies[request.unit]=value;Policies.UpdateSubsidyEffect(guestSide);RefreshPoliciesView();}
            if(category==2)GameVars.automanagefinances=value==1;
            LogDiagnostic("Finance request applied: category="+category+", index="+request.unit+", value="+value);
        }
        void CaptureControls(List<MpStateRow> rows) => rows.Add(DataRow(12,0,new MpCampaignControls{AutoFinances=GameVars.automanagefinances,Chapter=Policy.CurrentChapter,AutoGovernment=GameVars.automanageconstructiong,AutoCompanies=GameVars.automanageconstructionp,AutoRailroads=GameVars.automanageconstructionr,AutoPlace=GameVars.autoplacebuildings,AutoWeapons=GameVars.automanageweapons,UseFog=GameVars.usefow}));
        void ApplyControls(MpStateRow[] rows)
        {
            var row=rows.SingleOrDefault(r=>r.Kind==12);if(row==null)return;
            var state=ReadData<MpCampaignControls>(row);GameVars.automanagefinances=state.AutoFinances;Policy.CurrentChapter=state.Chapter;
            GameVars.automanageconstructiong=state.AutoGovernment;GameVars.automanageconstructionp=state.AutoCompanies;GameVars.automanageconstructionr=state.AutoRailroads;GameVars.autoplacebuildings=state.AutoPlace;GameVars.automanageweapons=state.AutoWeapons;
            GameVars.usefow=state.UseFog;
        }
    }
}
