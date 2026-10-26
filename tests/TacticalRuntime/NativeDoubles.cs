using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace UnityEngine {
 public class GameObject {
  public bool activeInHierarchy=true;
  public static GameObject Find(string name)=>new();
  public T GetComponent<T>() where T:new()=>new();
 }
 public static class Time {public static float realtimeSinceStartup;}
 public struct Color {public static Color white=>new();public static Color black=>new();}
 public sealed class Texture2D {public Texture2D(int x,int y){}public void SetPixel(int x,int y,Color color){}public void Apply(){}}
 public struct Rect {public Rect(float x,float y,float w,float h){}}
 public static class Screen {public static int width=1920,height=1080;}
 public static class GUI {public static Color color;public static int Covers;public static void DrawTexture(Rect rect,Texture2D texture){Covers++;}public static void Label(Rect rect,string text){}}
}
public sealed class Regiment {public GameObject gameObject=new();public bool initialized=true;}
public sealed class BattlefieldSetup {public int initialized=4;}
public static class BattleUnits {public static GameObject unitfolder;public static List<Regiment> completeunitlist=new();}
public static class GameVars {public static int frame=61;public static bool allregimentsinitialized=true,gamepaused,showallobjectivenames;public static object commander=new();}
public static class GamePrefs {public static string leveltoload="001",subleveltoload="A";public static bool debug_loaddebugarmysetup;}
public static class SceneManagement {
 public static int currentoperation=2;public static string currentbattlescenario;
 public static List<object> batchoperation=new();
 public static void SaveAll(bool a,bool b,bool c,bool d,bool e,string text){GTIP.MultiplayerCampaign.TestCurrent.SaveCalls++;}
}
public static class Saving {public static string latestfolder="SaveExisting";}
public static class ImportExportUnitData {public static void SaveBattle(bool autosave,string name){GTIP.MultiplayerCampaign.TestCurrent.BattleSaveCalls++;}}
public sealed class LoadingScreen {}
public sealed class BattleUiDouble {public bool IsCampaign=true,IsMainMenu;public PanelDouble panelhandler=new();}
public sealed class PanelDouble {public int panel_choosepaper,panel_briefing,panel_objectivespaper,panel_statistics;public object panel;public CameraDouble cambf=new();public void HidePanel(object panels,int index,bool hideimmediately){}}
public sealed class CameraDouble {public void PreventCamMovement(bool a,bool b){}}

namespace GTIP {
 public static class NativeAccess {
  public static object Call(object target,string name,params object[] args) {
   var mp=MultiplayerCampaign.TestCurrent;
   if(name=="GTIP_Original_TacticalPrepare"){mp.PrepareCalls++;SceneManagement.currentoperation=4;}
   else if(name=="GTIP_Original_TacticalHideBlack")mp.Reveals++;
   else if(name=="GTIP_Original_TacticalExit")mp.ResultExitCalls++;
   else throw new Exception("Unexpected native call "+name);
   return null;
  }
  public static T Get<T>(object target,string field)=>field=="initialized" && target is Regiment unit?(T)(object)unit.initialized:throw new Exception("Unexpected native field "+field);
 }
 public static class CampaignEditor {public static bool BlocksInput;}
 public static class MpCheckpoint {
  public const int MaxBytes=64*1024*1024,ChunkBytes=48*1024;
  public static string Hash(byte[] bytes)=>Convert.ToBase64String(SHA256.HashData(bytes));
  public static byte[] Pack(string path){var bytes=new byte[ChunkBytes+17];Encoding.UTF8.GetBytes("authoritative pre-battle campaign").CopyTo(bytes,0);return bytes;}
  public static void Unpack(byte[] data,string hash,string path){if(Hash(data)!=hash)throw new Exception("Wrong checkpoint hash");}
 }
 public static class MpTacticalCheckpoint {public static byte[] Pack(string path)=>Encoding.UTF8.GetBytes("authoritative paused battlefield");}
 public sealed class MpTacticalLoadPlan {
  public string ContentHash="installed game content",Battlefield="212",Scenario="001/A";public bool Returning;
  public static void ValidateScenario(string scenario){if(scenario!="001/A")throw new InvalidDataException("Unexpected fixture campaign");}
 }
 public sealed class MpTacticalNativeTicket {
  public MpTacticalLoadRequest Request;public MpTacticalLoadPlan Plan;
  public bool Returning=>Plan.Returning;public string Battlefield=>Plan.Battlefield;
 }
 public static class MpTacticalNativeScene {
  public static MpTacticalNativeTicket Load(MpTacticalLoadRequest request,string scenario,int side) {
   var mp=MultiplayerCampaign.TestCurrent;mp.NativeLoads++;mp.LastLoad=request;
   return new(){Request=request,Plan=new(){Returning=request.ReturningToCampaign,Scenario=scenario}};
  }
 }
 public static class MpTacticalNativeState {
  public static string Capture(bool campaign){MultiplayerCampaign.TestCurrent.CaptureCalls++;return MultiplayerCampaign.TestCurrent.WorldDigest;}
 }
 public sealed class MpNewspaperState {}
 public sealed class TestTransport {public int PendingOutgoing;}
 public sealed class TestBaseline {public int Revision=41;public string Hash=MpCheckpoint.Hash(Encoding.UTF8.GetBytes("campaign41"));}
 public sealed partial class MultiplayerCampaign {
  static MultiplayerCampaign instance;
  public static MultiplayerCampaign TestCurrent=>instance;
  bool host,authenticated=true,panel,replicaReady=true,blockPanelResume,officerViewDirty,guestFileMenu,replicaMutated,hasLiveData;
  string session="test-connection",phase="live",message,lastAlertCounts,orderStatus;
  int hostSide,guestSide,previewCommander,clockSequence,receivedClockSequence,nextId;
  float lastDataSend,lastSend;
  GameObject root;object officers,previewPanel,snapshot,restoreClock,hostClock,replicaTimePanel,replicaTopPanel,campaignClockLabels,guestSelection;
  bool choosingCommander;Texture2D white;
  readonly BattleUiDouble ui=new();TestTransport transport=new();readonly TestBaseline baseline=new();TestBaseline Baseline=>baseline;
  object outgoingData,incomingData,prepareTask,receiveTask,commitTask;
  readonly Dictionary<int,object> pendingUniforms=new(),pendingPromotions=new(),financeViews=new(),replicaAlerts=new(),alertPositions=new(),replicaVisibility=new(),originalPositions=new(),originalRotations=new(),originalActive=new(),originalParents=new(),originalSiblingIndices=new(),ids=new(),units=new(),groupIds=new(),groups=new();
  readonly List<int> panelPause=new(),newsBarrier=new();MpNewspaperState newspaperState=new();
  public readonly Queue<MpMessage> Sent=new();
  public int PrepareCalls,SaveCalls,BattleSaveCalls,Reveals,NativeLoads,CaptureCalls,ResultExitCalls,ManifestBuilds,ReplicaSaves,InputBlocks,InputRestores;
  public string WorldDigest="matching initialized scene";
  public MpTacticalLoadRequest LastLoad;
  public Action DataAction;
  GameObject localRoot=new();readonly List<object> localBatches=new();int localOperation=2,localFrame=61;bool localCampaign=true,localMenu,localInitialized=true;string localLevel="001",localSublevel="A",localBattle;
  public MultiplayerCampaign(bool isHost){host=isHost;tacticalProofEnabled=true;root=localRoot;}
  bool Ready=>ui.IsCampaign && !ui.IsMainMenu && GameVars.frame>50 && GameVars.allregimentsinitialized;
  public string Stage=>tacticalStage;public bool Held=>tacticalHolding || tacticalFailureHeld;public bool FailureHeld=>tacticalFailureHeld;
  public bool Replica=>replicaReady;public int Generation=>TacticalGeneration;public int Revision=>baseline.Revision;
  public bool Overlay {get{Use();int prior=GUI.Covers;DrawTacticalLoading();return GUI.Covers>prior;}}
  public bool HasReturnArchive=>tacticalReturnArchive!=null;
  void Use(){instance=this;Time.realtimeSinceStartup+=.01f;ui.IsCampaign=localCampaign;ui.IsMainMenu=localMenu;GameVars.frame=localFrame;GameVars.allregimentsinitialized=localInitialized;BattleUnits.unitfolder=localRoot;SceneManagement.currentoperation=localOperation;SceneManagement.currentbattlescenario=localBattle;GamePrefs.leveltoload=localLevel;GamePrefs.subleveltoload=localSublevel;SceneManagement.batchoperation.Clear();SceneManagement.batchoperation.AddRange(localBatches);}
  void Store(){localOperation=SceneManagement.currentoperation;localBatches.Clear();localBatches.AddRange(SceneManagement.batchoperation);}
  public void Begin(){Use();TacticalPrepare("212","A",null);Store();}
  public bool Message(MpMessage value){Use();bool accepted=TacticalMessage(MpCodec.Decode(MpCodec.Encode(value)));Store();return accepted;}
  public void Tick(){Use();TacticalTick();Store();}
  public void Return(){Use();ReturnTacticalCampaign();Store();}
  public bool End(){Use();bool guest=EndTactical();transport=null;Store();return guest;}
  public void Recover(){Use();RecoverTacticalCampaign();Store();}
  public void OfflineTick(){Use();TacticalOfflineTick();Store();}
  public void NativeHide(){Use();TacticalHideBlack();Store();}
  public void NativeExit(){Use();TacticalExit();Store();}
  public void FinishNative(bool campaign){localRoot=new();localCampaign=campaign;localOperation=campaign?2:3;localFrame=61;localInitialized=true;localLevel=campaign?"001":"212";localSublevel="A";localBattle=campaign?null:"212";localBatches.Clear();}
  public void UnexpectedRootReplacement(){localRoot=new();}
  public void QueueNativeBatch(){localBatches.Add(new());}
  public void FinishMainMenu(){localMenu=true;localCampaign=false;}
  public void SetFrame(int frame){localFrame=frame;}
  public void SetBaseline(int revision,string hash){baseline.Revision=revision;baseline.Hash=hash;}
  public void Busy(string work,bool busy){object value=busy?new():null;switch(work){case "prepare":prepareTask=value;break;case "outgoing":outgoingData=value;break;case "incoming":incomingData=value;break;case "receive":receiveTask=value;break;case "commit":commitTask=value;break;default:throw new Exception(work);}}
  public void QueueBusy(int count){transport.PendingOutgoing=count;}
  public void SetProof(bool enabled){tacticalProofEnabled=enabled;}
  public void SetNativeInitialized(bool initialized){localInitialized=initialized;}
  void Send(MpMessage value){Sent.Enqueue(MpCodec.Decode(MpCodec.Encode(value)));}
  void LogDiagnostic(string text){}
  void DataTick(){DataAction?.Invoke();}
  public static void HoldTacticalClock(){GameVars.gamepaused=true;}
  void BlockInput(){InputBlocks++;}void RestoreInput(){InputRestores++;}void ClosePanel(){panel=false;}
  void DestroyRecruitPreview(){}void ClearRecruitRequest(){}void ClearCommanderPortraits(){}
  void ClearData(bool restore){baseline.Revision=0;baseline.Hash=null;outgoingData=incomingData=prepareTask=receiveTask=commitTask=null;}
  void ClearMovement(){}void ClearNewspaperPresentation(){}void ClearMapNotices(){}void ClearDispatchState(){}void ClearWorld(){}
  void BuildManifest(){ManifestBuilds++;}void SaveReplicaState(){ReplicaSaves++;}
 }
}
