using GTIP;
using System.Net;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, name); }
void Wait(Func<bool> ready, string name) { var end = DateTime.UtcNow.AddSeconds(5); while (!ready() && DateTime.UtcNow < end) Thread.Sleep(10); Check(ready(), name); }
string Receive(MpTransport peer) { string value; var end=DateTime.UtcNow.AddSeconds(5); while (!peer.Receive(out value) && DateTime.UtcNow<end) Thread.Sleep(10); return value ?? throw new Exception("Receive timeout"); }
var identities = new[]{MpIdentity.Segment(0,18,15,"Army"),MpIdentity.Segment(0,16,16,"Corps")};
var sorted=MpIdentity.Order(identities);
Check(MpIdentity.Hash(sorted)==MpIdentity.Hash(MpIdentity.Order(identities.Reverse().ToArray())),"Reordered scene formations retain matching network IDs and hash");
Check(MpIdentity.Difference(sorted,identities.Reverse().ToArray())==null,"Matching roster accepts different enumeration order");
Reject(()=>MpIdentity.Order(new[]{identities[0],identities[0]}),"Ambiguous identities rejected instead of guessed");
Check(MpIdentity.Segment(0,18,15,"A/B")!=MpIdentity.Segment(0,18,15,"A")+"/B","Names containing separators remain distinct");
Check(MpIdentity.Difference(sorted,new[]{identities[0]}).Contains("host=2, guest=1"),"Missing formation diagnosed");
Check(MpIdentity.Difference(sorted,new[]{identities[0],MpIdentity.Segment(1,16,16,"Corps")}).Contains("guest-only"),"Different faction record diagnosed");
Check(MpIdentity.Hash(sorted)!=MpIdentity.Hash(MpIdentity.Order(new[]{identities[0],MpIdentity.Segment(0,16,99,"Corps")})),"Commander changes remain a checkpoint mismatch");
var topology=new[]{new MpUnit{id=1,name=sorted[0],parent=0},new MpUnit{id=2,name=sorted[1],parent=1}};
MpIdentity.ValidateHierarchy(topology,sorted); checks++;
topology[1].parent=0; MpIdentity.ValidateHierarchy(topology,sorted); checks++;
Check(MpIdentity.Hash(sorted)==MpIdentity.Hash(MpIdentity.Order(identities)),"Reparenting leaves identity mapping unchanged");
topology[0].parent=2; topology[1].parent=1;
Reject(()=>MpIdentity.ValidateHierarchy(topology,sorted),"Cyclic hierarchy rejected before mutations");
topology[0].parent=0; topology[1].parent=3;
Reject(()=>MpIdentity.ValidateHierarchy(topology,sorted),"Unknown parent rejected");
topology[1].parent=0; topology[1].id=1;
Reject(()=>MpIdentity.ValidateHierarchy(topology,sorted),"Duplicate hierarchy IDs rejected");
Reject(()=>MpIdentity.ValidateHierarchy(new[]{topology[0]},sorted),"Incomplete hierarchy rejected");
// A running campaign can rename a unit or replace its commander without creating a new object.
var liveName="Original"; var liveCommander=12;
var checkpointKeys=new[]{MpIdentity.Segment(0,16,liveCommander,liveName)};
var initialRecord=MpIdentity.HierarchyRecord(checkpointKeys,1,0);
liveName="Renamed Corps"; liveCommander=98;
var laterRecord=MpIdentity.HierarchyRecord(checkpointKeys,1,0);
Check(initialRecord.name==laterRecord.name,"Session identity survives live rename and commander replacement");
Check(laterRecord.name!=MpIdentity.Segment(0,16,liveCommander,liveName),"Hierarchy does not recompute mutable identity");
MpIdentity.ValidateHierarchy(new[]{laterRecord},checkpointKeys); checks++;
Reject(()=>MpIdentity.HierarchyRecord(checkpointKeys,2,0),"New formation cannot borrow an existing identity");
foreach(var paused in new[]{true,false,true}) {
 var c=new MpClock {year=1864,month=2,day=29,hour=23.75f,elapsed=4.2f,speed=20,paused=paused};
 var wire=MpCodec.Decode(MpCodec.Encode(new MpMessage {kind="clock",session="a",clock=c}));
 Check(wire.clock.year==1864 && wire.clock.month==2 && wire.clock.day==29 && wire.clock.hour==23.75f && wire.clock.speed==20 && wire.clock.paused==paused,"Authoritative clock and pause/resume round trip");
}
Reject(()=>new MpClock {year=1863,month=2,day=29}.Validate(),"Invalid date rejected");
Reject(()=>new MpClock {year=1863,month=1,day=1,hour=float.NaN}.Validate(),"Invalid clock hour rejected");
Reject(()=>new MpClock {year=1863,month=1,day=1,speed=-1}.Validate(),"Negative game speed rejected");
var rollover=MpCodec.Decode(MpCodec.Encode(new MpMessage{kind="clock",clock=new MpClock{year=1864,month=1,day=1,hour=0,elapsed=4,speed=50}}));
Check(rollover.clock.year==1864 && rollover.clock.hour==0,"New year midnight is preserved");
Check(MpRules.GuestSide("coop",0)==0 && MpRules.GuestSide("coop",1)==1,"Both co-op factions");
Check(MpRules.GuestSide("versus",0)==1 && MpRules.GuestSide("versus",1)==0,"Both versus factions");
Reject(()=>MpRules.GuestSide("bad",0),"Unknown mode rejected");
Reject(()=>MpRules.GuestSide("coop",2),"Foreign faction rejected");
var order = new MpMessage { kind="move",session="a",sequence=1,x=10,z=20 };
MpRules.Authorize(order,"a",1,1,true,0); checks++;
Reject(()=>MpRules.Authorize(order,"a",0,1,true,0),"Enemy ownership rejected");
Reject(()=>MpRules.Authorize(order,"b",1,1,true,0),"Previous campaign session rejected");
Reject(()=>MpRules.Authorize(order,"a",1,1,true,1),"Replay rejected");
Reject(()=>MpRules.Authorize(order,"a",1,1,false,0),"Unavailable unit rejected");
order.kind="delete"; Reject(()=>MpRules.Authorize(order,"a",1,1,true,0),"Unknown mutation rejected");
order.kind="move"; order.x=float.NaN; Reject(()=>MpRules.Authorize(order,"a",1,1,true,0),"NaN destination rejected");
order.x=float.PositiveInfinity; Reject(()=>MpRules.Authorize(order,"a",1,1,true,0),"Infinite destination rejected");
order.x=10; order.protocol=MpRules.Protocol+1; Reject(()=>MpRules.Authorize(order,"a",1,1,true,0),"Wire version rejected"); order.protocol=MpRules.Protocol;
var empty = MpCodec.Decode(MpCodec.Encode(new MpMessage { kind="snapshot" }));
MpRules.ValidateSnapshot(empty); Check(empty.units.Length==0 && empty.towns.Length==0,"Empty arrays survive production codec");
var fixture = new MpMessage { kind="snapshot", session="test", side=1, mode="versus", date=1863.125f, paused=true,
    units=new[]{new MpUnit{id=42,side=1,tier=18,name="Armée of the Tennessee",commander="MG. Test",men=28988,x=1024.25f,z=-500.125f,targetX=2000,targetZ=-600,controllable=true,moving=true}},
    towns=new[]{new MpTown{name="St. Louis",x=2100.25f,z=-800}} };
var encoded=MpCodec.Encode(fixture); var decoded=MpCodec.Decode(encoded); MpRules.ValidateSnapshot(decoded);
fixture.campaign="checkpoint-manifest"; fixture.units[0].y=488.125f; fixture.units[0].yaw=270.5f;
var replica=MpCodec.Decode(MpCodec.Encode(fixture));
Check(replica.campaign=="checkpoint-manifest" && replica.units[0].y==488.125f && replica.units[0].yaw==270.5f,"Native replica manifest, height and facing preserved");
replica.units[0].y=float.NaN; Reject(()=>MpRules.ValidateSnapshot(replica),"Native replica invalid height rejected");
replica.units[0].y=0; replica.units[0].yaw=float.PositiveInfinity; Reject(()=>MpRules.ValidateSnapshot(replica),"Native replica invalid facing rejected");
Check(decoded.units[0].name==fixture.units[0].name && decoded.units[0].z==fixture.units[0].z && decoded.units[0].controllable && decoded.paused && decoded.towns[0].x==fixture.towns[0].x,"Full production snapshot round trip");
foreach(string mode in new[]{"coop","versus"}) foreach(int side in new[]{0,1}) {
    fixture.mode=mode; fixture.side=MpRules.GuestSide(mode,side); var result=MpCodec.Decode(MpCodec.Encode(fixture));
    Check(result.side==fixture.side && result.mode==mode,"Snapshot mode and faction round trip");
}
fixture.units[0].x=float.NaN; Reject(()=>MpRules.ValidateSnapshot(MpCodec.Decode(MpCodec.Encode(fixture))),"Nonfinite position explicitly rejected"); fixture.units[0].x=1;
fixture.units[0].targetX=float.PositiveInfinity; Reject(()=>MpRules.ValidateSnapshot(fixture),"Nonfinite target rejected"); fixture.units[0].targetX=2;
Reject(()=>MpCodec.Decode("{}"),"Old JSON protocol rejected");
var bytes=Convert.FromBase64String(encoded); Array.Resize(ref bytes,bytes.Length-1);
Reject(()=>MpCodec.Decode(Convert.ToBase64String(bytes)),"Truncated production payload rejected");
bytes=Convert.FromBase64String(encoded); bytes[0]=99;
Reject(()=>MpCodec.Decode(Convert.ToBase64String(bytes)),"Different protocol rejected");
bytes=Convert.FromBase64String(encoded); Array.Resize(ref bytes,bytes.Length+1);
Reject(()=>MpCodec.Decode(Convert.ToBase64String(bytes)),"Trailing payload rejected");
Reject(()=>MpCodec.Encode(new MpMessage{kind=new string('x',4097)}),"Oversized string rejected");
Reject(()=>MpCodec.Encode(new MpMessage{units=new MpUnit[20001]}),"Oversized collection rejected");
using(var memory=new MemoryStream()) {
    MpRules.WriteFrame(memory,"Unicode: armée / Tennessee"); memory.Position=0;
    Check(MpRules.ReadFrame(new FragmentedStream(memory))=="Unicode: armée / Tennessee","Fragmented Unicode frame");
}
Reject(()=>MpRules.ReadFrame(new MemoryStream(new byte[]{127,255,255,255})),"Excessive length rejected before allocation");
Reject(()=>MpRules.ReadFrame(new MemoryStream(new byte[]{0,0,0,0})),"Empty frame rejected");
Reject(()=>MpRules.ReadFrame(new MemoryStream(new byte[]{0,0,0,4,1})),"Truncated payload rejected");
Reject(()=>MpRules.ReadFrame(new MemoryStream(new byte[]{0,0,0,1,255})),"Invalid UTF8 rejected");
Reject(()=>MpRules.WriteFrame(new MemoryStream(),new string('x',MpRules.MaxBytes+1)),"Oversized outgoing frame rejected");
// Checkpoint exchange uses production compression, codec and extraction.
string checkpointRoot=Path.Combine(Path.GetTempPath(),"gtip-checkpoint-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(checkpointRoot);
string source=Path.Combine(checkpointRoot,"source"); Directory.CreateDirectory(source);
foreach(var name in new[]{"scenario.dat","groups.dat","regiments.dat","commanders.txt","nations.dat","AICampaign.dat","version.dat"})
    File.WriteAllText(Path.Combine(source,name),"fixture "+name+" �");
byte[] checkpoint=MpCheckpoint.Pack(source);
string checkpointHash=MpCheckpoint.Hash(checkpoint);
string unpacked=Path.Combine(checkpointRoot,"unpacked");
MpCheckpoint.Unpack(checkpoint,checkpointHash,unpacked);
Check(Directory.GetFiles(source).All(f=>File.ReadAllBytes(f).SequenceEqual(File.ReadAllBytes(Path.Combine(unpacked,Path.GetFileName(f))))),"All campaign files survive checkpoint archive byte-for-byte");
Reject(()=>MpCheckpoint.Unpack(checkpoint,checkpointHash,unpacked),"Existing saves cannot be overwritten");
Reject(()=>MpCheckpoint.Unpack(checkpoint,"bad checksum",Path.Combine(checkpointRoot,"bad")),"Corrupt checkpoint rejected before extraction");
Check(!Directory.Exists(Path.Combine(checkpointRoot,"bad")),"Rejected checkpoint writes no files");
File.WriteAllText(Path.Combine(source,"unexpected.dat"),"unsupported state");
Reject(()=>MpCheckpoint.Pack(source),"Unknown persistent state fails rather than silently omitting it");
File.Delete(Path.Combine(source,"unexpected.dat"));
File.Delete(Path.Combine(source,"nations.dat"));
Reject(()=>MpCheckpoint.Pack(source),"Incomplete host saves cannot be transferred");
byte[] ArchiveName(string name) {
    using var output=new MemoryStream();
    using(var zip=new System.IO.Compression.GZipStream(output,System.IO.Compression.CompressionMode.Compress,true))
    using(var writer=new BinaryWriter(zip)) { writer.Write(7); writer.Write(name); writer.Write(0); }
    return output.ToArray();
}
foreach(var name in new[]{"../scenario.dat","C:/config.dat","loader.dll"}) {
    var bad=ArchiveName(name);
    Reject(()=>MpCheckpoint.Unpack(bad,MpCheckpoint.Hash(bad),Path.Combine(checkpointRoot,"rejected")),"Peer-controlled path or executable rejected: "+name);
}
var blob=new byte[MpCheckpoint.ChunkBytes]; new Random(42).NextBytes(blob);
Check(MpCodec.Decode(MpCodec.Encode(new MpMessage{kind="checkpoint-data",blob=blob})).blob.SequenceEqual(blob),"Full checkpoint chunk survives production codec");
Reject(()=>MpCodec.Encode(new MpMessage{blob=new byte[MpCheckpoint.ChunkBytes+1]}),"Oversized checkpoint chunk rejected");
Directory.Delete(checkpointRoot,true);
using(var host=MpTransport.Host(IPAddress.Loopback,0)) {
    using(var guest=MpTransport.Join(IPAddress.Loopback,host.Port)) {
        try { Wait(()=>host.Connected && guest.Connected,"Loopback connection"); }
        catch { Console.WriteLine("Host: " + host.Failure + " Guest: " + guest.Failure); throw; }
        guest.Send(MpCodec.Encode(order));
        var parsed=MpCodec.Decode(Receive(host));
        MpRules.Authorize(parsed,"a",1,1,true,0); checks++;
        host.Send(MpCodec.Encode(new MpMessage {kind="snapshot",session="a",units=new[]{new MpUnit{id=42,name="Army of the Tennessee",x=15,z=25}},towns=Array.Empty<MpTown>()}));
        var snapshot=MpCodec.Decode(Receive(guest)); MpRules.ValidateSnapshot(snapshot);
        Check(snapshot.units.Single().id==42 && snapshot.units[0].x==15,"Authoritative snapshot reaches guest");
        for(int i=0;i<20;i++) guest.Send("order "+i);
        for(int i=0;i<20;i++) Check(Receive(host)=="order "+i,"Ordered delivery "+i);
        host.Send(MpCodec.Encode(new MpMessage {kind="reject",text="Campaign time mismatch"}));
        var rejection=MpCodec.Decode(Receive(guest));
        Check(rejection.kind=="reject" && rejection.text=="Campaign time mismatch","Rejection reason delivered over production transport");
        guest.Dispose(); Wait(()=>host.Closed,"Disconnect detected");
    }
}
using(var stopped=MpTransport.Host(IPAddress.Loopback,0)) { stopped.Dispose(); Check(stopped.Closed,"Listener cancellation"); }
Console.WriteLine($"Multiplayer: {checks} checks passed (real TCP transport and pure protocol rules; not a live game test).");

sealed class FragmentedStream(Stream source) : Stream
{
    public override int Read(byte[] b,int o,int n)=>source.Read(b,o,Math.Min(1,n));
    public override bool CanRead=>true; public override bool CanSeek=>false; public override bool CanWrite=>false;
    public override long Length=>throw new NotSupportedException(); public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
    public override void Flush()=>throw new NotSupportedException(); public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
    public override void SetLength(long n)=>throw new NotSupportedException(); public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
}
