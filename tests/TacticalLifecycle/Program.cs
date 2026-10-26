using GTIP;

int checks=0;
const string Session="same-session", Other="other-session";
string Hash(byte value)=>Convert.ToBase64String(Enumerable.Repeat(value,32).ToArray());
string Setup=Hash(0xaa), Battle=Hash(0xbb), Campaign=Hash(0xcc), OtherDigest=Hash(0xdd);
void Check(bool condition,string description){if(!condition)throw new Exception(description);checks++;Console.WriteLine("PASS "+description);}
void Reject(Action action,string description){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,description);}
(MpTacticalLifecycle host,MpTacticalLifecycle guest) Pair(){var h=new MpTacticalLifecycle(true);var g=new MpTacticalLifecycle(false);h.Reset(Session);g.Reset(Session);return(h,g);}
void Enter(MpTacticalLifecycle h,MpTacticalLifecycle g,int generation,double now=1){h.PrepareBattle(generation,Setup,now);g.ReceiveBattleOffer(Session,generation,Setup,now);h.BeginLoading(Session,generation,now);g.BeginLoading(Session,generation,now);h.BattleLoaded(Session,generation,Battle,now);g.BattleLoaded(Session,generation,Battle,now);h.ReceiveBattleReady(Session,generation,Battle,now);g.ReceiveBattleReady(Session,generation,Battle,now);}
void Return(MpTacticalLifecycle h,MpTacticalLifecycle g,int generation,double now=2){h.BeginReturn(Setup,now);g.ReceiveReturnOffer(Session,generation,Setup,now);h.CampaignLoaded(Session,generation,Campaign,now);g.CampaignLoaded(Session,generation,Campaign,now);h.ReceiveCampaignReady(Session,generation,Campaign,now);g.ReceiveCampaignReady(Session,generation,Campaign,now);}

var uninitialized=new MpTacticalLifecycle(true);
Check(uninitialized.MustPause && !uninitialized.CanUseCampaign && !uninitialized.CombatEnabled,"Uninitialized lifecycle cannot authorize play");
Reject(()=>uninitialized.PrepareBattle(1,Setup,1),"An uninitialized host cannot enter a battle");
Reject(()=>uninitialized.Reset(" "),"Blank session identities are rejected");
var (host,guest)=Pair();
Check(host.CanUseCampaign && guest.CanUseCampaign && host.Generation==0,"A new matching session begins in campaign generation zero");
Reject(()=>guest.PrepareBattle(1,Setup,1),"A guest cannot initiate the authoritative battle");
Reject(()=>host.ReceiveBattleOffer(Session,1,Setup,1),"A host cannot consume another host offer");
Reject(()=>host.PrepareBattle(0,Setup,1),"Battle generation zero is rejected");
Reject(()=>host.PrepareBattle(2,Setup,1),"A future generation cannot skip a battle");
Reject(()=>host.PrepareBattle(1,"not-a-digest",1),"Malformed checkpoint hashes are rejected");
Reject(()=>host.PrepareBattle(1,new string('g',64),1),"Alternate hexadecimal representations cannot create another checkpoint identity");
Reject(()=>host.PrepareBattle(1,new string(' ',44),1),"Whitespace cannot masquerade as a Base64 digest");
Reject(()=>host.PrepareBattle(1,Setup.Substring(0,43),1),"Truncated Base64 hashes are rejected");
Reject(()=>host.PrepareBattle(1,new string('!',43)+"=",1),"Malformed Base64 hash characters are rejected");
Reject(()=>host.PrepareBattle(1,Hash(0).Substring(0,42)+"B=",1),"Noncanonical Base64 pad bits cannot alias an existing digest");
Reject(()=>guest.ReceiveBattleOffer(Other,1,Setup,1),"An offer from another session cannot start loading");
host.PrepareBattle(1,Setup,1);guest.ReceiveBattleOffer(Session,1,Setup,1);
Check(host.MustPause && guest.MustPause && host.Phase==MpTacticalPhase.PreparingBattle,"An accepted battle offer pauses both peers before loading");
Check(host.BattleCheckpointHash==Setup,"The authoritative checkpoint identity retains the protocol's canonical SHA-256 Base64 representation");
Check(!guest.ReceiveBattleOffer(Session,1,Setup,2),"A duplicated identical battle offer is idempotent");
Reject(()=>guest.ReceiveBattleOffer(Session,1,OtherDigest,2),"A conflicting duplicate offer cannot replace its checkpoint");
Reject(()=>host.BeginLoading(Other,1,2),"Loading rejects a foreign session");
Reject(()=>host.BeginLoading(Session,2,2),"Loading rejects a future battle context");
Reject(()=>host.BattleLoaded(Session,1,Battle,2),"A ready callback cannot skip native scene loading");
host.ReceiveBattleReady(Session,1,Battle,2);
Check(host.PeerReady && !host.LocalReady && host.Phase==MpTacticalPhase.PreparingBattle,"Early peer readiness is buffered without opening the battle barrier");
host.BeginLoading(Session,1,3);guest.BeginLoading(Session,1,3);
host.BattleLoaded(Session,1,Battle,4);
Check(host.Phase==MpTacticalPhase.PausedBattle && host.MustPause && !host.CombatEnabled,"Matching early readiness opens only the paused battle phase");
guest.BattleLoaded(Session,1,Battle,4);
Check(guest.Phase==MpTacticalPhase.VerifyingBattle && guest.MustPause,"Local load completion alone waits for the peer");
guest.ReceiveBattleReady(Session,1,Battle,5);
Check(guest.Phase==MpTacticalPhase.PausedBattle,"The guest also reaches the matching paused battlefield");
Check(!host.BattleLoaded(Session,1,Battle,6) && !guest.ReceiveBattleReady(Session,1,Battle,6),"Duplicate matching loaded and ready receipts cannot restart a phase");
Reject(()=>host.ClaimCampaignCommand(Session,1,1),"Campaign commands are rejected while the tactical scene owns the session");
Reject(()=>host.ClaimBattleCommand(Session,1,1),"Combat commands remain unavailable in the paused technical milestone");
Reject(()=>guest.BeginReturn(Setup,6),"A guest cannot independently calculate or initiate campaign results");
host.BeginReturn(Setup,6);guest.ReceiveReturnOffer(Session,1,Setup,6);
Check(host.Phase==MpTacticalPhase.ReturningCampaign && host.MustPause && !host.LocalReady && !host.PeerReady,"Returning clears battle readiness and remains paused");
Check(!guest.ReceiveReturnOffer(Session,1,Setup,7),"Duplicate return offers do not restart campaign restoration");
Reject(()=>host.ReceiveBattleReady(Session,1,Battle,7),"Late battle readiness cannot satisfy the campaign return barrier");
host.CampaignLoaded(Session,1,Campaign,7);guest.ReceiveCampaignReady(Session,1,Campaign,7);
Check(host.Phase==MpTacticalPhase.VerifyingCampaign && guest.Phase==MpTacticalPhase.ReturningCampaign,"Either order of campaign load and peer readiness still waits for both");
host.ReceiveCampaignReady(Session,1,Campaign,8);guest.CampaignLoaded(Session,1,Campaign,8);
Check(host.CanUseCampaign && guest.CanUseCampaign && !host.MustPause,"The return barrier reopens campaign handling after matching authoritative state");
Check(!guest.ReceiveReturnOffer(Session,1,Setup,9) && !host.ReceiveCampaignReady(Session,1,Campaign,9),"Completed return duplicates are harmless without reentering a scene");
Reject(()=>guest.ReceiveBattleOffer(Session,1,Setup,9),"A stale battle offer cannot reopen a completed battle");
host.ClaimCampaignCommand(Session,1,3);
Reject(()=>host.ClaimCampaignCommand(Session,1,3),"Duplicate accepted campaign commands are rejected");
Reject(()=>host.ClaimCampaignCommand(Session,1,2),"Older command sequences are rejected");
Reject(()=>host.ClaimCampaignCommand(Session,0,4),"A delayed pre-battle campaign command has the wrong generation");
Reject(()=>guest.ClaimCampaignCommand(Session,1,4),"A guest cannot execute authoritative campaign commands locally");
Enter(host,guest,2,10);Return(host,guest,2,11);
Check(host.Generation==2 && guest.Generation==2 && host.CanUseCampaign && guest.CanUseCampaign,"A second full round trip preserves a monotonic battle generation");
Reject(()=>host.ClaimCampaignCommand(Session,2,3),"Command sequence ownership survives battle return");
host.ClaimCampaignCommand(Session,2,4);
Reject(()=>host.ValidateContext(Session,1),"Messages from a prior battle remain stale after the next return");

(host,guest)=Pair();host.PrepareBattle(1,Setup,1);host.BeginLoading(Session,1,1);host.BattleLoaded(Session,1,Battle,2);
Reject(()=>host.ReceiveBattleReady(Session,1,OtherDigest,2),"Different initialized battle baselines are rejected");
Check(host.Phase==MpTacticalPhase.Faulted && host.MustPause && !host.CanUseCampaign,"A baseline mismatch fails closed with the clock paused");
Reject(()=>host.ValidateContext(Session,1),"Faulted sessions cannot accept later commands under their old identity");
(host,guest)=Pair();Enter(host,guest,1);Reject(()=>host.ReceiveBattleReady(Session,1,OtherDigest,2),"A peer cannot change a baseline after declaring readiness");
Check(host.Phase==MpTacticalPhase.Faulted && host.MustPause,"Conflicting duplicate readiness faults the session");
(host,guest)=Pair();Enter(host,guest,1);host.BeginReturn(Setup,2);guest.ReceiveReturnOffer(Session,1,Setup,2);host.CampaignLoaded(Session,1,Campaign,3);
Reject(()=>host.ReceiveCampaignReady(Session,1,OtherDigest,3),"A different post-battle campaign baseline cannot resume play");
Check(host.MustPause && host.Phase==MpTacticalPhase.Faulted,"Campaign return mismatch remains paused");

(host,guest)=Pair();host.PrepareBattle(1,Setup,1);host.BeginLoading(Session,1,1);host.BattleLoaded(Session,1,Battle,2);
Reject(()=>host.BattleLoaded(Session,1,OtherDigest,3),"A local scene cannot change its battle attestation while waiting for the peer");
Reject(()=>host.ReceiveBattleReady(Session,1,Battle,3),"A late matching peer receipt cannot revive an invalid local battle baseline");
Check(host.MustPause && host.Phase==MpTacticalPhase.Faulted,"Changed local battle readiness fails closed");
(host,guest)=Pair();Enter(host,guest,1);
Reject(()=>host.BattleLoaded(Session,1,OtherDigest,2),"A verified paused battlefield cannot replace its local readiness attestation");
Check(host.MustPause && host.Phase==MpTacticalPhase.Faulted,"Post-barrier local battle disagreement remains paused");
(host,guest)=Pair();Enter(host,guest,1);host.BeginReturn(Setup,2);host.CampaignLoaded(Session,1,Campaign,3);
Reject(()=>host.CampaignLoaded(Session,1,OtherDigest,4),"A restored campaign cannot change its local attestation while waiting for the peer");
Reject(()=>host.ReceiveCampaignReady(Session,1,Campaign,4),"A late peer receipt cannot resume a campaign with invalid local readiness");
Check(host.MustPause && host.Phase==MpTacticalPhase.Faulted,"Changed local campaign readiness fails closed before release");
(host,guest)=Pair();Enter(host,guest,1);Return(host,guest,1);
Reject(()=>host.CampaignLoaded(Session,1,OtherDigest,3),"Changing local campaign readiness after release invalidates the completed barrier");
Check(host.MustPause && !host.CanUseCampaign && host.Phase==MpTacticalPhase.Faulted,"Post-barrier local campaign disagreement restores the pause requirement");

(host,guest)=Pair();host.PrepareBattle(1,Setup,1);
Check(!host.Tick(5,10),"A progressing transition within its timeout remains connected");
host.Progress(Session,1,7);Check(!host.Tick(16,10),"Verified transfer progress extends the transition deadline");
Check(host.Tick(17,10) && host.MustPause && host.Phase==MpTacticalPhase.Faulted,"An exhausted progress deadline faults and pauses the transition");
(host,guest)=Pair();Enter(host,guest,1);Check(!host.Tick(10000,1) && host.Phase==MpTacticalPhase.PausedBattle,"An intentionally paused verified battlefield has no loading deadline");
host.PeerLost("guest disconnected");Check(host.MustPause && host.Failure=="guest disconnected","Peer loss preserves a paused fault rather than allowing independent play");
(host,guest)=Pair();Reject(()=>host.Tick(double.NaN,2),"Nonfinite local clocks are rejected");Reject(()=>host.Tick(1,0),"Nonpositive timeouts are rejected");
host.Tick(5,1);Reject(()=>host.Tick(4,1),"A backwards clock cannot extend a transition deadline");
host.Reset(Other);Check(host.Generation==0 && host.CanUseCampaign,"An explicit new session clears old phase and sequence ownership");
Reject(()=>host.ValidateContext(Session,0),"Old session traffic stays invalid after reset");
Console.WriteLine($"Tactical lifecycle: {checks} checks. Combat remains disabled in this milestone.");
