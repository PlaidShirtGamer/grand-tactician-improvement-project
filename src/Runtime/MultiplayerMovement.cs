using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        static readonly MpStateSchema[] MovementSchema={MpStateSchema.Scalars(typeof(MpMovementView))};
        object guestRouteOrders;
        int routeUnit,routeOrder,executingRouteUnit,executingRouteOrder,lastGuestMove;
        MpMovementView movementView;
        void ClearMovement()
        {guestRouteOrders=null;routeUnit=routeOrder=executingRouteUnit=executingRouteOrder=lastGuestMove=0;movementView=null;fleetViewDirty=false;pathSamples.Clear();receivedPaths.Clear();nextPathSample=0;}
        void WithGuestRoute(Regiment unit,int order,bool fresh,Action native)
        {
            var engine=EditorFormationOperations.Engine;
            var saved=NativeAccess.Get<object>(engine,"currentwaypointorders");
            var last=NativeAccess.Get<Vector3>(engine,"lastsetwaypointpos");
            NativeAccess.Set(engine,"currentwaypointorders",fresh?Activator.CreateInstance(saved.GetType()):guestRouteOrders);
            executingRouteUnit=Id(unit);executingRouteOrder=order;
            bool selected=unit.selected;unit.selected=true;
            try {
                WithNativeSelection(unit,native);guestRouteOrders=NativeAccess.Get<object>(engine,"currentwaypointorders");routeUnit=Id(unit);routeOrder=order;
                if(fresh && movementView?.Order==order && saved is IList prior) {
                    var affected=(unit.allattachedunits??new Regiment[0]).Concat(new[]{unit}).ToArray();
                    for(int i=prior.Count-1;i>=0;i--)if(affected.Contains(NativeAccess.Get<Regiment>(prior[i],"regid")))prior.RemoveAt(i);
                }
            }
            finally {unit.selected=selected;NativeAccess.Set(engine,"currentwaypointorders",saved);NativeAccess.Set(engine,"lastsetwaypointpos",last);executingRouteUnit=executingRouteOrder=0;}
        }
        public static void ReplicaStopAll(BattleUnits target,GameObject selectedunit)
        {
            if(IsReplica)return;
            var mp=instance;
            if(mp!=null && mp.guestRouteOrders is IList orders && orders.Cast<object>().Any(o=>{var reg=NativeAccess.Get<Regiment>(o,"regid");return reg!=null && reg.gameObject==selectedunit;})) {
                mp.guestRouteOrders=null;mp.routeUnit=mp.routeOrder=0;
            }
            NativeAccess.Call(target,"GTIP_Original_ReplicaStopAll",selectedunit);
            if(mp!=null && selectedunit!=null)mp.SendFastPaths(selectedunit.GetComponent<Regiment>());
        }
        public static void ReplicaWaypoint(BattleUnits target,Regiment reg,Vector3 targetpos,bool newpath,bool doublequick,float manualfinalrotation,bool modifylastwaypoint,bool useorderdelay,float timetomove,int direction,bool showmovementoptions,bool ignorebattlemonuments,bool groupmoveonly,bool ignoredisabledships,bool checkforreadiness,bool clearinterruptionpaths)
        {
            if(IsReplica)return;
            var mp=instance;
            if(mp!=null && mp.host && !mp.remoteIntent && mp.guestRouteOrders is IList orders &&
                orders.Cast<object>().Any(o=>NativeAccess.Get<Regiment>(o,"regid")==reg)) {
                mp.guestRouteOrders=null;mp.routeUnit=mp.routeOrder=0;
            }
            NativeAccess.Call(target,"GTIP_Original_ReplicaWaypoint",reg,targetpos,newpath,doublequick,manualfinalrotation,modifylastwaypoint,useorderdelay,timetomove,direction,showmovementoptions,ignorebattlemonuments,groupmoveonly,ignoredisabledships,checkforreadiness,clearinterruptionpaths);
            if(mp!=null)mp.SendFastPaths(reg);
        }
        public static void ReplicaMovementPanel(PanelHandler target,int panelid,float secsremaining,GameObject go_ref,Vector3 mapposition,Vector2 screenposition,GameObject secondaryreference,int[] activatedbuttonids,int[] deactivatedbuttonids)
        {
            var mp=instance;
            if(mp!=null && mp.host && mp.remoteIntent && mp.executingRouteUnit>0 && (panelid==target.panel_movement || panelid==target.panel_premovement)) {
                var v=new MpMovementView{Unit=mp.executingRouteUnit,Order=mp.executingRouteOrder,Minimized=panelid==target.panel_premovement,
                    X=mapposition.x,Y=mapposition.y,Z=mapposition.z,Seconds=secsremaining,Active=activatedbuttonids??new int[0],Disabled=deactivatedbuttonids??new int[0]};
                MpMovementRules.View(v);mp.movementView=v;
                mp.Send(new MpMessage{kind="movement-panel",session=mp.session,blob=MpStateCodec.Encode(MovementSchema,new[]{new MpStateRow{Values=MovementSchema[0].Capture(v)}})});
                mp.LogDiagnostic("Native movement panel sent: unit="+v.Unit+"; order="+v.Order);
                return;
            }
            NativeAccess.Call(target,"GTIP_Original_ReplicaMovementPanel",panelid,secsremaining,go_ref,mapposition,screenposition,secondaryreference,activatedbuttonids,deactivatedbuttonids);
        }
        void ReceiveMovementPanel(MpMessage message)
        {
            var rows=MpStateCodec.Decode(MovementSchema,message.blob);
            if(rows.Length!=1 || rows[0].Deleted)throw new InvalidOperationException("Invalid movement panel message.");
            var v=new MpMovementView();MovementSchema[0].Apply(v,rows[0].Values);MpMovementRules.View(v);
            if(v.Order!=lastGuestMove || !units.TryGetValue(v.Unit,out var unit) || unit==null || unit.alliance!=guestSide)return;
            movementView=v;ShowGuestMovement(v.Minimized);LogDiagnostic("Native movement panel received: unit="+v.Unit+"; order="+v.Order);
        }
        void ShowGuestMovement(bool minimized)
        {
            var v=movementView;if(v==null || !units.TryGetValue(v.Unit,out var unit) || unit==null)return;
            var h=ui.panelhandler;h.HidePanel(h.panel[h.panel_movement]);h.HidePanel(h.panel[h.panel_premovement]);
            h.CallPanelOnRMBReleased(minimized?h.panel_premovement:h.panel_movement,v.Seconds,null,new Vector3(v.X,v.Y,v.Z),default(Vector2),unit.gameObject,v.Active,v.Disabled);
        }
        void HideGuestMovement()
        {
            var h=ui.panelhandler;NativeAccess.Set(h,"cp_panelid",-1);h.HidePanel(h.panel[h.panel_movement]);h.HidePanel(h.panel[h.panel_premovement]);h.HidePanel(h.panel[h.panel_timedmovement]);
        }
        bool GuestMovementAction(ButtonTooltip button)
        {
            if(movementView==null || button==null || button.state==2)return false;
            var v=movementView;var h=ui.panelhandler;
            if(!new[]{h.panel_movement,h.panel_premovement,h.panel_timedmovement}.Any(i=>button.transform.IsChildOf(h.panel[i].go.transform)))return false;
            string action=null;float value=0;
            switch(button.name) {
                case "013_ButtonMovementOpen":ShowGuestMovement(false);return true;
                case "192_AcceptRoute":case "006_AcceptRoute":action="route-accept";break;
                case "193_CancelRoute":case "007_CancelRoute":action="route-cancel";break;
                case "409_AcceptCTM":
                    var date=NativeAccess.Get<Tools.Date>(h,"moveoutdate");
                    value=Tools.GetDateFraction(date.day,date.month,date.year)-1860f;action="route-time";break;
                case "000_O_MOVEMENT-GO":action="route-time";value=9999999f;break;
                case "001_C_DOUBLEQUICK":action="route-march";value=button.state==0?1:0;break;
                case "002_C_RETREAT":action="route-retreat";break;
                case "003_C_RAILMOVEMENT":action="route-transport";value=button.state==0?6:7;break;
                case "004_C_SEAMOVEMENT":action="route-transport";value=button.state==0?8:9;break;
                case "005_C_RIVERMOVEMENT":action="route-transport";value=button.state==0?10:11;break;
                default:return false;
            }
            RequestNative(action,v.Unit,value,v.Order.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(action=="route-accept" || action=="route-cancel" || action=="route-time")HideGuestMovement();
            else button.SetState(button.state==0?1:0);
            return true;
        }
        bool ExecuteRouteAction(string action,MpMessage request,string detail)
        {
            if(!action.StartsWith("route-",StringComparison.Ordinal))return false;
            if(!int.TryParse(detail,out int serial))throw new InvalidOperationException("Missing movement option identity.");
            MpMovementRules.Ticket(request.unit,serial,routeUnit,routeOrder);
            if(guestRouteOrders==null || !units.TryGetValue(request.unit,out var unit) || !Controllable(unit) || unit.alliance!=guestSide)throw new InvalidOperationException("Movement order is unavailable.");
            var engine=EditorFormationOperations.Engine;
            WithGuestRoute(unit,serial,false,()=> {
                switch(action) {
                    case "route-accept":break;
                    case "route-cancel":engine.CancelRoutes(false,true);break;
                    case "route-time":if(request.x< GameVars.currenttimefromstart || request.x>9999999f)throw new InvalidOperationException("Invalid movement date.");unit.OrderTimedMovement(request.x);break;
                    case "route-march":MpWorldRules.UnitOrder("forced-march",request.x,unit.unittyp,CampaignArmyPanel.GetReadinessStep(unit),false);unit.SetMovingConditionForAll((int)request.x);break;
                    case "route-retreat":unit.SetMovementModeForAll(5);break;
                    case "route-transport":
                        int code=(int)request.x;if(code!=request.x || code<6 || code>11)throw new InvalidOperationException("Invalid transport option.");
                        int type=(code-6)/2;
                        bool incompatible=unit.unittyp==17 && (type==0 || (type==1 && !unit.fleetref.abletomoveonsea) || (type==2 && !unit.fleetref.abletomoveonriver));
                        if(code%2==0 && (incompatible || GameVars.alliance[guestSide].transportcapacityused[type]/Mathf.Max(1,GameVars.alliance[guestSide].transportcapacity[type])>GamePrefs.greyoutmeansoftransportcap))throw new InvalidOperationException("Native transport capacity is unavailable.");
                        engine.PerformActionForGroupForAllUnitsOnLevel(unit.gameObject,-1,"SetCoverAndRoadAI",code);
                        engine.PerformActionForGroupForAllUnitsOnLevel(unit.gameObject,-1,"ResetCoverAndRoadAI",null);engine.CancelRoutes(true,true);break;
                    default:throw new InvalidOperationException("Unsupported movement option.");
                }
            });
            if(action=="route-accept" || action=="route-cancel" || action=="route-time"){guestRouteOrders=null;routeUnit=routeOrder=0;}
            SendFastPaths(unit);
            return true;
        }
    }
}
