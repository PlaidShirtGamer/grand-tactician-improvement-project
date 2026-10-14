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
            "123_OOBSymbol", "124_HierarchySymbol", "470_OOB", "471_List", "123_UnitSymbol",
            "099_MenuButton", "159_ContinueGame", "210_ContinueGame", "097_CToolsButton", "098_ZoomInOut", "094_GameGuide",
            "860_Units", "899_Garrisons", "861_Fleets", "862_Map", "095_CHide", "096_CShow",
            "100_ToolsCommandControl", "101_ToolsMovementArrows", "102_ToolsAllTelegraphs", "103_ToolsMilitaryIcons",
            "104_ToolsMapIcons", "105_MapBorders", "106_MapText", "107_SupplyLines", "120_BattleMonuments"
        };
        public static void ReplicaBattleUi(BattleUI target)
        {
            if(instance==null || BlocksInput || CampaignEditor.BlocksInput || !NativeAccess.Get<bool>(target,"initiated"))return;
            var handler=GameObject.Find("UI")?.GetComponent<PanelHandler>();
            if(handler==null)return;
            RepairShortcutFocus();
            ModularUIFunctions.UpdateWindowOperations();
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
            if(Input.GetMouseButtonDown(0) && button!=null)instance.RequestTimeButton(button.name);
            bool allowed=button!=null && ReplicaNavigation.Contains(button.name);
            foreach(var p in handler.panel.Concat(handler.symbol).Concat(handler.multiplepanel))
                if(p.buttons!=null)foreach(var b in p.buttons)if(b!=null) {
                    if(b==button) { if(!b.mouseonobject || !b.mouseonobject2)b.ActivateMouseOnObject(); }
                    else if(b.mouseonobject || b.mouseonobject2)b.DeactivateMouseOnObject();
                }
            bool left=allowed && Input.GetMouseButtonDown(0), right=false, twice=false;
            handler.UpdatePanel(ref left,ref right,ref twice);
            target.UpdateUnitPanelCampaign();
            var selected=target.selectedunitreg;
            if(selected!=null && selected.alliance==instance.guestSide && instance.ids.ContainsKey(selected))instance.guestSelection=selected;
            if(EventSystem.current==null || EventSystem.current.currentSelectedGameObject==null)GameVars.deactivateshortcuts=false;
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
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaUiLate");return;}
            if(BlocksInput || target.panelhandler==null)return;
            var main=NativeAccess.Get<CampaignMainPanel>(target.panelhandler,"campaignmainpanel");
            if(main!=null && (main.contentselection==0 || main.contentselection==10 || main.contentselection==11))
                NativeAccess.Call(target,"GTIP_Original_ReplicaUiLate");
        }
        public static void RepairShortcutFocus()
        {
            if(instance==null || instance.transport==null || instance.phase!="live" || BlocksInput || CampaignEditor.BlocksInput)return;
            var selected=EventSystem.current==null?null:EventSystem.current.currentSelectedGameObject;
            var input=selected==null?null:selected.GetComponent<TMPro.TMP_InputField>();
            var legacy=selected==null?null:selected.GetComponent<UnityEngine.UI.InputField>();
            // Several native panels set this flag merely because their name field exists.
            // In multiplayer suppress shortcuts only while the user is actually typing.
            GameVars.deactivateshortcuts=(input!=null && input.isFocused)||(legacy!=null && legacy.isFocused);
        }
        void RequestTimeButton(string name)
        {
            if(name=="072_Pause"){SubmitGlobal("speed",0);return;}
            string[] names={"073_Play","074_2x","075_4x","076_10x"};
            int index=System.Array.IndexOf(names,name);
            if(index>=0 && index<GameVars.gamespeedcampaigns.Length)SubmitGlobal("speed",GameVars.gamespeedcampaigns[index]);
        }
        static bool PointerOverReplicaUi()
        {
            if(EventSystem.current==null)return false;
            var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            // Physics raycasters also return terrain and units. Those are the map, not blocking UI.
            return hits.Any(h=>h.module is UnityEngine.UI.GraphicRaycaster);
        }
        void RefreshReplicaRosterUi()
        {
            var main=GameObject.Find("UI/Panel_Main")?.GetComponent<CampaignMainPanel>();
            if(main!=null)main.UpdateOOBs(true,true);
        }
    }
}
