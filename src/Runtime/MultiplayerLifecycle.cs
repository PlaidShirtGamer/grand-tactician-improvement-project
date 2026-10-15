using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<ArmyGroup,int> groupIds=new Dictionary<ArmyGroup,int>();
        readonly Dictionary<int,ArmyGroup> groups=new Dictionary<int,ArmyGroup>();
        int nextGroupId;
        bool hasLiveData;
        void InitializeGroupIds()
        {
            groupIds.Clear();groups.Clear();nextGroupId=0;
            foreach(var g in BattleUnits.armygroups.Where(g=>g!=null).OrderBy(g=>g.name,StringComparer.Ordinal).ThenBy(g=>g.GetCommanderID()))GroupId(g);
        }
        int GroupId(ArmyGroup group)
        {if(!groupIds.TryGetValue(group,out int id)){id=++nextGroupId;groupIds[group]=id;groups[id]=group;}return id;}
        bool RosterChanged() => BattleUnits.completeunitlist.Count(u=>u!=null)!=units.Count || BattleUnits.completeunitlist.Any(u=>u!=null && !ids.ContainsKey(u));
        void RefreshHostIds()
        {
            var live=new HashSet<Regiment>(BattleUnits.completeunitlist.Where(u=>u!=null));
            foreach(var pair in units.ToArray())if(pair.Value==null || !live.Contains(pair.Value)){ids.Remove(pair.Value);units.Remove(pair.Key);}
            foreach(var unit in live)Id(unit); // Monotonic IDs; deletion never makes an old order target a new unit.
            foreach(var pair in groups.ToArray())if(pair.Value==null || !BattleUnits.armygroups.Contains(pair.Value)){groupIds.Remove(pair.Value);groups.Remove(pair.Key);}
            foreach(var group in BattleUnits.armygroups.Where(g=>g!=null))GroupId(group);
        }
        static float[] ColorData(Color c) => new[]{c.r,c.g,c.b,c.a};
        static Color FromColor(float[] c,int offset=0) => new Color(c[offset],c[offset+1],c[offset+2],c[offset+3]);
        static float[] DateData(Tools.Date d) => d==null?new float[6]:new[]{1f,d.day,d.month,d.year,d.yearfraction,d.hourfraction};
        static Tools.Date FromDate(float[] d,int offset=0)
        {if(d[offset]==0)return null;return new Tools.Date(0,0,0){day=(int)d[offset+1],month=(int)d[offset+2],year=(int)d[offset+3],yearfraction=d[offset+4],hourfraction=d[offset+5]};}
        MpFormationState FormationState(Regiment u)
        {
            var p=u.transform.position;var colors=u.componentcolor;
            return new MpFormationState {Parent=ParentId(u),Side=u.alliance,Tier=u.unittyp,Commander=u.commander,
                Garrison=CanonicalBuildingIndex(u.garrisonreference),ParentRegiment=u.parentregiment==null?0:ReferenceId(u.parentregiment.GetComponent<Regiment>()),
                Camp=BuildingReference(u.campbuilding),Entrenchment=BuildingReference(u.entrenchment),Fort=BuildingReference(u.fortinrange),
                SupplyIip=u.closestiipforsupply==null?0:BattleUnits.iips.IndexOf(u.closestiipforsupply)+1,Telegraph=BuildingReference(u.hastelegraphconnection),
                Active=u.gameObject.activeSelf,Visible=Visible(u),Horse=u.ishorseartillery,Top=u.istopunit,Detached=u.permanentlydetached,
                X=p.x,Y=p.y,Z=p.z,Yaw=u.transform.eulerAngles.y,Colors=ColorData(colors!=null && colors.Length>1?colors[1]:Color.gray).Concat(ColorData(colors!=null && colors.Length>0?colors[0]:Color.gray)).ToArray(),Contract=DateData(u.contractdate)};
        }
        static MpOfficerDates OfficerDates(GameVars.Commander c)
        {
            var dates=new[]{c.dateofbirth,c.dateoffame,c.dateofdefame,c.dateofunavailability}.Concat(c.dateofpromotion??new Tools.Date[0]);
            return new MpOfficerDates{Promotions=c.dateofpromotion?.Length??0,Dates=dates.SelectMany(DateData).ToArray()};
        }
        static void ApplyOfficerDates(GameVars.Commander c,MpOfficerDates d)
        {
            c.dateofbirth=FromDate(d.Dates);c.dateoffame=FromDate(d.Dates,6);c.dateofdefame=FromDate(d.Dates,12);c.dateofunavailability=FromDate(d.Dates,18);
            c.dateofpromotion=Enumerable.Range(0,d.Promotions).Select(i=>FromDate(d.Dates,24+i*6)).ToArray();
        }
        T ReadData<T>(MpStateRow row) where T:new() {var value=new T();DataSchemas[row.Kind].Apply(value,row.Values);return value;}
        void ApplyLifecycleData(MpStateRow[] rows)
        {
            var byKind=rows.GroupBy(r=>r.Kind).ToDictionary(g=>g.Key,g=>g.ToDictionary(r=>r.Id));
            Dictionary<int,MpStateRow> Kind(int k)=>byKind.TryGetValue(k,out var found)?found:new Dictionary<int,MpStateRow>();
            var officerRows=Kind(2);var unitRows=Kind(3);
            var states=Kind(4).ToDictionary(p=>p.Key,p=>ReadData<MpFormationState>(p.Value));
            var dates=Kind(5).ToDictionary(p=>p.Key,p=>ReadData<MpOfficerDates>(p.Value));
            var groupStates=Kind(6).ToDictionary(p=>p.Key,p=>ReadData<MpArmyGroupState>(p.Value));
            if(!officerRows.Keys.OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,officerRows.Count)) || !new HashSet<int>(dates.Keys).SetEquals(officerRows.Keys) || !new HashSet<int>(unitRows.Keys).SetEquals(states.Keys))throw new InvalidDataException("Incomplete lifecycle records.");
            MpLifecycle.Validate(states,officerRows.Count,BattleUnits.cbuildings.Count);
            foreach(int k in new[]{0,1}) {
                var targets=k==0?GameVars.alliance.Cast<object>().ToArray():GameVars.nation.Cast<object>().ToArray();
                if(!Kind(k).Keys.OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,targets.Length).Where(i=>targets[i]!=null)))throw new InvalidDataException("Nation/alliance roster mismatch.");
            }
            foreach(var row in officerRows.Values)if((row.Current!=0 && !states.ContainsKey(row.Current)) || (row.Last!=0 && !states.ContainsKey(row.Last)))throw new InvalidDataException("Unknown officer command reference.");
            foreach(var d in dates.Values)if(d.Promotions<0 || d.Promotions>32 || d.Dates==null || d.Dates.Length!=(4+d.Promotions)*6)throw new InvalidDataException("Invalid officer dates.");
            var members=new HashSet<int>();
            foreach(var g in groupStates.Values) {
                if(g.Commander<0 || g.Commander>=officerRows.Count || g.Members==null || g.Color==null || g.Color.Length!=4)throw new InvalidDataException("Invalid Army Group record.");
                foreach(int id in g.Members)if(!states.ContainsKey(id) || !members.Add(id))throw new InvalidDataException("Invalid Army Group membership.");
            }
            // Validation above is pure; native mutations begin only once the complete batch is checked.
            // Failures from native initialization end the replica session rather than leave it playable.
            hasLiveData=true;
            while(GameVars.commander.Count<officerRows.Count)GameVars.commander.Add(new GameVars.Commander());
            foreach(var row in officerRows.Values){DataSchemas[2].Apply(GameVars.commander[row.Id],row.Values);ApplyOfficerDates(GameVars.commander[row.Id],dates[row.Id]);}
            bool changed=false;
            var removed=units.Where(p=>!states.ContainsKey(p.Key) || p.Value.unittyp!=states[p.Key].Tier).ToArray();
            foreach(var pair in removed) {
                foreach(Transform child in pair.Value.transform.Cast<Transform>().ToArray())if(child.GetComponent<Regiment>()!=null)child.SetParent(BattleUnits.unitfolder.transform,true);
                foreach(var g in BattleUnits.armygroups.Where(g=>g!=null))g.attachedunits.Remove(pair.Value);
                ids.Remove(pair.Value);units.Remove(pair.Key);originalActive.Remove(pair.Value);
                UnityEngine.Object.DestroyImmediate(pair.Value.gameObject);changed=true;
            }
            var created=new List<Regiment>();
            foreach(var pair in states) if(!units.ContainsKey(pair.Key)) {
                var s=pair.Value;
                var go=UnityEngine.Object.Instantiate(Resources.Load("Units/RegimentBlock")) as GameObject;
                if(go==null)throw new InvalidOperationException("Native regiment prefab is unavailable.");
                var u=go.GetComponent<Regiment>();units[pair.Key]=u;ids[u]=pair.Key;
                go.transform.SetParent(BattleUnits.unitfolder.transform,false);go.transform.position=new Vector3(s.X,s.Y,s.Z);go.name=unitRows[pair.Key].Name;
                go.tag=s.Tier<14?"Brigade":s.Tier==14?"Division":s.Tier==15?"Corps":s.Tier==16?"Army":"Untagged";
                u.calledfromcampaign=true;u.unittyp=s.Tier;u.alliance=s.Side;u.ishorseartillery=s.Horse;u.commander=s.Commander;
                u.istopunit=s.Top;u.permanentlydetached=s.Detached;
                DataSchemas[3].Apply(u,unitRows[pair.Key].Values);u.contractdate=FromDate(s.Contract);u.SetUniformColors(FromColor(s.Colors),FromColor(s.Colors,4));
                u.perks=Enumerable.Range(0,s.Tier<=13?1:4).Select(i=>new Regiment.Perk()).ToList();
                if(s.Tier==1)u.mounted=1;
                if(s.Tier==17){u.fleetref=go.AddComponent<Fleet>();u.fleetref.abletomoveonriver=true;u.fleetref.abletomoveonsea=true;}
                created.Add(u);changed=true;
            }
            // Reparent in two phases to support reversed links without transient Unity cycles.
            foreach(var pair in states)if(ParentId(units[pair.Key])!=pair.Value.Parent){units[pair.Key].transform.SetParent(BattleUnits.unitfolder.transform,true);changed=true;}
            foreach(var pair in states) {
                var u=units[pair.Key];var s=pair.Value;
                u.transform.SetParent(s.Parent==0?BattleUnits.unitfolder.transform:units[s.Parent].transform,true);
                u.parentregiment=s.ParentRegiment==0?null:units[s.ParentRegiment].gameObject;
                u.garrisonreference=s.Garrison<0?null:BattleUnits.cbuildings[s.Garrison];
            }
            foreach(var u in created)u.Initialize(u.garrisonreference!=null);
            foreach(var pair in groups.ToArray())if(!groupStates.ContainsKey(pair.Key)){pair.Value.RemoveArmyGroup();groupIds.Remove(pair.Value);groups.Remove(pair.Key);changed=true;}
            // Clear every old membership before assigning any new one. Otherwise a later
            // group's old membership can erase an earlier group's newly assigned member.
            foreach(var g in groups.Values)
                foreach(var u in g.attachedunits.Where(u=>u!=null).ToArray())u.AssignArmyGroup(null);
            foreach(var pair in groupStates) {
                var s=pair.Value;var row=Kind(6)[pair.Key];
                if(!groups.TryGetValue(pair.Key,out var g)) {
                    g=ArmyGroup.CreateNewArmyGroup(EditorFormationOperations.Engine.ArmyGroupPrefab,row.Name,s.Commander,new List<Regiment>(),FromColor(s.Color),s.Override,true);
                    if(g==null)throw new InvalidOperationException("Native Army Group creation failed.");groups[pair.Key]=g;groupIds[g]=pair.Key;changed=true;
                }
                g.name=row.Name;NativeAccess.Set(g,"commanderid",s.Commander);g.overridelev=s.Override;g.SetColor(FromColor(s.Color));
                g.attachedunits=s.Members.Select(id=>units[id]).ToList();
                foreach(var u in g.attachedunits)u.AssignArmyGroup(g);
            }
            foreach(var row in rows.Where(r=>r.Kind<=3)) {
                var target=DataTarget(row);DataSchemas[row.Kind].Apply(target,row.Values);
                if(row.Kind==2){var c=(GameVars.Commander)target;c.currentcommand=row.Current==0?null:units[row.Current];c.lastcommand=row.Last==0?null:units[row.Last];}
                if(row.Kind==3)((Regiment)target).name=row.Name;
            }
            if(GameVars.commander.Count>officerRows.Count)GameVars.commander.RemoveRange(officerRows.Count,GameVars.commander.Count-officerRows.Count);
            foreach(var pair in states.OrderBy(p=>Depth(units[p.Key]))) {
                var u=units[pair.Key];var s=pair.Value;u.istopunit=s.Top;u.permanentlydetached=s.Detached;u.contractdate=FromDate(s.Contract);
                ApplyPose(u,new Vector3(s.X,s.Y,s.Z),Quaternion.Euler(0,s.Yaw,0));originalActive[u]=s.Active;
                replicaVisibility[u]=s.Visible;u.gameObject.SetActive(s.Active);
                if(changed){u.UpdateAllAttachedUnits();NativeAccess.Call(u,"ReferenceGroupsAbove");}
            }
            if(changed) {
                BattleUnits.UpdateUnitList();
                RefreshReplicaRosterUi();
                foreach(var a in GameVars.alliance)if(a!=null){a.unitlist.RemoveAll(u=>u==null || !ids.ContainsKey(u));a.lastmonthunitsformed?.RemoveAll(u=>u==null || !ids.ContainsKey(u));}
                LogDiagnostic("Live lifecycle applied: "+created.Count+" created, "+removed.Length+" removed; formations="+units.Count+", officers="+officerRows.Count);
            }
            // Structural/UI refreshes recompute group caches. Install the authoritative values
            // after those callbacks, before readback; do not suppress divergence verification.
            if(changed)foreach(var row in unitRows.Values)DataSchemas[3].Apply(units[row.Id],row.Values);
        }
    }
}
