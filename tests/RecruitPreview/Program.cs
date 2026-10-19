using GTIP;
using UnityEngine;

int checks=0;
void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var panel=new CampaignMainPanel();
var preview=new Regiment {uniformset=2,AvailableUniforms=new[]{2,8},recruitingtype=0};
var authority=new Regiment {uniformset=4,recruitingtype=0};
var mp=new MultiplayerCampaign {Panel=panel,Preview=preview,Open=true};
mp.Register(authority,17);panel.Used=authority;
CampaignMainPanel.dummyregiment=preview;MultiplayerCampaign.IsReplica=true;
ButtonTooltip Button(string name)=>new ButtonTooltip {name=name,transform=new Transform{Parent=panel.transform}};
var next=Button("477_NextSoldier");
Check(mp.Apply(next),"The next-uniform click is consumed for the active guest preview");
Check(preview.uniformset==8 && preview.LastUniformDirection==1,"Uniform cycling delegates to the native eligibility operation instead of incrementing an arbitrary uniform ID");
Check(panel.InfoRefreshes==1,"A preview uniform change immediately refreshes the native panel");
Check(mp.Apply(next) && preview.uniformset==2,"Successive uniform changes remain usable and wrap within the eligible native list");
Check(mp.Apply(Button("476_PrevSoldier")) && preview.uniformset==8 && preview.LastUniformDirection==-1,"Previous-uniform selection retains native backward cycling");
Check(mp.Apply(Button("475_SwitchRecruitType")) && preview.recruitingtype==1,"The guest can switch its uncommitted preview to draftees");
Check(panel.InfoRefreshes==4 && !panel.RecruitEnabled,"Native information refresh keeps Recruit unavailable when the selected pool has insufficient recruits");
Check(mp.Apply(Button("475_SwitchRecruitType")) && preview.recruitingtype==0 && panel.RecruitEnabled,"The guest can switch back to available volunteers without reopening the screen");
Check(authority.uniformset==4 && authority.recruitingtype==0,"Preview controls do not edit an authoritative formation");
int calls=panel.InfoRefreshes,uniform=preview.uniformset;
next.state=2;
Check(!mp.Apply(next) && panel.InfoRefreshes==calls && preview.uniformset==uniform,"A natively disabled preview control is ignored");
next.state=0;next.isActiveAndEnabled=false;
Check(!mp.Apply(next),"An inactive preview control is ignored");
next.isActiveAndEnabled=true;next.transform.Parent=new Transform();
Check(!mp.Apply(next),"A similarly named button outside the active recruiting panel cannot change the preview");
next.transform.Parent=panel.transform;mp.Open=false;CampaignMainPanel.dummyregiment=null;
panel.Used=null;
Check(!mp.Apply(next),"A closed recruiting view does not dispatch preview controls");
panel.Used=authority;
mp.Open=true;CampaignMainPanel.dummyregiment=authority;
Check(!mp.Apply(next) && authority.uniformset==4,"A replaced native dummy never redirects the click to an authoritative unit");
CampaignMainPanel.dummyregiment=preview;mp.Preview=null;
Check(!mp.Apply(next),"A missing guest preview does not fall back to the selected map formation");
mp.Preview=preview;MultiplayerCampaign.IsReplica=false;
Check(!mp.Apply(next),"Host and offline uniform controls remain in the native dispatcher");
MultiplayerCampaign.IsReplica=true;
Check(!mp.Apply(Button("481_Recruit")) && panel.InfoRefreshes==calls,"The local preview handler cannot consume recruitment commits or unrelated controls");
panel.List.chosenstate=9;panel.List.SelectedRow=3;panel.List.Filter="Own states";
Check(mp.Refresh(panel),"A live roster update uses the active guest recruiting refresh");
Check(panel.List.ListRefreshes==1 && panel.DataRefreshes==1,"The recruiting list and native unit information refresh after live roster changes");
Check(panel.List.chosenstate==9 && panel.List.SelectedRow==3 && panel.List.Filter=="Own states","Roster refresh retains the chosen recruiting state, row and filters");
Check(ReferenceEquals(CampaignMainPanel.dummyregiment,preview) && preview.uniformset==8 && preview.recruitingtype==0,"Roster refresh preserves the same uncommitted uniform and recruit-pool choices");
var otherPanel=new CampaignMainPanel();
Check(!mp.Refresh(otherPanel) && otherPanel.List.ListRefreshes==0,"An unrelated military view uses its normal roster refresh");
mp.Open=false;Check(!mp.Refresh(panel),"Closed recruitment falls back to the normal military roster refresh");
mp.Open=true;MultiplayerCampaign.IsReplica=false;
Check(!mp.Refresh(panel),"Host and offline roster refreshes retain native behavior");
MultiplayerCampaign.IsReplica=true;mp.Open=true;mp.Preview=preview;CampaignMainPanel.dummyregiment=preview;
panel.paletteactive=true;
var colorButton=Button("480_Color02");colorButton.Palette=panel.Palettes[1];
Check(mp.Apply(colorButton) && preview.componentcolor[1].Id==1,"Recruiting color swatches edit the correct component of only the uncommitted dummy");
Check(mp.Requests.Count==0,"Dummy uniform and color changes do not emit authoritative formation requests");
var scrollButton=Button("479_Right");scrollButton.Palette=panel.Palettes[0];
Check(mp.Apply(scrollButton) && panel.Palettes[0].Scroll==1,"Native palette navigation is dispatched even when the palette is outside UnitInfoPanel");
var unrelatedPalette=new Palette();colorButton.Palette=unrelatedPalette;
Check(!mp.Apply(colorButton),"A color swatch from a different palette cannot recolor a formation");
mp.Open=false;CampaignMainPanel.dummyregiment=null;
var recolor=Button("947_RecolorUniform");panel.paletteactive=false;
Check(mp.Apply(recolor) && panel.paletteactive,"Existing-unit recolor opens the native palette without sending a world mutation");
Check(mp.Apply(recolor) && !panel.paletteactive,"Existing-unit recolor closes the palette on its next click");
authority.AvailableUniforms=new[]{4,7};int previousUniform=authority.uniformset;
Check(mp.Apply(next) && authority.uniformset==previousUniform && mp.Requests.Last().text=="uniform-step" && mp.Requests.Last().unit==17,"Existing-unit next-uniform sends an authoritative intent without editing the guest's roster");
mp.Execute(mp.Requests.Last());
Check(authority.uniformset==7,"Host executes next-uniform with the actual native eligibility operation");
panel.paletteactive=true;colorButton.Palette=panel.Palettes[0];
int oldColor=authority.componentcolor[0].Id;
Check(mp.Apply(colorButton) && authority.componentcolor[0].Id==oldColor && mp.Requests.Last().text=="uniform-color|1","Existing-unit color swatch sends the selected native palette index without local authority drift");
mp.Execute(mp.Requests.Last());
Check(authority.componentcolor[0].Id==1,"Host applies the selected native palette color to the requested component");
var beforeColors=authority.componentcolor;
panel.NormalizeUniformOnDraw=true;
MultiplayerCampaign.ReplicaSoldierPicture(panel);
Check(ReferenceEquals(authority.componentcolor,beforeColors) && authority.uniformset==7 && authority.componentcolor[0].Id==1,"A native soldier redraw can preview colors without altering the authoritative replica color array or uniform");
Check(panel.LastRenderedColor==1,"The native renderer still sees the chosen local palette color");
void Reject(Action action,string label){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,label);}
Reject(()=>mp.Execute(new MpMessage {unit=17,x=3,text="uniform-step"}),"Host rejects an unsupported uniform direction");
Reject(()=>mp.Execute(new MpMessage {unit=17,x=0,text="uniform-color|999"}),"Host rejects a color outside the native palette");
authority.alliance=1;Reject(()=>mp.Execute(new MpMessage {unit=17,x=1,text="uniform-step"}),"Host rejects appearance changes to the other side");authority.alliance=0;
CampaignMainPanel.dummyregiment=preview;mp.Preview=preview;
MultiplayerCampaign.ReplicaSoldierPicture(panel);
Check(preview.uniformset==0,"Uncommitted dummy redraws retain native local appearance editing");
CampaignMainPanel.dummyregiment=null;

var field=panel.Field;UnityEngine.Time.realtimeSinceStartup=1;
MultiplayerCampaign.ReplicaUnitName(panel);
Check(field.text==authority.name,"A newly bound guest unit name starts from the authoritative name");
Check(field.Label.text==authority.name,"The native label beside the input field reflects the authoritative name");
field.isFocused=true;MultiplayerCampaign.ReplicaUnitName(panel);field.text="Guest name";
MultiplayerCampaign.ReplicaUnitName(panel);
Check(field.text=="Guest name" && authority.name!="Guest name","An in-progress name edit survives native information refresh without mutating the unit");
Check(field.Label.text=="Guest name","The separate native TMP label displays the guest's current name draft");
field.isFocused=false; // Deliberately omit onEndEdit to exercise the real native focus-loss fallback.
MultiplayerCampaign.ReplicaUnitName(panel);
Check(mp.Requests.Last().text=="unit-name|Guest name" && field.text=="Guest name","Guest focus loss submits a rename even when the legacy panel omits onEndEdit");
int requests=mp.Requests.Count;field.onEndEdit.Invoke("Guest name");
Check(mp.Requests.Count==requests,"An eventual end-edit event does not submit the same rename twice");
authority.name="Guest name";MultiplayerCampaign.ReplicaUnitName(panel);
Check(field.text==authority.name,"Matching authority acknowledges the pending rename");
field.isFocused=true;MultiplayerCampaign.ReplicaUnitName(panel);field.text="Second name";field.onEndEdit.Invoke(field.text);field.isFocused=false;
MultiplayerCampaign.ReplicaUnitName(panel);
Check(mp.Requests.Last().text=="unit-name|Second name","Native end-edit submits the next rename without reopening the panel");
UnityEngine.Time.realtimeSinceStartup=5;MultiplayerCampaign.ReplicaUnitName(panel);
Check(field.text=="Guest name","A rejected or unacknowledged rename eventually restores authority");
requests=mp.Requests.Count;field.onEndEdit.Invoke("bad\nname");
Check(mp.Requests.Count==requests,"Invalid multiline names are not sent");
CampaignMainPanel.dummyregiment=preview;mp.Preview=preview;field.text="Draft regiment";
MultiplayerCampaign.ReplicaUnitName(panel);
Check(preview.name=="Draft regiment" && mp.Requests.Count==requests,"Recruiting dummy names remain local until the recruitment commit");
Console.WriteLine($"Recruit preview, appearance and names: {checks} checks passed using the production bridges and native-operation doubles; not a live Unity test.");

public sealed class Transform
{
    public Transform Parent;
    public TMPro.TMP_InputField Field;
    public bool IsChildOf(Transform other)=>ReferenceEquals(this,other) || Parent!=null && Parent.IsChildOf(other);
    public Transform Find(string path)=>this;
    public T GetComponent<T>() where T:class=>Field as T;
}
public sealed class GameObject {public bool activeInHierarchy=true;public Transform transform=new Transform();}
public sealed class ButtonTooltip
{
    public string name;
    public int state;
    public bool isActiveAndEnabled=true;
    public Transform transform=new Transform();
    public Palette Palette;
    public T GetComponentInParent<T>() where T:class=>Palette as T;
}
public sealed class Regiment
{
    public int uniformset,recruitingtype,LastUniformDirection;
    public string name="Authoritative regiment";
    public int alliance,unittyp;
    public GameObject gameObject=new GameObject();
    public Color[] componentcolor={new Color(0),new Color(0)};
    public int[] AvailableUniforms=Array.Empty<int>();
    public void AdjustUniformSet(int direction)
    {
        LastUniformDirection=direction;
        int index=Array.IndexOf(AvailableUniforms,uniformset);
        uniformset=AvailableUniforms[(index+direction+AvailableUniforms.Length)%AvailableUniforms.Length];
    }
}
public sealed class RecruitingList
{
    public int chosenstate=-1,SelectedRow=-1,ListRefreshes;
    public string Filter="All";
    public void UpdateList(){ListRefreshes++;}
}
public sealed class CampaignMainPanel
{
    public static Regiment dummyregiment;
    public Transform transform=new Transform();
    public bool isActiveAndEnabled=true,paletteactive,NormalizeUniformOnDraw;
    public int LastRenderedColor;
    public Regiment Used;
    public Palette[] Palettes={new Palette(),new Palette()};
    public TMPro.TMP_InputField Field=new TMPro.TMP_InputField();
    public RecruitingList List=new RecruitingList();
    public int InfoRefreshes,DataRefreshes;
    public bool RecruitEnabled;
    public void UpdateUnitInfoPanel(){InfoRefreshes++;RecruitEnabled=dummyregiment==null || dummyregiment.recruitingtype==0;MultiplayerCampaign.ReplicaSoldierPicture(this);}
    public void UpdateUnitData(){DataRefreshes++;}
    public void ActivatePalette(bool value=true){paletteactive=value;}
}
public sealed class Palette
{
    public static Color[] Colors={new Color(0),new Color(1),new Color(2)};
    public int currentcolorchosen=-1,Scroll;
    public void SelectColor(int id){currentcolorchosen=Math.Clamp(id-1,0,Colors.Length-1);}
    public Color GetSelectedColor()=>Colors[Math.Max(0,currentcolorchosen)];
    public void SwitchColorIndex(int amount){Scroll+=amount;}
    public static Color GetColorFromIndex(int id)=>id>=0 && id<Colors.Length?Colors[id]:default;
}
public sealed class Panel {public GameObject go=new GameObject();}
public sealed class PanelHandler
{
    public int cpanel_unitinfopanel,cpanel_mainpanelshipinfo;
    public Panel[] panel={new Panel()};
    public CampaignMainPanel Main;
}
public sealed class BattleUI {public PanelHandler panelhandler;}
public sealed class Vessel {public string name;}
public sealed class FleetInfoPanel {public Vessel chosenvessel;public string manualname="";}
namespace UnityEngine
{
    public struct Color {public int Id;public Color(int id){Id=id;}}
    public static class Time {public static float realtimeSinceStartup;}
}
namespace TMPro
{
    public sealed class FieldEvent
    {
        readonly List<Action<string>> listeners=new List<Action<string>>();
        public void AddListener(Action<string> callback)=>listeners.Add(callback);
        public void Invoke(string value){foreach(var callback in listeners)callback(value);}
    }
    public sealed class TMP_InputField
    {
        public bool isFocused;
        public GameObject gameObject=new GameObject();
        public FieldEvent onValueChanged=new FieldEvent(),onEndEdit=new FieldEvent();
        public TextMeshProUGUI Label=new TextMeshProUGUI();
        public T GetComponent<T>() where T:class=>Label as T;
        string value="";
        public string text {get=>value;set{this.value=value;onValueChanged.Invoke(value);}}
    }
    public sealed class TextMeshProUGUI {public string text;}
}
namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static bool IsReplica;
        static MultiplayerCampaign instance;
        public MultiplayerCampaign(){instance=this;}
        public CampaignMainPanel Panel {get=>previewPanel;set{previewPanel=value;ui=new BattleUI {panelhandler=new PanelHandler{Main=value}};ui.panelhandler.panel[0].go.transform.Field=value.Field;}}
        public Regiment Preview {get=>nativePreview;set=>nativePreview=value;}
        public bool Open;
        CampaignMainPanel previewPanel;
        Regiment nativePreview;
        BattleUI ui;
        int guestSide=0;
        readonly Dictionary<Regiment,int> ids=new Dictionary<Regiment,int>();
        readonly Dictionary<int,Regiment> units=new Dictionary<int,Regiment>();
        readonly Dictionary<Vessel,int> vesselIds=new Dictionary<Vessel,int>();
        public List<MpMessage> Requests=new List<MpMessage>();
        void RequestNative(string action,int target=0,float value=0,string detail="")=>Requests.Add(new MpMessage {text=action+(detail.Length>0?"|"+detail:""),unit=target,x=value});
        public void Register(Regiment unit,int id){ids[unit]=id;units[id]=unit;}
        public bool Execute(MpMessage message){var parts=message.text.Split('|');return ExecuteAppearanceAction(parts[0],message,parts.Length>1?parts[1]:"");}
        public static bool IsRecruitPreview(Regiment unit)=>unit!=null && unit==instance.nativePreview;
        bool NativeRecruitOpen=>Open;
        public bool Apply(ButtonTooltip button)=>GuestRecruitPreviewAction(button);
        public bool Refresh(CampaignMainPanel panel)=>RefreshRecruitPreviewRosterUi(panel);
        void LogDiagnostic(string value){}
    }
    public static class NativeAccess
    {
        public static T Get<T>(object target,string name)
        {
            object value;
            if(target is CampaignMainPanel panel && name=="recruitinglist")value=panel.List;
            else if(target is CampaignMainPanel colors && name=="palette")value=colors.Palettes;
            else if(target is PanelHandler handler && name=="campaignmainpanel")value=handler.Main;
            else if(target is Type type && type==typeof(Palette) && name=="palettecolors")value=Palette.Colors;
            else throw new InvalidOperationException("Unexpected native field "+name);
            return (T)value;
        }
        public static object Call(object target,string name,params object[] args)
        {
            if(target is CampaignMainPanel panel) {
                var unit=CampaignMainPanel.dummyregiment??panel.Used;
                if(name=="GetUsedUnit")return unit;
                if(name=="GTIP_Original_ReplicaSoldierPicture") {
                    if(unit==null)return null;
                    if(panel.paletteactive)for(int i=0;i<2;i++)if(panel.Palettes[i].currentcolorchosen>=0)unit.componentcolor[i]=panel.Palettes[i].GetSelectedColor();
                    if(panel.NormalizeUniformOnDraw)unit.uniformset=0;
                    panel.LastRenderedColor=unit.componentcolor[0].Id;return null;
                }
                if(name=="GTIP_Original_ReplicaUnitName"){if(unit!=null)unit.name=panel.Field.text;return null;}
            }
            throw new InvalidOperationException("Unexpected native method "+name);
        }
    }
    public sealed class MpMessage {public int unit;public float x;public string text;}
}
