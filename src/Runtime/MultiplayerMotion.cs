using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<Regiment,MpVisualPosition> visualMotion=new Dictionary<Regiment,MpVisualPosition>();
        readonly Dictionary<Transform,Vector3> visualLocals=new Dictionary<Transform,Vector3>();
        readonly HashSet<Regiment> visualSpriteMoving=new HashSet<Regiment>();
        Vector3 VisualOffset(Regiment unit)
        {
            if(!IsReplica || unit==null || !ids.TryGetValue(unit,out int id))return Vector3.zero;
            var p=unit.transform.position;
            if(poseHistory.TryGetValue(id,out var history) && history.Evaluate(poseClock.RenderTime(Time.realtimeSinceStartup),hostClock==null || hostClock.paused))
                return new Vector3(history.X-p.x,history.Y-p.y,history.Z-p.z);
            if(!visualMotion.TryGetValue(unit,out var state))visualMotion[unit]=state=new MpVisualPosition();
            state.Sample(p.x,p.y,p.z,Time.realtimeSinceStartup,hostClock==null || hostClock.paused);
            return new Vector3(state.X-p.x,state.Y-p.y,state.Z-p.z);
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
            foreach(var unit in units.Values) {
                if(unit==null)continue;
                var offset=VisualOffset(unit);if(offset.sqrMagnitude<.000001f)continue;
                // Explicit visual children only. Regiment, subordinate formations and
                // block transforms remain at their exact host positions at all times.
                var roots=new List<Transform>{unit.transform.Find("UnitSymbol"),unit.transform.Find("Flag"),unit.transform.Find("Flag2")};
                var label=NativeAccess.Get<Component>(unit,"unitnametext");if(label!=null)roots.Add(label.transform);
                foreach(var root in roots.Where(t=>t!=null).Distinct()) {
                    if(roots.Any(parent=>parent!=null && parent!=root && root.IsChildOf(parent)))continue;
                    visualLocals[root]=root.localPosition;root.position+=offset;
                }
            }
        }
        void ClearMotion(){RestoreMotionSymbols();visualMotion.Clear();visualSpriteMoving.Clear();ClearPoseStream();}
    }
}
