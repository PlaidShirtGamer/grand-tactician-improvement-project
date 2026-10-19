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
            "108_MessageLog","109_Hide","113_Show", "382_PrevArmy", "383_NextArmy", "382_PrevArmy2", "383_NextArmy2",
            "390_Unit", "391_Ship", "392_Commander", "393_History",
            "123_OOBSymbol", "124_HierarchySymbol", "470_OOB", "471_List", "123_UnitSymbol",
            "099_MenuButton", "159_ContinueGame", "210_ContinueGame", "097_CToolsButton", "098_ZoomInOut", "094_GameGuide",
            "860_Units", "899_Garrisons", "861_Fleets", "862_Map", "095_CHide", "096_CShow",
            "100_ToolsCommandControl", "101_ToolsMovementArrows", "102_ToolsAllTelegraphs", "103_ToolsMilitaryIcons",
            "104_ToolsMapIcons", "105_MapBorders", "106_MapText", "107_SupplyLines", "120_BattleMonuments",
            "034_OptionsC","034_OptionsB","211_OptionsB","297_Accept","298_Cancel","176_OOBSymbolP",
            "362_EconomyReport","363_IntelligenceReport"
            ,"385_Replace","487_ScrollLeft","488_ScrollRight","084_Commander","490_Assign","491_Cancel","910_ScrollUp","911_ScrollDn",
            "755_RingBackground","881_List","885_AveragePrice","886_Demand","887_Production","888_InStock",
            "187_RemoveDispatches","250_RemoveDispatches","188_MarkAllAsRead","251_MarkAllAsRead",
            "493_Armies","494_Fleets","495_Commanders","496_Weapons","497_Logistics",
            "499_SelectedFleet","500_AllFleets","382_PrevFleet","383_NextFleet","604_MANAGEMENT","653_ADMINISTRATION",
            "600_STANCE","601_CAVALRY","602_CONSTRUCT","603_MOVEMENT","650_STANCE","651_FLEET","652_MOVEMENT",
            "890_FederalBuildings","891_CompanyApplications","892_Railroads","630_FORT","631_DEPOT","632_TELEGRAPH",
            "090_ChangeWeapon","485_Cancel","484_Upgrade","384_Promote","757_Upgrade",
            "505_CreateShip","505_CreateShipPool","507_Cancel","927_IncBuilt","928_DecBuilt","502_ShipImage","502_ImageLarge","506_Build",
            "512_NewFleet","511_Cancel","510_Build","513_DeleteShip",
            "411_C_PlayBattle","412_C_Autoresolve","413_C_Defend","414_C_Withdraw","415_C_Retreat","416_C_GoToLocation"
            ,"000_C_MOVEMENT-TIMED","008_ArrowUpLeftCTM","008_ArrowDnLeftCTM","008_ArrowUpRightCTM","008_ArrowDnRightCTM","410_CancelCTM"
        };
        ButtonTooltip replicaHover;
        public static void ReplicaBattleUi(BattleUI target)
        {
            if(instance==null || BlocksInput || CampaignEditor.BlocksInput || instance.GuestDialogOpen || !NativeAccess.Get<bool>(target,"initiated"))return;
            var handler=GameObject.Find("UI")?.GetComponent<PanelHandler>();
            if(handler==null)return;
            RepairShortcutFocus();
            ModularUIFunctions.UpdateWindowOperations();
            // BattleUI.Update normally owns these real-time presentation timers.
            // They must run even while the authoritative campaign clock is paused.
            if(RollingText.rollingtext!=null) {RollingText.UpdatePositionsAndExistance();RollingText.UpdateDatabase();}
            var hits=new List<RaycastResult>();
            if(EventSystem.current!=null)EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            // Most replica panels deliberately have their Unity click module disabled.
            // Deliver scrolling separately so their native ScrollRects still work.
            if(EventSystem.current!=null && Input.mouseScrollDelta!=Vector2.zero && !instance.blockedModules.Any(m=>m!=null && m.enabled)) {
                var scrollHit=hits.FirstOrDefault(h=>h.module is UnityEngine.UI.GraphicRaycaster);
                if(scrollHit.gameObject!=null)ExecuteEvents.ExecuteHierarchy(scrollHit.gameObject,
                    new PointerEventData(EventSystem.current){position=Input.mousePosition,scrollDelta=Input.mouseScrollDelta},ExecuteEvents.scrollHandler);
            }
            // Raycasting doesn't require an enabled input module. Keep automatic Unity click
            // callbacks disabled: many of those callbacks execute unreplicated mutations.
            var button=HitReplicaButton(hits);
            // Dynamically created project/policy buttons are not necessarily in panel.buttons.
            if(instance.replicaHover!=button) {
                if(instance.replicaHover!=null)instance.replicaHover.DeactivateMouseOnObject();
                instance.replicaHover=button;
            }
            if(button!=null && (!button.mouseonobject || !button.mouseonobject2))button.ActivateMouseOnObject();
            bool clicked=Input.GetMouseButtonDown(0);
            if(clicked) {
                var field=hits.Select(h=>h.gameObject.GetComponentInParent<TMPro.TMP_InputField>()).FirstOrDefault(f=>f!=null && f.interactable);
                if(field!=null){
                    // The replica's input module is normally disabled. Enable it before
                    // activation so TMP receives keyboard/focus events on the first click.
                    foreach(var module in instance.blockedModules)if(module!=null)module.enabled=true;
                    field.Select();field.ActivateInputField();
                } else if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null &&
                    EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>()!=null)EventSystem.current.SetSelectedGameObject(null);
            }
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
            bool movementAction=clicked && instance.GuestMovementAction(button);
            bool recruitAction=clicked && instance.GuestRecruitPreviewAction(button);
            if(clicked && button!=null && button.state!=2 && !movementAction)instance.GuestUnitAction(button.name);
            bool logEntry=button!=null && button.name.Length>=14 && button.name.Substring(3,9)=="_LogEntry";
            bool recruitment=button!=null && instance.NativeRecruitOpen && (button.name.StartsWith("474_RecruitingRow") || button.name.StartsWith("480_Color") ||
                new[]{"475_SwitchRecruitType","476_PrevSoldier","477_NextSoldier","478_Left","479_Right","590_RecruitInfantry","591_RecruitCavalry","592_RecruitArtillery","593_RecruitHorseArtillery","481_Recruit","482_Cancel","514_Cancel","575_InitialBrigadeSize","947_RecolorUniform"}.Contains(button.name));
            bool alert=button!=null && int.TryParse(button.name.Substring(0,Mathf.Min(3,button.name.Length)),out int alarm) && alarm>=841 && alarm<=851;
            bool nativeList=button!=null && (button.name.StartsWith("486_WeaponList") || button.name.StartsWith("508_BuildLine") || button.name.StartsWith("509_BuildLine"));
            bool allowed=button!=null && (ReplicaNavigation.Contains(button.name) || nativeList || recruitment || alert || logEntry || button.GetComponentInParent<Options>()!=null);
            foreach(var p in handler.panel.Concat(handler.symbol).Concat(handler.multiplepanel))
                if(p.buttons!=null)foreach(var b in p.buttons)if(b!=null) {
                    if(b==button && b.isActiveAndEnabled) { if(!b.mouseonobject || !b.mouseonobject2)b.ActivateMouseOnObject(); }
                    else if(b.mouseonobject || b.mouseonobject2)b.DeactivateMouseOnObject();
                }
            bool left=allowed && !movementAction && !recruitAction && button.name!="108_MessageLog" && Input.GetMouseButtonDown(0), right=false, twice=false;
            handler.UpdatePanel(ref left,ref right,ref twice);
            if(clicked)instance.ReplicaLocalClose(hits,handler);
            instance.OptionsInput(handler);
            instance.DriveGuestNativeViews();
            instance.GuestEngagementChecks();
            target.UpdateUnitPanelCampaign(forceotherpanelopen:handler.panel[handler.cpanel_mainpanel].state>0);
            instance.RefreshGuestBottomPanel(target,handler);
            var selected=target.selectedunitreg;
            if(selected!=null && selected.alliance==instance.guestSide && instance.ids.ContainsKey(selected))instance.guestSelection=selected;
            if(EventSystem.current==null || EventSystem.current.currentSelectedGameObject==null)GameVars.deactivateshortcuts=false;
        }
        void ReplicaLocalClose(List<RaycastResult> hits,PanelHandler handler)
        {
            var tooltip=HitReplicaButton(hits);
            var log=handler.panel[handler.panel_messagelog];
            if(tooltip!=null && log.state>0 && tooltip.transform.IsChildOf(log.go.transform) &&
                (tooltip.name=="018_ClosePanel" || tooltip.name=="400_ClosePanel")) {
                handler.HidePanel(log);handler.SetButtonsCondition(handler.panel[handler.panel_messagesshown],0,108);return;
            }
            if(tooltip!=null && tooltip.CloseThisPanelIfPressed!=null) {
                ModularUIFunctions.OpenClosePanelStatic(tooltip.CloseThisPanelIfPressed,false,true);return;
            }
            // Native X buttons use Unity onClick. Invoke only known local close actions.
            var control=hits.Select(h=>h.gameObject.GetComponentInParent<UnityEngine.UI.Button>()).FirstOrDefault(b=>b!=null);
            if(control==null)return;
            for(int i=0;i<control.onClick.GetPersistentEventCount();i++) {
                var receiver=control.onClick.GetPersistentTarget(i);
                string method=control.onClick.GetPersistentMethodName(i);
                if(receiver is Newspaper paper && method=="Close"){paper.Close();return;}
                if((method=="ClosePanel" || method=="OpenClosePanel") && receiver is ModularUIFunctions) {
                    var containing=handler.panel.FirstOrDefault(p=>p.go!=null && control.transform.IsChildOf(p.go.transform) && p.state>0);
                    if(containing!=null && containing.go.GetComponent<Options>()==null) {handler.HidePanel(containing);return;}
                }
                if(receiver is Messages messages && method=="CloseMessage"){messages.CloseMessage();return;}
            }
        }
        void OptionsInput(PanelHandler handler)
        {
            // Unity objects can be destroyed while their managed reference remains non-null.
            var events=EventSystem.current;
            var focused=events!=null?events.currentSelectedGameObject:null;
            var selected=focused!=null?focused.GetComponent<TMPro.TMP_InputField>():null;
            bool open=handler.panel[handler.cpanel_optionspanel].state>0 || handler.panel[handler.cpanel_finances].state>0 || NativeRecruitOpen || selected!=null;
            // Local Options needs Unity's sliders, input fields and drop-downs.
            // Gameplay dispatch remains filtered while this modal is open.
            foreach(var module in blockedModules)if(module!=null)module.enabled=open;
        }
        static ButtonTooltip ReplicaHitButton()
        {
            if(EventSystem.current==null)return null;
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
            return HitReplicaButton(hits);
        }
        static ButtonTooltip HitReplicaButton(List<RaycastResult> hits) => hits
            .Where(h=>h.module is UnityEngine.UI.GraphicRaycaster)
            .Select(h=>h.gameObject.GetComponentInParent<ButtonTooltip>()).FirstOrDefault(b=>b!=null && b.isActiveAndEnabled);
        void OpenReplicaTab(PanelHandler h,string name)
        {
            if(name=="108_MessageLog") {
                var log=h.panel[h.panel_messagelog];
                bool showLog=log.state<=0 || log.state==3;
                if(showLog){h.ShowPanel(h.panel_messagelog);NativeAccess.Get<MessageLog>(h,"messagelog").ResetEntries();}
                else h.HidePanel(log);
                h.SetButtonsCondition(h.panel[h.panel_messagesshown],showLog?1:0,108);return;
            }
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
            if(main!=null)
                NativeAccess.Call(target,"GTIP_Original_ReplicaUiLate");
            instance.RefreshGuestBattleControls();
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
            return hits.Any(h=>h.module is UnityEngine.UI.GraphicRaycaster &&
                (h.gameObject.GetComponentInParent<ButtonTooltip>()==null || h.gameObject.GetComponentInParent<ButtonTooltip>().isActiveAndEnabled));
        }
        internal static void ReplicaCamera(Camera_Battlefield target)
        {
            bool oldZoom=target.preventmanualzoom,oldPointer=BattleUI.pointeroverui;
            bool block=instance!=null && (PointerOverReplicaUi() || instance.NativeModalOpen());
            try {
                target.preventmanualzoom=oldZoom || block;BattleUI.pointeroverui=block;
                if(block) {NativeAccess.Set(target,"wheely",0f);NativeAccess.Set(target,"mspeedy",0f);}
                NativeAccess.Call(target,"GTIP_Original_EditorCamera");
            } finally {target.preventmanualzoom=oldZoom;BattleUI.pointeroverui=oldPointer;}
        }
        bool NativeModalOpen()
        {
            var h=ui?.panelhandler;if(h==null)return false;
            return new[]{h.cpanel_mainpanel,h.cpanel_finances,h.cpanel_production,h.cpanel_trade,h.cpanel_strategy,h.cpanel_policies,h.cpanel_projects,h.cpanel_newspaper,h.cpanel_optionspanel,h.cpanel_battle}
                .Any(i=>i>=0 && i<h.panel.Count && h.panel[i].state>0);
        }
        void RefreshReplicaRosterUi()
        {
            var main=GameObject.Find("UI/Panel_Main")?.GetComponent<CampaignMainPanel>();
            if(main!=null && !RefreshRecruitPreviewRosterUi(main))main.UpdateOOBs(true,true);
        }
    }
}
