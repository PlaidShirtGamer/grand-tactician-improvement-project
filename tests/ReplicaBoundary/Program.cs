using GTIP;

int checks=0;
void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var child=new Regiment{Morale=.42f,Readiness=.6f,Condition=.8f};
var parent=new Regiment{Children=new[]{child},GroupMorale=.42f};
MultiplayerCampaign.IsReplica=true;
// A sighting tooltip calls the production bridge through the native aggregate entry.
MultiplayerCampaign.ReplicaGroupStats(parent,true,true,null);
Check(NativeAccess.Calls==0,"Guest tooltip cannot enter the native simulation routine");
Check(child.Morale==.42f && child.Readiness==.6f && child.Condition==.8f,"Host-supplied subordinate morale, readiness and condition survive tooltip refresh");
Check(parent.GroupMorale==.42f,"Guest does not replace host aggregate statistics");
MultiplayerCampaign.Preview=child;
MultiplayerCampaign.ReplicaGroupStats(parent,true,true,null);
Check(NativeAccess.Calls==0,"Selecting a recruiting preview does not permit simulation for another formation");
MultiplayerCampaign.ReplicaGroupStats(child,false,true,parent);
Check(NativeAccess.Calls==1 && ReferenceEquals(NativeAccess.LastTarget,child),"Recruit preview still uses native calculation inside its transaction");
Check((bool)NativeAccess.LastArguments[0]==false && (bool)NativeAccess.LastArguments[1] && ReferenceEquals(NativeAccess.LastArguments[2],parent),"All native aggregate arguments retain their meaning");
MultiplayerCampaign.Preview=null;MultiplayerCampaign.IsReplica=false;
MultiplayerCampaign.ReplicaGroupStats(parent,true,false,child);
Check(NativeAccess.Calls==2 && child.Morale>.42f && child.Readiness>.6f && child.Condition>.8f,"Host and offline campaigns keep native simulation effects");
Check(NativeAccess.LastMethod=="GTIP_Original_ReplicaGroupStats","Bridge invokes the preserved native implementation");
NativeAccess.Fail=true;
bool failed=false;try{MultiplayerCampaign.ReplicaGroupStats(parent,false,false,null);}catch(InvalidOperationException){failed=true;}
Check(failed,"Host native failures remain visible");
Console.WriteLine($"Replica boundary: {checks} checks passed using the production bridge and native-operation doubles; not a live Unity test.");

public sealed class Regiment
{
    public float Morale,Readiness,Condition,GroupMorale;
    public Regiment[] Children=Array.Empty<Regiment>();
}
namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static bool IsReplica;
        public static Regiment Preview;
        public static bool IsRecruitPreview(Regiment unit)=>ReferenceEquals(unit,Preview);
    }
    public static class NativeAccess
    {
        public static int Calls;
        public static Regiment LastTarget;
        public static string LastMethod;
        public static object[] LastArguments;
        public static bool Fail;
        public static object Call(Regiment target,string method,params object[] args)
        {
            if(Fail)throw new InvalidOperationException("Native failure");
            Calls++;LastTarget=target;LastMethod=method;LastArguments=args;
            foreach(var child in target.Children){child.Morale+=.01f;child.Readiness+=.01f;child.Condition+=.01f;}
            target.GroupMorale=target.Children.Sum(child=>child.Morale);return null;
        }
    }
}
