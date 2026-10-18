using System;
using System.IO;

namespace GTIP
{
    // Presentation only: interpolate received endpoints, never extrapolate or run
    // pathfinding. A new sample starts at the currently displayed point.
    public sealed class MpVisualPosition
    {
        public float X {get;private set;} public float Y {get;private set;} public float Z {get;private set;}
        float fromX,fromY,fromZ,toX,toY,toZ,started,lastSample,duration;
        bool initialized;
        public void Sample(float x,float y,float z,float now,bool snap)
        {
            if(!MpRules.Finite(x)||!MpRules.Finite(y)||!MpRules.Finite(z)||!MpRules.Finite(now))throw new InvalidDataException("Invalid visual position.");
            Evaluate(now);
            float dx=x-toX,dy=y-toY,dz=z-toZ;
            if(!initialized || snap || dx*dx+dy*dy+dz*dz>40000) {
                initialized=true;X=fromX=toX=x;Y=fromY=toY=y;Z=fromZ=toZ=z;started=lastSample=now;duration=0;return;
            }
            if(x==toX && y==toY && z==toZ)return;
            fromX=X;fromY=Y;fromZ=Z;toX=x;toY=y;toZ=z;
            duration=Math.Max(.1f,Math.Min(1.25f,now-lastSample));started=lastSample=now;
        }
        void Evaluate(float now)
        {
            float t=duration<=0?1:Math.Max(0,Math.Min(1,(now-started)/duration));
            X=fromX+(toX-fromX)*t;Y=fromY+(toY-fromY)*t;Z=fromZ+(toZ-fromZ)*t;
        }
    }
    public sealed class MpPathOrder
    {
        readonly System.Collections.Generic.Dictionary<int,int> samples=new System.Collections.Generic.Dictionary<int,int>();
        public bool Accept(int unit,int sample)
        {
            if(unit<=0 || sample<0)throw new InvalidDataException("Invalid path sample.");
            if(samples.TryGetValue(unit,out int prior) && sample<prior)return false;
            samples[unit]=sample;return true;
        }
        public void Clear()=>samples.Clear();
    }
    public sealed class MpMovementView
    {
        public int Unit,Order;
        public bool Minimized;
        public float X,Y,Z,Seconds;
        public int[] Active,Disabled;
    }
    public static class MpMovementRules
    {
        public static void View(MpMovementView view)
        {
            if(view.Unit<=0 || view.Order<=0 || !MpRules.Finite(view.X) || !MpRules.Finite(view.Y) || !MpRules.Finite(view.Z) ||
                !MpRules.Finite(view.Seconds) || view.Seconds<=0 || view.Active==null || view.Disabled==null ||
                view.Active.Length>8 || view.Disabled.Length>8 || Array.Exists(view.Active,x=>x<0 || x>5) || Array.Exists(view.Disabled,x=>x<0 || x>5))
                throw new InvalidDataException("Invalid native movement panel.");
        }
        public static void Ticket(int unit,int order,int currentUnit,int currentOrder)
        {if(unit<=0 || order<=0 || unit!=currentUnit || order!=currentOrder)throw new InvalidOperationException("Movement options expired; issue a new movement order.");}
    }
}
