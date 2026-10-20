using System;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        int recruitPendingSequence;
        Regiment recruitPendingPreview;
        CampaignMainPanel recruitPendingPanel;
        string recruitNotice="";
        float recruitNoticeUntil;

        string RecruitNotice => recruitPendingSequence!=0?"Recruitment request sent. Waiting for the host...":Time.realtimeSinceStartup<recruitNoticeUntil?recruitNotice:"";
        bool BeginRecruitRequest(CampaignMainPanel target,Regiment preview,MpMessage request)
        {
            // Keep the local draft until its own receipt arrives. Repeated clicks may not
            // submit the same draft twice while the host is still creating the first unit.
            if(recruitPendingSequence!=0)return false;
            recruitPendingSequence=request.sequence;recruitPendingPreview=preview;recruitPendingPanel=target;
            recruitNotice="";recruitNoticeUntil=0;
            Send(request);
            return true;
        }
        void HandleRecruitResult(int sequence,string result)
        {
            if(sequence!=recruitPendingSequence || sequence==0)return;
            var preview=recruitPendingPreview;var target=recruitPendingPanel;
            recruitPendingSequence=0;recruitPendingPreview=null;recruitPendingPanel=null;
            bool rejected=result!=null && result.StartsWith("Order rejected:",StringComparison.Ordinal);
            if(rejected){recruitNotice=result;recruitNoticeUntil=Time.realtimeSinceStartup+12;}
            if(target==null || target!=previewPanel || preview==null || preview!=nativePreview)return;
            try {
                // Re-evaluate native eligibility against the latest replicated pools and
                // credit rating. The draft, chosen state, colors and uniform remain intact.
                if(rejected)RestoreRecruitPreviewPanel(target,preview);
                else ReplicaRecruitCancel(target,true);
            }
            catch(Exception error) {LogDiagnostic("Recruitment receipt UI refresh deferred: "+error);}
        }
        void ClearRecruitRequest()
        {
            recruitPendingSequence=0;recruitPendingPreview=null;recruitPendingPanel=null;
            recruitNotice="";recruitNoticeUntil=0;
        }
    }
}
