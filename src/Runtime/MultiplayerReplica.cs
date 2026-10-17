using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        string manifest;
        readonly Dictionary<Regiment, Vector3> originalPositions = new Dictionary<Regiment, Vector3>();
        readonly Dictionary<Regiment, Quaternion> originalRotations = new Dictionary<Regiment, Quaternion>();
        readonly Dictionary<Regiment, bool> originalActive = new Dictionary<Regiment, bool>();
        readonly Dictionary<Regiment,bool> replicaVisibility=new Dictionary<Regiment,bool>();
        public static bool ReplicaHidden(bool native,Regiment unit) => IsReplica && instance!=null && instance.replicaVisibility.TryGetValue(unit,out bool visible)?!visible:native;
        bool leavingReplica;
        public static bool IsReplica => instance != null && ((instance.transport != null && !instance.host && instance.replicaReady) || instance.leavingReplica);
        public static void ReplicaSpeed(float newspeed, bool overrideminmax, bool manualset)
        {
            if(IsReplica)return;
            var mp=instance;
            if(mp!=null && mp.transport!=null && mp.host) {
                if(manualset && !mp.blockPanelResume)mp.panelPause.ManualPause(newspeed==0);
                if(mp.blockPanelResume && !manualset && newspeed!=0)return;
                if(mp.phase=="sending" && newspeed>0)return;
                if(mp.newsBarrier.Waiting) {
                    if(manualset && newspeed==0)mp.newsResumeSpeed=0;
                    if(newspeed!=0)return;
                }
            }
            NativeAccess.Call(typeof(GameVars), "GTIP_Original_ReplicaSpeed", newspeed, overrideminmax, manualset);
        }
        public static void ReplicaAutocalc(Autocalc target)
        {if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaAutocalc");}
        public static void ReplicaSave(bool saveunits, bool saveobjects, bool savecommanders, bool savecampaigndata, bool isautosave, string manualsavename)
        {
            // Saving belongs to the host; a guest has only partially replicated campaign data.
            if(!IsReplica) NativeAccess.Call(typeof(SceneManagement),"GTIP_Original_ReplicaSave",saveunits,saveobjects,savecommanders,savecampaigndata,isautosave,manualsavename);
        }
        public static void ReplicaCampaign(CampaignController target)
        { if (!IsReplica) NativeAccess.Call(target, "GTIP_Original_ReplicaCampaign"); }
        public static void ReplicaUnits(BattleUnits target)
        { if (!IsReplica) NativeAccess.Call(target, "GTIP_Original_ReplicaUnits"); }
        public static void ReplicaTown(Town target)
        { if(!IsReplica || !NativeAccess.Get<bool>(target,"initialized"))NativeAccess.Call(target,"GTIP_Original_ReplicaTown"); }
        public static void ReplicaIip(IIP target)
        {
            if(!IsReplica || !NativeAccess.Get<bool>(target,"initialized"))NativeAccess.Call(target,"GTIP_Original_ReplicaIip");
            else if(Camera_Battlefield.IsCamUIUpdated())NativeAccess.Call(target,"UpdatePaperMapFacing");
        }
        public static void ReplicaBuilding(CBuilding target)
        { if(!IsReplica || !NativeAccess.Get<bool>(target,"initialized"))NativeAccess.Call(target,"GTIP_Original_ReplicaBuilding"); }
        public static void ReplicaGroupExperience(ArmyGroup target)
        { if(!IsReplica)NativeAccess.Call(target,"GTIP_Original_ReplicaGroupExperience"); }
        public static void ReplicaRegiment(Regiment target)
        {
            if (!IsReplica) { NativeAccess.Call(target, "GTIP_Original_ReplicaRegiment"); return; }
            if(IsRecruitPreview(target))return;
            // Deliberately skip movement, supply, combat, recruitment and AI. Keep native rendering.
            NativeAccess.Call(target, "CheckRegimentVisibility");
            target.UpdateRegimentSpritePos();
            NativeAccess.Call(target, "UpdateSpriteAnimatorAll", -1, false);
            if((Time.frameCount+target.GetInstanceID())%30==0)target.UpdateUnitVisuals();
            // This native presentation refresh is otherwise inside the suppressed simulation loop.
            if((Time.frameCount+target.GetInstanceID())%30==0)NativeAccess.Call(target,"UpdatePaperMapFlagTooltipInfo");
            if((Time.frameCount+target.GetInstanceID())%15==0 && target.blockobjectcurrentpath!=null && target.blockobjectcurrentpath.Length>0) {
                target.ShowPathArrows();
                // This native display method also clears an expired delay. The host owns
                // that scalar, so retain it while updating only the native map symbols.
                float delay=target.totaltimeoforderdelay;
                try {NativeAccess.Call(target,"UpdateOnMapSymbols");}
                finally {target.totaltimeoforderdelay=delay;}
            }
        }
        string[] manifestKeys;
        float checkpointDate;
        void BuildManifest()
        {
            var records = BattleUnits.completeunitlist.Where(u => u != null).Select(u => new { Unit=u, Key=ReplicaKey(u) }).ToArray();
            manifestKeys = MpIdentity.Order(records.Select(p=>p.Key).ToArray());
            var byKey = records.ToDictionary(p=>p.Key,p=>p.Unit,StringComparer.Ordinal);
            ids.Clear(); units.Clear(); nextId=0;
            foreach(var key in manifestKeys) Id(byKey[key]);
            InitializeGroupIds();
            // Scene sibling indices and non-formation containers are not saved identities.
            manifest = MpIdentity.Hash(manifestKeys);
            checkpointDate = GameVars.GetCampaignFloatingDate();
        }
        string ReplicaKey(Regiment unit) => MpIdentity.Segment(unit.alliance,unit.unittyp,unit.commander,unit.name);
        readonly Dictionary<Regiment, Transform> originalParents = new Dictionary<Regiment, Transform>();
        readonly Dictionary<Regiment, int> originalSiblingIndices = new Dictionary<Regiment, int>();
        MpUnit[] HierarchyManifest()
        {
            if(BattleUnits.completeunitlist.Count(u=>u!=null)!=ids.Count || BattleUnits.completeunitlist.Any(u=>u!=null && !ids.ContainsKey(u)))
                throw new InvalidOperationException("Formation roster changed. New/deleted formations are not synchronized yet; reload a shared checkpoint.");
            return units.OrderBy(p=>p.Key).Select(p=>MpIdentity.HierarchyRecord(manifestKeys,p.Key,ParentId(p.Value))).ToArray();
        }
        int ParentId(Regiment unit)
        {
            for(var node=unit.transform.parent;node!=null && node!=BattleUnits.unitfolder.transform;node=node.parent) {
                var parent=node.GetComponent<Regiment>();
                if(parent!=null) { if(ids.TryGetValue(parent,out var id)) return id; throw new InvalidOperationException("Formation "+unit.name+" has an unknown session parent: "+parent.name); }
            }
            return 0;
        }
        void ApplyHierarchy(MpUnit[] records)
        {
            MpIdentity.ValidateHierarchy(records,manifestKeys);
            int changed=0;
            // Detach first so reversing a valid parent relationship cannot create a transient cycle.
            foreach(var record in records) if(ParentId(units[record.id])!=record.parent) {
                units[record.id].transform.SetParent(BattleUnits.unitfolder.transform,true); changed++;
            }
            foreach(var record in records) if(ParentId(units[record.id])!=record.parent)
                units[record.id].transform.SetParent(record.parent==0?BattleUnits.unitfolder.transform:units[record.parent].transform,true);
            if(changed>0) LogDiagnostic("Applied host hierarchy: "+changed+" formation parent(s) differed after loading.");
        }
        void SaveReplicaState()
        {
            SaveClock();
            originalPositions.Clear(); originalRotations.Clear(); originalActive.Clear(); originalParents.Clear(); originalSiblingIndices.Clear();
            foreach(var unit in units.Values) { originalPositions[unit]=unit.transform.position; originalRotations[unit]=unit.transform.rotation; originalActive[unit]=unit.gameObject.activeSelf; originalParents[unit]=unit.transform.parent; originalSiblingIndices[unit]=unit.transform.GetSiblingIndex(); }
        }
        static int Depth(Regiment unit) { int n=0; for(var t=unit.transform.parent;t!=null;t=t.parent)n++; return n; }
        void RestoreReplicaState()
        {
            RestoreClock();
            foreach(var pair in originalParents) if(pair.Key!=null) pair.Key.transform.SetParent(null,true);
            foreach(var pair in originalParents) if(pair.Key!=null) pair.Key.transform.SetParent(pair.Value,true);
            foreach(var pair in originalSiblingIndices.OrderBy(p=>p.Value)) if(pair.Key!=null) pair.Key.transform.SetSiblingIndex(pair.Value);
            originalParents.Clear(); originalSiblingIndices.Clear();
            // Parents first: restoring a parent also moves children; children then receive absolute positions.
            foreach(var entry in originalPositions.Where(p=>p.Key!=null).OrderBy(p=>Depth(p.Key))) {
                ApplyPose(entry.Key,entry.Value,originalRotations[entry.Key]);
                entry.Key.gameObject.SetActive(originalActive[entry.Key]);
            }
            originalPositions.Clear(); originalRotations.Clear(); originalActive.Clear();
        }
        void ApplyReplica(MpMessage value)
        {
            if(value.campaign!=manifest) throw new InvalidOperationException("Host campaign manifest changed; reload the same checkpoint on both clients.");
            var seen=new HashSet<int>();
            foreach(var state in value.units) {
                if(!seen.Add(state.id) || !units.TryGetValue(state.id,out var unit) || unit==null || unit.alliance!=state.side || unit.unittyp!=state.tier)
                    throw new InvalidOperationException("Snapshot formation does not match the loaded checkpoint: "+state.id);
            }
            foreach(var state in value.units.OrderBy(s=>Depth(units[s.id]))) {
                var unit=units[state.id]; ApplyPose(unit,new Vector3(state.x,state.y,state.z),Quaternion.Euler(0,state.yaw,0));
            }
            foreach(var pair in units) {
                // Full host snapshots determine which native formations are visible. Never retain stale enemies.
                if(pair.Value!=null) {
                    replicaVisibility[pair.Value]=seen.Contains(pair.Key);
                    // An invisible parent's transform still carries visible subordinate units.
                    // Visibility belongs to renderers/icons, not the whole hierarchy node.
                    pair.Value.gameObject.SetActive(originalActive[pair.Value]);
                }
            }
            message="Native map synchronized | "+value.units.Length+" formations | host "+(value.paused?"paused":"running");
        }
        static void ApplyPose(Regiment unit,Vector3 position,Quaternion rotation)
        {
            var oldPosition=unit.transform.position; var deltaRotation=rotation*Quaternion.Inverse(unit.transform.rotation);
            var blocks=unit.blockobject;
            var positions=blocks==null?new Vector3[0]:blocks.Select(b=>b==null?Vector3.zero:b.transform.position).ToArray();
            var rotations=blocks==null?new Quaternion[0]:blocks.Select(b=>b==null?Quaternion.identity:b.transform.rotation).ToArray();
            unit.SetPosition(position); unit.SetRotation(rotation);
            if(blocks!=null) for(int i=0;i<blocks.Length;i++) if(blocks[i]!=null) {
                var p=position+deltaRotation*(positions[i]-oldPosition); var q=deltaRotation*rotations[i];
                blocks[i].transform.position=p; blocks[i].transform.rotation=q;
                unit.SetBlockobjectPosition(i,p); unit.SetBlockobjectRotation(i,q);
            }
            unit.immediatespriteplacement=true;
        }
    }
}
