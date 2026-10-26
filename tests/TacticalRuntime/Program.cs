using GTIP;
using System.Text;

int checks=0;
void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
void Reject(Action action,string message){bool rejected=false;try{action();}catch(Exception e)when(e is InvalidOperationException || e is InvalidDataException){rejected=true;}Check(rejected,message);}
string Hash(string text)=>MpCheckpoint.Hash(Encoding.UTF8.GetBytes(text));
MpMessage Take(MultiplayerCampaign mp,string kind){if(!mp.Sent.TryDequeue(out var message) || message.kind!=kind)throw new Exception("Expected "+kind+", got "+message?.kind);return message;}
void Pump(MultiplayerCampaign host,MultiplayerCampaign guest){int limit=200;while(host.Sent.Count+guest.Sent.Count>0){if(--limit==0)throw new Exception("Tactical adapter transport did not settle");if(host.Sent.TryDequeue(out var toGuest))guest.Message(toGuest);if(guest.Sent.TryDequeue(out var toHost))host.Message(toHost);}}
void Drain(MultiplayerCampaign host,MultiplayerCampaign guest){host.Begin();guest.Message(Take(host,"tactical-hold"));host.Tick();guest.Message(Take(host,"tactical-drained"));guest.Tick();host.Message(Take(guest,"tactical-held"));host.Tick();}
void LoadBoth(MultiplayerCampaign host,MultiplayerCampaign guest){host.FinishNative(false);host.Tick();Pump(host,guest);host.FinishNative(false);guest.FinishNative(false);host.Tick();Pump(host,guest);guest.Tick();Pump(host,guest);}

var h=new MultiplayerCampaign(true);var g=new MultiplayerCampaign(false);
h.Busy("prepare",true);h.Begin();
Check(h.Held && h.Stage=="drain" && h.PrepareCalls==0,"Host holds input and simulation before any campaign object is replaced");
Check(h.Overlay,"Host draws the shared loading cover immediately");
g.Message(Take(h,"tactical-hold"));
Check(g.Held && g.Stage=="drain" && g.Overlay,"The first hold message covers and freezes the guest immediately");
int guestReveals=g.Reveals;g.NativeHide();
Check(g.Reveals==guestReveals,"A native hide-loading callback cannot expose the guest before readiness");
g.Tick();
Check(g.Sent.Count==0 && g.Stage=="drain","A locally quiet guest does not acknowledge before the host's final fence");
h.Tick();
Check(h.Sent.Count==0 && h.PrepareCalls==0,"A delayed capture worker cannot be bypassed by an early guest hold");
g.NativeExit();
Check(g.ResultExitCalls==0 && g.Held,"Guest exit controls cannot calculate battle results or remove the shared hold");
Reject(h.Return,"The host cannot return before both players finish battle loading");

// The capture that was pending at the hold may produce a last campaign delta.
// Keep the guest in drain while it applies that revision, then send a FIFO fence.
h.Busy("prepare",false);h.Busy("outgoing",true);h.Tick();
Check(h.Sent.Count==0,"An unacknowledged campaign transfer prevents the final host fence");
g.Busy("receive",true);string finalHash=Hash("last in-flight revision");h.SetBaseline(42,finalHash);h.Busy("outgoing",false);
h.QueueBusy(1);h.Tick();Check(h.Sent.Count==0,"Queued reliable campaign frames remain ahead of the final fence");
h.QueueBusy(0);h.Tick();
var fence=Take(h,"tactical-drained");
Check(fence.baseRevision==42 && fence.stateHash==finalHash && h.PrepareCalls==0,"The fence identifies the final drained host revision without starting native loading");
g.Message(fence);g.Tick();
Check(g.Stage=="drain" && g.Sent.Count==0,"A delivered fence cannot bypass the guest's pending campaign application worker");
g.SetBaseline(42,finalHash);g.Busy("receive",false);g.Tick();
Check(g.Stage=="waiting-battle" && g.Overlay,"Only matching applied guest state advances to waiting for the battle archive");
var held=Take(g,"tactical-held");h.Message(held);
Check(h.PrepareCalls==0,"Receipt handling itself does not reenter the native scene loader");
h.Tick();
Check(h.Stage=="host-loading" && h.PrepareCalls==1 && h.SaveCalls==1 && h.HasReturnArchive,"The next drained host tick saves recovery state and invokes native battle preparation once");
h.Tick();Check(h.PrepareCalls==1,"Repeated loading ticks cannot invoke native battle preparation twice");

h.FinishNative(false);h.SetFrame(50);h.Tick();
Check(h.BattleSaveCalls==0 && h.NativeLoads==0,"Early native startup cannot be archived or claim initialized-world readiness");
h.SetFrame(61);h.SetNativeInitialized(false);h.Tick();
Check(h.BattleSaveCalls==0,"An incomplete regiment initialization keeps the native scene barrier closed");
h.SetNativeInitialized(true);h.Tick();Pump(h,g);
Check(h.Stage=="load-battle" && g.Stage=="load-battle" && h.NativeLoads==1 && g.NativeLoads==1,"Both peers load the same verified tactical archive through the real session controller");
Check(h.Overlay && g.Overlay && h.Reveals==0 && g.Reveals==0,"Checkpoint delivery alone cannot reveal either battlefield");
Check(h.Generation==1 && g.Generation==1,"Both adapters advance the shared world generation exactly once");
h.FinishNative(false);h.Tick();Pump(h,g);
Check(h.Stage=="load-battle" && g.Stage=="load-battle" && h.Overlay && g.Overlay,"A faster host's live native digest cannot expose an unfinished guest scene");
g.FinishNative(false);g.Tick();Pump(h,g);
Check(h.Stage=="paused" && g.Stage=="paused" && h.Held && g.Held,"Matching native digests reveal both peers only in the guarded paused battle phase");
Check(!h.Overlay && !g.Overlay && h.Reveals==1 && g.Reveals==1,"The shared loading cover is removed once after both initialized worlds match");
Check(h.CaptureCalls==1 && g.CaptureCalls==1,"The runtime fingerprints each initialized native graph once for the readiness barrier");
Reject(g.Return,"Only the host may initiate restoration of the pre-battle campaign");

h.NativeExit();
int guestLoadsBeforeReturn=g.NativeLoads,guestRevealsBeforeReturn=g.Reveals;
g.Message(Take(h,"tactical-offer"));
Check(g.Stage=="returning" && g.Overlay && g.NativeLoads==guestLoadsBeforeReturn,"The return offer immediately covers the guest's battlefield before any campaign archive bytes arrive");
g.Tick();g.NativeHide();
Check(g.Stage=="returning" && g.Reveals==guestRevealsBeforeReturn && g.NativeLoads==guestLoadsBeforeReturn,"A slow campaign transfer keeps the guest covered without prematurely loading or revealing a scene");
h.Message(Take(g,"tactical-next"));g.Message(Take(h,"tactical-chunk"));
Check(g.Stage=="returning" && g.Overlay && g.NativeLoads==guestLoadsBeforeReturn,"Receiving only the first campaign archive chunk cannot expose or replace the guest battlefield");
Pump(h,g);
Check(h.Stage=="load-campaign" && g.Stage=="load-campaign" && h.Overlay && g.Overlay,"Host exit begins a shared pre-battle campaign restore behind the loading cover");
Check(h.ResultExitCalls==0 && g.ResultExitCalls==0 && h.LastLoad.ReturningToCampaign && g.LastLoad.ReturningToCampaign,"Shared return loads the preserved campaign without invoking native random battle-result calculation");
g.FinishNative(true);g.Tick();Pump(h,g);
Check(g.Held && h.Held && g.Overlay,"A faster guest cannot release campaign input before the host restores its native graph");
h.FinishNative(true);h.Tick();Pump(h,g);
Check(!h.Held && !g.Held && !h.Overlay && !g.Overlay,"Both matching restored campaigns release the scene hold");
Check(h.ManifestBuilds==1 && g.ManifestBuilds==1 && g.ReplicaSaves==1 && g.Replica,"Campaign return rebuilds native identities and restores the guest's replica ownership");
Check(h.Revision==0 && g.Revision==0 && h.Generation==1 && g.Generation==1,"Restoration resets campaign baselines for full resync while retaining the new world-generation namespace");
Check(!h.HasReturnArchive && !g.HasReturnArchive && h.Reveals==2 && g.Reveals==2,"Completed restoration discards the temporary return archive and reveals each campaign once");

var pendingHost=new MultiplayerCampaign(true);pendingHost.Busy("prepare",true);pendingHost.Begin();pendingHost.End();
Check(!pendingHost.Held && !pendingHost.FailureHeld && pendingHost.PrepareCalls==0,"A disconnect before native entry releases the untouched host campaign into an ordinary pause");
var replacedHost=new MultiplayerCampaign(true);replacedHost.Busy("prepare",true);replacedHost.Begin();replacedHost.UnexpectedRootReplacement();replacedHost.End();
Check(replacedHost.Held && replacedHost.FailureHeld && replacedHost.Overlay,"A superficially campaign-shaped scene with a replaced root cannot use the safe host release path");
var queuedHost=new MultiplayerCampaign(true);queuedHost.Busy("prepare",true);queuedHost.Begin();queuedHost.QueueNativeBatch();queuedHost.End();
Check(queuedHost.Held && queuedHost.FailureHeld && queuedHost.Overlay,"Pending native scene operations keep a disconnected host held even before a recovery archive exists");
var waitingGuest=new MultiplayerCampaign(false);waitingGuest.Message(new MpTacticalDrain("test-connection",1).Offer());
Check(waitingGuest.End() && waitingGuest.FailureHeld && waitingGuest.Overlay,"A guest disconnect during loading retains the simulation guard until it exits the replica");
waitingGuest.FinishMainMenu();waitingGuest.OfflineTick();
Check(!waitingGuest.Held && waitingGuest.InputRestores==1,"A disconnected guest releases the residual hold only after reaching the main menu");

var failedHost=new MultiplayerCampaign(true);var failedGuest=new MultiplayerCampaign(false);Drain(failedHost,failedGuest);LoadBoth(failedHost,failedGuest);
failedHost.End();
Check(failedHost.FailureHeld && failedHost.HasReturnArchive && failedHost.Overlay,"A host disconnect after native battle entry retains recovery data and the paused simulation guard");
failedHost.Recover();Check(failedHost.Stage=="offline-return" && failedHost.FailureHeld,"Offline recovery remains held while the saved campaign loads");
failedHost.OfflineTick();Check(failedHost.FailureHeld,"Offline recovery cannot release before a fresh initialized campaign root exists");
failedHost.FinishNative(true);failedHost.OfflineTick();
Check(!failedHost.Held && !failedHost.HasReturnArchive && failedHost.ResultExitCalls==0,"Verified offline recovery restores pre-battle campaign authority without calculating results");

var badHost=new MultiplayerCampaign(true);var badGuest=new MultiplayerCampaign(false);Drain(badHost,badGuest);
badHost.FinishNative(false);badHost.Tick();Pump(badHost,badGuest);badHost.FinishNative(false);badHost.Tick();Pump(badHost,badGuest);
badGuest.WorldDigest="different live native graph";badGuest.FinishNative(false);
Reject(badGuest.Tick,"Different initialized native graphs cannot satisfy the actual runtime readiness path");
Check(badGuest.Held && badGuest.Overlay && badGuest.Reveals==0,"A native state mismatch leaves the guest covered and unable to simulate");

Console.WriteLine($"Tactical runtime: {checks} checks using production adapter/session/drain/codec plus native dependency doubles; actual Unity scene initialization and campaign DataTick are not exercised.");
