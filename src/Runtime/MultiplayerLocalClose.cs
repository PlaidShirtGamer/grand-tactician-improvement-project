using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        void ReplicaLocalClose(List<RaycastResult> hits,PanelHandler handler)
        {
            // A foreground graphic consumes this click even when it has no close
            // component. Searching later hits can dismiss a newspaper behind a menu.
            var front=hits.FirstOrDefault(h=>h.module is UnityEngine.UI.GraphicRaycaster).gameObject;
            if(front==null)return;
            var tooltip=front.GetComponentInParent<ButtonTooltip>();
            if(tooltip!=null && !tooltip.isActiveAndEnabled)tooltip=null;
            var log=handler.panel[handler.panel_messagelog];
            if(tooltip!=null && log.state>0 && tooltip.transform.IsChildOf(log.go.transform) &&
                (tooltip.name=="018_ClosePanel" || tooltip.name=="400_ClosePanel")) {
                handler.HidePanel(log);handler.SetButtonsCondition(handler.panel[handler.panel_messagesshown],0,108);return;
            }
            if(tooltip!=null && tooltip.CloseThisPanelIfPressed!=null) {
                if(handler.cpanel_newspaper>=0 && handler.cpanel_newspaper<handler.panel.Count &&
                    tooltip.CloseThisPanelIfPressed==handler.panel[handler.cpanel_newspaper].go &&
                    !tooltip.transform.IsChildOf(tooltip.CloseThisPanelIfPressed.transform))return;
                if(GuestNewspaperCloseIntent(handler,tooltip.CloseThisPanelIfPressed))return;
                ModularUIFunctions.OpenClosePanelStatic(tooltip.CloseThisPanelIfPressed,false,true);return;
            }
            // Native X buttons use Unity onClick. Invoke only known local close actions.
            var control=front.GetComponentInParent<UnityEngine.UI.Button>();
            if(control==null || !control.isActiveAndEnabled || !control.interactable)return;
            for(int i=0;i<control.onClick.GetPersistentEventCount();i++) {
                var receiver=control.onClick.GetPersistentTarget(i);
                string method=control.onClick.GetPersistentMethodName(i);
                if(receiver is Newspaper paper && method=="Close") {
                    if(control.transform.IsChildOf(paper.transform))GuestNewspaperCloseIntent(handler,paper.gameObject);
                    return;
                }
                if((method=="ClosePanel" || method=="OpenClosePanel") && receiver is ModularUIFunctions) {
                    var containing=handler.panel.FirstOrDefault(p=>p.go!=null && control.transform.IsChildOf(p.go.transform) && p.state>0);
                    if(containing!=null && containing.go.GetComponent<Options>()==null) {
                        if(!GuestNewspaperCloseIntent(handler,containing.go))handler.HidePanel(containing);
                        return;
                    }
                }
                if(receiver is Messages messages && method=="CloseMessage"){messages.CloseMessage();return;}
            }
        }
    }
}
