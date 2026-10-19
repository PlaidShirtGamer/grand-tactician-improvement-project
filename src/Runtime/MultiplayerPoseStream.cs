using System.Collections.Generic;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        struct SentPose {public Vector3 Position;public float Yaw,Changed;}
        readonly Dictionary<int,SentPose> sentPoses=new Dictionary<int,SentPose>();
        readonly Dictionary<int,MpPoseHistory> poseHistory=new Dictionary<int,MpPoseHistory>();
        readonly MpPoseClock poseClock=new MpPoseClock();
        float lastMotionSend;
        bool sentMotion,previousMotionPaused;
        int motionSequence,receivedMotionSequence,appliedMotionRevision;
        int KnownMotionParent(Regiment unit)
        {
            for(var node=unit.transform.parent;node!=null && node!=BattleUnits.unitfolder.transform;node=node.parent) {
                var parent=node.GetComponent<Regiment>();
                if(parent!=null)return ids.TryGetValue(parent,out int id)?id:-1;
            }
            return 0;
        }
        void SendMotion()
        {
            if(snapshotRoster.Revision==0)return;
            float now=Time.realtimeSinceStartup;bool paused=GameVars.gamepaused;
            bool edge=!sentMotion || previousMotionPaused!=paused;
            if(!edge && (paused || now-lastMotionSend<.1f))return;
            // Never let a presentation stream fill the reliable command/data queue.
            // Retain unsent endpoints for the next frame instead of marking them sent.
            if(transport.PendingOutgoing>8)return;
            var samples=new List<MpUnit>();
            foreach(var pair in units) {
                var u=pair.Value;if(u==null || !u.gameObject.activeInHierarchy || !Visible(u))continue;
                int parent=KnownMotionParent(u);
                if(!snapshotRoster.Contains(pair.Key,parent,u.alliance,u.unittyp))continue;
                var p=u.transform.position;float yaw=u.transform.eulerAngles.y;
                if(!MpRules.Finite(p.x) || !MpRules.Finite(p.y) || !MpRules.Finite(p.z) || !MpRules.Finite(yaw))continue;
                bool changed=!sentPoses.TryGetValue(pair.Key,out var prior) || p!=prior.Position || yaw!=prior.Yaw;
                // A short stationary tail brackets the exact stopping point.
                if(!edge && !changed && now-prior.Changed>.35f)continue;
                sentPoses[pair.Key]=new SentPose{Position=p,Yaw=yaw,Changed=changed?now:prior.Changed};
                samples.Add(new MpUnit{id=pair.Key,parent=parent,side=u.alliance,tier=u.unittyp,x=p.x,y=p.y,z=p.z,yaw=yaw});
            }
            if(samples.Count>0 || edge)Send(new MpMessage{kind="motion",session=session,sequence=++motionSequence,
                baseRevision=snapshotRoster.Revision,date=now,paused=paused,units=samples.ToArray()});
            lastMotionSend=now;previousMotionPaused=paused;sentMotion=true;
        }
        void RecordPose(int id,float time,float x,float y,float z)
        {
            if(!poseHistory.TryGetValue(id,out var history))poseHistory[id]=history=new MpPoseHistory();
            history.Push(time,x,y,z);
        }
        void ReceiveMotion(MpMessage value)
        {
            MpMotionRules.Validate(value,appliedMotionRevision);
            if(value.sequence<=receivedMotionSequence)return;
            receivedMotionSequence=value.sequence;poseClock.Observe(value.date,Time.realtimeSinceStartup);
            foreach(var sample in value.units) {
                // A frame can wait behind a newer lifecycle transaction. A removed,
                // reparented or retyped formation must not acquire the old visual pose.
                if(!units.TryGetValue(sample.id,out var u) || u==null || u.alliance!=sample.side || u.unittyp!=sample.tier || KnownMotionParent(u)!=sample.parent)continue;
                if(!replicaVisibility.TryGetValue(u,out bool visible) || !visible)continue;
                RecordPose(sample.id,value.date,sample.x,sample.y,sample.z);
            }
        }
        void RecordReliableMotion(MpStateRow[] rows,float capturedAt,int revision)
        {
            appliedMotionRevision=revision;
            poseClock.Observe(capturedAt,Time.realtimeSinceStartup);
            foreach(var row in rows)if(row.Kind==4 && !row.Deleted) {
                var state=ReadData<MpFormationState>(row);
                if(state.Visible)RecordPose(row.Id,capturedAt,state.X,state.Y,state.Z);
            }
            var removed=new List<int>();foreach(int id in poseHistory.Keys)if(!units.ContainsKey(id))removed.Add(id);
            foreach(int id in removed)poseHistory.Remove(id);
        }
        void ClearPoseStream()
        {
            sentPoses.Clear();poseHistory.Clear();poseClock.Clear();lastMotionSend=0;sentMotion=false;
            previousMotionPaused=false;motionSequence=receivedMotionSequence=appliedMotionRevision=0;
        }
    }
}
