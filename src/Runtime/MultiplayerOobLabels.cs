using System;
using System.Collections;
using TMPro;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        static string ReplicaOobLabel(Regiment unit)
        {
            int prior=unit.groupunittype;
            // Reuse native abbreviation, commander and detached-artillery label rules.
            // The symbol getter also recomputes this campaign cache; the host owns it.
            try {return unit.GetUnitSymbolData().unitname;}
            finally {unit.groupunittype=prior;}
        }

        void RefreshReplicaOobLabels()
        {
            var handler=ui?.panelhandler;
            if(handler==null || handler.panel[handler.cpanel_mainpanel].state<=0)return;
            var main=NativeAccess.Get<CampaignMainPanel>(handler,"campaignmainpanel");
            if(main?.oobs==null)return;
            foreach(var view in main.oobs) {
                if(view==null || !view.isActiveAndEnabled || !view.initialized || view.panelhandler==null)continue;
                try {
                    var registered=NativeAccess.Get<IList>(view,"unitsregistered");
                    if(registered==null)continue;
                    foreach(var entry in registered) {
                        if(entry==null)continue;
                        var unit=NativeAccess.Get<Regiment>(entry,"reg");
                        if(unit==null || !ids.ContainsKey(unit) || unit.commander<0 || unit.commander>=GameVars.commander.Count)continue;
                        int index=NativeAccess.Get<int>(entry,"multiplepanelid");
                        var panels=view.panelhandler.multiplepanel;
                        if(index<0 || index>=panels.Count)continue;
                        var panel=panels[index];
                        // Panel slots can be reused after a structural refresh. Never
                        // write through a stale registration or expose native blank icons.
                        if(panel==null || panel.go==null || panel.secondaryreference!=unit.gameObject || panel.go.name=="OOBInvisible")continue;
                        var label=panel.go.transform.Find("Background/UnitName")?.GetComponent<TextMeshProUGUI>();
                        if(label==null)continue;
                        string text=ReplicaOobLabel(unit);
                        if(label.text!=text)label.text=text;
                    }
                }
                catch(Exception error) {LogDiagnostic("Replica OOB label refresh failed: "+error);}
            }
        }
    }
}
