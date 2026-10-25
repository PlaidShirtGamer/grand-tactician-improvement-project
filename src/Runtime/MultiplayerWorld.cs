using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<CBuilding,int> buildingIds=new Dictionary<CBuilding,int>();
        readonly Dictionary<int,CBuilding> worldBuildings=new Dictionary<int,CBuilding>();
        readonly Dictionary<CBuilding,int> canonicalBuildingIndices=new Dictionary<CBuilding,int>();
        int nextBuilding;
        bool buildingIdsInitialized;
        void InitializeBuildingIds()
        {
            if(buildingIdsInitialized)return;
            buildingIdsInitialized=true;
            foreach(var b in BattleUnits.cbuildings)if(b!=null && b!=CBuilding.manualplacedbuilding){buildingIds[b]=++nextBuilding;worldBuildings[nextBuilding]=b;}
        }
        void ClearWorld()
        {ClearMotion();ClearMapStatus();ClearBuildingEconomy();buildingIds.Clear();worldBuildings.Clear();canonicalBuildingIndices.Clear();nextBuilding=0;buildingIdsInitialized=false;ClearBattles();ClearNaval();ClearEngagement();ClearSightings();bottomUnit=null;bottomState=0;buildingListView=null;railroadListView=null;weaponListViews=null;productionView=null;tradeView=null;financesView=null;weaponViewDirty=false;constructionPreferences.Clear();nameEdits.Clear();}
        int BuildingReference(CBuilding b) => b!=null && buildingIds.TryGetValue(b,out int id)?id:0;
        int CanonicalBuildingIndex(CBuilding b) => b!=null && canonicalBuildingIndices.TryGetValue(b,out int i)?i:-1;
        void BindCheckpointBuildings(Dictionary<int,MpBuildingRecord> states)
        {
            // A checkpoint load can reorder buildings of the same prefab. List
            // indices cannot identify banks/depots at different map locations.
            var available=BattleUnits.cbuildings.Where(b=>b!=null && b!=CBuilding.manualplacedbuilding).ToList();
            foreach(var pair in states.OrderBy(p=>p.Value.NativeIndex)) {
                var s=pair.Value;
                var matches=available.Where(b=>MpWorldRules.SameBuilding(b.BuildingType,b.transform.position.x,b.transform.position.z,s)).ToArray();
                var b=matches.FirstOrDefault(candidate=>candidate.BuildingName==s.Name)??matches.FirstOrDefault();
                if(b==null)continue; // New host construction is created below.
                available.Remove(b);buildingIds[b]=pair.Key;worldBuildings[pair.Key]=b;nextBuilding=Math.Max(nextBuilding,pair.Key);
            }
            foreach(var stale in available)stale.RemoveBuilding();
            buildingIdsInitialized=true;
            LogDiagnostic("Checkpoint buildings bound by native type/location: "+worldBuildings.Count+" of "+states.Count+"; stale="+available.Count);
        }
        void CaptureWorld(List<MpStateRow> rows)
        {
            InitializeBuildingIds();
            foreach(var pair in worldBuildings.ToArray())if(pair.Value==null || !BattleUnits.cbuildings.Contains(pair.Value)) {buildingIds.Remove(pair.Value);worldBuildings.Remove(pair.Key);}
            var liveBuildings=BattleUnits.cbuildings.Where(b=>b!=null && b!=CBuilding.manualplacedbuilding).ToArray();canonicalBuildingIndices.Clear();
            for(int i=0;i<liveBuildings.Length;i++) {
                var b=liveBuildings[i];canonicalBuildingIndices[b]=i;
                if(!buildingIds.TryGetValue(b,out int id)){id=++nextBuilding;buildingIds[b]=id;worldBuildings[id]=b;}
                var p=b.transform.position;
                rows.Add(DataRow(17,id,new MpBuildingRecord{Type=b.BuildingType,Owner=b.Owner,Level=b.level,NativeIndex=i,Garrison=DispatchReference(b.garrisonunit),
                    X=p.x,Y=p.y,Z=p.z,Yaw=b.transform.eulerAngles.y,Condition=b.Condition,Name=b.BuildingName,Active=b.gameObject.activeSelf}));
                rows.Add(DataRow(18,id,b));
            }
            for(int i=0;i<BattleUnits.railroad.Count;i++) {
                var r=BattleUnits.railroad[i];rows.Add(DataRow(19,i,new MpRailroadRecord{Side=r.alliancetoconstruct,Progress=r.constructionprogress,LastUpdate=r.lastconstructionupdate,Permitted=r.scriptref.IsPermitted}));
            }
            CaptureNaval(rows);
            for(int a=0;a<GameVars.alliance.Length;a++)for(int w=0;w<GameVars.alliance[a].weaponorders.Length;w++)rows.Add(DataRow(23,a*GameVars.weapon.Length+w,GameVars.alliance[a].weaponorders[w]));
            for(int i=0;i<BattleUnits.iips.Count;i++)rows.Add(DataRow(26,i,BattleUnits.iips[i]));
            for(int i=0;i<BattleUnits.towns.Count;i++)rows.Add(DataRow(27,i,BattleUnits.towns[i]));
            for(int a=0;a<GameVars.alliance.Length;a++)rows.Add(DataRow(30,a,new MpConstructionSites{Places=GameVars.alliance[a].bestiipplaces?.Select(p=>p==null?0:BattleUnits.iips.IndexOf(p)+1).ToArray()}));
        }
        void ApplyWorldBuildings(MpStateRow[] rows,MpStateRow[] changes)
        {
            if(!changes.Any(r=>r.Kind==17 || r.Kind==18))return;
            if(!changes.Any(r=>r.Kind==17)) {
                if(!buildingIdsInitialized)throw new InvalidDataException("Building values precede their roster.");
                foreach(var row in changes.Where(r=>r.Kind==18)) {
                    if(row.Deleted || !worldBuildings.TryGetValue(row.Id,out var b))throw new InvalidDataException("Unknown building data.");
                    DataSchemas[18].Apply(b,row.Values);var icon=b.GetComponent<CBuildingIcon>();if(icon!=null && icon.enlarged)icon.UpdateCampaignIcon(false,true);
                }
                return;
            }
            var states=rows.Where(r=>r.Kind==17).ToDictionary(r=>r.Id,ReadData<MpBuildingRecord>);
            var values=rows.Where(r=>r.Kind==18).ToDictionary(r=>r.Id);
            if(!new HashSet<int>(states.Keys).SetEquals(values.Keys) || !states.Values.Select(s=>s.NativeIndex).OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,states.Count)))throw new InvalidDataException("Incomplete building roster.");
            foreach(var s in states.Values)MpWorldRules.Building(s,GameVars.buildingtypes.Count,GameVars.alliance.Length);
            if(!buildingIdsInitialized && !host)BindCheckpointBuildings(states);else InitializeBuildingIds();
            foreach(var pair in worldBuildings.ToArray())if(!states.ContainsKey(pair.Key)) {
                var b=pair.Value;if(b!=null){b.GetComponent<CBuildingIcon>()?.UpdateCampaignIcon(true);b.RemoveBuilding();buildingIds.Remove(b);}
                worldBuildings.Remove(pair.Key);
            }
            bool changed=false;
            var dirty=new HashSet<int>(changes.Where(r=>(r.Kind==17 || r.Kind==18) && !r.Deleted).Select(r=>r.Id));
            foreach(var pair in states) {
                var s=pair.Value;
                if(!worldBuildings.TryGetValue(pair.Key,out var b) || b==null) {
                    b=CBuilding.Place(new Vector3(s.X,s.Y,s.Z),s.Type,s.Owner,s.Level,s.Yaw,s.Condition,false,0,false);
                    worldBuildings[pair.Key]=b;buildingIds[b]=pair.Key;nextBuilding=Math.Max(nextBuilding,pair.Key);changed=true;
                }
                else if(!dirty.Contains(pair.Key))continue;
                if(b.BuildingType!=s.Type)throw new InvalidDataException("Building identity changed type.");
                if(!MpWorldRules.SameBuilding(b.BuildingType,b.transform.position.x,b.transform.position.z,s))throw new InvalidDataException("Building identity changed location: "+pair.Key);
                b.Owner=s.Owner;b.BuildingName=s.Name;b.gameObject.name=s.Name;
                if(b.level!=s.Level){b.SetLevel(s.Level);changed=true;}
                DataSchemas[18].Apply(b,values[pair.Key].Values);b.gameObject.SetActive(s.Active);
            }
            BattleUnits.cbuildings=states.OrderBy(p=>p.Value.NativeIndex).Select(p=>worldBuildings[p.Key]).ToList();
            canonicalBuildingIndices.Clear();foreach(var pair in states)canonicalBuildingIndices[worldBuildings[pair.Key]]=pair.Value.NativeIndex;
            BattleUnits.fort=BattleUnits.cbuildings.Where(b=>b.BuildingType==CBuilding.id_fort).ToList();
            BattleUnits.supplydepot=BattleUnits.cbuildings.Where(b=>b.BuildingType==CBuilding.id_supplydepot).ToList();
            BattleUnits.telegraphstation=BattleUnits.cbuildings.Where(b=>b.BuildingType==CBuilding.id_telegraphstation).ToList();
            BattleUnits.powcamps=BattleUnits.cbuildings.Where(b=>b.BuildingType==CBuilding.id_powcamp).ToList();
            if(changed)LogDiagnostic("World building roster applied: "+states.Count);
        }
        void ApplyWorldAfterUnits(MpStateRow[] rows,MpStateRow[] changes)
        {
            foreach(var row in changes.Where(r=>r.Kind==30)) {
                if(row.Deleted || row.Id<0 || row.Id>=GameVars.alliance.Length)throw new InvalidDataException("Unknown construction side.");
                var s=ReadData<MpConstructionSites>(row);
                if(s.Places!=null && (s.Places.Length!=GameVars.buildingtypes.Count || s.Places.Any(p=>p<0 || p>BattleUnits.iips.Count)))throw new InvalidDataException("Invalid construction site reference.");
                GameVars.alliance[row.Id].bestiipplaces=s.Places?.Select(p=>p==0?null:BattleUnits.iips[p-1]).ToArray();
            }
            foreach(var row in changes.Where(r=>r.Kind==17 && !r.Deleted)) {
                var s=ReadData<MpBuildingRecord>(row);var b=worldBuildings[row.Id];
                b.garrisonunit=units.TryGetValue(s.Garrison,out var garrison)?garrison:null;
            }
            foreach(var row in changes.Where(r=>r.Kind==26 || r.Kind==27)) {
                object target=row.Kind==26?(object)(row.Id<BattleUnits.iips.Count?BattleUnits.iips[row.Id]:null):(row.Id<BattleUnits.towns.Count?BattleUnits.towns[row.Id]:null);
                if(row.Deleted || target==null)throw new InvalidDataException("Unknown campaign location.");
                DataSchemas[row.Kind].Apply(target,row.Values);
                var icon=((Component)target).GetComponent<CBuildingIcon>();if(icon!=null)icon.UpdateCampaignIcon(false,true);
            }
            foreach(var row in changes.Where(r=>r.Kind==19)) {
                if(row.Id<0 || row.Id>=BattleUnits.railroad.Count || row.Deleted)throw new InvalidDataException("Unknown railroad.");
                var s=ReadData<MpRailroadRecord>(row);var r=BattleUnits.railroad[row.Id];
                if(s.Side< -1 || s.Side>=GameVars.alliance.Length || s.Progress<0 || s.Progress>2)throw new InvalidDataException("Invalid railroad state.");
                bool completed=r.constructionprogress<1 && s.Progress>=1;
                r.alliancetoconstruct=s.Side;r.constructionprogress=s.Progress;r.lastconstructionupdate=s.LastUpdate;r.scriptref.IsPermitted=s.Permitted;
                if(completed)NativeAccess.Call(r,"UpdateBuildStatus");
            }
            foreach(var row in changes.Where(r=>r.Kind==23)) {
                int side=row.Id/GameVars.weapon.Length,weapon=row.Id%GameVars.weapon.Length;
                if(row.Deleted || side>=GameVars.alliance.Length || weapon>=GameVars.alliance[side].weaponorders.Length)throw new InvalidDataException("Unknown weapon order.");
                DataSchemas[23].Apply(GameVars.alliance[side].weaponorders[weapon],row.Values);
            }
            if(changes.Any(r=>r.Kind==23 || r.Kind==0))weaponViewDirty=true;
            ApplyNaval(rows,changes);
            foreach(var row in changes.Where(r=>r.Kind==4 && !r.Deleted)) {
                var s=ReadData<MpFormationState>(row);var u=units[row.Id];
                u.campbuilding=worldBuildings.TryGetValue(s.Camp,out var camp)?camp:null;
                u.entrenchment=worldBuildings.TryGetValue(s.Entrenchment,out var entrench)?entrench:null;
                u.fortinrange=worldBuildings.TryGetValue(s.Fort,out var fort)?fort:null;
                if(s.SupplyIip<0 || s.SupplyIip>BattleUnits.iips.Count)throw new InvalidDataException("Invalid supply location reference.");
                u.closestiipforsupply=s.SupplyIip==0?null:BattleUnits.iips[s.SupplyIip-1];
                u.hastelegraphconnection=worldBuildings.TryGetValue(s.Telegraph,out var telegraph)?telegraph:null;
            }
        }
        public static void ReplicaGarrison(BattleUnits target,CBuilding fort,int newowneralliance,Regiment addexistinggarrison)
        {if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaGarrison",fort,newowneralliance,addexistinggarrison);}
        public static void ReplicaBuildingLists(CBuilding target)
        {
            if(IsReplica || (instance!=null && instance.transport!=null && target==CBuilding.manualplacedbuilding))return;
            NativeAccess.Call(target,"GTIP_Original_ReplicaBuildingLists");
        }
    }
}
