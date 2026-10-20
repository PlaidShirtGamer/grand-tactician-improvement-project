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
            // This native unit tooltip needs real ancestry. The two recruitment UI
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
            var parent=NativeAccess.Get<Regiment>(target,"unittorecruitfor");
            var nativeParent=parent!=null && ReachesNativeUnitRoot(parent.transform)?parent.transform:
                BattleUnits.unitfolder!=null?BattleUnits.unitfolder.transform:null;
            if(nativeParent==null || !ReachesNativeUnitRoot(nativeParent))
                throw new InvalidOperationException("Recruiting commander preview has no native Units ancestor.");
            var priorParent=preview.transform.parent;
            var prior=officer.currentcommand;
            var battleUi=NativeAccess.Get<BattleUI>(target,"bui");
            var priorSelection=battleUi==null?null:battleUi.selectedunitreg;
            IsolateRecruitPreview(preview);
            // Commander tooltips/attributes also call UnitIsInsideRadius through the
            // parent commander's influence calculation. That getter requires a Regiment
            // parent (or the literal Units root), so tooltip-only parenting is insufficient.
            // A null selection makes GetUsedUnit select a fallback formation before it
            // returns the dummy, and SelectUnit scans descendants. Supply only the read
            // reference so that fallback cannot collect the temporarily attached dummy.
            // Do not call SelectUnit or alter its selection lists in this scope.
            try {
                if(battleUi!=null && priorSelection==null)battleUi.selectedunitreg=preview;
                preview.transform.SetParent(nativeParent,true);officer.currentcommand=preview;draw();
            }
            finally {
                officer.currentcommand=prior;preview.transform.SetParent(priorParent,true);
                if(battleUi!=null)battleUi.selectedunitreg=priorSelection;
            }
        }
        static void RestoreRecruitPreviewPanel(CampaignMainPanel target,Regiment preview)
        {
            if(preview==null)return;
            int commander=preview.commander;
            if(commander<0 || commander>=GameVars.commander.Count)return;
            NativeAccess.Set(target,"currentcommanderchosen",commander);
            // SwitchCommanderPanel(0) changes to the commander page if the chosen
            // officer has no current command. Preview transactions intentionally restore
            // that authoritative reference, so supply it only for this native redraw.
            // Re-run the complete pane switch after state selection; UpdateUnitData alone
            // cannot hide an already-open commander attributes/history pane.
            WithRecruitPreviewCommander(target,preview,GameVars.commander[commander],()=>target.SwitchCommanderPanel(0));
        }
    }
}
