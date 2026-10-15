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
            "104_ToolsMapIcons", "105_MapBorders", "106_MapText", "107_SupplyLines", "120_BattleMonuments",
            "034_OptionsC","034_OptionsB","211_OptionsB","297_Accept","298_Cancel","176_OOBSymbolP",
            "362_EconomyReport","363_IntelligenceReport"
            ,"385_Replace","487_ScrollLeft","488_ScrollRight","084_Commander","490_Assign","491_Cancel","910_ScrollUp","911_ScrollDn"
        };
        ButtonTooltip replicaHover;
        public static void ReplicaBattleUi(BattleUI target)
        {
            if(instance==null || BlocksInput || CampaignEditor.BlocksInput || instance.GuestDialogOpen || !NativeAccess.Get<bool>(target,"initiated"))return;
            var handler=GameObject.Find("UI")?.GetComponent<PanelHandler>();
            if(handler==null)return;
            RepairShortcutFocus();
            ModularUIFunctions.UpdateWindowOperations();
            var hits=new List<RaycastResult>();
            if(EventSystem.current!=null)EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            // Raycasting doesn't require an enabled input module. Keep automatic Unity click
            // callbacks disabled: many of those callbacks execute unreplicated mutations.
            var button=hits.Select(h=>h.gameObject.GetComponentInParent<ButtonTooltip>()).FirstOrDefault(b=>b!=null);
            // Dynamically created project/policy buttons are not necessarily in panel.buttons.
            if(instance.replicaHover!=button) {
                if(instance.replicaHover!=null)instance.replicaHover.DeactivateMouseOnObject();
                instance.replicaHover=button;
            }
            if(button!=null && (!button.mouseonobject || !button.mouseonobject2))button.ActivateMouseOnObject();
            bool clicked=Input.GetMouseButtonDown(0);
            if(clicked && button!=null) {
                if(button.name=="169_SaveGame" || button.name=="213_SaveGameB" || button.name=="160_Quit" || button.name=="212_Quit")instance.guestFileMenu=true;
            }
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
            bool logEntry=button!=null && button.name.Length>=14 && button.name.Substring(3,9)=="_LogEntry";
            bool recruitment=button!=null && instance.NativeRecruitOpen && (button.name.StartsWith("474_RecruitingRow") || button.name.StartsWith("480_Color") ||
                new[]{"475_SwitchRecruitType","476_PrevSoldier","477_NextSoldier","478_Left","479_Right","590_RecruitInfantry","591_RecruitCavalry","592_RecruitArtillery","593_RecruitHorseArtillery","481_Recruit","482_Cancel","514_Cancel","575_InitialBrigadeSize","947_RecolorUniform"}.Contains(button.name));
            bool alert=button!=null && int.TryParse(button.name.Substring(0,Mathf.Min(3,button.name.Length)),out int alarm) && alarm>=841 && alarm<=851;
            bool allowed=button!=null && (ReplicaNavigation.Contains(button.name) || recruitment || alert || logEntry || button.GetComponentInParent<Options>()!=null);
            foreach(var p in handler.panel.Concat(handler.symbol).Concat(handler.multiplepanel))
                if(p.buttons!=null)foreach(var b in p.buttons)if(b!=null) {
                    if(b==button) { if(!b.mouseonobject || !b.mouseonobject2)b.ActivateMouseOnObject(); }
                    else if(b.mouseonobject || b.mouseonobject2)b.DeactivateMouseOnObject();
                }
            bool left=allowed && Input.GetMouseButtonDown(0), right=false, twice=false;
            handler.UpdatePanel(ref left,ref right,ref twice);
            if(clicked)instance.ReplicaLocalClose(hits,handler);
            instance.OptionsInput(handler);
            target.UpdateUnitPanelCampaign();
            var selected=target.selectedunitreg;
            if(selected!=null && selected.alliance==instance.guestSide && instance.ids.ContainsKey(selected))instance.guestSelection=selected;
            if(EventSystem.current==null || EventSystem.current.currentSelectedGameObject==null)GameVars.deactivateshortcuts=false;
        }
        void ReplicaLocalClose(List<RaycastResult> hits,PanelHandler handler)
        {
            // Native X buttons use Unity onClick. Invoke only known local close actions.
            var control=hits.Select(h=>h.gameObject.GetComponentInParent<UnityEngine.UI.Button>()).FirstOrDefault(b=>b!=null);
            if(control==null)return;
            for(int i=0;i<control.onClick.GetPersistentEventCount();i++) {
                var receiver=control.onClick.GetPersistentTarget(i);
                string method=control.onClick.GetPersistentMethodName(i);
                if(receiver is Newspaper paper && method=="Close"){paper.Close();return;}
                if(method=="ClosePanel" && receiver is ModularUIFunctions) {
                    var containing=handler.panel.FirstOrDefault(p=>p.go!=null && control.transform.IsChildOf(p.go.transform) && p.state>0);
                    if(containing!=null && containing.go.GetComponent<Options>()==null) {handler.HidePanel(containing);return;}
                }
                if(receiver is Messages messages && method=="CloseMessage"){messages.CloseMessage();return;}
            }
        }
        void OptionsInput(PanelHandler handler)
        {
            bool open=handler.panel[handler.cpanel_optionspanel].state>0 || handler.panel[handler.cpanel_finances].state>0 || NativeRecruitOpen;
            // Local Options needs Unity's sliders, input fields and drop-downs.
            // Gameplay dispatch remains filtered while this modal is open.
            foreach(var module in blockedModules)if(module!=null)module.enabled=open;
        }
        static ButtonTooltip ReplicaHitButton()
        {
            if(EventSystem.current==null)return null;
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            return hits.Select(h=>h.gameObject.GetComponentInParent<ButtonTooltip>()).FirstOrDefault(b=>b!=null);
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
            if(instance.GuestDialogOpen){GameVars.deactivateshortcuts=true;return;}
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
