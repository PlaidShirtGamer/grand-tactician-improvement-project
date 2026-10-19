using System.Collections.Generic;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<Regiment,MpVisualPosition> visualMotion=new Dictionary<Regiment,MpVisualPosition>();
        readonly Dictionary<Transform,Vector3> visualLocals=new Dictionary<Transform,Vector3>();
        readonly HashSet<Regiment> visualSpriteMoving=new HashSet<Regiment>();
        sealed class MotionFrame
        {
            public int Frame=-1,Version;
            public bool Paused;
            public Vector3 Logical,Offset;
        }
        readonly Dictionary<int,MotionFrame> motionFrames=new Dictionary<int,MotionFrame>();
        readonly Dictionary<int,Transform[]> motionRoots=new Dictionary<int,Transform[]>();
        int poseVersion;
        long poseEvaluations;
        Vector3 VisualOffset(Regiment unit)
        {
            if(!IsReplica || unit==null || !ids.TryGetValue(unit,out int id))return Vector3.zero;
            var p=unit.transform.position;
            bool paused=hostClock==null || hostClock.paused;
            if(!motionFrames.TryGetValue(id,out var frame))motionFrames[id]=frame=new MotionFrame();
            // Sprites, panel anchors and map picking all ask for the same pose. A
            // new packet, logical-position change or pause invalidates this cache,
            // including when native/script execution order changes within a frame.
            if(frame.Frame==Time.frameCount && frame.Version==poseVersion && frame.Paused==paused &&
                frame.Logical.x==p.x && frame.Logical.y==p.y && frame.Logical.z==p.z)return frame.Offset;
            frame.Frame=Time.frameCount;frame.Version=poseVersion;frame.Paused=paused;frame.Logical=p;poseEvaluations++;
            if(poseHistory.TryGetValue(id,out var history) && history.Evaluate(poseClock.RenderTime(Time.realtimeSinceStartup),paused))
                frame.Offset=new Vector3(history.X-p.x,history.Y-p.y,history.Z-p.z);
            else {
                if(!visualMotion.TryGetValue(unit,out var state))visualMotion[unit]=state=new MpVisualPosition();
                state.Sample(p.x,p.y,p.z,Time.realtimeSinceStartup,paused);
                frame.Offset=new Vector3(state.X-p.x,state.Y-p.y,state.Z-p.z);
            }
            return frame.Offset;
        }
        // Called only from native drawing methods, never from simulation, hit-target
        // validation, construction range checks, order execution or map readback.
        public static Vector3 ReplicaDrawPosition(Transform transform)
        {
            var p=transform.position;
            if(!IsReplica)return p;
            var unit=transform.GetComponent<Regiment>();
            return unit==null?p:p+instance.VisualOffset(unit);
        }
        public static Vector3 ReplicaDrawBlock(Regiment unit,int block) =>
            unit.GetBlockobjectPosition(block)+(IsReplica?instance.VisualOffset(unit):Vector3.zero);
        bool UpdateVisualSprites(Regiment unit)
        {
            bool wasMoving=visualSpriteMoving.Remove(unit),moving=VisualOffset(unit).sqrMagnitude>.000001f;
            if(moving)visualSpriteMoving.Add(unit);
            if(wasMoving && hostClock!=null && hostClock.paused)unit.immediatespriteplacement=true;
            return moving || wasMoving;
        }
        void RestoreMotionSymbols()
        {
            foreach(var pair in visualLocals)if(pair.Key!=null)pair.Key.localPosition=pair.Value;
            visualLocals.Clear();
        }
        void DrawMotionSymbols()
        {
            RestoreMotionSymbols();
            foreach(var pair in units) {
                var unit=pair.Value;
                if(unit==null || !unit.gameObject.activeInHierarchy || replicaVisibility.TryGetValue(unit,out bool visible) && !visible)continue;
                var offset=VisualOffset(unit);if(offset.sqrMagnitude<.000001f)continue;
                // Explicit visual children only. Regiment, subordinate formations and
                // block transforms remain at their exact host positions at all times.
                foreach(var root in MotionRoots(pair.Key,unit)) {
                    if(root==null)continue;
                    visualLocals[root]=root.localPosition;root.position+=offset;
                }
            }
        }
        Transform[] MotionRoots(int id,Regiment unit)
        {
            if(motionRoots.TryGetValue(id,out var cached)) {
                bool destroyed=false;foreach(var root in cached)if(root==null){destroyed=true;break;}
                if(!destroyed)return cached;
            }
            // Native prefab roots and the name TextMesh are assigned in Regiment's
            // initializer and retained across ordinary visual refreshes. Cache only
            // those explicit children; never cache/move a logical formation root.
            var roots=new List<Transform>();
            var candidates=new[]{unit.transform.Find("UnitSymbol"),unit.transform.Find("Flag"),unit.transform.Find("Flag2"),NativeAccess.Get<Component>(unit,"unitnametext")?.transform};
            foreach(var root in candidates) {
                if(root==null || roots.Contains(root))continue;
                bool nested=false;foreach(var parent in candidates)if(parent!=null && parent!=root && root.IsChildOf(parent)){nested=true;break;}
                if(!nested)roots.Add(root);
            }
            return motionRoots[id]=roots.ToArray();
        }
        void PruneMotionVisuals()
        {
            var removed=new List<int>();foreach(int id in motionFrames.Keys)if(!units.ContainsKey(id))removed.Add(id);
            foreach(int id in removed){motionFrames.Remove(id);motionRoots.Remove(id);}
        }
        void ClearMotion(){RestoreMotionSymbols();visualMotion.Clear();visualSpriteMoving.Clear();motionFrames.Clear();motionRoots.Clear();poseVersion=0;poseEvaluations=0;ClearPoseStream();}
    }
}
