using GTIP;

if(args.Length<1 || args.Length>2)throw new ArgumentException("Expected original Assembly-CSharp.dll and optional Managed directory.");
int checks=0;
void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;Console.WriteLine("PASS "+reason);}
void Reject(Action action,string reason){bool failed=false;try{action();}catch(System.IO.InvalidDataException){failed=true;}Check(failed,reason);}
NativeSelectorChecks.Run(args[0],Check,args.Length==2?args[1]:Path.GetDirectoryName(Path.GetFullPath(args[0])));

var unit=new Regiment{NativeValue=18};var client=new MultiplayerCampaign(unit);
MultiplayerCampaign.IsReplica=false;MultiplayerCampaign.ReplicaMapStatusIcon(unit);
Check(unit.OriginalCalls==1 && client.Handler.Rendered==18,"Offline/host rendering retains the unmodified native texture path");
unit.NativeValue=25;Check(client.Capture(unit)==25 && unit.GetterCalls==1,"Host capture reads the extracted native status even without a rendered panel");
MultiplayerCampaign.IsReplica=true;unit.NativeValue=18;
client.Receive(1,25);
Check(client.Handler.Rendered==25 && unit.Camp==null && unit.Entrenchment==null,"The guest draws the host's entrenchment icon despite absent transient building references");
int original=unit.OriginalCalls;MultiplayerCampaign.ReplicaMapStatusIcon(unit);MultiplayerCampaign.ReplicaMapStatusIcon(unit);
Check(client.Handler.Rendered==25 && unit.OriginalCalls==original,"Periodic native icon refreshes cannot overwrite the authoritative crown with a local stance chevron");
int getters=unit.GetterCalls;
Check(client.Capture(unit)==25 && unit.GetterCalls==getters,"Guest readback retains the received presentation index rather than recomputing from incomplete local references");
client.Receive(1,28);Check(client.Handler.Rendered==28,"Subsequent host priority changes replace the displayed native status");
client.Handler.multiplepanel[0].go.Active=false;client.Receive(1,11);
Check(!client.Handler.multiplepanel[0].go.Active && client.Handler.Rendered==11,"Texture synchronization does not reveal a hidden map icon");
unit.uniticonid=-1;int renders=client.Handler.Renders;client.Receive(1,23);
Check(client.Handler.Renders==renders,"Formations without a current map panel still retain their authoritative status");
unit.uniticonid=0;MultiplayerCampaign.ReplicaMapStatusIcon(unit);
Check(client.Handler.Rendered==23,"A later map panel refresh uses the cached host status");
client.Handler.multiplepanel[0].secondaryreference=new GameObject();renders=client.Handler.Renders;client.Receive(1,24);
Check(client.Handler.Renders==renders,"Reused native panel slots cannot be written through a stale formation icon index");
client.Handler.multiplepanel[0].secondaryreference=unit.gameObject;
Reject(()=>client.Receive(1,29),"Unknown high status indices are rejected");
Reject(()=>client.Receive(1,-1),"Negative status indices are rejected");
Reject(()=>client.Receive(999,25),"Status updates require a registered lifecycle identity");
client.Handler.UnitStatusTextures=new int[20];
Reject(()=>MultiplayerCampaign.ReplicaMapStatusIcon(unit),"A changed native texture table fails explicitly instead of indexing unrelated art");
client.Handler.UnitStatusTextures=Enumerable.Range(0,29).ToArray();
client.Delete(1);Check(client.Cached==0,"Lifecycle deletion releases its presentation status");
Reject(()=>client.Capture(unit),"Guest capture cannot fabricate a missing authoritative map status");
client.Receive(1,25);client.ClearSession();Check(client.Cached==0,"Session teardown clears all map status identities");

var formation=new MpFormationState{Commander=0,Tier=14,Garrison=-1,Colors=new float[8],Contract=new float[6],StatusTexture=25};
var formations=new Dictionary<int,MpFormationState>{{1,formation}};
MpLifecycle.Validate(formations,1,0);
Check(true,"Lifecycle validation accepts the native entrenchment status range");
formation.StatusTexture=29;Reject(()=>MpLifecycle.Validate(formations,1,0),"Lifecycle preflight rejects status indices outside the audited native table");
formation.StatusTexture=-1;Reject(()=>MpLifecycle.Validate(formations,1,0),"Lifecycle preflight rejects negative map status values");
Console.WriteLine($"Map status icons: {checks} checks; actual native selector metadata/interpreter and production replica adapter. Unity rendering is not exercised.");

public sealed class GameObject {public bool Active=true;}
public sealed class Regiment
{
    public int uniticonid,NativeValue=18,OriginalCalls,GetterCalls;public object Camp,Entrenchment;
    public GameObject gameObject=new();public PanelHandler panelhandler;
}
public sealed class PanelHandler
{
    public sealed class Panel {public GameObject go=new(),secondaryreference;}
    public List<Panel> multiplepanel=new();public int[] UnitStatusTextures=Enumerable.Range(0,29).ToArray(),UnitStatusTexturesHighlighted=Enumerable.Range(100,29).ToArray();
    public int Rendered,Renders;
    public void UpdateButtonPic(Panel panel,int button,int normal,int highlighted,int grey)
    {if(button!=183 || normal!=grey || highlighted!=normal+100)throw new Exception("Native status sprite channel mismatch");Rendered=normal;Renders++;}
}
namespace GTIP
{
    public sealed class MpStateRow {public int Id,Kind;public bool Deleted;public object State;}
    public sealed class MpUnit {public int id,parent,side,tier;}
    public static class MpRules {public static bool Finite(float value)=>float.IsFinite(value);}
    public sealed partial class MultiplayerCampaign
    {
        static MultiplayerCampaign instance;public static bool IsReplica;
        readonly Dictionary<int,Regiment> units=new();readonly Dictionary<Regiment,int> ids=new();
        public PanelHandler Handler {get;}public int Cached=>mapStatusIcons.Count;
        public MultiplayerCampaign(Regiment unit)
        {instance=this;units[1]=unit;ids[unit]=1;Handler=unit.panelhandler=new PanelHandler();Handler.multiplepanel.Add(new PanelHandler.Panel{secondaryreference=unit.gameObject});}
        public void Receive(int id,int status)=>ApplyMapStatus(new[]{new MpStateRow{Kind=4,Id=id,State=new MpFormationState{StatusTexture=status}}});
        public void Delete(int id)=>ApplyMapStatus(new[]{new MpStateRow{Kind=4,Id=id,Deleted=true}});
        public int Capture(Regiment unit)=>NativeMapStatusIndex(unit);
        public void ClearSession()=>ClearMapStatus();
        static T ReadData<T>(MpStateRow row)=>(T)row.State;
    }
    static class NativeAccess
    {
        public static object Call(object value,string method,params object[] args)
        {
            var unit=(Regiment)value;
            if(method=="GTIP_NativeMapStatusIndex"){unit.GetterCalls++;return unit.NativeValue;}
            if(method=="GTIP_Original_ReplicaMapStatusIcon") {
                unit.OriginalCalls++;
                if(unit.uniticonid>=0)unit.panelhandler.UpdateButtonPic(unit.panelhandler.multiplepanel[unit.uniticonid],183,unit.NativeValue,unit.NativeValue+100,unit.NativeValue);
                return null;
            }
            throw new MissingMethodException(method);
        }
        public static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field).GetValue(value);
    }
}
