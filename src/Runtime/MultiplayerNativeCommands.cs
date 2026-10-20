using System;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        Regiment bottomUnit;
        int bottomState;
        bool remoteIntent;
        readonly System.Collections.Generic.Dictionary<int,int> pendingPromotions=new System.Collections.Generic.Dictionary<int,int>();
        bool officerViewDirty;
        void RefreshGuestBottomPanel(BattleUI target,PanelHandler h)
        {
            if(h.panel[h.cpanel_mainpanel].state<=0) {
                var u=target.selectedunitreg;
                int state=u==null?0:1+u.campaignstanceordered*2+u.cavalryordersordered*8+u.fleetorders*32+(u.inbattle?256:0)+(u.onretreat?512:0)+
                    u.campaignstance*1024+u.cavalryorders*4096+CampaignArmyPanel.GetReadinessStep(u)*16384+(u.doublequick?131072:0);
                if(u!=bottomUnit || state!=bottomState) {target.UpdateCampaignBottomPanel();bottomUnit=u;bottomState=state;}
            }
            NativeAccess.Call(target,"CheckConstructionPanel");
        }
        void RequestNative(string action,int target=0,float value=0,string detail="")
        {
            if(host || !authenticated || phase!="live")return;
            Send(new MpMessage{kind="native-action",session=session,sequence=++guestSequence,unit=target,x=value,text=action+"|"+detail});
            orderStatus="Request "+guestSequence+" sent: "+action;
        }
        void GuestUnitAction(string name)
        {
            var u=ui.selectedunitreg;if(u==null || !ids.TryGetValue(u,out int id))return;
            switch(name) {
                case "610_DEFENSIVE":case "660_DEFENSIVE":RequestNative("campaign-order",id,101);break;
                case "611_OFFENSIVE":case "661_OFFENSIVE":RequestNative("campaign-order",id,100);break;
                case "620_SCOUT":RequestNative("campaign-order",id,u.cavalryordersordered==1?120:121);break;
                case "621_RAID":RequestNative("campaign-order",id,u.cavalryordersordered==2?120:122);break;
                case "622_GUARD":RequestNative("campaign-order",id,u.cavalryordersordered==3?120:123);break;
                case "670_BLOCKADE":RequestNative("fleet-order",id,u.fleetorders==1?0:1);break;
                case "671_PATROL":RequestNative("fleet-order",id,u.fleetorders==2?0:2);break;
                case "672_RAID":RequestNative("fleet-order",id,u.fleetorders==3?0:3);break;
                case "640_FORCED":RequestNative("forced-march",id,u.doublequick?0:1);break;
                case "641_STOP":case "680_STOP":RequestNative("unit-stop",id);CloseFleetMovement();break;
                case "681_PORT":RequestNative("fleet-port",id);CloseFleetMovement();break;
            }
        }
        bool ExecuteNative(MpMessage request)
        {
            if(request.kind!="native-action")return false;
            if(mode!="coop" || guestSide!=hostSide || request.sequence<=lastSequence || request.sequence<=0 || !MpRules.Finite(request.x))throw new InvalidOperationException("Stale or unauthorized native request.");
            lastSequence=request.sequence;
            var parts=(request.text??"").Split(new[]{'|'},2);string action=parts[0],detail=parts.Length>1?parts[1]:"";
            if(ExecuteAppearanceAction(action,request,detail) || ExecuteRouteAction(action,request,detail) || ExecuteWorldAction(action,request,detail) || ExecuteNavalAction(action,request,detail) || ExecuteEngagementAction(action,request,detail))return true;
            if(action=="weapon-order") {
                int weapon=(int)request.x,quantity=request.unit;
                if(request.x!=weapon || weapon<0 || weapon>=GameVars.weapon.Length || quantity<1 || quantity>1000000 || GameVars.alliance[guestSide].weaponorders[weapon].quantity>0)throw new InvalidOperationException("Weapon order unavailable.");
                WeaponList.PlaceWeaponOrder(guestSide,weapon,quantity);return true;
            }
            if(action=="promote") {
                int officer=(int)request.x;
                if(request.x!=officer || officer<0 || officer>=GameVars.commander.Count || GameVars.commander[officer].alliance!=guestSide || detail!=OfficerCommandIdentity(officer))throw new InvalidOperationException("Officer changed or is unavailable.");
                var c=GameVars.commander[officer];
                MpOfficerRules.Promote(c.rank,request.unit,GameVars.GetMaxRankForUnittyp(c.historicalbranch==4?17:18,guestSide,true),c.status,
                    !DLC_WL.dlc_scenarioactive || DLC_WL.IsCommanderBelowPlayer(officer));
                DLC_WL.PromotionEffects(officer);c.UpdateRankToFit(null,false,1);c.refreshcommand=false;
                officerViewDirty=true;return true;
            }
            // Native management can address inactive subordinate regiments. Map
            // movement eligibility is checked separately by Controllable below.
            if(!units.TryGetValue(request.unit,out var unit) || unit==null || unit.alliance!=guestSide || !BattleUnits.completeunitlist.Contains(unit))throw new InvalidOperationException("Formation unavailable.");
            var engine=EditorFormationOperations.Engine;
            if(action=="unit-name") {if(detail.Length<1 || detail.Length>100 || detail.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidOperationException("Invalid formation name.");unit.name=detail;return true;}
            if(action=="weapon-assign") {
                int weapon=(int)request.x;
                if(request.x!=weapon || weapon<0 || weapon>=GameVars.weapon.Length || unit.unittyp>2 || GameVars.weapon[weapon].isari!=(unit.unittyp==2))throw new InvalidOperationException("Weapon does not fit this unit.");
                var list=Resources.FindObjectsOfTypeAll<WeaponList>().First(w=>w.gameObject.scene.IsValid());list.AssignWeapon(weapon,unit);return true;
            }
            if(!Controllable(unit))throw new InvalidOperationException("Formation cannot receive this order.");
            if(action=="fleet-port") {
                if(unit.unittyp!=17 || unit.fleetref==null)throw new InvalidOperationException("Formation is not a fleet.");
                var port=engine.GetClosestHarbor(unit.transform.position,guestSide,excludeblocked:false,includeseaports:unit.fleetref.abletomoveonsea,includeriverports:unit.fleetref.abletomoveonriver);
                if(port==null)throw new InvalidOperationException("No suitable native return port is available.");
                WithNativeSelection(unit,()=>engine.SetWaypoint(unit,port.transform.position,useorderdelay:false,showmovementoptions:false));return true;
            }
            bool pending=action=="campaign-order" && (request.x<120?unit.campaignstance!=unit.campaignstanceordered:unit.cavalryorders!=unit.cavalryordersordered);
            MpWorldRules.UnitOrder(action,request.x,unit.unittyp,CampaignArmyPanel.GetReadinessStep(unit),pending);
            switch(action) {
                case "campaign-order":if(!new[]{100f,101f,120f,121f,122f,123f}.Contains(request.x))throw new InvalidOperationException("Invalid army order.");engine.ChangeCampaignOrders(unit,(int)request.x);break;
                case "fleet-order":if(unit.unittyp!=17 || request.x!=(int)request.x || request.x<0 || request.x>3)throw new InvalidOperationException("Invalid fleet order.");unit.SetFleetOrders((int)request.x);break;
                case "forced-march":if(request.x!=0 && request.x!=1)throw new InvalidOperationException("Invalid march order.");engine.PerformActionForGroupForAllUnitsOnLevel(unit.gameObject,-1,"SetMovingCondition",(int)request.x);break;
                case "unit-stop":WithNativeSelection(unit,()=>engine.StopAllUnits(unit.gameObject));break;
                default:throw new InvalidOperationException("Unsupported native action: "+action);
            }
            return true;
        }
        void CloseFleetMovement()
        {
            if(ui==null || ui.panelhandler==null)return;
            var h=ui.panelhandler;h.HidePanel(h.panel[h.cpanel_fleetmovement]);h.SetButtonsCondition(h.panel[h.cpanel_fleetbottompanel],0,652);
        }
        public static void ReplicaAssignWeapon(WeaponList target,int manualweapon,Regiment manualunit)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaAssignWeapon",manualweapon,manualunit);return;}
            var u=manualunit??NativeAccess.Get<Regiment>(target,"unittocall");
            int weapon=manualweapon<0?NativeAccess.Get<int>(target,"selectedweapon"):manualweapon;
            if(u!=null && instance.ids.TryGetValue(u,out int id))instance.RequestNative("weapon-assign",id,weapon);
        }
        void WithNativeSelection(Regiment unit,Action action)
        {
            var selected=ui.selectedunit;var selectedReg=ui.selectedunitreg;var selection=ui.selection;var regs=ui.selectionreg;
            try {ui.selectedunit=unit.gameObject;ui.selectedunitreg=unit;ui.selection=new System.Collections.Generic.List<GameObject>{unit.gameObject};ui.selectionreg=new System.Collections.Generic.List<Regiment>{unit};action();}
            finally {ui.selectedunit=selected;ui.selectedunitreg=selectedReg;ui.selection=selection;ui.selectionreg=regs;}
        }
        public static void ReplicaWeaponOrder(int alliance,int weaponid,int quantity)
        {if(IsReplica)instance?.RequestNative("weapon-order",quantity,weaponid);else NativeAccess.Call(typeof(WeaponList),"GTIP_Original_ReplicaWeaponOrder",alliance,weaponid,quantity);}
        public static void ReplicaPromote(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaPromote");return;}
            int id=NativeAccess.Get<int>(target,"currentcommanderchosen");
            if(id<0 || id>=GameVars.commander.Count)return;
            var c=GameVars.commander[id];
            if(instance.pendingPromotions.ContainsValue(id))return;
            if(c.status>0 || c.rank>=GameVars.GetMaxRankForUnittyp(c.historicalbranch==4?17:18,c.alliance,true))return;
            int sequence=instance.guestSequence+1;
            instance.RequestNative("promote",c.rank,id,OfficerCommandIdentity(id));
            instance.pendingPromotions[sequence]=id;
        }
    }
}
