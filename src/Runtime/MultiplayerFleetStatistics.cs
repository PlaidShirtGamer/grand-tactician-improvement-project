namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        // Native naval battle UI forces this cache refresh through GetSideStats.
        // Only a validated vessel/hierarchy transaction may rebuild it on a guest;
        // that transaction reinstalls the received fleet and vessel scalars afterward.
        public static void ReplicaFleetStats(Fleet target,bool forceupdate)
        {
            if(!IsReplica || instance.applyingNaval)NativeAccess.Call(target,"GTIP_Original_ReplicaFleetStats",forceupdate);
        }
    }
}
