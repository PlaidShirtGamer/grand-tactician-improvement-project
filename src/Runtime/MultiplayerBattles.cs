using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        // Native records are mirrored for native viewing code. StartLandBattle,
        // AddTopUnit and every combat cycle are deliberately never run by a guest.
        readonly Dictionary<BattleMonument,int> battleIds=new Dictionary<BattleMonument,int>();
        readonly Dictionary<int,BattleMonument> worldBattles=new Dictionary<int,BattleMonument>();
        readonly MpBattleSourceIds<Autocalc> battleAudioIds=new MpBattleSourceIds<Autocalc>();
        readonly Dictionary<int,Autocalc> worldBattleAudio=new Dictionary<int,Autocalc>();
        readonly Dictionary<Autocalc,List<BattleEvent>> battleEvents=new Dictionary<Autocalc,List<BattleEvent>>();
        readonly MpBattleEventCursor battleEventCursor=new MpBattleEventCursor();
        int nextBattle,battleEventSerial;
        bool battlesBound;
        sealed class BattleEvent {public int Serial,Kind,Purpose;public bool Active;public Vector3 Position;}
        BattlefieldSetup BattleSetup => EditorFormationOperations.Engine.GetComponent<BattlefieldSetup>();
        int BattleReference(BattleMonument b)=>b!=null && battleIds.TryGetValue(b,out int id)?id:0;
        BattleMonument BattleById(int id)=>worldBattles.TryGetValue(id,out var b)?b:null;
        void ClearBattles()
        {
            foreach(var a in worldBattleAudio.Values)if(a!=null){StopReplicaBattleAudio(a);Destroy(a.gameObject);}
            worldBattleAudio.Clear();battleAudioIds.Clear();battleIds.Clear();worldBattles.Clear();battleEvents.Clear();battleEventCursor.Clear();nextBattle=0;battleEventSerial=0;battlesBound=false;
        }
        int[] BattleUnitIds(IEnumerable<Regiment> list)=>(list??Enumerable.Empty<Regiment>()).Where(u=>u!=null).Select(ReferenceId).ToArray();
        Regiment[] BattleUnitsById(int[] list)
        {
            if(list==null || list.Any(id=>id<=0 || !units.ContainsKey(id)))throw new InvalidDataException("Battle references an unavailable formation.");
            return list.Select(id=>units[id]).ToArray();
        }
        static int[] BattleDate(Tools.Date date)=>date==null?null:new[]{date.day,date.month,date.year};
        static Tools.Date BattleDate(int[] date)
        {
            if(date==null)return null;
            if(date.Length!=3 || date[0]<0 || date[0]>31 || date[1]<0 || date[1]>12 || date[2]<0 || date.Any(v=>v==0) && date.Any(v=>v!=0))throw new InvalidDataException("Invalid battle date.");
            return new Tools.Date(date[0],date[1],date[2]);
        }
        void CaptureBattles(List<MpStateRow> rows)
        {
            var live=BattleSetup.battlemonument.Where(b=>b!=null).ToArray();var set=new HashSet<BattleMonument>(live);
            foreach(var pair in worldBattles.ToArray())if(pair.Value==null || !set.Contains(pair.Value)){battleIds.Remove(pair.Value);worldBattles.Remove(pair.Key);}
            foreach(var b in live) {
                if(!battleIds.TryGetValue(b,out int id)){id=++nextBattle;battleIds[b]=id;worldBattles[id]=b;}
                var a=b.autocalcref;var p=b.position;
                rows.Add(DataRow(34,id,new MpBattleRecord{Key=b.name,Active=a!=null,X=p.x,Y=p.y,Z=p.z,From=BattleDate(b.datefrom),To=BattleDate(b.dateto),
                    Top0=BattleUnitIds(b.autocalc_topunitsparticipatingside0),Top1=BattleUnitIds(b.autocalc_topunitsparticipatingside1),UnitSides=a?.envolvedunits?.Length??0,FleetSides=a?.envolvedfleets?.Length??0}));
                rows.Add(DataRow(35,id,b));
                if(a==null)continue;
                rows.Add(DataRow(36,id,a));
                for(int side=0;side<2;side++) {
                    var u=a.envolvedunits!=null && side<a.envolvedunits.Length?a.envolvedunits[side]:null;
                    var f=a.envolvedfleets!=null && side<a.envolvedfleets.Length?a.envolvedfleets[side]:null;
                    var top=u==null?Array.Empty<int>():Enumerable.Range(0,u.topunits.Count).Where(i=>DispatchReference(u.topunits[i])!=0).ToArray();
                    var single=u==null?Array.Empty<int>():Enumerable.Range(0,u.singleunitlist.Count).Where(i=>DispatchReference(u.singleunitlist[i])!=0 && i<u.topunitref.Count && DispatchReference(u.topunitref[i])!=0).ToArray();
                    var ships=f==null?Array.Empty<int>():Enumerable.Range(0,f.singlevessels.Count).Where(i=>f.singlevessels[i]!=null && vesselIds.ContainsKey(f.singlevessels[i]) && i<f.singleshipregref.Count && DispatchReference(f.singleshipregref[i])!=0).ToArray();
                    rows.Add(DataRow(37,id*2+side,new MpBattleSide {Top=top.Select(i=>ReferenceId(u.topunits[i])).ToArray(),Single=single.Select(i=>ReferenceId(u.singleunitlist[i])).ToArray(),Parent=single.Select(i=>ReferenceId(u.topunitref[i])).ToArray(),
                        Arrival=single.Select(i=>u.arrivaltime.ElementAtOrDefault(i)).ToArray(),Arrived=top.Select(i=>u.topunitsarrived.ElementAtOrDefault(i)).ToArray(),Withdrawn=single.Select(i=>u.withdrawn.ElementAtOrDefault(i)).ToArray(),
                        Ammo=single.Select(i=>u.ammoinfo.ElementAtOrDefault(i)).ToArray(),TopCasualties=top.Select(i=>u.topunitcasualties.ElementAtOrDefault(i)).ToArray(),SiegeStrength=u?.siegestrength??0,
                        Fleets=BattleUnitIds(f?.regrefs),Vessels=ships.Select(i=>vesselIds[f.singlevessels[i]]).ToArray(),
                        VesselParents=ships.Select(i=>ReferenceId(f.singleshipregref[i])).ToArray(),Sunken=f?.sunkships??0,SunkenNames=f?.sunkshipsnames?.ToArray()??Array.Empty<string>(),SunkenTypes=f?.sunkshipstypes?.ToArray()??Array.Empty<int>()}));
                }
                CaptureBattlePresentation(rows,id,a);
            }
            CaptureBattleAudio(rows);
            foreach(var a in battleEvents.Keys.ToArray())if(a==null || !live.Any(b=>b.autocalcref==a) && !battleAudioIds.Sources.ContainsKey(a))battleEvents.Remove(a);
        }
        void CaptureBattleAudio(List<MpStateRow> rows)
        {
            // Native minor raids/skirmishes, bombardments and interceptions are
            // attached to Regiment objects rather than battle monuments.
            battleAudioIds.Update(units.Values.Where(u=>u!=null).SelectMany(u=>u.GetComponents<Autocalc>()).Where(a=>a!=null && a.GetComponent<BattleMonument>()==null),()=>checked(++nextBattle));
            foreach(var pair in battleAudioIds.Sources) {
                var a=pair.Key;var p=a.transform.position;
                rows.Add(DataRow(39,pair.Value,new MpBattleAudioSource{Type=a.type,X=p.x,Y=p.y,Z=p.z}));
                CaptureBattlePresentation(rows,pair.Value,a);
            }
        }
        void CaptureBattlePresentation(List<MpStateRow> rows,int id,Autocalc a)
        {
                var status=a.statusicons??new List<Autocalc.StatusIcons>();
                var events=battleEvents.TryGetValue(a,out var e)?e:new List<BattleEvent>();
                var soundengine=NativeAccess.Get<SoundEngine>(a,"soundengine");var sounds=NativeAccess.Get<int[]>(a,"soundobjects");
                var objects=soundengine==null?null:NativeAccess.Get<IList>(soundengine,"soundobject");
                var loops=objects==null || sounds==null?Array.Empty<int>():BattleLoopPurposes(a.type).Where(p=>sounds[p]>=0 && sounds[p]<objects.Count).ToArray();
                rows.Add(DataRow(38,id,new MpBattlePresentation {Log=NativeAccess.Get<List<string>>(a,"log")?.ToArray()??Array.Empty<string>(),
                    LoopPurpose=loops,LoopActive=loops.Select(p=>NativeAccess.Get<bool>(objects[sounds[p]],"permanentlyactive")).ToArray(),
                    StatusSide=status.Select(s=>s.side).ToArray(),StatusKind=status.Select(s=>s.statusref).ToArray(),StatusUnit=status.Select(s=>DispatchReference(s.relatedunit)).ToArray(),
                    StatusVessel=status.Select(s=>s.relatedvessel!=null && vesselIds.TryGetValue(s.relatedvessel,out int v)?v:0).ToArray(),StatusSunken=status.Select(s=>s.sunkenshipid).ToArray(),
                    StatusTime=status.Select(s=>s.triggeredtime).ToArray(),StatusDuration=status.Select(s=>s.triggerduration).ToArray(),
                    EventSerial=events.Select(s=>s.Serial).ToArray(),EventKind=events.Select(s=>s.Kind).ToArray(),EventPurpose=events.Select(s=>s.Purpose).ToArray(),EventActive=events.Select(s=>s.Active).ToArray(),
                    EventX=events.Select(s=>s.Position.x).ToArray(),EventY=events.Select(s=>s.Position.y).ToArray(),EventZ=events.Select(s=>s.Position.z).ToArray()}));
        }
        void ApplyBattles(MpStateRow[] rows,MpStateRow[] changes)
        {
            if(!changes.Any(r=>r.Kind>=34 && r.Kind<=39))return;
            var records=rows.Where(r=>r.Kind==34).ToDictionary(r=>r.Id,ReadData<MpBattleRecord>);
            var metadata=rows.Where(r=>r.Kind==35).ToDictionary(r=>r.Id);
            var states=rows.Where(r=>r.Kind==36).ToDictionary(r=>r.Id);
            var sides=rows.Where(r=>r.Kind==37).ToDictionary(r=>r.Id,ReadData<MpBattleSide>);
            var presentation=rows.Where(r=>r.Kind==38).ToDictionary(r=>r.Id,ReadData<MpBattlePresentation>);
            if(!new HashSet<int>(records.Keys).SetEquals(metadata.Keys))throw new InvalidDataException("Incomplete native battle roster.");
            if(!battlesBound) {
                var existing=BattleSetup.battlemonument.Where(b=>b!=null).ToList();
                foreach(var pair in records) {
                    var s=pair.Value;var match=existing.FirstOrDefault(b=>b.name==s.Key && Mathf.Abs(b.position.x-s.X)<0.1f && Mathf.Abs(b.position.z-s.Z)<0.1f);
                    if(match==null)continue;existing.Remove(match);worldBattles[pair.Key]=match;battleIds[match]=pair.Key;
                }
                foreach(var stale in existing)stale.Remove();
                battlesBound=true;
            }
            foreach(var pair in worldBattles.ToArray())if(!records.ContainsKey(pair.Key)) {
                if(pair.Value!=null){battleIds.Remove(pair.Value);pair.Value.Remove();}worldBattles.Remove(pair.Key);
            }
            foreach(var pair in records) {
                int id=pair.Key;var s=pair.Value;
                if(id<=0 || id>int.MaxValue/2 || string.IsNullOrEmpty(s.Key) || s.UnitSides<0 || s.UnitSides>2 || s.FleetSides<0 || s.FleetSides>2)throw new InvalidDataException("Invalid native battle identity.");
                if(!worldBattles.TryGetValue(id,out var b) || b==null) {
                    int land=(int)metadata[id].Values[Array.FindIndex(DataSchemas[35].Fields,f=>f.Name=="landorsea")];
                    if(land<0 || land>=EditorFormationOperations.Engine.preloadedmodels.Monuments.Length)throw new InvalidDataException("Battle monument prefab unavailable.");
                    b=Instantiate(EditorFormationOperations.Engine.preloadedmodels.Monuments[land]).GetComponent<BattleMonument>();
                    b.Add(checkfordoublename:false,manualobjname:s.Key);BattleSetup.battlemonument.Add(b);worldBattles[id]=b;battleIds[b]=id;
                }
                DataSchemas[35].Apply(b,metadata[id].Values);b.name=s.Key;b.position=new Vector3(s.X,s.Y,s.Z);b.transform.position=b.position;
                b.datefrom=BattleDate(s.From);b.dateto=BattleDate(s.To);b.autocalc_topunitsparticipatingside0=BattleUnitsById(s.Top0).ToList();b.autocalc_topunitsparticipatingside1=BattleUnitsById(s.Top1).ToList();
                if(!s.Active) {if(b.autocalcref!=null){StopReplicaBattleAudio(b.autocalcref);Destroy(b.autocalcref);}b.autocalcref=null;b.UpdateAllianceWon();continue;}
                if(!states.ContainsKey(id) || !presentation.ContainsKey(id) || !sides.ContainsKey(id*2) || !sides.ContainsKey(id*2+1))throw new InvalidDataException("Incomplete active battle record.");
                var a=b.autocalcref;if(a==null){a=b.gameObject.AddComponent<Autocalc>();b.autocalcref=a;LogDiagnostic("Native replica battle created: "+id+" "+b.battlename);}
                DataSchemas[36].Apply(a,states[id].Values);a.mapposition=b.position;
                a.envolvedunits=s.UnitSides==0?null:new Autocalc.EnvolvedUnits[s.UnitSides];
                a.envolvedfleets=s.FleetSides==0?null:new Autocalc.EnvolvedFleets[s.FleetSides];
                for(int side=0;side<2;side++)ApplyBattleSide(a,side,sides[id*2+side]);
                ApplyBattlePresentation(id,a,presentation[id]);
                b.UpdateAllianceWon();
            }
            ApplyBattleAudio(rows,presentation);
            BattleSetup.UpdateBattleMonumentVisibility();
        }
        void ApplyBattleAudio(MpStateRow[] rows,Dictionary<int,MpBattlePresentation> presentation)
        {
            var records=rows.Where(r=>r.Kind==39).ToDictionary(r=>r.Id,ReadData<MpBattleAudioSource>);
            foreach(var pair in worldBattleAudio.ToArray())if(!records.ContainsKey(pair.Key)) {
                if(pair.Value!=null){StopReplicaBattleAudio(pair.Value);Destroy(pair.Value.gameObject);}worldBattleAudio.Remove(pair.Key);
            }
            foreach(var pair in records) {
                int id=pair.Key;var s=pair.Value;
                if(id<=0 || worldBattles.ContainsKey(id) || s.Type<0 || s.Type>6 || !presentation.ContainsKey(id))throw new InvalidDataException("Invalid standalone battle sound source.");
                if(!worldBattleAudio.TryGetValue(id,out var a) || a==null) {
                    var go=new GameObject("GTIP battle sounds "+id);go.transform.SetParent(BattleSetup.transform,true);a=go.AddComponent<Autocalc>();worldBattleAudio[id]=a;
                }
                if(NativeAccess.Get<SoundEngine>(a,"soundengine")!=null && a.type!=s.Type) {
                    StopReplicaBattleAudio(a);Destroy(a.gameObject);var go=new GameObject("GTIP battle sounds "+id);go.transform.SetParent(BattleSetup.transform,true);a=go.AddComponent<Autocalc>();worldBattleAudio[id]=a;
                }
                a.type=s.Type;a.mapposition=new Vector3(s.X,s.Y,s.Z);a.transform.position=a.mapposition;
                ApplyBattlePresentation(id,a,presentation[id]);
            }
        }
        void ApplyBattleSide(Autocalc a,int side,MpBattleSide s)
        {
            if(a.envolvedunits!=null && side<a.envolvedunits.Length) {
                var top=BattleUnitsById(s.Top);var single=BattleUnitsById(s.Single);var parent=BattleUnitsById(s.Parent);
                if(s.Arrival==null || s.Withdrawn==null || s.Ammo==null || s.Arrived==null || s.TopCasualties==null || single.Length!=parent.Length || single.Length!=s.Arrival.Length || single.Length!=s.Withdrawn.Length || single.Length!=s.Ammo.Length || top.Length!=s.Arrived.Length || top.Length!=s.TopCasualties.Length)throw new InvalidDataException("Invalid battle army participation.");
                var u=new Autocalc.EnvolvedUnits(null,a.mapposition){topunits=top.ToList(),singleunitlist=single.ToList(),topunitref=parent.ToList(),arrivaltime=s.Arrival.ToList(),withdrawn=s.Withdrawn.ToList(),ammoinfo=s.Ammo.ToList(),topunitsarrived=s.Arrived.ToList(),topunitcasualties=s.TopCasualties.ToList(),siegestrength=s.SiegeStrength};
                a.envolvedunits[side]=u;
            }
            if(a.envolvedfleets!=null && side<a.envolvedfleets.Length) {
                var fleet=BattleUnitsById(s.Fleets);var parents=BattleUnitsById(s.VesselParents);
                if(s.Vessels==null || s.Vessels.Any(id=>!vessels.ContainsKey(id)) || s.Vessels.Length!=parents.Length || fleet.Any(u=>u.fleetref==null) || s.Sunken<0 || s.SunkenNames==null || s.SunkenTypes==null || s.Sunken!=s.SunkenNames.Length || s.Sunken!=s.SunkenTypes.Length)throw new InvalidDataException("Invalid battle fleet participation.");
                var f=new Autocalc.EnvolvedFleets(null){regrefs=fleet.ToList(),fleetrefs=fleet.Select(u=>u.fleetref).ToList(),shiprefs=fleet.Select(u=>u.GetComponent<Ship>()).ToList(),
                    singlevessels=s.Vessels.Select(id=>vessels[id]).ToList(),singleshipregref=parents.ToList(),singlevesselshipref=parents.Select(u=>u.shownmodel).ToList(),sunkships=s.Sunken,sunkshipsnames=s.SunkenNames.ToList(),sunkshipstypes=s.SunkenTypes.ToList()};
                a.envolvedfleets[side]=f;
            }
        }
        static void BattleColumns(int count,params Array[] columns)
        {if(columns.Any(v=>v==null || v.Length!=count))throw new InvalidDataException("Invalid battle presentation columns.");}
        void ApplyBattlePresentation(int id,Autocalc a,MpBattlePresentation p)
        {
            if(p.StatusSide==null || p.EventSerial==null || p.Log==null || p.LoopPurpose==null)throw new InvalidDataException("Missing battle presentation.");
            BattleColumns(p.LoopPurpose.Length,p.LoopActive);
            if(p.LoopPurpose.Distinct().Count()!=p.LoopPurpose.Length || p.LoopPurpose.Any(purpose=>!BattleLoopPurposes(a.type).Contains(purpose)))throw new InvalidDataException("Invalid battle sound loop.");
            BattleColumns(p.StatusSide.Length,p.StatusKind,p.StatusUnit,p.StatusVessel,p.StatusSunken,p.StatusTime,p.StatusDuration);
            BattleColumns(p.EventSerial.Length,p.EventKind,p.EventPurpose,p.EventActive,p.EventX,p.EventY,p.EventZ);
            NativeAccess.Set(a,"log",p.Log.ToList());a.statusicons=new List<Autocalc.StatusIcons>();
            for(int i=0;i<p.StatusSide.Length;i++) {
                if(p.StatusSide[i]<0 || p.StatusSide[i]>1)throw new InvalidDataException("Invalid battle status side.");
                UnityEngine.Object target=null;
                if(p.StatusUnit[i]!=0 && units.TryGetValue(p.StatusUnit[i],out var unit))target=unit;
                if(p.StatusVessel[i]!=0 && vessels.TryGetValue(p.StatusVessel[i],out var vessel))target=vessel;
                var icon=new Autocalc.StatusIcons(p.StatusSide[i],p.StatusKind[i],target,p.StatusDuration[i]){triggeredtime=p.StatusTime[i],sunkenshipid=p.StatusSunken[i]};a.statusicons.Add(icon);
            }
            ReplicaAutocalcPresentation(a);
            for(int i=0;i<p.LoopPurpose.Length;i++)PlayBattleSound(a,p.LoopPurpose[i],false,p.LoopActive[i],default(Vector3));
            foreach(int i in battleEventCursor.Take(id,p.EventSerial,incomingDataSerial==1)) {
                var pos=new Vector3(p.EventX[i],p.EventY[i],p.EventZ[i]);
                if(p.EventKind[i]==0)NativeAccess.Call(a,"GTIP_Original_ReplicaBattleExplosion",pos);
                else PlayBattleSound(a,p.EventPurpose[i],p.EventKind[i]==2,p.EventActive[i],pos);
            }
        }
        void ReplicaAutocalcPresentation(Autocalc a)
        {
            if(a!=null && NativeAccess.Get<SoundEngine>(a,"soundengine")==null && GameObject.Find("SoundEngine")!=null) {
                NativeAccess.Call(a,"InitSounds");
                // Native raids emit SingleExplosion but leave its sound array
                // unassigned. A standalone replica must allocate this purpose,
                // rather than accidentally use another object's array at zero.
                if(a.type==1 || a.type==6) {
                    var engine=NativeAccess.Get<SoundEngine>(a,"soundengine");var sounds=NativeAccess.Get<int[]>(a,"soundobjects");
                    sounds[Regiment.csound_explosion]=engine.AddPurposeArray(a.gameObject,Regiment.csound_explosion,20);
                }
            }
        }
        void RefreshGuestBattleControls()
        {
            var panel=AutoresolvePanel.autoresolvepanel;
            if(panel==null || !panel.isActiveAndEnabled)return;
            // Manual play sends a validated intent to the host. Assault and
            // retreat retain their existing native guest mutation guards.
            if(panel.BT_PlayManually!=null && AutoresolvePanel.autocalcrefopen!=null)
                panel.BT_PlayManually.SetState(TacticalManualEligible(AutoresolvePanel.autocalcrefopen,guestSide)?0:2);
            foreach(var button in new[]{panel.BT_AutoAssault,panel.BT_Retreat})if(button!=null) {
                button.SetState(2);button.ShowTooltipForDeactivated(true);
                button.ExchangeTooltipText("Host battle command","The host controls retreat and assault during an active battle.");
            }
        }
        void AddBattleEvent(Autocalc a,int kind,int purpose,bool active,Vector3 position)
        {
            if(a==null)return;
            if(!battleEvents.TryGetValue(a,out var events))battleEvents[a]=events=new List<BattleEvent>();
            events.Add(new BattleEvent{Serial=checked(++battleEventSerial),Kind=kind,Purpose=purpose,Active=active,Position=position});if(events.Count>128)events.RemoveAt(0);
        }
        public static void ReplicaBattleExplosion(Autocalc target,Vector3 position)
        {
            NativeAccess.Call(target,"GTIP_Original_ReplicaBattleExplosion",position);
            var mp=instance;if(mp!=null && mp.host && mp.transport!=null && mp.phase=="live" && NativeAccess.Get<SoundEngine>(target,"soundengine")!=null)mp.AddBattleEvent(target,0,0,true,position);
        }
        public static void ReplicaBattleStart(Autocalc target)
        {if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaBattleStart");}
        public static void ReplicaBattleMonument(BattleMonument target)
        {if(TacticalHolding)return;if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaBattleMonument");else NativeAccess.Call(target,"UpdateBalanceBar");}
        public static void ReplicaAutocalcRetreat(Autocalc target,bool playerside)
        {if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaAutocalcRetreat",playerside);}
        public static void ReplicaAutocalcStance(Autocalc target,int stance,bool playerside)
        {if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaAutocalcStance",stance,playerside);}
        public static void ReplicaAutocalcManual(Autocalc target,bool launchedbyplayer)
        {if(TacticalHolding)return;if(IsReplica){if(launchedbyplayer)instance?.RequestTacticalManual(target);return;}NativeAccess.Call(target,"GTIP_Original_ReplicaAutocalcManual",launchedbyplayer);}
        public static void ReplicaBattleSound(SoundEngine target,int soundobjectid,bool activate,bool checktimer)
        {
            var mp=instance;bool observe=mp!=null && mp.host && mp.transport!=null && mp.phase=="live";
            var objects=observe?NativeAccess.Get<IList>(target,"soundobject"):null;
            bool before=observe && soundobjectid>=0 && soundobjectid<objects.Count && NativeAccess.Get<bool>(objects[soundobjectid],"active");
            NativeAccess.Call(target,"GTIP_Original_ReplicaBattleSound",soundobjectid,activate,checktimer);
            if(observe && before!=NativeAccess.Get<bool>(objects[soundobjectid],"active"))CaptureBattleSound(target,soundobjectid,false,activate,default(Vector3));
        }
        public static void ReplicaBattleSoundArray(SoundEngine target,int soundobjectarrayid,bool activate,Vector3 positionofplay)
        {
            var mp=instance;bool observe=mp!=null && mp.host && mp.transport!=null && mp.phase=="live";
            var arrays=observe?NativeAccess.Get<IList>(target,"soundobjectarray"):null;
            float before=observe && soundobjectarrayid>=0 && soundobjectarrayid<arrays.Count?NativeAccess.Get<float>(arrays[soundobjectarrayid],"timelastclipplayed"):-1;
            NativeAccess.Call(target,"GTIP_Original_ReplicaBattleSoundArray",soundobjectarrayid,activate,positionofplay);
            if(observe && (!activate || before!=NativeAccess.Get<float>(arrays[soundobjectarrayid],"timelastclipplayed")))CaptureBattleSound(target,soundobjectarrayid,true,activate,positionofplay);
        }
        static void CaptureBattleSound(SoundEngine target,int index,bool array,bool active,Vector3 pos)
        {
            var mp=instance;if(mp==null || !mp.host || mp.transport==null || mp.phase!="live")return;
            int nativeSlot=index;
            var objects=NativeAccess.Get<IList>(target,"soundobject");
            if(array) {var arrays=NativeAccess.Get<IList>(target,"soundobjectarray");if(index<0 || index>=arrays.Count)return;index=NativeAccess.Get<int>(arrays[index],"startsoundobjectid");}
            if(index<0 || index>=objects.Count)return;
            var sound=objects[index];int purpose=NativeAccess.Get<int>(sound,"purposeid");
            var go=NativeAccess.Get<GameObject>(sound,"oldgo")??NativeAccess.Get<GameObject>(sound,"go");
            // Several Autocalcs may share a regiment object. Their native sound
            // slots identify the owner more precisely than GetComponent's first.
            var a=go==null?null:go.GetComponents<Autocalc>().FirstOrDefault(candidate=> {
                var slots=NativeAccess.Get<int[]>(candidate,"soundobjects");return slots!=null && purpose>=0 && purpose<slots.Length && slots[purpose]==nativeSlot;
            });
            if(a==null)return;
            // The explosion event already carries its accompanying array sound.
            if(purpose==Regiment.csound_explosion)return;
            mp.AddBattleEvent(a,array?2:1,purpose,active,pos);
        }
        static void PlayBattleSound(Autocalc a,int purpose,bool array,bool active,Vector3 pos)
        {
            var engine=NativeAccess.Get<SoundEngine>(a,"soundengine");var sounds=NativeAccess.Get<int[]>(a,"soundobjects");
            if(engine==null || sounds==null || purpose<0 || purpose>=sounds.Length)return;
            if(array)engine.ActivateArray(sounds[purpose],active,pos);else engine.Activate(sounds[purpose],active);
        }
        static void StopReplicaBattleAudio(Autocalc a)
        {
            foreach(int purpose in BattleLoopPurposes(a.type))PlayBattleSound(a,purpose,false,false,default(Vector3));
        }
        static int[] BattleLoopPurposes(int type)=>type==0?new[]{Regiment.csound_armyfighting}:type==2?new[]{Regiment.csound_armybesieging,Regiment.csound_armyassaulting}:Array.Empty<int>();
    }
}
