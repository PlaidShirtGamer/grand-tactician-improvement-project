using System;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static bool ReplicaBuildingPlacement(Vector3 position,bool LMB)
        {
            if(!IsReplica)return (bool)NativeAccess.Call(typeof(CBuilding),"GTIP_Original_ReplicaBuildingPlacement",position,LMB);
            NativeAccess.Call(typeof(CBuilding),"GTIP_Original_ReplicaBuildingPlacement",position,false);
            var b=CBuilding.manualplacedbuilding;
            if(!LMB || b==null || !b.IsPlaceable())return false;
            var d=new MpBuildingDraft{Type=b.BuildingType,Unit=instance.DispatchReference(CBuilding.unitthatcallsmanualplacement),X=position.x,Y=position.y,Z=position.z};
            var schema=MpStateSchema.Scalars(typeof(MpBuildingDraft));
            instance.RequestNative("building-place",0,0,Convert.ToBase64String(MpStateCodec.Encode(new[]{schema},new[]{new MpStateRow{Values=schema.Capture(d)}})));
            CBuilding.ActivateManualBuildingPlacement(false);BuildingList.ClearButtonStates();return true;
        }
        public static void ReplicaBuildingUpgrade(CBuildingIcon target,int allianceforupgrade)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaBuildingUpgrade",allianceforupgrade);return;}
            var b=target.GetComponent<CBuilding>()??target.iipattached?.harborbuildingref;
            if(b!=null)instance.RequestNative("building-upgrade",instance.BuildingReference(b));
        }
        public static void ReplicaBuildingStock(CBuildingIcon target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaBuildingStock");return;}
            var b=target.GetComponent<CBuilding>();if(b!=null)instance.RequestNative("building-stock",instance.BuildingReference(b));
        }
        public static void ReplicaRailroadConstruction(int alliance)
        {
            if(!IsReplica){NativeAccess.Call(typeof(RailroadList),"GTIP_Original_ReplicaRailroadConstruction",alliance);return;}
            var chosen=NativeAccess.Get<object>(typeof(RailroadList),"lastchosenrailroadline");
            if(chosen!=null)instance.RequestNative("railroad-build",BattleUnits.railroad.IndexOf(NativeAccess.Get<BattleUnits.Railroad>(chosen,"railroadref")));
        }
        public static void ReplicaAutoBuilding(BuildingList target,object buildingline)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaAutoBuilding",buildingline);return;}
            var type=NativeAccess.Get<GameVars.BuildingType>(buildingline,"buildingtype");instance.RequestNative("building-auto",0,GameVars.buildingtypes.IndexOf(type));
        }
        public static void ReplicaConstructionOptions(BuildingList target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaConstructionOptions");return;}
            var auto=NativeAccess.Get<CheckBox>(target,"automanageconstruction");var place=NativeAccess.Get<CheckBox>(target,"AutoPlacement");
            int shown=NativeAccess.Get<int>(target,"showncontent");
            instance.GuestPreference(auto,shown==0?0:1,shown==0?GameVars.automanageconstructiong:GameVars.automanageconstructionp);
            instance.GuestPreference(place,3,GameVars.autoplacebuildings);
        }
        public static void ReplicaRailroadOptions(RailroadList target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaRailroadOptions");return;}
            instance.GuestPreference(target.automanageconstruction,2,GameVars.automanageconstructionr);
            instance.GuestPreference(target.AutoPlacement,3,GameVars.autoplacebuildings);
        }
        void ValidateBuildingType(int type)
        {
            if(type<0 || type>=GameVars.buildingtypes.Count || !GameVars.buildingtypes[type].HasPolicy(guestSide))throw new InvalidOperationException("Building is unavailable under native policy rules.");
            var t=GameVars.buildingtypes[type];
            if(!GameVars.alliance[guestSide].IsRatingOkForConstruction() && (t.subsidytype<0 || t.GetMissingSubsidyFundingCost(guestSide,0)<0))throw new InvalidOperationException("Native debt rating prohibits construction.");
        }
        bool ExecuteWorldAction(string action,MpMessage request,string detail)
        {
            if(action=="construction-option") {
                if(request.unit<0 || request.unit>4 || (request.x!=0 && request.x!=1))throw new InvalidOperationException("Invalid construction preference.");
                bool value=request.x==1;
                switch(request.unit){case 0:GameVars.automanageconstructiong=value;break;case 1:GameVars.automanageconstructionp=value;break;case 2:GameVars.automanageconstructionr=value;break;case 3:GameVars.autoplacebuildings=value;break;case 4:GameVars.automanageweapons=value;break;}return true;
            }
            if(action=="railroad-build") {
                if(request.unit<0 || request.unit>=BattleUnits.railroad.Count)throw new InvalidOperationException("Unknown railroad.");
                var r=BattleUnits.railroad[request.unit];
                if(r.constructionprogress>0 || !r.StartConstruction(guestSide,true))throw new InvalidOperationException("Native rules prohibit this railroad.");
                r.StartConstruction(guestSide);GameVars.alliance[guestSide].AdjustSubsidyFunding(GameVars.subsidyidrailroads,r.GetConstructionSubsidyCosts());
                GameVars.alliance[guestSide].treasury+=r.GetMissingSubsidyCosts(guestSide);return true;
            }
            if(action=="building-auto") {
                int type=(int)request.x;if(type!=request.x)throw new InvalidOperationException("Invalid building type.");ValidateBuildingType(type);
                var iip=GameVars.alliance[guestSide].bestiipplaces[type];if(iip==null)throw new InvalidOperationException("No suitable native construction site.");
                CBuilding.AddConstructionWish(type,iip.transform.position,iip);CBuilding created=null;
                for(int i=0;i<500 && created==null;i++)created=CBuilding.WorkDownConstructionWishes();
                if(created!=null)GameVars.alliance[guestSide].bestiipplaces[type]=null;return true;
            }
            if(action=="building-place") {
                var schema=MpStateSchema.Scalars(typeof(MpBuildingDraft));var rows=MpStateCodec.Decode(new[]{schema},Convert.FromBase64String(detail));
                if(rows.Length!=1)throw new InvalidOperationException("Invalid building draft.");var s=new MpBuildingDraft();schema.Apply(s,rows[0].Values);ValidateBuildingType(s.Type);
                Regiment caller=null;if(s.Unit!=0 && (!units.TryGetValue(s.Unit,out caller) || !Controllable(caller)))throw new InvalidOperationException("Construction unit unavailable.");
                if(GameVars.buildingtypes[s.Type].needsunitforplacement && caller==null)throw new InvalidOperationException("This building requires an army.");
                var terrain=Terrain.activeTerrain;var p=new Vector3(s.X,s.Y,s.Z);var origin=terrain.transform.position;var size=terrain.terrainData.size;
                if(!MpRules.Finite(s.X)||!MpRules.Finite(s.Y)||!MpRules.Finite(s.Z)||s.X<origin.x||s.Z<origin.z||s.X>origin.x+size.x||s.Z>origin.z+size.z)throw new InvalidOperationException("Building site is outside the map.");
                p.y=terrain.SampleHeight(p)+origin.y;
                var saved=CBuilding.manualplacedbuilding;var savedCaller=CBuilding.unitthatcallsmanualplacement;
                CBuilding preview=null;
                try {
                    preview=CBuilding.Place(p,s.Type,guestSide,s.Type==CBuilding.id_fort?1:0,0,1,false,0,false);
                    CBuilding.manualplacedbuilding=preview;CBuilding.unitthatcallsmanualplacement=caller;
                    if(!CBuilding.UpdateManualBuildingPlacement(p,true))throw new InvalidOperationException("Native terrain, ownership or unit rules rejected this site.");
                } finally {
                    if(preview!=null){BattleUnits.CheckForEmptyBuildingReferences(preview);BattleUnits.cbuildings.Remove(preview);Destroy(preview.gameObject);}
                    CBuilding.manualplacedbuilding=saved;CBuilding.unitthatcallsmanualplacement=savedCaller;
                }
                return true;
            }
            if(action!="building-upgrade" && action!="building-stock")return false;
            InitializeBuildingIds();
            if(!worldBuildings.TryGetValue(request.unit,out var building) || building==null || building.Owner!=guestSide)throw new InvalidOperationException("Building unavailable or not owned.");
            var icon=building.GetComponent<CBuildingIcon>();if(icon==null)throw new InvalidOperationException("Building control unavailable.");
            if(action=="building-stock") {if(building.BuildingType!=CBuilding.id_supplydepot)throw new InvalidOperationException("Not a supply depot.");icon.SwitchStorageLevelTarget();}
            else {
                ValidateBuildingType(building.BuildingType);
                if(building.upgradetimer>0 || building.level>=GameVars.buildingtypes[building.BuildingType].maxlevels-1 || building.constructiontimer>0)throw new InvalidOperationException("Upgrade unavailable.");
                icon.Upgrade(guestSide);
            }
            return true;
        }
    }
}
