using GTIP;
using TMPro;

int checks=0;
void Check(bool valid,string why){if(!valid)throw new Exception(why);checks++;Console.WriteLine("PASS "+why);}
GameVars.commander.Add(new GameVars.Commander{name="Wessels"});
GameVars.commander.Add(new GameVars.Commander{name="Runyon"});
var client=new MultiplayerCampaign();
var unit=new Regiment{commander=0,unittyp=14,groupunittype=77};
var label=client.Show(548,unit,"Wessels");
var panel=client.Panel;var view=client.View;
view.Scroll=.27f;panel.Filter="Virginia";panel.Selection=unit;
int selectedIndex=panel.SelectedIndex=4;
unit.commander=1;
Check(label.text=="Wessels","The native OOB icon retains its creation-time Wessels label after the commander scalar changes");
client.Apply();
Check(label.text=="Runyon","A nonstructural authoritative replacement updates the existing OOB label before the roster fence returns");
Check(client.Rebuilds==0 && ReferenceEquals(label,client.LabelAt(0)),"Label refresh retains the original panel objects without rebuilding the OOB");
Check(view.Scroll==.27f && panel.Filter=="Virginia" && panel.Selection==unit && panel.SelectedIndex==selectedIndex,
    "Scroll, filters and exact unit selection survive the label refresh");
Check(unit.groupunittype==77 && unit.commander==1,"The native symbol getter's campaign cache is restored and commander authority is unchanged");
int writes=label.Writes;client.Apply();client.Apply();
Check(label.Writes==writes,"Unchanged labels do not dirty TextMeshPro or rebuild their layout");
GameVars.commander[1].name="Theodore Runyon";client.Apply();
Check(label.text=="Theodore Runyon","An officer rename refreshes labels without a new portrait identity or commander ID");
unit.abrvname="IV DIV.";client.Apply();
Check(label.text=="IV DIV.","Native formation abbreviations take precedence over officer surnames");
unit.abrvname="";unit.unittyp=2;unit.guns=0;client.Apply();
Check(label.text=="(L)","Native empty artillery labels are retained");
unit.parentregiment=new GameObject();client.Apply();
Check(label.text=="(D)","Detached empty artillery labels use native symbol rules");
unit.unittyp=3;client.Apply();
Check(label.text=="","Native skirmisher labels remain blank");
unit.unittyp=14;unit.parentregiment=null;

var invisible=new Regiment{commander=1};var hidden=client.Show(549,invisible,"",invisible:true);
client.Apply();Check(hidden.text=="" && hidden.Writes==0,"Invisible OOB spacer icons remain blank");
var stale=new Regiment{commander=0};var staleLabel=client.Show(550,stale,"stale");
client.Handler.multiplepanel[2].secondaryreference=new GameObject();
client.Apply();Check(staleLabel.text=="stale","A reused panel slot cannot receive a stale registered unit's label");
var removed=new Regiment{commander=0};var removedLabel=client.Show(551,removed,"removed");client.Remove(removed);
client.Apply();Check(removedLabel.text=="removed","Removed replica formations cannot update retained old OOB registrations");
client.AddBadRegistration(unit,-1);client.AddBadRegistration(unit,99999);client.Apply();
Check(client.Diagnostics.Count==0,"Invalid registration indices are skipped without failing campaign synchronization");
var extra=new OOB(client.Handler){isActiveAndEnabled=false};panel.oobs=new[]{view,extra};
var extraLabel=client.AddIcon(extra,unit,"closed");client.Apply();
Check(extraLabel.text=="closed","Closed OOB views are not scanned or reopened");
client.Handler.panel[0].state=0;GameVars.commander[1].name="Renamed while closed";client.Apply();
Check(label.text!="Renamed while closed","A hidden Military panel stays hidden during authoritative updates");
client.Handler.panel[0].state=2;client.Apply();
Check(label.text=="Renamed while closed","Reopening the same view picks up the latest authoritative officer name");
unit.ThrowGetter=true;int retained=unit.groupunittype;client.Apply();
Check(unit.groupunittype==retained && client.Diagnostics.Count==1,"A native symbol failure restores the campaign cache and stays a display diagnostic");
unit.ThrowGetter=false;client.Apply();
Check(label.text=="Renamed while closed","A later batch can retry after a transient native label failure");
client.ScheduleRoster();client.Apply();
Check(client.Rebuilds==1,"Structural changes retain the existing roster-rebuild path rather than duplicating it");
Console.WriteLine($"OOB labels: {checks} checks using production label/fence/native-access adapters; native symbol generation and UI objects are doubled.");

public sealed class GameObject
{
    public string name="OOBSymbol";public Transform transform=new();
}
public sealed class Transform
{
    public Transform Label;public TextMeshProUGUI Text;
    public Transform Find(string path)=>path=="Background/UnitName"?Label:null;
    public T GetComponent<T>() where T:class=>Text as T;
}
namespace TMPro
{
    public sealed class TextMeshProUGUI
    {
        string value;public int Writes;
        public TextMeshProUGUI(string initial){value=initial;}
        public string text {get=>value;set{this.value=value;Writes++;}}
    }
}
public sealed class Regiment
{
    public int commander,groupunittype,unittyp,guns=10;public string name="Unit",abrvname="";public bool ThrowGetter;
    public GameObject gameObject=new(),parentregiment;
    public sealed class UnitSymbolData {public string unitname;}
    public UnitSymbolData GetUnitSymbolData()
    {
        groupunittype=1;if(ThrowGetter)throw new InvalidOperationException("Native symbol display failure");
        string label=abrvname.Length>1?abrvname:GameVars.commander[commander].name;
        if(unittyp==3 || unittyp==4)label="";
        if(unittyp==2 && guns<=0)label=parentregiment==null?"(L)":"(D)";
        return new UnitSymbolData{unitname=label};
    }
}
public static class GameVars {public static List<Commander> commander=new();public sealed class Commander{public string name;public Regiment currentcommand,lastcommand;}}
public sealed class CampaignMainPanel
{
    public OOB[] oobs;public string Filter="";public Regiment Selection;public int SelectedIndex;
}
public sealed class OOB
{
    public bool isActiveAndEnabled=true,initialized=true;public float Scroll;public PanelHandler panelhandler;
    readonly List<Entry> unitsregistered=new();
    public OOB(PanelHandler handler){panelhandler=handler;}
    public sealed class Entry {public Regiment reg;public int multiplepanelid;}
    public void Register(Regiment unit,int index)=>unitsregistered.Add(new Entry{reg=unit,multiplepanelid=index});
}
public sealed class PanelHandler
{
    public sealed class Panel {public int state=2;public GameObject go=new(),secondaryreference;}
    public int cpanel_mainpanel;public List<Panel> panel=new(){new Panel()},multiplepanel=new();
    readonly CampaignMainPanel campaignmainpanel=new();public CampaignMainPanel Main=>campaignmainpanel;
}
public sealed class BattleUI {public PanelHandler panelhandler;}
namespace GTIP
{
    public sealed class MpStateRow{public int Kind,Id,Current,Last;public bool Deleted;public object[] Values;public string Name;}
    public sealed class TestSchema {public void Apply(object target,object[] values){}}
    public static class MpPresentationFence
    {
        public static void Refresh(Action draw,Action restore,Action<Exception> error){try{try{draw();}finally{restore();}}catch(Exception e){error(e);}}
    }
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<Regiment,int> ids=new();readonly Dictionary<int,Regiment> units=new();readonly TestSchema[] DataSchemas={new(),new(),new(),new()};
        readonly BattleUI ui=new(){panelhandler=new PanelHandler()};string orderStatus;
        public readonly List<string> Diagnostics=new();public int Rebuilds;public string Status=>orderStatus;
        public PanelHandler Handler=>ui.panelhandler;public CampaignMainPanel Panel=>Handler.Main;public OOB View=>Panel.oobs[0];
        public MultiplayerCampaign(){Panel.oobs=new[]{new OOB(Handler)};}
        public TextMeshProUGUI Show(int id,Regiment unit,string text,bool invisible=false){ids[unit]=id;units[id]=unit;return AddIcon(View,unit,text,invisible);}
        public TextMeshProUGUI AddIcon(OOB view,Regiment unit,string text,bool invisible=false)
        {
            var label=new TextMeshProUGUI(text);var icon=new PanelHandler.Panel{secondaryreference=unit.gameObject};
            icon.go.name=invisible?"OOBInvisible":"OOBSymbol";icon.go.transform.Label=new Transform{Text=label};
            view.Register(unit,Handler.multiplepanel.Count);Handler.multiplepanel.Add(icon);return label;
        }
        public void AddBadRegistration(Regiment unit,int index)=>View.Register(unit,index);
        public void Remove(Regiment unit)=>ids.Remove(unit);
        public TextMeshProUGUI LabelAt(int index)=>Handler.multiplepanel[index].go.transform.Label.Text;
        public void Apply()=>RefreshReplicaPresentation(Array.Empty<MpStateRow>());
        public void ScheduleRoster()=>rosterUiPending=true;
        void RefreshReplicaCommanderPortraits(){}
        void RefreshReplicaRosterUi(){Rebuilds++;}
        void LogDiagnostic(string text)=>Diagnostics.Add(text);
        object DataTarget(MpStateRow row)=>null;
    }
}
