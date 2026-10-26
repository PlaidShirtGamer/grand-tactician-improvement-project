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

var tooltipClose=new MultiplayerCampaign();tooltipClose.Receive(Issue(1));tooltipClose.FadeIn();tooltipClose.Tick();
var closeX=tooltipClose.CloseControl(tooltipClose.Panel.go,true,"ClosePanel");
int genericCloses=ModularUIFunctions.Pending.Count;
tooltipClose.Click(closeX);
Check(tooltipClose.Sent.Count==1 && tooltipClose.Sent[0].unit==1 && tooltipClose.Paper.Closes==1,"The actual tooltip X close route acknowledges the newspaper before native close, even with a generic persistent callback");
Check(ModularUIFunctions.Pending.Count==genericCloses && tooltipClose.Panel.timeremaining==0 && !Newspaper.newspapershown,"The newspaper tooltip route bypasses generic window fading and performs native newspaper cleanup");
tooltipClose.Tick();tooltipClose.Click(closeX);
Check(tooltipClose.Sent.Count==1 && tooltipClose.Paper.Shows==1 && tooltipClose.Paper.runningnumber==2 && tooltipClose.Panel.timeremaining==0,"Following the X click, repair ticks and repeated clicks cannot reopen the same issue or send duplicate acknowledgements");
tooltipClose.Panel.go.SetActive(false);MultiplayerCampaign.ReplicaNewspaperClose(tooltipClose.Paper);tooltipClose.Tick();
Check(tooltipClose.Sent.Count==1 && !tooltipClose.Panel.go.activeInHierarchy,"The eventual OnDisable after an explicit X close stays dismissed");

foreach(string method in new[]{"ClosePanel","OpenClosePanel"}) {
    var unityClose=new MultiplayerCampaign();unityClose.Receive(Issue(1));unityClose.FadeIn();unityClose.Tick();
    unityClose.Click(unityClose.CloseControl(unityClose.Panel.go,false,method));unityClose.Tick();
    Check(unityClose.Sent.Count==1 && unityClose.Paper.Closes==1 && unityClose.Paper.Shows==1 && unityClose.Panel.timeremaining==0,
        "The actual persistent "+method+" X route dismisses the matching newspaper rather than only hiding its panel");
}
var directClose=new MultiplayerCampaign();directClose.Receive(Issue(1));directClose.FadeIn();directClose.Tick();
directClose.Click(directClose.CloseControl(directClose.Panel.go,false,"Close",directClose.Paper));
Check(directClose.Sent.Count==1 && directClose.Paper.Closes==1,"An explicit native Newspaper.Close Unity callback keeps its working dismissal route");

var otherClose=new MultiplayerCampaign();otherClose.Receive(Issue(1));otherClose.FadeIn();otherClose.Tick();
otherClose.MenuPanel.state=2;otherClose.MenuPanel.go.SetActive(true);genericCloses=ModularUIFunctions.Pending.Count;
otherClose.Click(otherClose.CloseControl(otherClose.MenuPanel.go,true,"ClosePanel"));
Check(otherClose.Sent.Count==0 && otherClose.Paper.Closes==0 && ModularUIFunctions.Pending.Count==genericCloses+1,"An unrelated tooltip close still follows the generic native window path without acknowledging the newspaper");
otherClose.Click(otherClose.CloseControl(otherClose.MenuPanel.go,false,"ClosePanel"));
Check(otherClose.Sent.Count==0 && otherClose.Paper.Closes==0 && otherClose.MenuPanel.timeremaining==0,"An unrelated persistent close keeps its native panel hide and cannot dismiss the newspaper");
ModularUIFunctions.Pending.Clear();

var queuedClose=new MultiplayerCampaign();queuedClose.Receive(Issue(1));queuedClose.FadeIn();queuedClose.Tick();
var queuedX=queuedClose.CloseControl(queuedClose.Panel.go,true,"ClosePanel");
ModularUIFunctions.OpenClosePanelStatic(queuedClose.Panel.go,false,true);queuedClose.Click(queuedX);ModularUIFunctions.Flush();queuedClose.Tick();
Check(queuedClose.Sent.Count==1 && !queuedClose.Panel.go.activeInHierarchy && queuedClose.Paper.Shows==1,"A native tooltip close already queued earlier in the click frame cannot race the explicit acknowledgement or resurrect the issue");

foreach(bool hasTooltip in new[]{true,false}) {
    var covered=new MultiplayerCampaign();covered.Receive(Issue(1));covered.FadeIn();covered.Tick();
    var behind=covered.CloseControl(covered.Panel.go,hasTooltip,"ClosePanel");
    var cover=new GameObject("Foreground modal graphic");cover.transform.Parent=covered.MenuPanel.go.transform;cover.SetActive(true);
    covered.MenuPanel.state=2;covered.MenuPanel.go.SetActive(true);genericCloses=ModularUIFunctions.Pending.Count;
    covered.Click(cover,behind);
    Check(covered.Sent.Count==0 && covered.Paper.Closes==0 && covered.Panel.timeremaining>0 && ModularUIFunctions.Pending.Count==genericCloses,
        "A foreground graphic blocks an underlying newspaper "+(hasTooltip?"tooltip":"persistent Button")+" close instead of acknowledging the covered issue");
}
var misdirected=new MultiplayerCampaign();misdirected.Receive(Issue(1));misdirected.FadeIn();misdirected.Tick();
misdirected.MenuPanel.state=2;misdirected.MenuPanel.go.SetActive(true);
var foreignCallback=misdirected.CloseControl(misdirected.MenuPanel.go,false,"Close",misdirected.Paper);misdirected.Click(foreignCallback);
Check(misdirected.Sent.Count==0 && misdirected.Paper.Closes==0,"A foreground control outside the newspaper cannot dismiss it through a foreign Newspaper.Close callback");
var foreignTooltip=misdirected.CloseControl(misdirected.MenuPanel.go,true,"ClosePanel");foreignTooltip.Tooltip.CloseThisPanelIfPressed=misdirected.Panel.go;
genericCloses=ModularUIFunctions.Pending.Count;misdirected.Click(foreignTooltip);
Check(misdirected.Sent.Count==0 && misdirected.Paper.Closes==0 && ModularUIFunctions.Pending.Count==genericCloses,"A foreign tooltip target cannot acknowledge or generically close the newspaper it does not belong to");
var childHit=new MultiplayerCampaign();childHit.Receive(Issue(1));childHit.FadeIn();childHit.Tick();
var parentX=childHit.CloseControl(childHit.Panel.go,false,"ClosePanel");var glyph=new GameObject("Close glyph");glyph.transform.Parent=parentX.transform;glyph.SetActive(true);
childHit.ClickHits((new GameObject("Physics terrain hit"),false),(glyph,true));
Check(childHit.Sent.Count==1 && childHit.Paper.Closes==1,"The first graphic hit resolves its own parent close button while preceding physics hits remain map input");

var barrierHost=new MultiplayerCampaign(true);
MultiplayerCampaign.ReplicaNewspaperShow(barrierHost.Paper,new[]{"Policy enacted"},"Body",1,"","",new Tools.Date(20,3,1863),false);
var barrierGuest=new MultiplayerCampaign();barrierGuest.Receive(Issue(1));barrierGuest.FadeIn();barrierGuest.Tick();
barrierGuest.Click(barrierGuest.CloseControl(barrierGuest.Panel.go,true,"ClosePanel"));barrierHost.Ack(barrierGuest.Sent.Single().unit);
Check(barrierHost.Waiting && GameVars.gamepaused,"An actual guest X click leaves the authoritative campaign paused until the host also dismisses");
MultiplayerCampaign.ReplicaNewspaperClose(barrierHost.Paper);
Check(!barrierHost.Waiting && !GameVars.gamepaused,"The matching routed guest acknowledgement and host dismissal release the normal two-player pause barrier");

foreach(bool producerFirst in new[]{true,false}) {
    var actual=new MultiplayerCampaign();actual.Receive(Issue(1));actual.FadeIn();actual.Tick();
    var close=actual.NativeCloseControl(actual.Panel.go);close.ThisButtonTooltip.mouseonobject=true;
    bool acknowledgedBeforeHide=false;close.BeforeHide=()=>acknowledgedBeforeHide=actual.Sent.Count==1;
    Check(close.ThisButtonTooltip.CloseThisPanelIfPressed==null && close.gameObject.Button==null,
        "The shipped newspaper X shape is reproduced with ClosePanel, a null tooltip target and no Unity Button");
    if(producerFirst)close.NativeInteraction(mouse:true,menuKey:false);
    actual.Click(close.gameObject);
    if(!producerFirst)close.NativeInteraction(mouse:true,menuKey:false);
    actual.Tick();
    Check(actual.Sent.Count==1 && acknowledgedBeforeHide && close.NativeCloses==1 && actual.Panel.timeremaining==0,
        "The actual native ClosePanel mouse producer acknowledges before HidePanel when it runs "+(producerFirst?"before":"after")+" replica UI routing");
    Check(close.ThisButtonTooltip.mouseonobject==false && close.ThisButtonTooltip.mouseonobject2==false && close.ButtonsToDeactivate[0].StateResets==1,
        "The native ClosePanel cleanup still clears hover and deactivates its configured buttons");
    actual.Panel.go.SetActive(false);MultiplayerCampaign.ReplicaNewspaperClose(actual.Paper);actual.Tick();
    Check(actual.Sent.Count==1 && !actual.Panel.go.activeInHierarchy && actual.Paper.Shows==1 && actual.Paper.runningnumber==2,
        "Native fade completion and OnDisable cannot reopen or acknowledge twice after the actual ClosePanel producer");
}
var nativeKey=new MultiplayerCampaign();nativeKey.Receive(Issue(1));nativeKey.FadeIn();nativeKey.Tick();
var keyClose=nativeKey.NativeCloseControl(nativeKey.Panel.go);
keyClose.NativeInteraction(mouse:false,menuKey:true);nativeKey.Tick();
Check(nativeKey.Sent.Count==1 && keyClose.NativeCloses==1 && nativeKey.Panel.timeremaining==0,
    "The native menu-key path dismisses the newspaper through the same DoClosePanel producer without mouse hover");
keyClose.NativeInteraction(mouse:false,menuKey:true);nativeKey.Tick();
Check(nativeKey.Sent.Count==1 && nativeKey.Paper.Shows==1,
    "Repeated native menu-key closes preserve the consumed newspaper identity");

var noClick=new MultiplayerCampaign();noClick.Receive(Issue(1));noClick.FadeIn();noClick.Tick();
var noClickControl=noClick.NativeCloseControl(noClick.Panel.go);noClickControl.NativeInteraction(mouse:true,menuKey:false);
Check(noClick.Sent.Count==0 && noClickControl.NativeCloses==0 && noClick.Panel.timeremaining>0,
    "The native ClosePanel producer does not run for a mouse click without its own hover eligibility");
noClick.Handler.HidePanel(noClick.Panel);noClick.Tick();
Check(noClick.Sent.Count==0 && noClick.Panel.timeremaining>0 && noClick.Paper.Shows==1,
    "A passive direct HidePanel remains recoverable and cannot become an explicit newspaper acknowledgement");

var foreignNative=new MultiplayerCampaign();foreignNative.Receive(Issue(1));foreignNative.FadeIn();foreignNative.Tick();
foreignNative.MenuPanel.state=2;foreignNative.MenuPanel.go.SetActive(true);
var foreignClose=foreignNative.NativeCloseControl(foreignNative.MenuPanel.go);foreignClose.ThisButtonTooltip.mouseonobject=true;
foreignClose.NativeInteraction(mouse:true,menuKey:false);
Check(foreignNative.Sent.Count==0 && foreignClose.NativeCloses==1 && foreignNative.MenuPanel.timeremaining==0,
    "A native ClosePanel for another panel retains its original cleanup without acknowledging the newspaper");
foreignClose.PanelsToClose=new[]{foreignNative.Panel.go};foreignClose.ThisButtonTooltip.mouseonobject=true;foreignClose.NativeInteraction(mouse:true,menuKey:false);
Check(foreignNative.Sent.Count==0,
    "A foreign native close component cannot acknowledge a newspaper merely by naming it in PanelsToClose");
var wrongHandler=foreignNative.NativeCloseControl(foreignNative.Panel.go);wrongHandler.PanelHandlerRef=new PanelHandler();wrongHandler.ThisButtonTooltip.mouseonobject=true;
wrongHandler.NativeInteraction(mouse:true,menuKey:false);
Check(foreignNative.Sent.Count==0 && wrongHandler.NativeCloses==1,
    "A close component bound to a different native handler cannot acknowledge the canonical newspaper");

var producerHost=new MultiplayerCampaign(true);
MultiplayerCampaign.ReplicaNewspaperShow(producerHost.Paper,new[]{"Policy enacted"},"Body",1,"","",new Tools.Date(20,3,1863),false);
var producerGuest=new MultiplayerCampaign();producerGuest.Receive(Issue(1));producerGuest.FadeIn();producerGuest.Tick();
var producerX=producerGuest.NativeCloseControl(producerGuest.Panel.go);producerX.ThisButtonTooltip.mouseonobject=true;producerX.NativeInteraction(mouse:true,menuKey:false);
producerHost.Ack(producerGuest.Sent.Single().unit);
Check(producerHost.Waiting && GameVars.gamepaused,"The actual native X producer sends its matching guest acknowledgement while retaining the host's pause barrier");
var hostX=producerHost.NativeCloseControl(producerHost.Panel.go);hostX.ThisButtonTooltip.mouseonobject=true;hostX.NativeInteraction(mouse:true,menuKey:false);
Check(producerHost.Waiting && hostX.NativeCloses==1 && producerHost.Sent.Count==0,
    "Host native ClosePanel still follows its original hide path and does not manufacture a guest acknowledgement");
producerHost.Panel.go.SetActive(false);MultiplayerCampaign.ReplicaNewspaperClose(producerHost.Paper);
Check(!producerHost.Waiting && !GameVars.gamepaused,"The original host fade and OnDisable complete the native two-player newspaper pause barrier");

var coveredNative=new MultiplayerCampaign();coveredNative.Receive(Issue(1));coveredNative.FadeIn();coveredNative.Tick();
var coveredNativeX=coveredNative.NativeCloseControl(coveredNative.Panel.go);coveredNativeX.ThisButtonTooltip.mouseonobject=true;
var foregroundGraphic=new GameObject("Covering graphic");foregroundGraphic.SetActive(true);coveredNative.PointAt((foregroundGraphic,true),(coveredNativeX.gameObject,true));
coveredNativeX.NativeInteraction(mouse:true,menuKey:false);
Check(coveredNative.Sent.Count==0 && coveredNativeX.NativeCloses==0 && coveredNative.Panel.timeremaining>0,
    "A stale native hover behind the front graphic cannot acknowledge or hide the newspaper at the actual producer");
Check(coveredNative.Diagnostics.Any(t=>t.StartsWith("Host newspaper close suppressed: issue=1; frame=") && t.Contains("firstGraphic=Covering graphic")),
    "A suppressed native close records its issue, frame and actual covering graphic");
var nativeGlyph=new GameObject("Native close glyph");nativeGlyph.transform.Parent=coveredNativeX.transform;nativeGlyph.SetActive(true);
coveredNative.PointAt((new GameObject("Physics terrain"),false),(nativeGlyph,true));coveredNativeX.NativeInteraction(mouse:true,menuKey:false);
Check(coveredNative.Sent.Count==1 && coveredNativeX.NativeCloses==1,
    "A current front graphic child of the actual ClosePanel is accepted without a Unity Button or button-name lookup");

var modalNative=new MultiplayerCampaign();modalNative.Receive(Issue(1));modalNative.FadeIn();modalNative.Tick();
var modalX=modalNative.NativeCloseControl(modalNative.Panel.go);modalX.ThisButtonTooltip.mouseonobject=true;modalNative.LocalMenu=true;
modalX.NativeInteraction(mouse:true,menuKey:false);
Check(modalNative.Sent.Count==0 && modalX.NativeCloses==0,"A local session modal suppresses the canonical native newspaper close producer");
Check(modalNative.Diagnostics.Any(t=>t.Contains("reason=session input blocked")),"A suppressed native close explains the local modal that owns input");
modalNative.LocalMenu=false;modalNative.OptionsPanel.state=2;modalX.NativeInteraction(mouse:false,menuKey:true);
Check(modalNative.Sent.Count==0 && modalX.NativeCloses==0,"The native menu key cannot dismiss the newspaper while native options cover it");
modalNative.OptionsPanel.state=0;modalX.NativeInteraction(mouse:false,menuKey:true);
Check(modalNative.Sent.Count==1 && modalX.NativeCloses==1,"The deliberate native menu key dismisses the same active newspaper after the covering modal closes");
Check(modalNative.Diagnostics.Any(t=>t.Contains("reason=native ClosePanel control")),"Accepted native producer diagnostics identify the actual ClosePanel acknowledgement route");

var boundedNative=new MultiplayerCampaign();boundedNative.Receive(Issue(1));boundedNative.FadeIn();boundedNative.Tick();
var boundedX=boundedNative.NativeCloseControl(boundedNative.Panel.go);boundedX.ThisButtonTooltip.mouseonobject=true;
var overlayParent=new GameObject("Overlay");var overlayChild=new GameObject("Foreground");overlayChild.transform.Parent=overlayParent.transform;
boundedNative.PointAt((overlayChild,true),(boundedX.gameObject,true));
boundedX.NativeInteraction(mouse:true,menuKey:false);boundedX.NativeInteraction(mouse:true,menuKey:false);
Check(boundedNative.Diagnostics.Count(t=>t.StartsWith("Host newspaper close suppressed:"))==1,
    "Suppression diagnostics are rate limited when a stale native producer repeats within the same frame");
for(int i=0;i<8;i++){Time.realtimeSinceStartup+=2;Time.frameCount++;boundedX.NativeInteraction(mouse:true,menuKey:false);}
Check(boundedNative.Sent.Count==0 && boundedX.NativeCloses==0 && boundedNative.Diagnostics.Count(t=>t.StartsWith("Host newspaper close suppressed:"))==4 &&
    boundedNative.Diagnostics.Any(t=>t.Contains("firstGraphic=Overlay/Foreground")),
    "Suppression evidence includes the full graphic path and is capped at four reports per issue");
boundedNative.Receive(Issue(2));boundedNative.FadeIn();boundedNative.Tick();boundedX.NativeInteraction(mouse:true,menuKey:false);
Check(boundedNative.Diagnostics.Count(t=>t.StartsWith("Host newspaper close suppressed: issue=2;"))==1,
    "A later issue gets a fresh bounded diagnostic budget without acknowledging the covered control");
Console.WriteLine($"Newspaper presentation: {checks} checks using production delivery and barrier code; Unity rendering and native UI callbacks are doubled.");

public static class GameVars
{
    public static bool debug_deactivatenewspapermessages,remainspausedpanelclosed,gamepaused;
    public static float gamespeed=5;
    public const int key_showmenu=0;public sealed class KeyBinding {public UnityEngine.KeyCode keycode=UnityEngine.KeyCode.Escape;}
    public static readonly KeyBinding[] keycodes={new()};
    public static void SetGameSpeed(float value){gamepaused=value==0;if(value>0)gamespeed=value;}
}
public static class Tools {public sealed class Date {public int day,month,year;public Date(int d,int m,int y){day=d;month=m;year=y;}}}
namespace TMPro {public sealed class TextMeshProUGUI {public string text="";}}
namespace UnityEngine
{
    public static class Time {public static float realtimeSinceStartup;public static int frameCount;}
    public enum KeyCode {Escape}
    public readonly struct Vector2 {}
    public static class Input
    {
        public static bool MouseDown,MenuDown;public static Vector2 mousePosition=>new();
        public static bool GetMouseButtonDown(int button)=>button==0&&MouseDown;
        public static bool GetKeyDown(KeyCode key)=>key==KeyCode.Escape&&MenuDown;
    }
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
        public readonly string name;public readonly Transform transform;public bool activeInHierarchy;public Newspaper Component;public ButtonTooltip Tooltip;public UnityEngine.UI.Button Button;public ClosePanel CloseControl;
        public GameObject(string value){name=value;transform=new Transform(this);}
        public void SetActive(bool active)=>activeInHierarchy=active;
        public T GetComponent<T>() where T:class=>(Component as T)??(Tooltip as T)??(Button as T)??(CloseControl as T);
        public T GetComponentInParent<T>() where T:class
        {for(var p=transform;p!=null;p=p.parent){var value=p.gameObject.GetComponent<T>();if(value!=null)return value;}return null;}
        public T GetComponentInChildren<T>(bool includeInactive) where T:class=>Component as T;
        public Scene scene=>new();
    }
    public readonly struct Scene {public bool IsValid()=>true;}
    public static class Resources {public static Newspaper[] Views=Array.Empty<Newspaper>();public static T[] FindObjectsOfTypeAll<T>()=>Views.OfType<T>().ToArray();}
}
namespace UnityEngine.EventSystems
{
    public struct RaycastResult {public GameObject gameObject;public object module;}
    public sealed class EventSystem
    {
        public static EventSystem current=new();public List<RaycastResult> Hits=new();
        public void RaycastAll(PointerEventData data,List<RaycastResult> hits)=>hits.AddRange(Hits);
    }
    public sealed class PointerEventData {public Vector2 position;public PointerEventData(EventSystem system){}}
}
namespace UnityEngine.UI
{
    public sealed class GraphicRaycaster {}
    public sealed class Button
    {
        public bool isActiveAndEnabled=true,interactable=true;
        public readonly Transform transform;public readonly ClickEvent onClick=new();public Button(GameObject go){transform=go.transform;go.Button=this;}
    }
    public sealed class ClickEvent
    {
        public readonly List<(object target,string method)> Callbacks=new();
        public int GetPersistentEventCount()=>Callbacks.Count;
        public object GetPersistentTarget(int index)=>Callbacks[index].target;
        public string GetPersistentMethodName(int index)=>Callbacks[index].method;
    }
}
public sealed class ButtonTooltip
{
    public bool isActiveAndEnabled=true,mouseonobject,mouseonobject2;public int StateResets;
    public GameObject CloseThisPanelIfPressed;public readonly GameObject gameObject;public string name=>gameObject.name;public Transform transform=>gameObject.transform;
    public ButtonTooltip(GameObject go){gameObject=go;go.Tooltip=this;}
    public void SetState(){StateResets++;}
}
public sealed class ClosePanel
{
    public readonly GameObject gameObject;public Transform transform=>gameObject.transform;
    public PanelHandler PanelHandlerRef;public GameObject[] PanelsToClose;public ButtonTooltip[] ButtonsToDeactivate;public ButtonTooltip ThisButtonTooltip;
    public int NativeCloses;public Action BeforeHide;
    public ClosePanel(GameObject go){gameObject=go;go.CloseControl=this;}
    public void NativeInteraction(bool mouse,bool menuKey)
    {Input.MouseDown=mouse;Input.MenuDown=menuKey;if(menuKey || mouse&&ThisButtonTooltip.mouseonobject)MultiplayerCampaign.ReplicaNewspaperClosePanel(this);}
    public void NativeDoClosePanel()
    {
        NativeCloses++;BeforeHide?.Invoke();
        foreach(var go in PanelsToClose) {
            if(PanelHandlerRef!=null) {var p=PanelHandlerRef.GetPanelFromGameObject(go);if(p!=null)PanelHandlerRef.HidePanel(p);}
            else ModularUIFunctions.OpenClosePanelStatic(go,false,true);
        }
        foreach(var button in ButtonsToDeactivate)button.SetState();
        ThisButtonTooltip.mouseonobject=ThisButtonTooltip.mouseonobject2=false;
    }
}
public sealed class Options {}
public sealed class Messages {public void CloseMessage(){}}
public sealed class ModularUIFunctions
{
    public static readonly List<GameObject> Pending=new();
    public static void OpenClosePanelStatic(GameObject target,bool open,bool close){if(close)Pending.Add(target);}
    public static void Flush()
    {
        foreach(var go in Pending){go.SetActive(false);if(go.Component!=null)MultiplayerCampaign.ReplicaNewspaperClose(go.Component);}
        Pending.Clear();
    }
}
public sealed class Newspaper
{
    public readonly GameObject gameObject;public Transform transform=>gameObject.transform;
    public TextMeshProUGUI DateT=new(),VolumeT=new(),ProsaLocationT=new(),ProsaT=new(),ImageTitleT=new();
    public TextMeshProUGUI[] HeadlineT=Enumerable.Range(0,5).Select(_=>new TextMeshProUGUI()).ToArray();
    public GameObject[] PanelsWhenOpenedNotResetSpeed=Array.Empty<GameObject>();
    public int runningnumber=1,Shows,Closes;public bool dontcontinuespeed,FailBefore,FailAfterCounter,SilentReturn;
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
    public void Close()=>MultiplayerCampaign.ReplicaNewspaperClose(this);
    public void NativeClose(){Closes++;Handler.panel[0].timeremaining=0;newspapershown=false;}
}
public sealed class PanelHandler
{
    public sealed class Panel {public int state;public float alpha,timeremaining;public readonly CanvasGroup canvasgroup=new();public readonly Canvas canvas=new();public GameObject go;}
    public readonly List<Panel> panel=new();public int cpanel_newspaper=0,cpanel_optionspanel=1,panel_mainmenusmall=2,cpanel_mainpanel=3,panel_messagelog=3,panel_messagesshown=4;
    public void ShowPanel(int id,float secsremaining,bool skipbuttonstatusupdate=false,bool skipmouseoverclearing=false)
    {
        var p=panel[id];p.timeremaining=secsremaining;if(p.state<=0 || p.state==2 || p.state==3)p.state=1;p.go.SetActive(true);if(p.alpha<0)p.alpha=0;
    }
    public Panel GetPanelFromGameObject(GameObject go)=>panel.FirstOrDefault(p=>p.go==go);
    public void HidePanel(Panel target){target.timeremaining=0;}
    public void SetButtonsCondition(Panel target,int state,int button){}
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
        public Newspaper Paper {get;}public PanelHandler.Panel Panel=>ui.panelhandler.panel[0];public PanelHandler.Panel OptionsPanel=>ui.panelhandler.panel[1];public PanelHandler.Panel MenuPanel=>ui.panelhandler.panel[2];
        public PanelHandler Handler=>ui.panelhandler;
        public bool Waiting=>newsBarrier.Waiting;
        public string Status=>orderStatus;
        public MultiplayerCampaign(bool isHost=false)
        {
            instance=this;host=isHost;Newspaper.newspapershown=false;
            UnityEngine.EventSystems.EventSystem.current=new();Input.MouseDown=Input.MenuDown=false;
            var handler=new PanelHandler();ui.panelhandler=handler;
            Paper=new Newspaper(new GameObject("Native newspaper")){Handler=handler};
            handler.panel.Add(new PanelHandler.Panel{go=Paper.gameObject});
            handler.panel.Add(new PanelHandler.Panel{go=new GameObject("Options")});handler.panel.Add(new PanelHandler.Panel{go=new GameObject("Menu")});
            handler.panel.Add(new PanelHandler.Panel{go=new GameObject("Message log")});handler.panel.Add(new PanelHandler.Panel{go=new GameObject("Message buttons")});
        }
        public void Receive(MpNewspaperState state){instance=this;ApplyNewspaper(new[]{new MpStateRow{Kind=11,State=state}});}
        public void Tick(){instance=this;RefreshGuestNewspaper();}
        public void FadeIn(){Panel.state=2;Panel.alpha=Panel.canvasgroup.alpha=1;}
        public void Clear(){ClearNewspaperPresentation();newspaperState=new MpNewspaperState();}
        public void Ack(int serial){instance=this;NewspaperMessage(new MpMessage{kind="news-ack",unit=serial});}
        public GameObject CloseControl(GameObject target,bool tooltip,string method,object receiver=null)
        {
            var go=new GameObject("018_ClosePanel");go.transform.Parent=target.transform;go.SetActive(true);
            if(tooltip)new ButtonTooltip(go){CloseThisPanelIfPressed=target};
            var control=new UnityEngine.UI.Button(go);control.onClick.Callbacks.Add((receiver??new ModularUIFunctions(),method));return go;
        }
        public ClosePanel NativeCloseControl(GameObject target)
        {
            instance=this;
            var go=new GameObject("018_ClosePanel");go.transform.Parent=target.transform;go.SetActive(true);
            var tooltip=new ButtonTooltip(go);
            var close=new ClosePanel(go){PanelHandlerRef=ui.panelhandler,PanelsToClose=new[]{target},ThisButtonTooltip=tooltip,
                ButtonsToDeactivate=new[]{new ButtonTooltip(new GameObject("Linked button"))}};
            PointAt((go,true));return close;
        }
        public void Click(params GameObject[] targets)=>ClickHits(targets.Select(target=>(target,true)).ToArray());
        public void ClickHits(params (GameObject target,bool graphic)[] targets)
        {instance=this;PointAt(targets);Input.MouseDown=true;Input.MenuDown=false;ReplicaLocalClose(UnityEngine.EventSystems.EventSystem.current.Hits,ui.panelhandler);}
        public void PointAt(params (GameObject target,bool graphic)[] targets)
        {instance=this;UnityEngine.EventSystems.EventSystem.current.Hits=targets.Select(h=>new UnityEngine.EventSystems.RaycastResult{gameObject=h.target,module=h.graphic?new UnityEngine.UI.GraphicRaycaster():null}).ToList();}
        static ButtonTooltip HitReplicaButton(List<UnityEngine.EventSystems.RaycastResult> hits)=>hits.Select(h=>h.gameObject.Tooltip).FirstOrDefault(t=>t!=null);
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
            if(target is ClosePanel close){if(method=="GTIP_Original_ReplicaNewspaperClosePanel")close.NativeDoClosePanel();return null;}
            var paper=(Newspaper)target;
            if(method=="GTIP_Original_ReplicaNewspaperShow")paper.NativeShow(args);
            else if(method=="GTIP_Original_ReplicaNewspaperClose")paper.NativeClose();
            return null;
        }
    }
}
