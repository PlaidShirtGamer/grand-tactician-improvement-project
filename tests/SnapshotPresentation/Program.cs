using GTIP;

int checks=0;
void Check(bool valid,string label){if(!valid)throw new Exception(label);checks++;}
void Reject(Action action,string label){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,label);}
var mp=new MultiplayerCampaign();
// Native state CSV at revision570: the guest-recruited inactive artillery unit and
// its inactive parent were in agreement immediately before the failing revision.
mp.Add(493,0,13,false,new Position(1608.921f,491.585236f,-732.6804f));
mp.Add(1973,0,2,false,new Position(3012.37256f,491.585236f,1566.302f));
mp.Add(9000,1,14,true,new Position(1200,400,-900));
var view=new MpMessage{campaign="manifest",sequence=574,units=new[]{
    new MpUnit{id=493,side=0,tier=13,x=1609.5f,y=491.585236f,z=-733,controllable=true},
    new MpUnit{id=1973,side=0,tier=2,x=3012.87256f,y=491.585236f,z=1566.802f,targetX=100,targetZ=-100,moving=true}}};
Reject(()=>mp.Accept(view),"A map view cannot precede authoritative campaign data");
mp.DataReady=true;Check(mp.AuditLogical(),"Observed revision570 positions start at the verified campaign-data baseline");
mp.OldSnapshotWrite(view);Check(!mp.AuditLogical(),"Reproduce the former failure: a newer same-roster view writes coordinates outside the older baseline tolerance");
mp.RestoreDataBaseline();mp.Accept(view);
Check(mp.AuditLogical(),"Production view application leaves the next scalar-only revision's logical readback valid");
Check(mp.PositionOf(1973)==new Position(3012.37256f,491.585236f,1566.302f),"Newer snapshot coordinates cannot move the inactive recruited child");
Check(mp.PositionOf(493)==new Position(1608.921f,491.585236f,-732.6804f),"Newer snapshot coordinates cannot move its inactive parent");
Check(!mp.IsActive(1973) && !mp.IsActive(493),"Map visibility preserves native inactive hierarchy nodes");
Check(mp.Visible(1973) && mp.Visible(493) && !mp.Visible(9000),"The complete view still updates friendly and missing-enemy visibility");
Check(mp.IsActive(9000),"An unseen parent keeps its native activation for visible descendants");
Check(ReferenceEquals(mp.Current,view) && mp.Current.units[0].controllable && mp.Current.units[1].moving && mp.Current.units[1].targetX==100,"Current controllability and movement-target metadata remain available to order UI");
Check(mp.Status.Contains("2 formations") && mp.Status.Contains("running"),"The guest status still reflects the received map view");
for(int revision=575;revision<580;revision++){view.sequence=revision;view.units[1].x+=.75f;mp.Accept(view);Check(mp.AuditLogical(),"Repeated newer views cannot create a second logical writer before scalar-only data revision "+revision);}
view.paused=true;mp.Accept(view);Check(mp.AuditLogical() && mp.Status.Contains("paused"),"Paused map views retain metadata without moving logical formations");
view.units=view.units.Append(new MpUnit{id=9000,side=1,tier=14,x=900,y=400,z=-800}).ToArray();mp.Accept(view);
Check(mp.Visible(9000) && mp.PositionOf(9000)==new Position(1200,400,-900),"Reappearing enemies become visible while keeping their authoritative campaign-data pose");
var prior=mp.Current;
Reject(()=>mp.Accept(new MpMessage{campaign="changed",units=view.units}),"Another campaign manifest cannot install a view");
Reject(()=>mp.Accept(new MpMessage{campaign="manifest",units=new[]{new MpUnit{id=9999,side=0,tier=2}}}),"A not-yet-created lifecycle identity cannot install a view");
Reject(()=>mp.Accept(new MpMessage{campaign="manifest",units=new[]{new MpUnit{id=1973,side=1,tier=2}}}),"Another side cannot reuse a formation identity");
Reject(()=>mp.Accept(new MpMessage{campaign="manifest",units=new[]{new MpUnit{id=1973,side=0,tier=1}}}),"A changed formation type requires authoritative lifecycle application");
Reject(()=>mp.Accept(new MpMessage{campaign="manifest",units=new[]{view.units[0],view.units[0]}}),"Duplicate map-view identities remain invalid");
Check(mp.AuditLogical() && mp.Visible(9000),"Rejected views leave verified logical positions and the prior visibility state intact");
mp.ApplyDataPose(1973,new Position(3000,491.585236f,1500));mp.Accept(view);
Check(mp.AuditLogical() && mp.PositionOf(1973)==new Position(3000,491.585236f,1500),"A genuine campaign-data pose change remains authoritative after further views");
Console.WriteLine($"Snapshot pose ownership: {checks} checks pass using the production view adapter and native-operation doubles, including the observed formation1973/493 coordinates. No live Unity state exercised.");

public readonly record struct Position(float X,float Y,float Z);
public sealed class NativeObject
{
    public bool activeSelf;
    public void SetActive(bool value)=>activeSelf=value;
}
public sealed class Regiment
{
    public int alliance,unittyp;
    public Position Position;
    public NativeObject gameObject=new NativeObject();
}
namespace GTIP
{
    public sealed class MpUnit {public int id,side,tier;public float x,y,z,targetX,targetZ;public bool moving,controllable;}
    public sealed class MpMessage {public string campaign;public int sequence;public bool paused;public MpUnit[] units;}
    public sealed partial class MultiplayerCampaign
    {
        bool hasLiveData;
        string manifest="manifest",message;
        readonly Dictionary<int,Regiment> units=new Dictionary<int,Regiment>();
        readonly Dictionary<Regiment,bool> replicaVisibility=new Dictionary<Regiment,bool>();
        readonly Dictionary<Regiment,bool> originalActive=new Dictionary<Regiment,bool>();
        readonly Dictionary<int,Position> baseline=new Dictionary<int,Position>();
        public MpMessage Current;
        public bool DataReady{set=>hasLiveData=value;}
        public string Status=>message;
        public void Add(int id,int side,int type,bool active,Position pose)
        {var unit=new Regiment{alliance=side,unittyp=type,Position=pose};units[id]=unit;unit.gameObject.SetActive(active);originalActive[unit]=active;baseline[id]=pose;}
        public void Accept(MpMessage value){ApplyReplica(value);Current=value;}
        public Position PositionOf(int id)=>units[id].Position;
        public bool IsActive(int id)=>units[id].gameObject.activeSelf;
        public bool Visible(int id)=>replicaVisibility[units[id]];
        public void OldSnapshotWrite(MpMessage value){foreach(var pose in value.units)units[pose.id].Position=new Position(pose.x,pose.y,pose.z);}
        public bool AuditLogical()=>units.All(pair=>Math.Abs(pair.Value.Position.X-baseline[pair.Key].X)<=.1f && Math.Abs(pair.Value.Position.Z-baseline[pair.Key].Z)<=.1f);
        public void RestoreDataBaseline(){foreach(var pair in units)pair.Value.Position=baseline[pair.Key];}
        public void ApplyDataPose(int id,Position pose){units[id].Position=pose;baseline[id]=pose;}
    }
}
