namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        // RecruitingList refreshes this cached campaign statistic while drawing.
        // The host owns the total; a guest refresh must not replace a replicated
        // value with a local hierarchy/preview count.
        public static void ReplicaNationStats(GameVars.Nation target,int stateid)
        {
            if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaNationStats",stateid);
        }
        public static void ReplicaNationRecruitData(GameVars.Nation target)
        {
            if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaNationRecruitData");
        }
    }
}
