using GTIP;
using TMPro;
using UnityEngine;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
MpNewspaperState Issue(int serial,string title="Military Policy III Enacted")=>new(){Serial=serial,Active=true,Headlines=new[]{title,"Another native headline"},Prosa="Policy report body",ImageHeadline="Illustration",Image=3,Location="Washington",Day=20,Month=3,Year=1863};
var client=new MultiplayerCampaign();var paper=client.Paper;var panel=client.Panel;
var unrelated=new Newspaper(new GameObject("Inactive unrelated newspaper"));Resources.Views=new[]{unrelated,paper};
client.Receive(Issue(1));
Check(paper.Shows==1 && unrelated.Shows==0,"A received native issue renders on its own panel controller rather than a scene-wide template");
Check(!client.Diagnostics.Any(t=>t.StartsWith("Host newspaper displayed:")),"Invoking native UpdateShow does not falsely confirm a still-transparent panel");
Check(client.Diagnostics.Any(t=>t.Contains("Military Policy III Enacted") && t.StartsWith("Host newspaper received:")),"Receipt diagnostics identify the actual article without a policy allowlist");
client.Tick();client.Tick();
Check(paper.Shows==1 && paper.runningnumber==2,"Waiting for native fade does not re-render content or advance the newspaper volume");
client.FadeIn();client.Tick();
Check(client.Diagnostics.Count(t=>t.StartsWith("Host newspaper displayed:"))==1,"A native active panel with visible alpha confirms display once");
client.Receive(Issue(1));client.Tick();
Check(paper.Shows==1 && paper.runningnumber==2 && client.Diagnostics.Count(t=>t.StartsWith("Host newspaper displayed:"))==1,"Repeated state for the same issue neither reopens nor duplicates the confirmed receipt");

panel.go.SetActive(false);panel.state=0;Newspaper.newspapershown=false;
MultiplayerCampaign.ReplicaNewspaperClose(paper);
Check(client.Sent.Count==0,"A passive native OnDisable close is not mistaken for the player's dismissal");
client.Tick();
Check(panel.go.activeInHierarchy && panel.state==1 && Newspaper.newspapershown && paper.Shows==1 && paper.runningnumber==2,"An implicitly hidden unread issue restores its prepared panel without creating another native edition");
client.FadeIn();client.Tick();
Newspaper.newspapershown=false;client.Tick();
Check(Newspaper.newspapershown && paper.Shows==1,"A native shown-flag mismatch repairs visibility without preparing content again");
client.FadeIn();panel.canvasgroup.alpha=0;client.Tick();
Check(panel.state==1 && paper.Shows==1,"An opaque-state panel with zero canvas alpha returns to the native fade path");
client.FadeIn();client.Tick();
MultiplayerCampaign.ReplicaNewspaperClose(paper);
Check(client.Sent.Count==1 && client.Sent[0].unit==1 && client.Sent[0].kind=="news-ack" && client.Status.Contains("Waiting"),"An explicit active close acknowledges the matching issue exactly once");
panel.go.SetActive(false);MultiplayerCampaign.ReplicaNewspaperClose(paper);client.Receive(Issue(1));client.Tick();
Check(client.Sent.Count==1 && !panel.go.activeInHierarchy && paper.Shows==1,"The later OnDisable, repeated state and retry tick cannot reopen or re-ack a deliberately dismissed issue");

client.Receive(Issue(2,"Historical event headline"));client.FadeIn();client.Tick();
Check(paper.Shows==2 && paper.runningnumber==3,"A later article from another native producer receives its own prepared content and edition");
client.LocalMenu=true;panel.go.SetActive(false);panel.state=0;Newspaper.newspapershown=false;client.Tick();
Check(!panel.go.activeInHierarchy && paper.Shows==2,"A local session menu can cover an unread newspaper without a reopen fight");
client.LocalMenu=false;client.Tick();
Check(panel.go.activeInHierarchy && paper.Shows==2,"Closing the local menu restores that same prepared article");
client.FadeIn();client.OptionsPanel.state=2;panel.go.SetActive(false);panel.state=0;Newspaper.newspapershown=false;client.Tick();
Check(!panel.go.activeInHierarchy,"Native options keep priority over newspaper visibility repairs");
client.OptionsPanel.state=0;client.Tick();client.FadeIn();
var inactive=Issue(2);inactive.Active=false;client.Receive(inactive);panel.go.SetActive(false);panel.state=0;client.Tick();
Check(!panel.go.activeInHierarchy && paper.Shows==2,"A host-inactive issue cannot be revived by a local retry");

GameVars.debug_deactivatenewspapermessages=true;
client.Receive(Issue(3));client.Tick();client.Receive(Issue(3));
Check(paper.Shows==2 && client.Sent.Count==2 && client.Sent.Last().unit==3,"The guest's native disable-newspapers preference skips rendering and acknowledges once without trapping the host barrier");
GameVars.debug_deactivatenewspapermessages=false;
client.Tick();
Check(paper.Shows==2,"Re-enabling newspapers does not reopen an issue already acknowledged through the local preference");

var delayed=new MultiplayerCampaign();delayed.Paper.FailBefore=true;delayed.Receive(Issue(1));
Check(delayed.Sent.Count==0 && delayed.Paper.runningnumber==1 && !delayed.Diagnostics.Any(t=>t.StartsWith("Host newspaper displayed:")),"Unavailable native initialization is deferred without a false receipt or connection failure");
delayed.Paper.FailBefore=false;Time.realtimeSinceStartup+=2;delayed.Tick();delayed.FadeIn();delayed.Tick();
Check(delayed.Paper.Shows==1 && delayed.Paper.runningnumber==2,"A pending issue retries after native view initialization becomes available");

var partial=new MultiplayerCampaign();partial.Paper.FailAfterCounter=true;partial.Receive(Issue(1));
Check(partial.Paper.runningnumber==1 && partial.Sent.Count==0,"A failed partial native render restores its edition counter and keeps the issue pending");
partial.Paper.FailAfterCounter=false;Time.realtimeSinceStartup+=2;partial.Tick();partial.FadeIn();partial.Tick();
Check(partial.Paper.runningnumber==2 && partial.Diagnostics.Any(t=>t.StartsWith("Host newspaper displayed:")),"A successful retry after partial failure advances exactly one edition");

var silent=new MultiplayerCampaign();silent.Paper.SilentReturn=true;silent.Receive(Issue(1));
Check(!silent.Diagnostics.Any(t=>t.StartsWith("Host newspaper displayed:")) && silent.Sent.Count==0,"A silent native UpdateShow return cannot consume the delivery receipt");
silent.Paper.SilentReturn=false;Time.realtimeSinceStartup+=2;silent.Tick();silent.FadeIn();silent.Tick();
Check(silent.Paper.Shows==1 && silent.Paper.runningnumber==2,"A silent native failure remains recoverable without duplicate editions");
silent.Paper.SilentReturn=true;silent.Receive(Issue(2));
Check(silent.Paper.Shows==1 && silent.Paper.runningnumber==2 && !silent.Diagnostics.Any(t=>t.StartsWith("Host newspaper displayed: issue=2;")),"An identical earlier body and headline cannot turn a silent return into a receipt for a new issue");
silent.Paper.SilentReturn=false;Time.realtimeSinceStartup+=2;silent.Tick();silent.FadeIn();silent.Tick();
Check(silent.Paper.Shows==2 && silent.Paper.runningnumber==3,"A repeated-content issue still prepares its own native edition after recovery");

var host=new MultiplayerCampaign(true);GameVars.debug_deactivatenewspapermessages=true;
MultiplayerCampaign.ReplicaNewspaperShow(host.Paper,new[]{"Suppressed"},"Body",1,"","",new Tools.Date(20,3,1863),false);
Check(!host.Waiting && host.Paper.Shows==0,"A locally suppressed host UpdateShow call cannot create an unseen two-player barrier");
GameVars.debug_deactivatenewspapermessages=false;
MultiplayerCampaign.ReplicaNewspaperShow(host.Paper,new[]{"New battle result"},"Battle body",1,"","",new Tools.Date(20,3,1863),false);
Check(host.Waiting && host.Paper.Shows==1,"An actually opened native host article still establishes the two-player barrier");
MultiplayerCampaign.ReplicaNewspaperClose(host.Paper);
Check(host.Waiting,"Host dismissal alone retains the barrier");
host.Ack(1);
Check(!host.Waiting,"The guest acknowledgement releases the matching native host issue");

var fresh=new MultiplayerCampaign();fresh.Receive(Issue(1));fresh.FadeIn();fresh.Tick();fresh.Clear();fresh.Receive(Issue(1));
Check(fresh.Paper.Shows==2,"Session teardown clears presentation identities even when a new session starts at issue one");
Console.WriteLine($"Newspaper presentation: {checks} checks using production delivery and barrier code; Unity rendering and native UI callbacks are doubled.");

public static class GameVars
{
    public static bool debug_deactivatenewspapermessages,remainspausedpanelclosed,gamepaused;
    public static float gamespeed=5;
    public static void SetGameSpeed(float value){gamepaused=value==0;if(value>0)gamespeed=value;}
}
public static class Tools {public sealed class Date {public int day,month,year;public Date(int d,int m,int y){day=d;month=m;year=y;}}}
namespace TMPro {public sealed class TextMeshProUGUI {public string text="";}}
namespace UnityEngine
{
    public static class Time {public static float realtimeSinceStartup;}
    public sealed class CanvasGroup {public float alpha;}
    public sealed class Canvas {public int sortingOrder;public bool overrideSorting;}
    public sealed class Transform
    {
        public readonly GameObject gameObject;public Transform Parent;public Transform(GameObject value){gameObject=value;}
        public Transform parent=>Parent;public string name=>gameObject.name;
        public bool IsChildOf(Transform root){for(var current=this;current!=null;current=current.Parent)if(current==root)return true;return false;}
    }
    public sealed class GameObject
    {
        public readonly string name;public readonly Transform transform;public bool activeInHierarchy;public Newspaper Component;
        public GameObject(string value){name=value;transform=new Transform(this);}
        public void SetActive(bool active)=>activeInHierarchy=active;
        public T GetComponent<T>() where T:class=>Component as T;
        public T GetComponentInChildren<T>(bool includeInactive) where T:class=>Component as T;
        public Scene scene=>new();
    }
    public readonly struct Scene {public bool IsValid()=>true;}
    public static class Resources {public static Newspaper[] Views=Array.Empty<Newspaper>();public static T[] FindObjectsOfTypeAll<T>()=>Views.OfType<T>().ToArray();}
}
public sealed class Newspaper
{
    public readonly GameObject gameObject;public Transform transform=>gameObject.transform;
    public TextMeshProUGUI DateT=new(),VolumeT=new(),ProsaLocationT=new(),ProsaT=new(),ImageTitleT=new();
    public TextMeshProUGUI[] HeadlineT=Enumerable.Range(0,5).Select(_=>new TextMeshProUGUI()).ToArray();
    public GameObject[] PanelsWhenOpenedNotResetSpeed=Array.Empty<GameObject>();
    public int runningnumber=1,Shows;public bool dontcontinuespeed,FailBefore,FailAfterCounter,SilentReturn;
    public static bool newspapershown;
    public PanelHandler Handler;
    public Newspaper(GameObject go){gameObject=go;go.Component=this;}
    public void NativeShow(object[] args)
    {
        if(GameVars.debug_deactivatenewspapermessages || SilentReturn)return;
        if(FailBefore)throw new InvalidOperationException("Native view unavailable");
        Handler.ShowPanel(0,100000000f,skipbuttonstatusupdate:true,skipmouseoverclearing:true);
        GameVars.SetGameSpeed(0);runningnumber++;
        if(FailAfterCounter)throw new InvalidOperationException("Native asset unavailable");
        Shows++;for(int i=0;i<HeadlineT.Length;i++)HeadlineT[i].text=i<((string[])args[0]).Length?((string[])args[0])[i]:"";
        ProsaT.text=(string)args[1];ImageTitleT.text=(string)args[3];dontcontinuespeed=(bool)args[6];newspapershown=true;
    }
    public void NativeClose(){Handler.panel[0].timeremaining=0;newspapershown=false;}
}
public sealed class PanelHandler
{
    public sealed class Panel {public int state;public float alpha,timeremaining;public readonly CanvasGroup canvasgroup=new();public readonly Canvas canvas=new();public GameObject go;}
    public readonly List<Panel> panel=new();public int cpanel_newspaper=0,cpanel_optionspanel=1,panel_mainmenusmall=2,cpanel_mainpanel=3;
    public void ShowPanel(int id,float secsremaining,bool skipbuttonstatusupdate=false,bool skipmouseoverclearing=false)
    {
        var p=panel[id];p.timeremaining=secsremaining;if(p.state<=0 || p.state==2 || p.state==3)p.state=1;p.go.SetActive(true);if(p.alpha<0)p.alpha=0;
    }
    public Panel GetPanelFromGameObject(GameObject go)=>panel.FirstOrDefault(p=>p.go==go);
}
public sealed class BattleUI {public PanelHandler panelhandler;}
namespace GTIP
{
    public static class CampaignEditor {public static bool BlocksInput;}
    public static class MpRules {public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);}
    public sealed class MpStateRow {public int Kind;public MpNewspaperState State;}
    public sealed class MpMessage {public string kind,session;public int unit;}
    public sealed partial class MultiplayerCampaign
    {
        static MultiplayerCampaign instance;readonly object transport=new();readonly bool host;readonly bool authenticated=true;readonly string phase="live",session="test";
        readonly BattleUI ui=new();string orderStatus;public bool LocalMenu;bool GuestDialogOpen=>false;
        public readonly List<string> Diagnostics=new();public readonly List<MpMessage> Sent=new();
        public static bool IsReplica=>instance!=null&&!instance.host;public static bool BlocksInput=>instance?.LocalMenu==true;
        public Newspaper Paper {get;}public PanelHandler.Panel Panel=>ui.panelhandler.panel[0];public PanelHandler.Panel OptionsPanel=>ui.panelhandler.panel[1];
        public bool Waiting=>newsBarrier.Waiting;
        public string Status=>orderStatus;
        public MultiplayerCampaign(bool isHost=false)
        {
            instance=this;host=isHost;Newspaper.newspapershown=false;
            var handler=new PanelHandler();ui.panelhandler=handler;
            Paper=new Newspaper(new GameObject("Native newspaper")){Handler=handler};
            handler.panel.Add(new PanelHandler.Panel{go=Paper.gameObject});
            handler.panel.Add(new PanelHandler.Panel{go=new GameObject("Options")});handler.panel.Add(new PanelHandler.Panel{go=new GameObject("Menu")});
        }
        public void Receive(MpNewspaperState state){instance=this;ApplyNewspaper(new[]{new MpStateRow{Kind=11,State=state}});}
        public void Tick(){instance=this;RefreshGuestNewspaper();}
        public void FadeIn(){Panel.state=2;Panel.alpha=Panel.canvasgroup.alpha=1;}
        public void Clear(){ClearNewspaperPresentation();newspaperState=new MpNewspaperState();}
        public void Ack(int serial){instance=this;NewspaperMessage(new MpMessage{kind="news-ack",unit=serial});}
        void LogDiagnostic(string text)=>Diagnostics.Add(text);void Send(MpMessage value)=>Sent.Add(value);
        T ReadData<T>(MpStateRow row) where T:class=>row.State as T;
        Clock CaptureClock()=>new();sealed class Clock {public int day=20,month=3,year=1863;}
    }
    public static class NativeAccess
    {
        public static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name).GetValue(target);
        public static void Set(object target,string name,object value)=>target.GetType().GetField(name).SetValue(target,value);
        public static object Call(object target,string method,params object[] args)
        {
            var paper=(Newspaper)target;
            if(method=="GTIP_Original_ReplicaNewspaperShow")paper.NativeShow(args);
            else if(method=="GTIP_Original_ReplicaNewspaperClose")paper.NativeClose();
            return null;
        }
    }
}
