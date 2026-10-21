using GTIP;

int checks=0;
void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var child=new Regiment{Morale=.42f,Readiness=.6f,Condition=.8f};
var parent=new Regiment{Children=new[]{child},GroupMorale=.42f};
var draft=new Regiment();
var mp=new MultiplayerCampaign();mp.Register(child,18);mp.Register(parent,610);
MultiplayerCampaign.IsReplica=true;
// A sighting tooltip calls the production bridge through the native aggregate entry.
MultiplayerCampaign.ReplicaGroupStats(parent,true,true,null);
Check(NativeAccess.Calls==0,"Guest tooltip cannot enter the native simulation routine");
Check(child.Morale==.42f && child.Readiness==.6f && child.Condition==.8f,"Host-supplied subordinate morale, readiness and condition survive tooltip refresh");
Check(parent.GroupMorale==.42f,"Guest does not replace host aggregate statistics");
MultiplayerCampaign.Preview=draft;
MultiplayerCampaign.ReplicaGroupStats(parent,true,true,null);
Check(NativeAccess.Calls==0,"Selecting a recruiting preview does not permit simulation for another formation");
MultiplayerCampaign.ReplicaGroupStats(draft,false,true,parent);
Check(NativeAccess.Calls==1 && ReferenceEquals(NativeAccess.LastTarget,draft),"The independent unregistered recruit preview still uses native calculation inside its transaction");
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

// Reproduce the native aggregate -> campaign stats -> hidden recruitment transfer
// side effect that moved formation 18 in the reported v0.35 session. The creation
// flag used to authorize every formation, including unrelated authoritative groups.
mp=new MultiplayerCampaign();MultiplayerCampaign.IsReplica=true;mp.BuildingPreview=true;
var hidden=new Regiment {Active=false,Morale=.42f,Readiness=.6f,Condition=.8f,X=1273.537f,Y=493.841522f,Z=-1075,
    TransferX=1273.537f,TransferY=493.841522f,TransferZ=-1075,TransferTargetX=1206,TransferTargetY=493.7893f,TransferTargetZ=-1075,TransferStep=.63063f};
var unrelated=new Regiment {Children=new[]{hidden},X=1206,Y=493.7893f,Z=-1075};
mp.Register(hidden,18);mp.Register(unrelated,610);
bool OldWildcard(Regiment unit)=>ReferenceEquals(unit,MultiplayerCampaign.Preview) || mp.BuildingPreview;
Check(OldWildcard(unrelated),"The former creation wildcard reproduces permission to simulate an unrelated registered group");
var savedScalars=new[]{hidden.Morale,hidden.Readiness,hidden.Condition};
NativeAccess.Call(unrelated,"GTIP_Original_ReplicaGroupStats",true,true,null);
hidden.Morale=savedScalars[0];hidden.Readiness=savedScalars[1];hidden.Condition=savedScalars[2];
Check(MathF.Abs(hidden.X-1272.90637f)<.001f && MathF.Abs(hidden.Y-493.841064f)<.001f && hidden.Z==-1075 && unrelated.X==1206,
    "Native hidden-unit MoveTowards reproduces the observed position drift while its parent remains stationary");
Check(hidden.Morale==.42f && hidden.Readiness==.6f && hidden.Condition==.8f && hidden.X!=1273.537f,
    "Restoring transaction scalar fields does not undo a native transfer write to the logical transform");
hidden.X=hidden.TransferX=1273.537f;hidden.Y=hidden.TransferY=493.841522f;hidden.Z=hidden.TransferZ=-1075;
before=NativeAccess.Calls;
MultiplayerCampaign.ReplicaGroupStats(unrelated,true,true,null);
Check(NativeAccess.Calls==before && hidden.X==1273.537f && hidden.Y==493.841522f,
    "The actual production identity predicate blocks registered aggregate simulation during preview creation and preserves the host pose");
Check(!MultiplayerCampaign.IsRecruitPreview(hidden) && !MultiplayerCampaign.IsRecruitPreview(unrelated),
    "Building a draft never grants preview identity to registered parent or child formations");
var creating=new Regiment();MultiplayerCampaign.Preview=null;
MultiplayerCampaign.ReplicaGroupStats(creating,true,true,null);
Check(NativeAccess.Calls==before+1 && ReferenceEquals(NativeAccess.LastTarget,creating),
    "An unregistered native draft can initialize before nativePreview has been assigned");
Check(!MultiplayerCampaign.IsRecruitPreview(null),"A null target cannot be authorized by the creation flag");
MultiplayerCampaign.ReplicaGroupStats(null,true,true,null);
Check(NativeAccess.Calls==before+1,"The guest aggregate bridge ignores a null target during preview creation");
mp.BuildingPreview=false;MultiplayerCampaign.Preview=unrelated;
MultiplayerCampaign.ReplicaGroupStats(unrelated,true,true,null);
Check(NativeAccess.Calls==before+1 && !MultiplayerCampaign.IsRecruitPreview(unrelated),
    "An accidental nativePreview reference cannot turn a registered formation into an editable draft");
MultiplayerCampaign.Preview=creating;MultiplayerCampaign.ReplicaGroupStats(creating,false,true,null);
Check(NativeAccess.Calls==before+2,"An explicitly owned unregistered draft retains native preview calculation after construction");
MultiplayerCampaign.Preview=null;MultiplayerCampaign.ReplicaGroupStats(creating,false,true,null);
Check(NativeAccess.Calls==before+2,"A closed draft no longer authorizes native aggregate simulation");
MultiplayerCampaign.IsReplica=false;mp.BuildingPreview=true;MultiplayerCampaign.ReplicaGroupStats(unrelated,true,true,null);
Check(NativeAccess.Calls==before+3 && hidden.X<1273.537f,"Host and offline native transfers remain active regardless of preview identity restrictions");
Console.WriteLine($"Replica boundary: {checks} checks passed using the production bridge and native-operation doubles; not a live Unity test.");

public static class GameVars {public sealed class Nation {public int menfielded;public int Volunteers=1000;}}
public sealed class Fleet {public int Cache=42,VesselStrength=7;}
public sealed class Regiment
{
    public float Morale,Readiness,Condition,GroupMorale;
    public bool Active=true;
    public float X,Y,Z,TransferX,TransferY,TransferZ,TransferTargetX,TransferTargetY,TransferTargetZ,TransferStep;
    public Regiment[] Children=Array.Empty<Regiment>();
    public void UpdateTransferTime()
    {
        if(TransferStep<=0)return;
        float dx=TransferTargetX-TransferX,dy=TransferTargetY-TransferY,dz=TransferTargetZ-TransferZ;
        float distance=MathF.Sqrt(dx*dx+dy*dy+dz*dz),step=MathF.Min(distance,TransferStep);
        if(distance>0){TransferX+=dx/distance*step;TransferY+=dy/distance*step;TransferZ+=dz/distance*step;}
        if(!Active){X=TransferX;Y=TransferY;Z=TransferZ;}
    }
}
namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static bool IsReplica;
        public static MultiplayerCampaign instance;
        public bool applyingNaval;
        public MultiplayerCampaign(){instance=this;}
        readonly Dictionary<Regiment,int> ids=new Dictionary<Regiment,int>();
        Regiment nativePreview;
        bool buildingPreview;
        public bool BuildingPreview {get=>buildingPreview;set=>buildingPreview=value;}
        public static Regiment Preview {get=>instance?.nativePreview;set{if(instance==null)instance=new MultiplayerCampaign();instance.nativePreview=value;}}
        public void Register(Regiment unit,int id){ids[unit]=id;}
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
            foreach(var child in target.Children)if(child.Active || (bool)args[1]){child.Morale+=.01f;child.Readiness+=.01f;child.Condition+=.01f;child.UpdateTransferTime();}
            target.GroupMorale=target.Children.Sum(child=>child.Morale);return null;
        }
    }
}
