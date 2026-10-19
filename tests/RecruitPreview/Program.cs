using GTIP;

int checks=0;
void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var panel=new CampaignMainPanel();
var preview=new Regiment {uniformset=2,AvailableUniforms=new[]{2,8},recruitingtype=0};
var authority=new Regiment {uniformset=4,recruitingtype=0};
var mp=new MultiplayerCampaign {Panel=panel,Preview=preview,Open=true};
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
next.transform.Parent=panel.transform;mp.Open=false;
Check(!mp.Apply(next),"A closed recruiting view does not dispatch preview controls");
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
Console.WriteLine($"Recruit preview: {checks} checks passed using the production control bridge and native-operation doubles; not a live Unity test.");

public sealed class Transform
{
    public Transform Parent;
    public bool IsChildOf(Transform other)=>ReferenceEquals(this,other) || Parent!=null && Parent.IsChildOf(other);
}
public sealed class ButtonTooltip
{
    public string name;
    public int state;
    public bool isActiveAndEnabled=true;
    public Transform transform=new Transform();
}
public sealed class Regiment
{
    public int uniformset,recruitingtype,LastUniformDirection;
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
    public RecruitingList List=new RecruitingList();
    public int InfoRefreshes,DataRefreshes;
    public bool RecruitEnabled;
    public void UpdateUnitInfoPanel(){InfoRefreshes++;RecruitEnabled=dummyregiment.recruitingtype==0;}
    public void UpdateUnitData(){DataRefreshes++;}
}
namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        public static bool IsReplica;
        public CampaignMainPanel Panel {get=>previewPanel;set=>previewPanel=value;}
        public Regiment Preview {get=>nativePreview;set=>nativePreview=value;}
        public bool Open;
        CampaignMainPanel previewPanel;
        Regiment nativePreview;
        bool NativeRecruitOpen=>Open;
        public bool Apply(ButtonTooltip button)=>GuestRecruitPreviewAction(button);
        public bool Refresh(CampaignMainPanel panel)=>RefreshRecruitPreviewRosterUi(panel);
        void LogDiagnostic(string value){}
    }
    public static class NativeAccess
    {
        public static T Get<T>(CampaignMainPanel target,string name)
        {
            if(name!="recruitinglist")throw new InvalidOperationException("Unexpected native field");
            return (T)(object)target.List;
        }
    }
}
