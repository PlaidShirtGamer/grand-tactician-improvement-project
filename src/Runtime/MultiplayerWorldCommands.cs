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
            var b=target.iipattached!=null && target.iipattached.IIPType==IIP.id_harbor?target.iipattached.harborbuildingref:
                NativeAccess.Get<CBuilding>(target,"cbuildingattached")??target.GetComponent<CBuilding>();
            if(b!=null)instance.RequestNative("building-upgrade",instance.BuildingReference(b));
        }
        public static void ReplicaBuildingStock(CBuildingIcon target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaBuildingStock");return;}
            var b=target.GetComponent<CBuilding>();if(b!=null)instance.RequestNative("building-stock",instance.BuildingReference(b));
        }
        public static void ReplicaProductionUpgrade(Production target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaProductionUpgrade");return;}
            var button=target.UpgradeTooltip;var building=NativeAccess.Get<CBuilding>(target,"chosenbuilding");
            // Production also lists buildings without a map-icon component.
            if(building!=null && button!=null && button.isActiveAndEnabled && button.mouseonobject && button.state!=2 && Input.GetMouseButtonDown(0))
                instance.RequestNative("building-upgrade",instance.BuildingReference(building));
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
        void ValidateBuildingType(int type,bool armyPlacement=false)
        {
            if(type<0 || type>=GameVars.buildingtypes.Count || !GameVars.buildingtypes[type].HasPolicy(guestSide))throw new InvalidOperationException("Building is unavailable under native policy rules.");
            var t=GameVars.buildingtypes[type];
            if(!MpConstructionRules.Funded(armyPlacement,GameVars.alliance[guestSide].IsRatingOkForConstruction(),t.subsidytype>=0,t.subsidytype<0?0:t.GetMissingSubsidyFundingCost(guestSide,0)))throw new InvalidOperationException("Native debt rating prohibits construction.");
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
                if(rows.Length!=1)throw new InvalidOperationException("Invalid building draft.");var s=new MpBuildingDraft();schema.Apply(s,rows[0].Values);
                Regiment caller=null;if(s.Unit!=0 && (!units.TryGetValue(s.Unit,out caller) || !Controllable(caller)))throw new InvalidOperationException("Construction unit unavailable.");
                bool army=caller!=null && (s.Type==CBuilding.id_fort || s.Type==CBuilding.id_supplydepot || s.Type==CBuilding.id_telegraphstation);
                ValidateBuildingType(s.Type,army);
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
            var icon=building.GetComponent<CBuildingIcon>();
            if(icon==null)icon=BattleUnits.iips.Where(i=>i!=null && i.harborbuildingref==building).Select(i=>i.GetComponent<CBuildingIcon>()).FirstOrDefault(i=>i!=null);
            if(action=="building-stock") {if(building.BuildingType!=CBuilding.id_supplydepot || icon==null)throw new InvalidOperationException("Not an available supply depot.");icon.SwitchStorageLevelTarget();}
            else {
                var t=GameVars.buildingtypes[building.BuildingType];int level=building.level+1;
                // Match UpdateUpgradeIcon: next-level funding here, not new-building
                // policy checks and level-zero funding at the capital.
                if(DLC_WL.dlc_scenarioactive || building.upgradetimer>0 || level>=t.maxlevels ||
                    (building.BuildingType==CBuilding.id_fort && building.level==0) ||
                    (building.BuildingType==CBuilding.id_supplydepot && !GameVars.alliance[guestSide].projects.Contains(115)))throw new InvalidOperationException("Upgrade unavailable under native building/project rules.");
                if(!MpConstructionRules.Funded(false,GameVars.alliance[guestSide].IsRatingOkForConstruction(),t.subsidytype>=0,
                    t.subsidytype<0?0:t.GetMissingSubsidyFundingCost(guestSide,level,building.transform.position,building.closestiip)))throw new InvalidOperationException("Native funding rules prohibit this upgrade.");
                if(icon!=null) {
                    if(icon.iipattached==null)NativeAccess.Set(icon,"cbuildingattached",building);
                    icon.Upgrade(guestSide);
                } else UpgradeWithoutMapIcon(building,guestSide);
                if(building.upgradetimer<=0)throw new InvalidOperationException("Native upgrade did not start.");
            }
            return true;
        }
        // CBuildingIcon.Upgrade transaction, for production buildings without an icon.
        static void UpgradeWithoutMapIcon(CBuilding b,int side)
        {
            var t=GameVars.buildingtypes[b.BuildingType];int level=b.level+1;var a=GameVars.alliance[side];
            b.upgradetimer=t.resourceusage[level].GetConstructionDuration(2);
            float subsidy=t.GetSubsidyCost(side,level,b.transform.position,b.closestiip);
            float cost=t.GetConstructionCost(level,b.transform.position,b.Owner,b.closestiip);
            if(t.subsidytype<0){a.AdjustTreasury(cost);a.constructioncosts+=cost;}
            else {
                float missing=t.GetMissingSubsidyFundingCost(b.Owner,level,b.transform.position,b.closestiip);
                a.AdjustSubsidyFunding(t.subsidytype,subsidy);a.AdjustTreasury(missing);
                a.constructioncosts+=missing;a.cumulatedspending_construction+=subsidy;
            }
            Economy.Consume(t.resourceusage[level],b.transform.position,-1,1);
        }
    }
}
