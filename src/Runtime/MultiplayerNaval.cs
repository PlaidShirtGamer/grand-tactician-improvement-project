using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<Vessel,int> vesselIds=new Dictionary<Vessel,int>();
        readonly Dictionary<int,Vessel> vessels=new Dictionary<int,Vessel>();
        int nextVessel;
        bool navalInitialized,applyingNaval;
        void InitializeNaval()
        {
            if(navalInitialized)return;navalInitialized=true;
            foreach(var v in BattleUnits.GetCompleteVesselList().Where(v=>v!=null).OrderBy(v=>v.name,StringComparer.Ordinal).ThenBy(v=>v.type).ThenBy(v=>v.attachedpool).ThenBy(v=>v.currentposition.x).ThenBy(v=>v.currentposition.z)) {
                vesselIds[v]=++nextVessel;vessels[nextVessel]=v;
            }
        }
        void ClearNaval(){vesselIds.Clear();vessels.Clear();nextVessel=0;navalInitialized=false;applyingNaval=false;}
        void CaptureNaval(List<MpStateRow> rows)
        {
            InitializeNaval();var live=BattleUnits.GetCompleteVesselList().Where(v=>v!=null).ToArray();var set=new HashSet<Vessel>(live);
            foreach(var pair in vessels.ToArray())if(pair.Value==null || !set.Contains(pair.Value)){vesselIds.Remove(pair.Value);vessels.Remove(pair.Key);}
            foreach(var v in live) {
                if(!vesselIds.TryGetValue(v,out int id)){id=++nextVessel;vesselIds[v]=id;vessels[id]=v;}
                rows.Add(DataRow(20,id,new MpVesselRecord{Fleet=DispatchReference(v.attachedfleetregref),Pool=v.attachedpool,Type=v.type,Harbor=v.harbortoreturn==null?-1:BattleUnits.iips.IndexOf(v.harbortoreturn),Name=v.name,
                    X=v.currentposition.x,Y=v.currentposition.y,Z=v.currentposition.z,DX=v.destination.x,DY=v.destination.y,DZ=v.destination.z}));
                rows.Add(DataRow(21,id,v));
            }
            foreach(var pair in units.Where(p=>p.Value.fleetref!=null))rows.Add(DataRow(22,pair.Key,pair.Value.fleetref));
        }
        void ApplyNaval(MpStateRow[] rows,MpStateRow[] changes)
        {
            if(!changes.Any(r=>r.Kind>=20 && r.Kind<=22))return;
            InitializeNaval();bool structural=false;
            var records=rows.Where(r=>r.Kind==20).ToDictionary(r=>r.Id,ReadData<MpVesselRecord>);
            var values=rows.Where(r=>r.Kind==21).ToDictionary(r=>r.Id);
            if(!new HashSet<int>(records.Keys).SetEquals(values.Keys))throw new InvalidDataException("Incomplete vessel roster.");
            applyingNaval=true;
            try {
                foreach(var pair in vessels.ToArray())if(!records.ContainsKey(pair.Key)) {
                    if(pair.Value!=null){vesselIds.Remove(pair.Value);BattleUnits.DeleteShip(pair.Value.gameObject);}vessels.Remove(pair.Key);structural=true;
                }
                foreach(var pair in records) {
                    var s=pair.Value;
                    if(s.Type<0 || s.Type>=GameVars.shiptypes.Count || s.Fleet<0 || (s.Fleet!=0 && (!units.TryGetValue(s.Fleet,out var f) || f.fleetref==null)) ||
                        (s.Fleet==0 && (s.Pool<0 || s.Pool>=GameVars.alliance.Length)) || s.Harbor< -1 || s.Harbor>=BattleUnits.iips.Count || s.Name==null)throw new InvalidDataException("Invalid vessel reference.");
                    Regiment fleet=s.Fleet==0?null:units[s.Fleet];
                    if(!vessels.TryGetValue(pair.Key,out var v) || v==null) {
                        v=EditorFormationOperations.Engine.CreateShip(s.Name,fleet,s.Pool,s.Type,100,100,100,100,0,new Vector3(s.X,s.Y,s.Z),new Vector3(s.DX,s.DY,s.DZ),0,true,false);
                        vesselIds[v]=pair.Key;vessels[pair.Key]=v;nextVessel=Math.Max(nextVessel,pair.Key);structural=true;
                    }
                    if(v.attachedfleetregref!=fleet || (fleet==null && v.attachedpool!=s.Pool)) {
                        if(fleet==null)v.AddToPool(s.Pool,false,true);else v.AddToFleet(fleet,false,true);structural=true;
                    }
                    DataSchemas[21].Apply(v,values[pair.Key].Values);v.gameObject.name=v.name;v.currentposition=new Vector3(s.X,s.Y,s.Z);v.destination=new Vector3(s.DX,s.DY,s.DZ);
                    v.harbortoreturn=s.Harbor<0?null:BattleUnits.iips[s.Harbor];
                }
                if(structural)foreach(var u in units.Values.Where(u=>u.fleetref!=null)){u.fleetref.UpdateAttachedShips();u.fleetref.UpdateFleetData(true);}
                foreach(var row in rows.Where(r=>r.Kind==22)) {
                    if(!units.TryGetValue(row.Id,out var u) || u.fleetref==null)throw new InvalidDataException("Unknown fleet record.");
                    DataSchemas[22].Apply(u.fleetref,row.Values);
                }
                if(structural){rosterUiPending=true;fleetViewDirty=true;foreach(var row in rows.Where(r=>r.Kind<=3))DataSchemas[row.Kind].Apply(DataTarget(row),row.Values);}
            } finally {applyingNaval=false;}
            if(structural)LogDiagnostic("Vessel roster applied: "+vessels.Count);
        }
        public static void ReplicaVesselConsumption(Vessel target,bool forceupdate)
        {if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaVesselConsumption",forceupdate);}
        public static void ReplicaCreateFleet(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaCreateFleet");return;}
            var list=NativeAccess.Get<FleetRecruitingList>(target,"fleetrecruitinglist");
            if(list?.choseniip!=null)instance.RequestNative("fleet-create",BattleUnits.iips.IndexOf(list.choseniip));
        }
        public static void ReplicaCreateShip(CampaignMainPanel target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaCreateShip");return;}
            var list=NativeAccess.Get<ShipRecruitingList>(target,"shiprecruitinglist");var fleet=NativeAccess.Get<Regiment>(target,"shiptoaddto");
            var draft=new MpShipDraft{Fleet=instance.DispatchReference(fleet),Type=list.chosenshiptype,Count=list.GetShipsToConstruct(),Name=NativeAccess.Get<FleetInfoPanel>(target,"fleetinfopanel").manualname??""};
            instance.RequestNative("ship-create",0,0,Convert.ToBase64String(MpStateCodec.Encode(new[]{MpStateSchema.Scalars(typeof(MpShipDraft))},new[]{new MpStateRow{Values=MpStateSchema.Scalars(typeof(MpShipDraft)).Capture(draft)}})));
        }
        public static void ReplicaDeleteShip(GameObject target)
        {
            if(!IsReplica || instance.applyingNaval){NativeAccess.Call(typeof(BattleUnits),"GTIP_Original_ReplicaDeleteShip",target);return;}
            var v=target==null?null:target.GetComponent<Vessel>();if(v!=null && instance.vesselIds.TryGetValue(v,out int id))instance.RequestNative("ship-delete",id);
        }
        public static void ReplicaShipToFleet(Vessel vessel,Regiment fleet)
        {
            if(!IsReplica){NativeAccess.Call(typeof(BattleUnits),"GTIP_Original_ReplicaShipToFleet",vessel,fleet);return;}
            if(vessel!=null && fleet!=null && instance.vesselIds.TryGetValue(vessel,out int id))instance.RequestNative("ship-move",id,instance.DispatchReference(fleet));
        }
        public static void ReplicaShipToPool(Vessel vessel,int alliance)
        {
            if(!IsReplica){NativeAccess.Call(typeof(BattleUnits),"GTIP_Original_ReplicaShipToPool",vessel,alliance);return;}
            if(vessel!=null && instance.vesselIds.TryGetValue(vessel,out int id))instance.RequestNative("ship-pool",id,alliance);
        }
        bool ExecuteNavalAction(string action,MpMessage request,string detail)
        {
            if(action=="fleet-create") {
                if(request.unit<0 || request.unit>=BattleUnits.iips.Count)throw new InvalidOperationException("Port unavailable.");
                var port=BattleUnits.iips[request.unit];
                if(port.allianceowner!=guestSide || (!port.isriverport && !port.isseaport))throw new InvalidOperationException("Port is not owned or suitable.");
                var p=BattlefieldSetup.SearchTerrainInRangePos(port.transform.position,new[]{3,4},10)?.position??port.transform.position;
                var fleet=EditorFormationOperations.Engine.CreateUnitDefault(null,-1,17,false,default(Color),default(Color),0,0,guestSide,p);
                fleet.fleetref.abletomoveonriver=port.isriverport;fleet.fleetref.abletomoveonsea=port.isseaport;return true;
            }
            if(action=="ship-create") {
                var schema=MpStateSchema.Scalars(typeof(MpShipDraft));var rows=MpStateCodec.Decode(new[]{schema},Convert.FromBase64String(detail));
                if(rows.Length!=1)throw new InvalidOperationException("Invalid ship draft.");var s=new MpShipDraft();schema.Apply(s,rows[0].Values);
                if(s.Type<0 || s.Type>=GameVars.shiptypes.Count || s.Count<1 || s.Count>9 || s.Name==null || s.Name.Length>100 || s.Name.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidOperationException("Invalid ship draft.");
                Regiment fleet=null;if(s.Fleet!=0 && (!units.TryGetValue(s.Fleet,out fleet) || fleet.alliance!=guestSide || fleet.fleetref==null))throw new InvalidOperationException("Fleet unavailable.");
                var old=NativeAccess.Get<Regiment>(typeof(ShipRecruitingList),"fleetthisshipisadded");
                try {NativeAccess.Set(typeof(ShipRecruitingList),"fleetthisshipisadded",fleet);if(ShipRecruitingList.IsShipChooseable(s.Type,guestSide,fleet?.fleetref,s.Count)!=0)throw new InvalidOperationException("Native rules prohibit this ship order.");}
                finally {NativeAccess.Set(typeof(ShipRecruitingList),"fleetthisshipisadded",old);}
                for(int i=0;i<s.Count;i++) {
                    string name=i==0 && s.Name.Length>0?s.Name:GameVars.alliance[guestSide].shipdesignation+" "+GameVars.alliance[guestSide].GetNewShipName(s.Type);
                    EditorFormationOperations.Engine.CreateShip(name,fleet,fleet==null?guestSide:-1,s.Type,1,1,1,1,0,default(Vector3),default(Vector3),0,false,true,0,default(Vector3),false,1);
                }
                GameVars.UpdateShipProductionCapacity();return true;
            }
            if(action!="ship-delete" && action!="ship-move" && action!="ship-pool" && action!="ship-name")return false;
            InitializeNaval();if(!vessels.TryGetValue(request.unit,out var v) || v==null || (v.attachedfleetregref==null?v.attachedpool:v.attachedfleetregref.alliance)!=guestSide)throw new InvalidOperationException("Ship unavailable.");
            if(action=="ship-name"){if(detail.Length<1 || detail.Length>100 || detail.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidOperationException("Invalid ship name.");v.name=detail;v.gameObject.name=detail;}
            else if(action=="ship-delete")BattleUnits.DeleteShip(v.gameObject);
            else if(action=="ship-pool") {if(request.x!=guestSide)throw new InvalidOperationException("Wrong ship pool.");BattleUnits.MoveShip(v,guestSide);}
            else {if(request.x!=(int)request.x || !units.TryGetValue((int)request.x,out var fleet) || fleet.alliance!=guestSide || fleet.fleetref==null || !fleet.fleetref.CanShipBeAdded())throw new InvalidOperationException("Fleet cannot accept the ship.");BattleUnits.MoveShip(v,fleet);}
            return true;
        }
        public static void ReplicaFleetUpdate(Fleet target)
        {if(TacticalHolding){if(GameVars.frame>=50 && !NativeAccess.Get<bool>(target,"initialized"))InitializeTactical(()=>NativeAccess.Call(target,"GTIP_Original_ReplicaFleetUpdate"));return;}if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaFleetUpdate");else NativeAccess.Call(target,"UpdateModelSize");}
    }
}
