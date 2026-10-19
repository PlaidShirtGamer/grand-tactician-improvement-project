namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        bool GuestRecruitPreviewAction(ButtonTooltip button)
        {
            if(!IsReplica || !NativeRecruitOpen || nativePreview==null || CampaignMainPanel.dummyregiment!=nativePreview ||
                button==null || !button.isActiveAndEnabled || button.state==2 || !button.transform.IsChildOf(previewPanel.transform))return false;
            // These three native controls edit only the uncommitted dummy. Dispatch them
            // directly rather than relying on the legacy panel's cached hover membership.
            // Consume the click so a registered native button cannot apply it a second time.
            switch(button.name) {
                case "476_PrevSoldier":nativePreview.AdjustUniformSet(-1);break;
                case "477_NextSoldier":nativePreview.AdjustUniformSet(1);break;
                case "475_SwitchRecruitType":nativePreview.recruitingtype=nativePreview.recruitingtype==0?1:0;break;
                default:return false;
            }
            // Native uniform selection retains state/policy eligibility. This refresh also
            // checks the chosen recruiting pool and disables Recruit when it is insufficient.
            previewPanel.UpdateUnitInfoPanel();
            LogDiagnostic("Guest recruit preview control: "+button.name+", uniform="+nativePreview.uniformset+", recruits="+nativePreview.recruitingtype);
            return true;
        }
        bool RefreshRecruitPreviewRosterUi(CampaignMainPanel target)
        {
            if(!IsReplica || !NativeRecruitOpen || target!=previewPanel)return false;
            // UpdateOOBs(true,true) reinitializes RecruitingList through its filters,
            // clearing chosenstate and interrupting an in-progress guest recruitment.
            NativeAccess.Get<RecruitingList>(target,"recruitinglist")?.UpdateList();
            target.UpdateUnitData();
            return true;
        }
    }
}
