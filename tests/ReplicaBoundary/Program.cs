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
NativeAccess.Fail=false;MultiplayerCampaign.IsReplica=true;
var nation=new GameVars.Nation{menfielded=25611};int before=NativeAccess.Calls;
MultiplayerCampaign.ReplicaNationStats(nation,9);
Check(nation.menfielded==25611 && NativeAccess.Calls==before,"Guest recruiting list retains the host men-fielded value instead of locally adding a placeholder soldier");
MultiplayerCampaign.Preview=child;MultiplayerCampaign.ReplicaNationStats(nation,9);
Check(nation.menfielded==25611 && NativeAccess.Calls==before,"A recruitment preview cannot authorize campaign-wide nation recalculation");
MultiplayerCampaign.Preview=null;MultiplayerCampaign.IsReplica=false;MultiplayerCampaign.ReplicaNationStats(nation,9);
Check(nation.menfielded==25612 && NativeAccess.Calls==before+1,"Host and offline recruiting lists retain native nation recalculation");
Check(NativeAccess.LastMethod=="GTIP_Original_ReplicaNationStats" && (int)NativeAccess.LastArguments[0]==9,"The native nation bridge retains the selected state and preserved entry point");
NativeAccess.Fail=true;failed=false;
try{MultiplayerCampaign.ReplicaNationStats(nation,9);}catch(InvalidOperationException){failed=true;}
Check(failed,"Native nation-statistics failures remain visible on the host");
NativeAccess.Fail=false;MultiplayerCampaign.IsReplica=true;before=NativeAccess.Calls;
MultiplayerCampaign.ReplicaNationRecruitData(nation);
Check(nation.Volunteers==1000 && NativeAccess.Calls==before,"Guest recruiting-list refresh cannot recompute host volunteer/draft pools");
MultiplayerCampaign.IsReplica=false;MultiplayerCampaign.ReplicaNationRecruitData(nation);
Check(nation.Volunteers==999 && NativeAccess.LastMethod=="GTIP_Original_ReplicaNationRecruitData" && NativeAccess.LastArguments.Length==0,"Host and offline recruitment pools retain the native parameterless calculation");
MultiplayerCampaign.IsReplica=true;MultiplayerCampaign.instance=new MultiplayerCampaign();var fleet=new Fleet();before=NativeAccess.Calls;
MultiplayerCampaign.ReplicaFleetStats(fleet,true);
Check(NativeAccess.Calls==before && fleet.Cache==42 && fleet.VesselStrength==7,"Viewing a guest naval battle cannot recompute fleet caches or vessel firing-strength data");
MultiplayerCampaign.instance.applyingNaval=true;MultiplayerCampaign.ReplicaFleetStats(fleet,true);
Check(NativeAccess.Calls==before+1 && fleet.Cache==43 && fleet.VesselStrength==8,"Validated naval structural application can rebuild native references before authoritative scalar restoration");
Check((bool)NativeAccess.LastArguments[0] && NativeAccess.LastMethod=="GTIP_Original_ReplicaFleetStats","Fleet-stat refresh retains its native force-update flag and preserved implementation");
MultiplayerCampaign.instance.applyingNaval=false;MultiplayerCampaign.IsReplica=false;MultiplayerCampaign.ReplicaFleetStats(fleet,false);
Check(NativeAccess.Calls==before+2 && !(bool)NativeAccess.LastArguments[0],"Host and offline fleet-cache refreshes keep native behavior");
Console.WriteLine($"Replica boundary: {checks} checks passed using the production bridge and native-operation doubles; not a live Unity test.");

public static class GameVars {public sealed class Nation {public int menfielded;public int Volunteers=1000;}}
public sealed class Fleet {public int Cache=42,VesselStrength=7;}
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
        public static MultiplayerCampaign instance;
        public bool applyingNaval;
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
        public static object Call(Fleet target,string method,params object[] args)
        {Calls++;LastMethod=method;LastArguments=args;target.Cache++;target.VesselStrength++;return null;}
        public static object Call(GameVars.Nation target,string method,params object[] args)
        {
            if(Fail)throw new InvalidOperationException("Native failure");
            Calls++;LastMethod=method;LastArguments=args;
            if(method=="GTIP_Original_ReplicaNationStats")target.menfielded++;else target.Volunteers--;
            return null;
        }
        public static object Call(Regiment target,string method,params object[] args)
        {
            if(Fail)throw new InvalidOperationException("Native failure");
            Calls++;LastTarget=target;LastMethod=method;LastArguments=args;
            foreach(var child in target.Children){child.Morale+=.01f;child.Readiness+=.01f;child.Condition+=.01f;}
            target.GroupMorale=target.Children.Sum(child=>child.Morale);return null;
        }
    }
}
