using GTIP;
using System.Net;

int checks=0;
void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;Console.WriteLine("PASS "+reason);}
void Reject(Action action,string reason){bool rejected=false;try{action();}catch(Exception e)when(e is InvalidDataException || e is IOException || e is InvalidOperationException){rejected=true;}Check(rejected,reason);}
MpMessage Wire(MpMessage message)=>MpCodec.Decode(MpCodec.Encode(message));
string Digest(string value)=>MpCheckpoint.Hash(System.Text.Encoding.UTF8.GetBytes(value));
byte[] Data(int size){var bytes=new byte[size];new Random(1701).NextBytes(bytes);return bytes;}
var source=Data(MpCheckpoint.ChunkBytes*2+17);string baseline=Digest("initialized battle world");
double now=0;
Queue<MpMessage> toHost=new(),toGuest=new();List<MpTacticalLoadRequest> hostLoads=new(),guestLoads=new();
MpTacticalSession host=null!,guest=null!;
void Setup(){now=0;toHost.Clear();toGuest.Clear();hostLoads.Clear();guestLoads.Clear();host=new(true,"session-one",m=>toGuest.Enqueue(Wire(m)),r=>hostLoads.Add(r));guest=new(false,"session-one",m=>toHost.Enqueue(Wire(m)),r=>guestLoads.Add(r));}
void Pump(){int limit=10000;while(toHost.Count+toGuest.Count>0){if(--limit==0)throw new Exception("Protocol did not settle");if(toGuest.TryDequeue(out var g))guest.Receive(g,++now);if(toHost.TryDequeue(out var h))host.Receive(h,++now);}}
void Paused(){host.SceneLoaded(hostLoads.Last(),baseline,++now);guest.SceneLoaded(guestLoads.Last(),baseline,++now);Pump();}
MpMessage Packet(string kind,int generation=1,int stage=1,int offset=0,string text=null,byte[] bytes=null)=>new(){kind=kind,session="session-one",sequence=generation,baseRevision=stage,mode=stage==1?"battle":"campaign",unit=offset,text=text,blob=bytes};

Setup();Check(!guest.Receive(new MpMessage{kind="ping"},++now),"Campaign frames remain outside the tactical controller");
host.BeginBattle(source,++now);
Check(host.Lifecycle.MustPause && !host.Lifecycle.CombatEnabled && hostLoads.Count==1 && guestLoads.Count==0,"Host starts a paused transition and guest does not load before receiving its archive");
Check(hostLoads[0].Checkpoint.SequenceEqual(source),"Host load request owns the prepared checkpoint bytes");
Pump();
Check(host.TransferComplete && guest.TransferComplete && host.PendingBytes==0 && guest.BufferedBytes==0,"Multi-chunk transfer finishes with an explicit archive receipt and released transfer buffers");
Check(guestLoads.Count==1 && guestLoads[0].Checkpoint.SequenceEqual(source) && guestLoads[0].Hash==MpCheckpoint.Hash(source),"Guest receives the exact verified archive once through production wire encoding");
Check(host.Lifecycle.Phase==MpTacticalPhase.LoadingBattle && guest.Lifecycle.Phase==MpTacticalPhase.LoadingBattle,"Archive receipt does not claim native scene readiness");
host.SceneLoaded(hostLoads.Last(),baseline,++now);Pump();
Check(host.Lifecycle.MustPause && guest.Lifecycle.MustPause && guest.Lifecycle.PeerReady && !guest.Lifecycle.LocalReady,"A faster host cannot release the guest's native loading barrier");
guest.SceneLoaded(guestLoads.Last(),baseline,++now);Pump();
Check(host.Lifecycle.Phase==MpTacticalPhase.PausedBattle && guest.Lifecycle.Phase==MpTacticalPhase.PausedBattle,"Matching initialized-world digests admit only the paused battle phase");
Reject(()=>host.Lifecycle.ClaimBattleCommand("session-one",1,1),"No battle command can start combat in this milestone");
int loads=guestLoads.Count;guest.Receive(Packet("tactical-offer",offset:source.Length,text:MpCheckpoint.Hash(source)),++now);Pump();
Check(guestLoads.Count==loads,"Repeated verified offer cannot load another native scene");
host.BeginReturn(Data(97),++now);Pump();
Check(hostLoads.Last().ReturningToCampaign && guestLoads.Last().ReturningToCampaign,"Only the host starts authoritative campaign restoration");
Reject(()=>host.SceneLoaded(hostLoads.First(),baseline,++now),"A delayed battle-load callback cannot attest a later campaign restoration");
guest.SceneLoaded(guestLoads.Last(),Digest("restored campaign"),++now);Pump();
Check(!host.Lifecycle.CanUseCampaign && !guest.Lifecycle.CanUseCampaign,"Campaign input remains unavailable until both restored scenes attest readiness");
host.SceneLoaded(hostLoads.Last(),Digest("restored campaign"),++now);Pump();
Check(host.Lifecycle.CanUseCampaign && guest.Lifecycle.CanUseCampaign,"Matching restored campaign digests reopen campaign operations");
host.BeginBattle(Data(1),++now);Pump();Paused();
Reject(()=>guest.SceneLoaded(guestLoads.First(),baseline,++now),"A previous battle's native completion cannot attest another generation with the same baseline");
Check(host.Lifecycle.Generation==2 && guest.Lifecycle.Generation==2,"A subsequent battle gets a new generation in the same connection");
Reject(()=>guest.Receive(Packet("tactical-ready",generation:1,text:baseline),++now),"Delayed messages from the previous battle cannot affect the current battle");
Check(guest.Lifecycle.Phase==MpTacticalPhase.PausedBattle,"Rejected stale context preserves the current paused state");

Setup();Reject(()=>guest.BeginBattle(source,++now),"Guests cannot supply authoritative battle checkpoints");
Reject(()=>host.BeginBattle(Array.Empty<byte>(),++now),"An empty host checkpoint is refused before leaving the campaign");
Check(host.Lifecycle.CanUseCampaign,"Invalid host checkpoint input leaves campaign state intact");
var mutable=Data(37);var expected=(byte[])mutable.Clone();host.BeginBattle(mutable,++now);mutable[0]^=255;hostLoads[0].Checkpoint[1]^=255;Pump();
Check(guestLoads[0].Checkpoint.SequenceEqual(expected),"Caller and native-loader mutations cannot alter the prepared outgoing archive");

Setup();host.BeginBattle(source,++now);var offer=toGuest.Dequeue();guest.Receive(offer,++now);
Reject(()=>host.Receive(Packet("tactical-received",offset:source.Length,text:MpCheckpoint.Hash(source)),++now),"A receipt cannot acknowledge bytes that the host has not sent");
Reject(()=>host.Receive(Packet("tactical-ready",text:baseline),++now),"Native readiness cannot bypass the verified checkpoint receipt");
Reject(()=>guest.Receive(Packet("tactical-offer",generation:2,offset:1,text:baseline),++now),"A future battle cannot replace a transfer in progress");
var foreign=Packet("tactical-chunk",bytes:Data(1));foreign.session="another-session";Reject(()=>guest.Receive(foreign,++now),"Foreign-session chunks are rejected before buffering");
var mixed=Packet("tactical-chunk",bytes:Data(1));mixed.units=new[]{new MpUnit()};Reject(()=>guest.Receive(mixed,++now),"A tactical envelope cannot smuggle campaign unit data");
Reject(()=>guest.Receive(Packet("tactical-chunk",offset:1,bytes:Data(1)),++now),"Out-of-order first chunk cannot move the receive cursor");
Reject(()=>guest.Receive(Packet("tactical-chunk",bytes:Data(MpCheckpoint.ChunkBytes+1)),++now),"Direct callers cannot bypass the wire chunk-size bound");
Check(guest.BufferedBytes==0 && guestLoads.Count==0,"Rejected transfer inputs do not mutate native scenes or buffered content");
var request=toHost.Dequeue();host.Receive(request,++now);var chunk=toGuest.Dequeue();guest.Receive(chunk,++now);
int buffered=guest.BufferedBytes;guest.Receive(chunk,++now);
Check(guest.BufferedBytes==buffered,"An exact retry of the most recent chunk does not append twice");
var corrupt=Wire(chunk);corrupt.blob[0]^=1;Reject(()=>guest.Receive(corrupt,++now),"A changed retry cannot overwrite previously received bytes");
Check(guest.BufferedBytes==buffered,"A conflicting retry preserves the prior receive buffer");
// Duplicate next requests generated above may replay the last response; FIFO retries remain harmless.
Pump();Check(guestLoads.Count==1 && guestLoads[0].Checkpoint.SequenceEqual(source),"Repeated pulls and chunks still produce exactly one verified load");

Setup();host.BeginBattle(Data(37),++now);guest.Receive(toGuest.Dequeue(),++now);host.Receive(toHost.Dequeue(),++now);var damaged=toGuest.Dequeue();damaged.blob[3]^=1;
Reject(()=>guest.Receive(damaged,++now),"A corrupted completed archive is rejected by SHA-256");
Check(guest.Lifecycle.Phase==MpTacticalPhase.Faulted && guest.Lifecycle.MustPause && guestLoads.Count==0 && guest.BufferedBytes==0,"Checksum failure releases buffers, holds pause, and never calls native loading");

Setup();host.BeginBattle(source,++now);Pump();host.SceneLoaded(hostLoads.Last(),baseline,++now);guest.SceneLoaded(guestLoads.Last(),Digest("different world"),++now);
Reject(Pump,"Different initialized native baselines stop the readiness barrier");
Check(host.Lifecycle.MustPause && guest.Lifecycle.MustPause,"A scene mismatch cannot release either peer into live combat");

Setup();host.BeginBattle(source,++now);guest.Receive(toGuest.Dequeue(),++now);
Check(guest.Tick(now+100,30) && guest.BufferedBytes==0 && guest.Lifecycle.MustPause,"A stalled transfer times out with no native load and no campaign resumption");
host.PeerLost("Test disconnect");Check(host.PendingBytes==0 && host.Lifecycle.MustPause && host.Lifecycle.Failure=="Test disconnect","Peer loss releases the outgoing checkpoint and retains the failure reason");
Setup();host.BeginBattle(source,++now);Pump();Paused();host.Dispose();Check(host.Lifecycle.MustPause && !host.Lifecycle.CanUseCampaign,"Disposal cannot accidentally restore campaign authority");

int callbacks=0;var failing=new MpTacticalSession(true,"failure",_=>{},_=>{callbacks++;throw new IOException("Native load failed");});
Reject(()=>failing.BeginBattle(Data(11),1),"Native loading failure propagates to the owning adapter");
Check(callbacks==1 && failing.Lifecycle.Phase==MpTacticalPhase.Faulted && failing.PendingBytes==0,"Native load failure clears payloads and holds the session faulted");

// Exercise the exact production framing, background I/O and session controller on loopback TCP.
using(var server=MpTransport.Host(IPAddress.Loopback,0)) {
 using var client=MpTransport.Join(IPAddress.Loopback,server.Port);
 var timer=System.Diagnostics.Stopwatch.StartNew();
 while(!server.Connected || !client.Connected){if(server.Closed || client.Closed)throw new Exception("TCP connection failed: "+server.Failure+" / "+client.Failure);if(timer.Elapsed.TotalSeconds>8)throw new Exception("TCP connect timed out");Thread.Sleep(5);}
 int hLoads=0,gLoads=0;double t=0;MpTacticalLoadRequest hRequest=null!,gRequest=null!;
 using var hs=new MpTacticalSession(true,"tcp",m=>server.Send(MpCodec.Encode(m)),r=>{hLoads++;hRequest=r;});
 using var gs=new MpTacticalSession(false,"tcp",m=>client.Send(MpCodec.Encode(m)),r=>{gLoads++;gRequest=r;});
 void Drive(Func<bool> done) {
  timer.Restart();while(!done()) {
   while(server.Receive(out var p))hs.Receive(MpCodec.Decode(p),++t);
   while(client.Receive(out var p))gs.Receive(MpCodec.Decode(p),++t);
   if(server.Closed || client.Closed)throw new Exception("TCP transport closed unexpectedly");
   if(timer.Elapsed.TotalSeconds>8)throw new Exception("TCP protocol timed out");Thread.Sleep(2);
  }
 }
 hs.BeginBattle(source,++t);Drive(()=>hs.TransferComplete && gs.TransferComplete && gLoads==1);
 hs.SceneLoaded(hRequest,baseline,++t);gs.SceneLoaded(gRequest,baseline,++t);Drive(()=>hs.Lifecycle.Phase==MpTacticalPhase.PausedBattle && gs.Lifecycle.Phase==MpTacticalPhase.PausedBattle);
 hs.BeginReturn(Data(79),++t);Drive(()=>hs.TransferComplete && gs.TransferComplete && gLoads==2);
 hs.SceneLoaded(hRequest,Digest("tcp campaign"),++t);gs.SceneLoaded(gRequest,Digest("tcp campaign"),++t);Drive(()=>hs.Lifecycle.CanUseCampaign && gs.Lifecycle.CanUseCampaign);
 Check(hLoads==2 && gLoads==2,"Actual TCP completes battle and campaign checkpoint transfers with separate native-readiness barriers");
}
Console.WriteLine($"Tactical protocol: {checks} checks. Native Unity scene loading is not exercised by this suite.");

if(args.Length==1) {
 var actual=Path.GetFullPath(args[0]);var battleArchive=MpTacticalCheckpoint.Pack(actual);var campaignArchive=MpCheckpoint.Pack(actual);
 string output=Path.Combine(Environment.CurrentDirectory,"artifacts","tactical-tcp-native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
 using var server=MpTransport.Host(IPAddress.Loopback,0);using var client=MpTransport.Join(IPAddress.Loopback,server.Port);
 var timer=System.Diagnostics.Stopwatch.StartNew();
 while(!server.Connected || !client.Connected){if(server.Closed || client.Closed || timer.Elapsed.TotalSeconds>8)throw new Exception("Native-file TCP connect failed");Thread.Sleep(5);}
 double t=0;MpTacticalLoadRequest hRequest=null!,gRequest=null!;string hPath="",gPath="";
 void Extract(MpTacticalLoadRequest request,string role) {
  string destination=Path.Combine(output,role+"-"+(request.ReturningToCampaign?"campaign":"battle"));
  if(request.ReturningToCampaign)MpCheckpoint.Unpack(request.Checkpoint,request.Hash,destination);
  else MpTacticalCheckpoint.Unpack(request.Checkpoint,request.Hash,destination);
  if(role=="host"){hRequest=request;hPath=destination;}else{gRequest=request;gPath=destination;}
 }
 using var hs=new MpTacticalSession(true,"native-files",m=>server.Send(MpCodec.Encode(m)),r=>Extract(r,"host"));
 using var gs=new MpTacticalSession(false,"native-files",m=>client.Send(MpCodec.Encode(m)),r=>Extract(r,"guest"));
 void Drive(Func<bool> done) {
  timer.Restart();while(!done()) {
   while(server.Receive(out var p))hs.Receive(MpCodec.Decode(p),++t);
   while(client.Receive(out var p))gs.Receive(MpCodec.Decode(p),++t);
   if(server.Closed || client.Closed || timer.Elapsed.TotalSeconds>20)throw new Exception("Native-file TCP transfer failed");Thread.Sleep(2);
  }
 }
 hs.BeginBattle(battleArchive,++t);Drive(()=>hs.TransferComplete && gs.TransferComplete && gRequest!=null);
 var originals=Directory.GetFiles(actual,"*",SearchOption.AllDirectories);
 Check(originals.All(p=>MpCheckpoint.Hash(File.ReadAllBytes(p))==MpCheckpoint.Hash(File.ReadAllBytes(Path.Combine(gPath,Path.GetRelativePath(actual,p))))),"A real native battle checkpoint survives production TCP, framing, session transfer and atomic extraction byte-for-byte");
 // This digest represents file validation in this harness, not an initialized Unity world.
 string fileBaseline=Digest("validated native battle files");hs.SceneLoaded(hRequest,fileBaseline,++t);gs.SceneLoaded(gRequest,fileBaseline,++t);
 Drive(()=>hs.Lifecycle.Phase==MpTacticalPhase.PausedBattle && gs.Lifecycle.Phase==MpTacticalPhase.PausedBattle);
 hs.BeginReturn(campaignArchive,++t);Drive(()=>hs.TransferComplete && gs.TransferComplete && gRequest.ReturningToCampaign);
 Check(Directory.GetFiles(actual).All(p=>MpCheckpoint.Hash(File.ReadAllBytes(p))==MpCheckpoint.Hash(File.ReadAllBytes(Path.Combine(gPath,Path.GetFileName(p))))),"The authoritative campaign checkpoint also survives the return transfer without losing any root save file");
 string restored=Digest("validated restored campaign files");hs.SceneLoaded(hRequest,restored,++t);gs.SceneLoaded(gRequest,restored,++t);Drive(()=>hs.Lifecycle.CanUseCampaign && gs.Lifecycle.CanUseCampaign);
 Check(!hs.Lifecycle.CombatEnabled && !gs.Lifecycle.CombatEnabled,"The native-file TCP round trip never enables battle simulation");
 Console.WriteLine($"Native-file TCP integration: {battleArchive.Length} battle bytes, {campaignArchive.Length} campaign bytes; {checks} total checks. Fixtures: {output}");
}
