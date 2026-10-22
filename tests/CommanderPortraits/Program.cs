using GTIP;

int checks=0;
void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;Console.WriteLine("PASS "+reason);}
GameVars.commander.Add(new GameVars.Commander{name="Old commander"});
GameVars.commander.Add(new GameVars.Commander{name="Replacement"});
var unit=new Regiment{commander=0};unit.UpdateCommanderPicture();
var client=new MultiplayerCampaign();client.Add(548,unit);client.Refresh();
int loads=unit.Loads;
unit.commander=1;
Check(NativePanel.Draw(unit)=="Replacement / historical:0","The native cache reproduces the reported new name / old portrait after scalar assignment");
var authority=GameVars.commander[1];authority.currentcommand=unit;authority.lastcommand=unit;authority.rank=4;
client.Refresh();
Check(NativePanel.Draw(unit)=="Replacement / historical:1","A nonstructural batch updates the native portrait cache before the presentation fence early return");
Check(unit.Loads==loads+1 && client.Dirty,"A replacement loads once and marks the existing officer view for redraw");
Check(unit.commander==1 && authority.currentcommand==unit && authority.lastcommand==unit && authority.rank==4,"Portrait refresh preserves authoritative commander fields and references");
client.ClearDirty();loads=unit.Loads;client.Refresh();client.Refresh();
Check(unit.Loads==loads && !client.Dirty,"Unchanged replication batches reuse the cached native portrait");
authority.name="Renamed";authority.rank=5;client.Refresh();
Check(unit.Loads==loads,"Officer name and rank changes do not redundantly reload the same picture");
authority.portraitfilename="custom-officer";client.Refresh();
Check(unit.commanderpic=="custom:custom-officer" && unit.Loads==loads+1,"Changing a portrait filename refreshes a still-assigned officer");
loads=unit.Loads;authority.usedummyphoto=true;client.Refresh();
Check(unit.Loads==loads+1 && unit.commanderpic=="custom:custom-officer","Dummy-photo changes invalidate identity while retaining native custom-portrait precedence");
authority.portraitfilename="";client.Refresh();
Check(unit.commanderpic=="placeholder","Removing a custom portrait uses the native dummy fallback");
authority.usedummyphoto=false;client.Refresh();
Check(unit.commanderpic=="historical:1","Historical portraits return after the dummy flag clears");
authority.portraitfilename="missing";client.Refresh();
Check(unit.commanderpic=="historical:1","A missing custom image retains the native historical fallback");

var other=new Regiment{commander=0};client.Add(549,other);client.Refresh();
loads=other.Loads;unit.commander=0;client.Refresh();
Check(other.Loads==loads && unit.commanderpic=="historical:0","An assignment refresh does not reload unrelated formation portraits");
client.Remove(548);client.Add(550,new Regiment{commander=1});client.Refresh();
Check(client.Cached==2 && !client.HasCache(548),"Equal-sized lifecycle replacement rosters discard removed scene references");
var recreated=new Regiment{commander=0};client.Add(549,recreated);client.Refresh();
Check(recreated.Loads==1 && client.Cached==2,"A recreated native object with the same formation ID receives its own portrait");
recreated.Destroyed=true;client.Refresh();
Check(!client.HasCache(549),"Destroyed Unity objects are dropped without invoking their image loader");
var invalid=new Regiment{commander=-1};client.Add(551,invalid);client.Refresh();
Check(invalid.Loads==0 && !client.HasCache(551),"An unassigned preview-like formation does not index an invalid officer");
invalid.commander=999;client.Refresh();
Check(invalid.Loads==0,"Unavailable officer IDs do not trigger native picture lookups");
invalid.commander=0;client.Refresh();
Check(invalid.Loads==1 && client.HasCache(551),"A later valid assignment can initialize a previously unavailable portrait");

var broken=new Regiment{commander=1,ThrowLoad=true};client.Add(552,broken);client.Refresh();
Check(client.Diagnostics.Count==1 && !client.HasCache(552) && broken.commander==1,"Local image loading failure is diagnostic and does not reject authoritative state");
broken.ThrowLoad=false;client.Refresh();
Check(client.HasCache(552) && broken.commanderpic=="historical:1","Failed image loads retry on the next authoritative batch");
client.ScheduleRoster();client.Refresh();
Check(client.RosterDraws==1 && client.PortraitsReadyAtRosterDraw,"Native roster rendering sees refreshed portrait caches");
client.ClearSession();
Check(client.Cached==0,"Session teardown drops retained native portrait objects");
client.Refresh();
Check(client.Cached==3,"A new session refreshes identities even when formation IDs are reused");
Console.WriteLine($"Commander portraits: {checks} checks using production portrait and presentation-fence code; native image loading is doubled.");

public sealed class Regiment
{
    public int commander,Loads;public string name="unit",commanderpic;public bool Destroyed,ThrowLoad;
    public void UpdateCommanderPicture()
    {
        Loads++;if(ThrowLoad)throw new InvalidOperationException("Unavailable local image");
        var officer=GameVars.commander[commander];
        commanderpic=officer.portraitfilename!="" && officer.portraitfilename!="missing"?"custom:"+officer.portraitfilename:
            officer.usedummyphoto?"placeholder":"historical:"+commander;
    }
    public static bool operator ==(Regiment left,Regiment right)=>ReferenceEquals(left,right) ||
        ReferenceEquals(left,null) && !ReferenceEquals(right,null) && right.Destroyed ||
        ReferenceEquals(right,null) && !ReferenceEquals(left,null) && left.Destroyed;
    public static bool operator !=(Regiment left,Regiment right)=>!(left==right);
    public override bool Equals(object obj)=>ReferenceEquals(this,obj);
    public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
}
public static class GameVars
{
    public static readonly List<Commander> commander=new();
    public sealed class Commander {public string name,portraitfilename="";public bool usedummyphoto;public int rank;public Regiment currentcommand,lastcommand;}
}
public static class NativePanel {public static string Draw(Regiment unit)=>GameVars.commander[unit.commander].name+" / "+unit.commanderpic;}
namespace GTIP
{
    public sealed class MpStateRow {public int Kind,Id,Current,Last;public bool Deleted;public object[] Values;public string Name;}
    public sealed class TestSchema {public void Apply(object target,object[] values){}}
    public static class MpPresentationFence
    {
        public static void Refresh(Action draw,Action restore,Action<Exception> error)
        {try{try{draw();}finally{restore();}}catch(Exception e){error(e);}}
    }
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<int,Regiment> units=new();readonly TestSchema[] DataSchemas={new(),new(),new(),new()};
        bool officerViewDirty;string orderStatus;
        public readonly List<string> Diagnostics=new();public int RosterDraws;public bool PortraitsReadyAtRosterDraw;
        public bool Dirty=>officerViewDirty;public int Cached=>commanderPortraits.Count;public string Status=>orderStatus;public bool HasCache(int id)=>commanderPortraits.ContainsKey(id);
        public void Add(int id,Regiment unit)=>units[id]=unit;public void Remove(int id)=>units.Remove(id);
        public void Refresh()=>RefreshReplicaPresentation(Array.Empty<MpStateRow>());
        public void ClearDirty()=>officerViewDirty=false;public void ScheduleRoster()=>rosterUiPending=true;
        public void ClearSession()=>ClearCommanderPortraits();
        void LogDiagnostic(string text)=>Diagnostics.Add(text);
        object DataTarget(MpStateRow row)=>null;
        void RefreshReplicaRosterUi(){RosterDraws++;PortraitsReadyAtRosterDraw=units.Values.Where(u=>u!=null).All(u=>u.commanderpic!=null);}
        void RefreshReplicaOobLabels(){}
    }
}
