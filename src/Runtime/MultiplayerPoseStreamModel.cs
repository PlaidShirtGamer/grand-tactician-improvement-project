using System;
using System.Collections.Generic;
using System.IO;

namespace GTIP
{
    // Host wall-clock samples, independent of campaign time compression. Rendering
    // follows a short history at the measured cadence, never a catch-up tween.
    public sealed class MpPoseHistory
    {
        struct Point { public float Time,X,Y,Z; }
        readonly Point[] points=new Point[8];
        int count;
        public float X {get;private set;} public float Y {get;private set;} public float Z {get;private set;}
        public bool Push(float time,float x,float y,float z)
        {
            if(!MpRules.Finite(time) || time<0 || !MpRules.Finite(x) || !MpRules.Finite(y) || !MpRules.Finite(z))
                throw new InvalidDataException("Invalid motion sample.");
            if(count>0 && time<points[count-1].Time)return false;
            if(count>0) {
                var p=points[count-1];float dx=x-p.X,dy=y-p.Y,dz=z-p.Z;
                if(dx*dx+dy*dy+dz*dz>40000)count=0;
                else if(time==p.Time){points[count-1]=new Point{Time=time,X=x,Y=y,Z=z};return true;}
            }
            if(count==points.Length){Array.Copy(points,1,points,0,--count);}
            points[count++]=new Point{Time=time,X=x,Y=y,Z=z};
            return true;
        }
        public bool Evaluate(float time,bool paused)
        {
            if(!MpRules.Finite(time))throw new InvalidDataException("Invalid motion render time.");
            if(count==0)return false;
            var a=points[0];var b=points[count-1];
            if(paused || time>=b.Time)a=b;
            else for(int i=1;i<count;i++)if(time<points[i].Time){a=points[i-1];b=points[i];break;}
            float t=a.Time==b.Time?0:Math.Max(0,Math.Min(1,(time-a.Time)/(b.Time-a.Time)));
            X=a.X+(b.X-a.X)*t;Y=a.Y+(b.Y-a.Y)*t;Z=a.Z+(b.Z-a.Z)*t;
            return true;
        }
    }
    public sealed class MpPoseClock
    {
        float offset=float.PositiveInfinity;
        public void Observe(float hostTime,float arrival)
        {
            if(!MpRules.Finite(hostTime) || hostTime<0 || !MpRules.Finite(arrival) || arrival<0)
                throw new InvalidDataException("Invalid motion clock.");
            offset=Math.Min(offset,arrival-hostTime);
        }
        public float RenderTime(float now) => float.IsPositiveInfinity(offset)?0:now-offset-.15f;
        public void Clear()=>offset=float.PositiveInfinity;
    }
    public static class MpMotionRules
    {
        public static void Validate(MpMessage frame,int appliedRevision)
        {
            if(frame==null || frame.kind!="motion" || frame.sequence<=0 || frame.baseRevision<=0 || frame.baseRevision>appliedRevision ||
                !MpRules.Finite(frame.date) || frame.date<0 || frame.units==null || frame.units.Length>20000)
                throw new InvalidDataException("Invalid motion frame generation.");
            var seen=new HashSet<int>();
            foreach(var u in frame.units)
                if(u==null || u.id<=0 || !seen.Add(u.id) || u.parent<0 || u.side<0 || u.side>5 || u.tier<0 || u.tier>18 ||
                    !MpRules.Finite(u.x) || !MpRules.Finite(u.y) || !MpRules.Finite(u.z) || !MpRules.Finite(u.yaw))
                    throw new InvalidDataException("Invalid motion frame identity or coordinates.");
        }
    }
}
