using GTIP;
using UnityEngine;

int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
void Reject(Action action,string text){bool rejected=false;try{action();}catch(InvalidOperationException error){rejected=error.Message.Contains(text);}Check(rejected,"Host rejects "+text);}
var guestUnit=new Regiment {unittyp=0,uniformset=0};guestUnit.gameObject.activeInHierarchy=false;
var hostUnit=new Regiment {unittyp=0,uniformset=0};hostUnit.gameObject.activeInHierarchy=false;
var guest=new MultiplayerCampaign(guestUnit,true);var host=new MultiplayerCampaign(hostUnit,false);
var guestPanel=guest.Panel;var hostPanel=host.Panel;
ButtonTooltip Control(string name,Palette palette=null)=>new ButtonTooltip {name=name,transform=new Transform{Parent=guestPanel.transform},Palette=palette};
host.Activate();hostPanel.gameObject.activeInHierarchy=false;hostPanel.isActiveAndEnabled=false;
Regiment.UseUniformLookupHook=false;bool hiddenLookupFailed=false;
try{host.Execute(new MpMessage{unit=42,x=1,text="uniform-step|"});}catch(NullReferenceException){hiddenLookupFailed=true;}
Check(hiddenLookupFailed && hostUnit.uniformset==0,"The native UniformSet constructor reproduces the hidden host panel lookup failure before changing the uniform");
Regiment.UseUniformLookupHook=true;host.Execute(new MpMessage{unit=42,x=1,text="uniform-step|"});
Check(hostUnit.uniformset==2 && !hostPanel.gameObject.activeInHierarchy && !hostPanel.isActiveAndEnabled && hostPanel.gameObject.Activations==0,
    "Remote native uniform eligibility resolves the cached inactive panel without activating or redrawing the host UI");
var found=new GameObject {name="Native result"};MultiplayerCampaign.LiveSession=false;
Check(ReferenceEquals(MultiplayerCampaign.ReplicaUniformPanel(found),found),"A native lookup result is preserved outside a multiplayer session");
Check(MultiplayerCampaign.ReplicaUniformPanel(null)==null,"The inactive panel fallback stays disabled without a live session");
MultiplayerCampaign.LiveSession=true;host.SetCachedPanel(null);
Check(MultiplayerCampaign.ReplicaUniformPanel(null)==null,"A missing cached panel does not fabricate a native UI object");
host.SetCachedPanel(hostPanel);hostPanel.gameObject.name="Other panel";
Check(MultiplayerCampaign.ReplicaUniformPanel(null)==null,"The fallback rejects a cached object with the wrong native panel identity");
hostPanel.gameObject.name="Panel_Main";MultiplayerCampaign.ClearInstance();
Check(MultiplayerCampaign.ReplicaUniformPanel(null)==null,"A disconnected controller cannot supply an inactive panel fallback");
host.Activate();hostPanel.gameObject.activeInHierarchy=true;hostPanel.isActiveAndEnabled=true;hostUnit.uniformset=0;
guest.Activate();
Check(guest.Click(Control("947_RecolorUniform")) && guestPanel.paletteactive && guestPanel.UniformLabel.activeSelf && guestPanel.UniformArrowsEnabled,
    "Native information refresh exposes the Uniform label and arrows for a managed inactive regiment");
int authority=guestUnit.uniformset;
Check(guest.Click(Control("477_NextSoldier")) && guestUnit.uniformset==authority && guestPanel.RenderedUniform==2,
    "Existing-unit next-uniform provides immediate native-eligible visual feedback without editing replica authority");
var next=guest.Requests.Last();
Check(next.unit==42 && next.x==1 && next.text=="uniform-step|","Uniform intents retain the registered formation ID and direction");
host.Activate();host.Execute(next);
Check(hostUnit.uniformset==2 && !hostUnit.gameObject.activeInHierarchy,"The host accepts native uniform changes on inactive subordinate regiments");
guestUnit.uniformset=hostUnit.uniformset;guest.Activate();guestPanel.UpdateUnitInfoPanel();
Check(guestPanel.RenderedUniform==2 && guest.PendingCount==0,"Matching host uniform state acknowledges the visual prediction");

// Native rendering copies active palette selections back into the unit. Reproduce the
// stale-selection overwrite before exercising the production host feedback prevention.
host.Activate();hostPanel.paletteactive=true;hostPanel.Palettes[0].currentcolorchosen=0;
hostUnit.componentcolor[0]=Palette.Colors[2];hostPanel.UpdateUnitInfoPanel();
Check(hostUnit.componentcolor[0]==Palette.Colors[0],"The native renderer reproduces stale host palette selections overwriting a direct remote color assignment");
guest.Activate();var color=Control("480_Color03",guestPanel.Palettes[0]);
Check(guest.Click(color) && guestUnit.componentcolor[0]==Palette.Colors[0] && guestPanel.RenderedColors[0]==Palette.Colors[2],
    "Guest color selection previews locally while leaving authoritative component colors untouched");
host.Activate();host.Execute(guest.Requests.Last());
Check(hostUnit.componentcolor[0]==Palette.Colors[2] && hostPanel.Palettes[0].currentcolorchosen==2,
    "Host acceptance aligns its open native palette with the remote absolute color index before redraw");
hostPanel.UpdateUnitInfoPanel();hostPanel.UpdateUnitInfoPanel();
Check(hostUnit.componentcolor[0]==Palette.Colors[2],"Accepted remote colors survive repeated native host panel refreshes");
hostPanel.ThrowRender=true;
Check(host.Execute(new MpMessage{unit=42,x=1,text="uniform-color|3"}) && hostUnit.componentcolor[1]==Palette.Colors[3],"A host display exception cannot reject or undo an accepted appearance transaction");
hostPanel.ThrowRender=false;
guestUnit.componentcolor[0]=hostUnit.componentcolor[0];guest.Activate();guest.Click(Control("947_RecolorUniform"));
Check(!guestPanel.paletteactive && guestUnit.componentcolor[0]==Palette.Colors[2] && !guestPanel.UniformLabel.activeSelf,
    "Closing recolor retains the acknowledged color and returns native label visibility to its normal state");

hostPanel.Palettes[1].Scroll=2;host.Activate();host.Execute(new MpMessage{unit=42,x=1,text="uniform-color|1"});
Check(hostUnit.componentcolor[1]==Palette.Colors[1] && hostPanel.Palettes[1].currentcolorchosen==1,
    "Accepted colors use an absolute native palette index independent of host palette scrolling");
var other=new Regiment();hostPanel.LastUsed=other;int draws=hostPanel.Draws;
host.Execute(new MpMessage{unit=42,x=1,text="uniform-step|"});
Check(hostPanel.Draws==draws && other.uniformset==0,"A guest appearance intent does not refresh or recolor a different host-selected unit");
hostPanel.LastUsed=hostUnit;
host.Select(other);draws=hostPanel.Draws;
host.Execute(new MpMessage{unit=42,x=0,text="uniform-color|1"});
Check(hostPanel.Draws==draws,"A stale host last-used pointer cannot redirect remote palette feedback into the newly selected unit");
host.Select(hostUnit);
Reject(()=>host.Execute(new MpMessage{unit=999,x=1,text="uniform-step|"}),"id=999");
hostUnit.alliance=1;Reject(()=>host.Execute(new MpMessage{unit=42,x=1,text="uniform-step|"}),"requestedSide=0");hostUnit.alliance=0;
hostUnit.unittyp=15;Reject(()=>host.Execute(new MpMessage{unit=42,x=1,text="uniform-step|"}),"type=15");hostUnit.unittyp=0;
Reject(()=>host.Execute(new MpMessage{unit=42,x=0,text="uniform-color|999"}),"native palette");
Reject(()=>host.Execute(new MpMessage{unit=42,x=3,text="uniform-step|"}),"direction");

guest.Activate();guest.Click(Control("947_RecolorUniform"));
guestUnit.uniformset=0;Time.realtimeSinceStartup=1;
guest.Click(Control("477_NextSoldier"));guest.Click(Control("477_NextSoldier"));
Check(guestUnit.uniformset==0 && guestPanel.RenderedUniform==8,"Rapid clicks advance the visual preview through native eligible uniforms while awaiting host updates");
Time.realtimeSinceStartup=5;guestPanel.UpdateUnitInfoPanel();
Check(guestPanel.RenderedUniform==0 && guest.PendingCount==0,"An expired or rejected uniform preview returns to host authority");
guestPanel.ThrowRender=true;var retained=guestUnit.componentcolor;
Reject(()=>guestPanel.UpdateUnitInfoPanel(),"display failure");guestPanel.ThrowRender=false;
Check(ReferenceEquals(guestUnit.componentcolor,retained) && guestUnit.uniformset==0,"Failed native replica rendering restores exact authoritative color and uniform state");
Console.WriteLine($"Appearance: {checks} checks using production appearance/control bridges, inactive native panel lookup and palette feedback doubles. Not a live Unity test.");

public sealed class Transform {public Transform Parent;public bool IsChildOf(Transform p)=>ReferenceEquals(this,p) || Parent!=null && Parent.IsChildOf(p);}
public sealed class GameObject
{
    public static GameObject NativePanel;public string name;public object Component;public int Activations;
    public bool activeInHierarchy=true,activeSelf=true;public Transform transform=new Transform();
    public static GameObject Find(string path)=>path=="UI/Panel_Main" && NativePanel?.activeInHierarchy==true?NativePanel:null;
    public T GetComponent<T>() where T:class=>Component as T;
    public void SetActive(bool value){activeSelf=value;Activations++;}
}
public sealed class ButtonTooltip
{
    public string name;public int state;public bool isActiveAndEnabled=true;public Transform transform;
    public Palette Palette;public T GetComponentInParent<T>() where T:class=>Palette as T;
}
public sealed class Regiment
{
    public int alliance,unittyp,uniformset,recruitingtype;public string name="Managed regiment";
    public GameObject gameObject=new GameObject();public Color[] componentcolor={Palette.Colors[0],Palette.Colors[0],new Color(9)};
    public int[] Eligible={0,2,8};public static bool UseUniformLookupHook=true;
    public void AdjustUniformSet(int direction)
    {
        // Match the native UniformSet constructor's unconditional Find + GetComponent
        // dependency before consulting the policy/state-specific eligible list.
        var found=GameObject.Find("UI/Panel_Main");
        if(UseUniformLookupHook)found=MultiplayerCampaign.ReplicaUniformPanel(found);
        var panel=found.GetComponent<CampaignMainPanel>();
        _=panel.NativeSpriteCount;
        int index=Array.IndexOf(Eligible,uniformset);uniformset=Eligible[(Math.Max(0,index)+direction+Eligible.Length)%Eligible.Length];
    }
}
public sealed class Palette
{
    public static Color[] Colors={new Color(0),new Color(1),new Color(2),new Color(3)};
    public int currentcolorchosen=-1,Scroll;
    public static Color GetColorFromIndex(int id)=>id<0 || id>=Colors.Length?default:Colors[id];
    public void SelectColor(int id){currentcolorchosen=Math.Clamp(Scroll+id-1,0,Colors.Length-1);}
    public void SwitchColorIndex(int direction){Scroll+=direction;}
    public Color GetSelectedColor()=>Colors[currentcolorchosen];
}
public sealed class CampaignMainPanel
{
    public static Regiment dummyregiment;
    public GameObject gameObject;public int NativeSpriteCount=>11;
    public Transform transform=new Transform();public bool isActiveAndEnabled=true,paletteactive,ThrowRender,UniformArrowsEnabled;
    public Regiment LastUsed;public Palette[] Palettes={new Palette(),new Palette()};public int Draws,RenderedUniform;
    public Color[] RenderedColors;public GameObject UniformTextObjUnitPanel=new GameObject();public GameObject UniformLabel=>UniformTextObjUnitPanel;
    public CampaignMainPanel(){gameObject=new GameObject{name="Panel_Main",Component=this};}
    public void ActivatePalette(bool active=true){paletteactive=active;for(int i=0;i<2;i++)Palettes[i].currentcolorchosen=LastUsed.componentcolor[i].Id;}
    public void UpdateUnitInfoPanel(){MultiplayerCampaign.ReplicaSoldierPicture(this);UniformLabel.SetActive(paletteactive);UniformArrowsEnabled=paletteactive;}
    public void OriginalSoldierPicture()
    {
        Draws++;var u=dummyregiment??LastUsed;
        if(paletteactive)for(int i=0;i<2;i++)if(Palettes[i].currentcolorchosen>=0)u.componentcolor[i]=Palettes[i].GetSelectedColor();
        RenderedUniform=u.uniformset;RenderedColors=(Color[])u.componentcolor.Clone();
        if(ThrowRender)throw new InvalidOperationException("Native display failure");
    }
    public void UpdateUnitData(){}
}
public sealed class RecruitingList {public void UpdateList(){}}
public sealed class PanelHandler {public CampaignMainPanel Main;}
public sealed class BattleUI {public PanelHandler panelhandler;public Regiment selectedunitreg;}
namespace UnityEngine {public record struct Color(int Id);public static class Time {public static float realtimeSinceStartup;}}
namespace GTIP
{
    public sealed class MpMessage {public int unit;public float x;public string text;}
    public sealed partial class MultiplayerCampaign
    {
        static MultiplayerCampaign instance;public static bool IsReplica;static bool session=true;
        public static bool LiveSession {get=>instance!=null && session;set=>session=value;}
        readonly bool replica;int guestSide=0;
        readonly Dictionary<int,Regiment> units=new Dictionary<int,Regiment>();readonly Dictionary<Regiment,int> ids=new Dictionary<Regiment,int>();
        BattleUI ui;CampaignMainPanel previewPanel=null;Regiment nativePreview=null;
        bool NativeRecruitOpen=>false;
        public CampaignMainPanel Panel=>ui.panelhandler.Main;
        public int PendingCount=>pendingUniforms.Count;
        public List<MpMessage> Requests=new List<MpMessage>();
        public MultiplayerCampaign(Regiment unit,bool replica)
        {
            this.replica=replica;units[42]=unit;ids[unit]=42;ui=new BattleUI{selectedunitreg=unit,panelhandler=new PanelHandler{Main=new CampaignMainPanel{LastUsed=unit}}};
        }
        public void Activate(){instance=this;IsReplica=replica;GameObject.NativePanel=Panel.gameObject;}
        public void SetCachedPanel(CampaignMainPanel value){ui.panelhandler.Main=value;}
        public static void ClearInstance(){instance=null;}
        public void Select(Regiment unit){ui.selectedunitreg=unit;}
        public bool Click(ButtonTooltip button)=>GuestRecruitPreviewAction(button);
        public bool Execute(MpMessage request){var parts=request.text.Split('|');return ExecuteAppearanceAction(parts[0],request,parts.Length>1?parts[1]:"");}
        void RequestNative(string action,int target,float value=0,string detail="")=>Requests.Add(new MpMessage{unit=target,x=value,text=action+"|"+detail});
        void LogDiagnostic(string value){}
        public static bool IsRecruitPreview(Regiment unit)=>false;
        static void IsolateRecruitPreview(Regiment unit){}
        static void RestoreRecruitPreviewPanel(CampaignMainPanel target,Regiment preview){}
    }
    public static class NativeAccess
    {
        public static object Call(object target,string name,params object[] args)
        {
            var main=(CampaignMainPanel)target;
            if(name=="GetUsedUnit")return CampaignMainPanel.dummyregiment??main.LastUsed;
            if(name=="GTIP_Original_ReplicaSoldierPicture"){main.OriginalSoldierPicture();return null;}
            throw new InvalidOperationException("Unexpected native method: "+name);
        }
        public static T Get<T>(object target,string name)
        {
            object value;
            if(target is PanelHandler h && name=="campaignmainpanel")value=h.Main;
            else if(target is CampaignMainPanel main && name=="lastusedunit")value=main.LastUsed;
            else if(target is CampaignMainPanel p && name=="palette")value=p.Palettes;
            else if(target is Type t && t==typeof(Palette) && name=="palettecolors")value=Palette.Colors;
            else throw new InvalidOperationException("Unexpected native field: "+name);
            return (T)value;
        }
    }
}
