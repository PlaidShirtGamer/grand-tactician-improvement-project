using GTIP;

int checks=0;
void Check(bool valid,string text) {if(!valid)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
(MultiplayerCampaign Mp,Autocalc Battle,AutoresolvePanel Panel) Setup(bool guest=false)
{
    MultiplayerCampaign.TacticalHolding=false;GameVars.playeralliance=0;GamePrefs.debug_loaddebugarmysetup=false;
    SceneManagement.currentoperation=2;SceneManagement.batchoperation.Clear();
    var mp=new MultiplayerCampaign{host=!guest};
    var battle=new Autocalc();var monument=new BattleMonument{autocalcref=battle};battle.Monument=monument;
    mp.Register(1,monument);
    var panel=new AutoresolvePanel(battle);AutoresolvePanel.autoresolvepanel=panel;AutoresolvePanel.autocalcrefopen=battle;
    return(mp,battle,panel);
}
var context=Setup(true);
Check(!context.Mp.Click(new ButtonTooltip{name=context.Panel.BT_PlayManually.name}),
    "A different button with the same name cannot issue an active-battle manual intent");
context.Panel.BT_PlayManually.state=2;
Check(context.Mp.Click(context.Panel.BT_PlayManually) && context.Mp.Requests.Count==0,
    "The actual disabled manual button is consumed without a request or native callback");
context.Panel.BT_PlayManually.state=0;
Check(context.Mp.Click(context.Panel.BT_PlayManually) && context.Mp.Requests.Count==1 && context.Battle.ManualCalls==0,
    "Guest play sends one intent and does not execute native battle creation locally");
Check(context.Mp.Requests[0].Action=="tactical-manual" && context.Mp.Requests[0].Id==1,
    "The guest request addresses the registered active battle identity");
context=Setup(true);AutoresolvePanel.autocalcrefopen=new Autocalc();
context.Mp.Click(context.Panel.BT_PlayManually);
Check(context.Mp.Requests.Count==0,"A stale static panel reference cannot target another battle");
context=Setup(true);context.Panel.ReplaceReference(new Autocalc());
context.Mp.Click(context.Panel.BT_PlayManually);
Check(context.Mp.Requests.Count==0,"The panel's private native reference must match the open battle");
context=Setup(true);context.Mp.Unregister();context.Mp.Click(context.Panel.BT_PlayManually);
Check(context.Mp.Requests.Count==0,"An unregistered guest battle cannot become a launch target");
context=Setup(true);context.Panel.isActiveAndEnabled=false;context.Mp.Click(context.Panel.BT_PlayManually);
Check(context.Mp.Requests.Count==0,"A closed or inactive battle panel cannot issue a guest intent");
context=Setup(true);MultiplayerCampaign.TacticalHolding=true;context.Mp.RequestFallback(context.Battle);
Check(context.Mp.Requests.Count==0,"A native guest fallback cannot issue another launch after the shared hold begins");
context=Setup(true);context.Mp.RequestFallback(context.Battle);
Check(context.Mp.Requests.Count==1 && context.Battle.ManualCalls==0,
    "The native guest fallback uses the same validated intent route");

context=Setup();
Check(!context.Mp.Execute("other",new MpMessage{unit=1},""),"Unrelated native commands remain available to other handlers");
Check(context.Mp.Execute("tactical-manual",new MpMessage{unit=1},"") && context.Battle.ManualCalls==1 && context.Battle.LaunchedByPlayer && MultiplayerCampaign.TacticalHolding,
    "An admitted host intent invokes native manual battle entry once and starts the shared transition");
bool denied=false;
try {context.Mp.Execute("tactical-manual",new MpMessage{unit=1},"");}catch(InvalidOperationException){denied=true;}
Check(denied && context.Battle.ManualCalls==1,"A second intent cannot start a second native battle after the hold begins");

var invalid=new (string Name,Action<MultiplayerCampaign,Autocalc> Change)[] {
    ("guest-side handler",(m,b)=>m.host=false),
    ("unauthenticated peer",(m,b)=>m.authenticated=false),
    ("non-live campaign",(m,b)=>m.phase="sending"),
    ("disabled shared battle proof",(m,b)=>m.tacticalProofEnabled=false),
    ("debug army override",(m,b)=>GamePrefs.debug_loaddebugarmysetup=true),
    ("already held campaign",(m,b)=>MultiplayerCampaign.TacticalHolding=true),
    ("unfinished native initialization",(m,b)=>m.Ready=false),
    ("non-cooperative session",(m,b)=>m.mode="versus"),
    ("different guest side",(m,b)=>m.guestSide=1),
    ("changed native player side",(m,b)=>GameVars.playeralliance=1),
    ("another native operation",(m,b)=>SceneManagement.currentoperation=3),
    ("queued native scene operation",(m,b)=>SceneManagement.batchoperation.Add(new object())),
    ("missing battle identity",(m,b)=>m.Unregister()),
    ("finished battle monument",(m,b)=>b.Monument.autocalcref=null),
    ("inactive battle monument",(m,b)=>b.Monument.gameObject.activeInHierarchy=false),
    ("inactive autocalculation",(m,b)=>b.isActiveAndEnabled=false),
    ("naval engagement",(m,b)=>b.type=3),
    ("unsupported minor engagement",(m,b)=>b.type=0),
    ("no player controlled units",(m,b)=>b.hasplayercontrolledunits=false),
    ("fort engagement",(m,b)=>b.Fort=true),
    ("empty opposing army",(m,b)=>b.envolvedunits[1].topunits.Clear()),
    ("missing initial opposing unit",(m,b)=>b.envolvedunits[1].topunits[0]=null),
    ("unrelated battle alliance",(m,b)=>b.alliance=new[]{1,2}),
    ("unready player's army",(m,b)=>b.envolvedunits[0].topunits[0].readiness=.25f),
};
foreach(var test in invalid) {
    context=Setup();test.Change(context.Mp,context.Battle);denied=false;
    try {context.Mp.Execute("tactical-manual",new MpMessage{unit=1},"");}catch(InvalidOperationException){denied=true;}
    Check(denied && context.Battle.ManualCalls==0,"Host rejects "+test.Name+" before native battle creation");
}
foreach(var request in new[]{new MpMessage{unit=0},new MpMessage{unit=2},new MpMessage{unit=1,x=1},new MpMessage{unit=1,x=float.NaN}}) {
    context=Setup();denied=false;
    try {context.Mp.Execute("tactical-manual",request,"");}catch(InvalidOperationException){denied=true;}
    Check(denied && context.Battle.ManualCalls==0,"Malformed or stale launch arguments cannot enter the native method");
}
context=Setup();denied=false;
try {context.Mp.Execute("tactical-manual",new MpMessage{unit=1},"assault");}catch(InvalidOperationException){denied=true;}
Check(denied && context.Battle.ManualCalls==0,"Extra action data cannot turn manual play into an assault or retreat command");
context=Setup();context.Battle.envolvedunits[0].topunits=new List<Regiment>{new Regiment{readiness=.25f},new Regiment{readiness=.5f}};
denied=false;try {context.Mp.Execute("tactical-manual",new MpMessage{unit=1},"");}catch(InvalidOperationException){denied=true;}
Check(denied && context.Battle.ManualCalls==0,"The native floor-average readiness gate rejects an average below one step");
context=Setup();context.Battle.envolvedunits[0].topunits=new List<Regiment>{new Regiment{readiness=.5f},new Regiment{readiness=.5f}};
Check(context.Mp.Execute("tactical-manual",new MpMessage{unit=1},"") && context.Battle.ManualCalls==1,
    "The native floor-average readiness gate admits an army with one full readiness step");
Console.WriteLine("Tactical commands: "+checks+" checks passed using the production admission and dispatch bridge.");

public sealed class ButtonTooltip {public string name="700_PlayManually";public int state;}
public sealed class FakeGameObject {public bool activeInHierarchy=true;}
public sealed class BattleMonument {public Autocalc autocalcref;public FakeGameObject gameObject=new FakeGameObject();}
public sealed class Regiment {public float readiness=.75f;}
public static class CampaignArmyPanel
{
    public static int GetReadinessStep(Regiment reg)=>reg.readiness<=.25f?0:reg.readiness<=.5f?1:reg.readiness<=.75f?2:3;
}
public sealed class Autocalc
{
    public sealed class EnvolvedUnits {public List<Regiment> topunits=new List<Regiment>{new Regiment()};}
    public bool isActiveAndEnabled=true,hasplayercontrolledunits=true,Fort,LaunchedByPlayer;
    public int type=2,ManualCalls;
    public int[] alliance=new[]{0,1};
    public EnvolvedUnits[] envolvedunits=new[]{new EnvolvedUnits(),new EnvolvedUnits()};
    public BattleMonument Monument;
    public T GetComponent<T>() where T:class => Monument as T;
    public bool IsFortEngagement()=>Fort;
    public List<Regiment> GetPlayerUnits()=>envolvedunits[Array.IndexOf(alliance,GameVars.playeralliance)].topunits;
    public void StartManualBattleFromAutocalc(bool launchedbyplayer=true) {ManualCalls++;LaunchedByPlayer=launchedbyplayer;MultiplayerCampaign.TacticalHolding=true;}
}
public sealed class AutoresolvePanel
{
    public static AutoresolvePanel autoresolvepanel;
    public static Autocalc autocalcrefopen;
    public ButtonTooltip BT_PlayManually=new ButtonTooltip();
    public bool isActiveAndEnabled=true;
#pragma warning disable CS0414
    Autocalc autocalcref;
#pragma warning restore CS0414
    public AutoresolvePanel(Autocalc battle){autocalcref=battle;}
    public void ReplaceReference(Autocalc battle){autocalcref=battle;}
}
public static class GameVars {public static int playeralliance;}
public static class GamePrefs {public static bool debug_loaddebugarmysetup;}
public static class SceneManagement {public static int currentoperation=2;public static List<object> batchoperation=new List<object>();}
namespace GTIP
{
    public sealed class MpMessage {public int unit;public float x;}
    public sealed partial class MultiplayerCampaign
    {
        public static bool TacticalHolding;
        public bool host=true,authenticated=true,tacticalProofEnabled=true,Ready=true;
        public string phase="live",mode="coop";
        public int guestSide,hostSide;
        readonly Dictionary<int,BattleMonument> monuments=new Dictionary<int,BattleMonument>();
        public readonly List<(string Action,int Id)> Requests=new List<(string,int)>();
        public void Register(int id,BattleMonument monument){monuments.Add(id,monument);}
        public void Unregister(){monuments.Clear();}
        int BattleReference(BattleMonument monument)=>monuments.Where(p=>p.Value==monument).Select(p=>p.Key).FirstOrDefault();
        BattleMonument BattleById(int id)=>monuments.TryGetValue(id,out var result)?result:null;
        void RequestNative(string action,int target=0,float value=0,string detail=""){Requests.Add((action,target));}
        public bool Click(ButtonTooltip button)=>GuestTacticalManualButton(button);
        public void RequestFallback(Autocalc battle)=>RequestTacticalManual(battle);
        public bool Execute(string action,MpMessage request,string detail)=>ExecuteTacticalManual(action,request,detail);
    }
}
