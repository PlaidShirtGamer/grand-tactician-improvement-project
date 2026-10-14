using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        // Explicit viewing operations. Native recruitment, spending and hierarchy edits must
        // continue through host commands, never through the replica's native click dispatcher.
        static readonly HashSet<string> ReplicaNavigation = new HashSet<string> {
            "108_MessageLog", "382_PrevArmy", "383_NextArmy", "382_PrevArmy2", "383_NextArmy2",
            "390_Unit", "391_Ship", "392_Commander", "393_History",
            "123_OOBSymbol", "124_HierarchySymbol", "470_OOB", "471_List", "123_UnitSymbol"
        };
        public static void ReplicaBattleUi(BattleUI target)
        {
            if(instance==null || BlocksInput || CampaignEditor.BlocksInput || !NativeAccess.Get<bool>(target,"initiated"))return;
            var handler=GameObject.Find("UI")?.GetComponent<PanelHandler>();
            if(handler==null)return;
            var hits=new List<RaycastResult>();
            if(EventSystem.current!=null)EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            // Raycasting doesn't require an enabled input module. Keep automatic Unity click
            // callbacks disabled: many of those callbacks execute unreplicated mutations.
            var button=hits.Select(h=>h.gameObject.GetComponentInParent<ButtonTooltip>()).FirstOrDefault(b=>b!=null);
            if(Input.GetMouseButtonDown(0) && button!=null)instance.OpenReplicaTab(handler,button.name);
            if(Input.GetMouseButtonDown(0) && button!=null && button.name.Length>=10 && button.name.Substring(3,7)=="_Symbol") {
                var p=handler.multiplepanel.FirstOrDefault(x=>x.buttons!=null && x.buttons.Contains(button));
                var messages=Dispatches;
                if(p!=null && messages!=null) {
                    int index=messages.message.FindIndex(m=>m.number==p.uniqueid);
                    if(index>=0)messages.OpenMessage(index);
                }
            }
            bool allowed=button!=null && ReplicaNavigation.Contains(button.name);
            foreach(var p in handler.panel.Concat(handler.symbol).Concat(handler.multiplepanel))
                if(p.buttons!=null)foreach(var b in p.buttons)if(b!=null) {
                    b.mouseonobject=allowed && b==button;
                    b.mouseonobject2=false;
                }
            bool left=allowed && Input.GetMouseButtonDown(0), right=false, twice=false;
            handler.UpdatePanel(ref left,ref right,ref twice);
            handler.ClearDragAndDrop();
            target.UpdateUnitPanelCampaign();
        }
        void OpenReplicaTab(PanelHandler h,string name)
        {
            int selected=-1;
            switch(name) {
                case "371_Military":selected=h.cpanel_mainpanel;break;
                case "372_Economy":selected=h.cpanel_finances;break;
                case "373_Production":selected=h.cpanel_production;break;
                case "375_Trade":selected=h.cpanel_trade;break;
                case "370_Strategy":selected=h.cpanel_strategy;break;
                case "374_Policies":selected=h.cpanel_policies;break;
                case "376_Projects":selected=h.cpanel_projects;break;
            }
            if(selected<0)return;
            bool open=h.panel[selected].state<=0;
            foreach(int id in new[]{h.cpanel_mainpanel,h.cpanel_finances,h.cpanel_production,h.cpanel_trade,h.cpanel_strategy,h.cpanel_policies,h.cpanel_projects})
                h.HidePanel(h.panel[id]);
            if(open) {
                h.ShowPanel(selected);
                if(selected==h.cpanel_mainpanel) {
                    var main=NativeAccess.Get<CampaignMainPanel>(h,"campaignmainpanel");
                    main.SetTab(0);main.UpdateOOBs(true,true);
                }
            }
        }
        public static void ReplicaUiLate(BattleUI target)
        { if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaUiLate"); }
        static bool PointerOverReplicaUi()
        {
            if(EventSystem.current==null)return false;
            var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            return hits.Count>0;
        }
        void RefreshReplicaRosterUi()
        {
            var main=GameObject.Find("UI/Panel_Main")?.GetComponent<CampaignMainPanel>();
            if(main!=null)main.UpdateOOBs(true,true);
        }
    }
}
