using System;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        static bool ReachesNativeUnitRoot(Transform node)
        {
            for(;node!=null;node=node.parent)if(node.name=="Units")return true;
            return false;
        }
        static void IsolateRecruitPreview(Regiment preview)
        {
            if(preview!=null && instance!=null && preview.transform.parent!=instance.transform)preview.transform.SetParent(instance.transform,true);
        }
        public static Regiment.UnitTooltipInfo ReplicaUnitTooltip(Regiment unit,bool hierarchyonly,bool onlyuptocampaigngroup,bool usebrackets,bool useenter)
        {
            var mp=instance;
            if(!IsReplica || mp==null || unit==null || unit!=mp.nativePreview || mp.previewPanel==null)
                return (Regiment.UnitTooltipInfo)NativeAccess.Call(unit,"GTIP_Original_ReplicaUnitTooltip",hierarchyonly,onlyuptocampaigngroup,usebrackets,useenter);
            var parent=NativeAccess.Get<Regiment>(mp.previewPanel,"unittorecruitfor");
            var nativeParent=parent!=null && ReachesNativeUnitRoot(parent.transform)?parent.transform:
                BattleUnits.unitfolder!=null?BattleUnits.unitfolder.transform:null;
            if(nativeParent==null || !ReachesNativeUnitRoot(nativeParent))
                throw new InvalidOperationException("Recruiting preview has no native Units ancestor.");
            var prior=unit.transform.parent;
            // Only this native getter needs the real ancestry. The two recruitment UI
            // callers request hierarchy-only data and return before aggregate calculations.
            // Always detach again before lifecycle, OOB or global roster scans can run.
            try {
                unit.transform.SetParent(nativeParent,true);
                return (Regiment.UnitTooltipInfo)NativeAccess.Call(unit,"GTIP_Original_ReplicaUnitTooltip",hierarchyonly,onlyuptocampaigngroup,usebrackets,useenter);
            }
            finally {unit.transform.SetParent(prior,true);}
        }
        static void WithRecruitPreviewCommander(CampaignMainPanel target,Regiment preview,GameVars.Commander officer,Action draw)
        {
            IsolateRecruitPreview(preview);
            var prior=officer.currentcommand;
            try {officer.currentcommand=preview;draw();}
            finally {officer.currentcommand=prior;}
        }
    }
}
