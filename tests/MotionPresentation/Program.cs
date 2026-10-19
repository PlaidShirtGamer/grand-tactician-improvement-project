using GTIP;
using UnityEngine;

int checks=0;
void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;}
Time.frameCount=1;Time.realtimeSinceStartup=100.2f;
var mp=MultiplayerCampaign.instance=new MultiplayerCampaign();var unit=mp.Add(1);
float offset=mp.Offset(unit).x;
for(int i=0;i<100;i++){MultiplayerCampaign.ReplicaDrawBlock(unit,0);MultiplayerCampaign.ReplicaDrawPosition(unit.transform);}
Check(mp.Evaluations==1 && Math.Abs(offset-5)<.005,"All sprite and panel reads share one pose interpolation per unit per frame");
Time.frameCount++;mp.Offset(unit);Check(mp.Evaluations==2,"A new render frame refreshes the interpolation");
mp.NewPose(1,100.2f,20);mp.Offset(unit);Check(mp.Evaluations==3,"A new network pose invalidates a previously rendered frame immediately");
unit.transform.position=new Vector3(2,0,0);mp.Offset(unit);Check(mp.Evaluations==4,"An authoritative logical-position change invalidates a cached offset within the frame");
mp.Paused=true;Check(mp.Offset(unit).x==18 && mp.Evaluations==5,"Pause changes immediately select the final received endpoint");
mp.Paused=false;mp.Draw();int finds=Transform.FindCalls;var symbol=unit.transform.Find("UnitSymbol");
float drawn=symbol.position.x;mp.Draw();
Check(Math.Abs(symbol.position.x-drawn)<.001 && unit.transform.position.x==2,"Repeated drawing preserves logical coordinates and never accumulates visual offsets");
mp.Restore();Check(symbol.localPosition.x==0 && unit.transform.position.x==2,"Rendered children restore their exact native local positions");
finds=Transform.FindCalls;Time.frameCount++;mp.Draw();mp.Restore();
Check(Transform.FindCalls==finds,"Warm symbol rendering reuses prefab references without hierarchy searches");
mp.Hide(unit);Time.frameCount++;mp.Draw();mp.Restore();Check(Transform.FindCalls==finds,"Hidden formations skip symbol drawing work");
mp.Remove(1,unit);Check(mp.Frames==0 && mp.Roots==0,"Deleted lifecycle identities release their cached poses and prefab references");
mp.Add(2);mp.Draw();mp.Clear();Check(mp.Frames==0 && mp.Roots==0 && mp.Evaluations==0,"Session cleanup restores children and clears every rendering cache");

// Exercise the production renderer with synthetic transforms to quantify managed
// bookkeeping only. This is not a Unity frame-rate or native transform benchmark.
mp=MultiplayerCampaign.instance=new MultiplayerCampaign();
for(int i=1;i<=2000;i++)mp.Add(i);
mp.Draw();mp.Restore();finds=Transform.FindCalls;
var timer=System.Diagnostics.Stopwatch.StartNew();long before=GC.GetAllocatedBytesForCurrentThread();
for(int frame=0;frame<60;frame++){Time.frameCount++;mp.Draw();mp.Restore();}
long allocated=GC.GetAllocatedBytesForCurrentThread()-before;timer.Stop();
Check(Transform.FindCalls==finds,"Two thousand moving formations perform no hierarchy searches after cache warmup");
Check(allocated<4096,"Steady rendering avoids per-unit lists, LINQ and iterator allocations");
Check(mp.AllLogicalZero,"Synthetic render workload leaves all authoritative unit transforms untouched");
Console.WriteLine($"Motion presentation: {checks} checks; production renderer / synthetic transforms, 2,000 formations ×60 frames: {timer.Elapsed.TotalMilliseconds:F1} ms, {allocated:N0} managed bytes, no warm hierarchy searches. No Unity frame-rate claim.");

public sealed class GameObject {public bool activeInHierarchy=true;}
public sealed class Regiment:Component
{
    public GameObject gameObject=new GameObject();
    public Component Label;
    public bool immediatespriteplacement;
    public Vector3 GetBlockobjectPosition(int block)=>transform.position;
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new Vector3();
        public float sqrMagnitude=>x*x+y*y+z*z;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static bool operator ==(Vector3 a,Vector3 b)=>a.x==b.x && a.y==b.y && a.z==b.z;
        public static bool operator !=(Vector3 a,Vector3 b)=>!(a==b);
        public override bool Equals(object other)=>other is Vector3 p && this==p;
        public override int GetHashCode()=>HashCode.Combine(x,y,z);
    }
    public class Component {public Transform transform=new Transform();}
    public sealed class Transform
    {
        public static int FindCalls;
        public Transform parent;
        public object Component;
        public readonly Dictionary<string,Transform> Children=new Dictionary<string,Transform>();
        public Vector3 localPosition;
        public Vector3 position {get=>parent==null?localPosition:parent.position+localPosition;set=>localPosition=parent==null?value:value-parent.position;}
        public Transform Find(string name){FindCalls++;return Children.TryGetValue(name,out var child)?child:null;}
        public bool IsChildOf(Transform other){for(var p=parent;p!=null;p=p.parent)if(p==other)return true;return false;}
        public T GetComponent<T>() where T:class=>Component as T;
    }
    public static class Time {public static int frameCount;public static float realtimeSinceStartup;}
}
namespace GTIP
{
    public sealed class MpClock {public bool paused;}
    public sealed class MpMessage {public string kind;public int sequence,baseRevision;public float date;public MpUnit[] units;}
    public sealed class MpUnit {public int id,parent,side,tier;public float x,y,z,yaw;}
    public static class MpRules {public static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);}
    public static class NativeAccess {public static T Get<T>(Regiment unit,string field) where T:class=>unit.Label as T;}
    public sealed partial class MultiplayerCampaign
    {
        public static bool IsReplica=true;
        public static MultiplayerCampaign instance;
        readonly Dictionary<Regiment,int> ids=new Dictionary<Regiment,int>();
        readonly Dictionary<int,Regiment> units=new Dictionary<int,Regiment>();
        readonly Dictionary<Regiment,bool> replicaVisibility=new Dictionary<Regiment,bool>();
        readonly Dictionary<int,MpPoseHistory> poseHistory=new Dictionary<int,MpPoseHistory>();
        readonly MpPoseClock poseClock=new MpPoseClock();
        readonly MpClock hostClock=new MpClock();
        public long Evaluations=>poseEvaluations;
        public int Frames=>motionFrames.Count;
        public int Roots=>motionRoots.Count;
        public bool AllLogicalZero=>units.Values.All(u=>u.transform.position==Vector3.zero);
        public bool Paused{set=>hostClock.paused=value;}
        public Regiment Add(int id)
        {
            var u=new Regiment();u.transform.Component=u;
            foreach(string name in new[]{"UnitSymbol","Flag","Flag2","UnitNameText"})u.transform.Children[name]=new Transform{parent=u.transform};
            u.Label=new Component{transform=u.transform.Children["UnitNameText"]};
            units[id]=u;ids[u]=id;replicaVisibility[u]=true;
            var history=poseHistory[id]=new MpPoseHistory();history.Push(100,0,0,0);history.Push(100.1f,10,0,0);poseClock.Observe(100,100);
            return u;
        }
        public Vector3 Offset(Regiment unit)=>VisualOffset(unit);
        public void NewPose(int id,float time,float x){poseHistory[id].Push(time,x,0,0);poseVersion++;}
        public void Draw()=>DrawMotionSymbols();
        public void Restore()=>RestoreMotionSymbols();
        public void Hide(Regiment unit)=>replicaVisibility[unit]=false;
        public void Remove(int id,Regiment unit){units.Remove(id);ids.Remove(unit);PruneMotionVisuals();}
        public void Clear()=>ClearMotion();
        void ClearPoseStream(){poseHistory.Clear();poseClock.Clear();}
    }
}
