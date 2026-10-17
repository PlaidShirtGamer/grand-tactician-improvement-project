namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static void ReplicaGroupStats(Regiment target,bool overridecalc,bool ignoreactivestate,Regiment ignoreactivestateonlyforthisunit)
        {
            // Native tooltips also call this method. It advances subordinate morale,
            // condition and campaign readiness, so it is not a presentation refresh.
            // Replicas already receive its aggregate outputs from the host.
            // Recruitment previews run inside the existing save/restore transaction.
            if(IsReplica && !IsRecruitPreview(target))return;
            NativeAccess.Call(target,"GTIP_Original_ReplicaGroupStats",overridecalc,ignoreactivestate,ignoreactivestateonlyforthisunit);
        }
    }
}
