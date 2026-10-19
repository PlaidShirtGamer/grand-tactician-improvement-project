using System;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        bool rosterUiPending;
        void RefreshReplicaPresentation(MpStateRow[] rows)
        {
            if(!rosterUiPending)return;
            rosterUiPending=false;
            MpPresentationFence.Refresh(RefreshReplicaRosterUi,()=> {
                foreach(var row in rows)if(row.Kind<=3 && !row.Deleted) {
                    var target=DataTarget(row);DataSchemas[row.Kind].Apply(target,row.Values);
                    if(row.Kind==2) {
                        var c=(GameVars.Commander)target;
                        c.currentcommand=row.Current==0?null:units[row.Current];c.lastcommand=row.Last==0?null:units[row.Last];
                    }
                    if(row.Kind==3)((Regiment)target).name=row.Name;
                }
            },error=> {
                orderStatus="The roster panel could not refresh. Reopen it; campaign synchronization continues.";
                LogDiagnostic("Replica roster presentation failed after state application; authoritative scalar restoration and verification remain enabled: "+error);
            });
        }
    }
}
