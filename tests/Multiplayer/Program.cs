using GTIP;
using System.Net;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, name); }
var schema=MpStateSchema.Scalars(typeof(StateFixture));
var managementSchemas=new[]{MpStateSchema.Scalars(typeof(MpProjectState)),MpStateSchema.Scalars(typeof(MpNewspaperState))};
var projects=new MpProjectState{Projects=new[]{95,95,103},LastMonth=new[]{95,103}};
var issue=new MpNewspaperState{Serial=7,Active=true,HostClosed=true,Headlines=new[]{"News from the front","Policy ratified"},Prosa="Line one\nLine two",Image=4,ImageHeadline="Dispatch",Location="Washington",Day=8,Month=2,Year=1863};
var managementRows=new[]{new MpStateRow{Kind=0,Values=managementSchemas[0].Capture(projects)},new MpStateRow{Kind=1,Values=managementSchemas[1].Capture(issue)}};
var managementDecoded=MpStateCodec.Decode(managementSchemas,MpStateCodec.Encode(managementSchemas,managementRows));
var restoredProjects=new MpProjectState();managementSchemas[0].Apply(restoredProjects,managementDecoded[0].Values);
Check(restoredProjects.Projects.SequenceEqual(new[]{95,95,103}),"Repeating project levels survive state replication");
Check(restoredProjects.LastMonth.SequenceEqual(projects.LastMonth),"Project report membership survives state replication");
var restoredIssue=new MpNewspaperState();managementSchemas[1].Apply(restoredIssue,managementDecoded[1].Values);
Check(restoredIssue.Serial==7 && restoredIssue.Active && restoredIssue.HostClosed && restoredIssue.Day==8 && restoredIssue.Year==1863,"Newspaper identity, acknowledgement and date round trip");
Check(restoredIssue.Headlines.SequenceEqual(issue.Headlines) && restoredIssue.Prosa==issue.Prosa && restoredIssue.Location=="Washington","Native newspaper content round trip");
var news=new MpNewsBarrier();
Check(!news.Waiting,"No newspaper barrier before an issue");
news.Begin(1);Check(news.Waiting,"New issue prevents resuming");
Check(!news.Close(1,true) && news.Waiting,"Host dismissal alone keeps campaign paused");
Check(!news.Close(1,true) && news.Waiting,"Duplicate host dismissal cannot stand in for guest");
Check(news.Close(1,false) && !news.Waiting,"Both dismissals release campaign");
news.Begin(2);Check(!news.Close(1,false) && news.Waiting && !news.GuestClosed,"Delayed acknowledgement cannot dismiss a later issue");
Check(!news.Close(3,false) && news.Waiting,"Future issue acknowledgement cannot release pause");
Check(!news.Close(2,false) && news.Waiting,"Guest can dismiss before host");
Check(news.Close(2,true) && !news.Waiting,"Guest-first barrier releases only after host dismissal");
Reject(()=>news.Begin(2),"Newspaper issue serial cannot be reused");
news.Clear();Check(!news.Waiting && news.Serial==0,"Disconnect clears newspaper barrier");
var draft=new MpRecruitDraft{Parent=42,State=10,Type=2,SizeLevel=1,RecruitType=1,Horse=true};
draft.Validate();Check(true,"Bounded horse artillery draft accepted");
var recruitSchemas=new[]{MpStateSchema.Scalars(typeof(MpRecruitDraft))};
var draftRows=MpStateCodec.Decode(recruitSchemas,MpStateCodec.Encode(recruitSchemas,new[]{new MpStateRow{Values=recruitSchemas[0].Capture(draft)}}));
var draftCopy=new MpRecruitDraft();recruitSchemas[0].Apply(draftCopy,draftRows[0].Values);draftCopy.Validate();
Check(draftCopy.Parent==42 && draftCopy.RecruitType==1 && draftCopy.Horse && draftCopy.SizeLevel==1,"Recruit request preserves native choices");
draftCopy.Type=0;Reject(draftCopy.Validate,"Horse infantry request rejected");draftCopy.Type=2;
draftCopy.SizeLevel=3;Reject(draftCopy.Validate,"Unsupported recruitment size rejected");draftCopy.SizeLevel=1;
draftCopy.RecruitType=2;Reject(draftCopy.Validate,"Unsupported recruit pool rejected");draftCopy.RecruitType=0;
draftCopy.Parent=0;Reject(draftCopy.Validate,"Recruit request requires an existing parent identity");draftCopy.Parent=42;
draftCopy.State=-1;Reject(draftCopy.Validate,"Recruit request requires a state");
var schemas=new[]{schema};
var sourceState=new StateFixture { Treasury=12345.5f, Officer=91, Name="Army of the Tennessee", Active=true,
    Recruits=new[]{400,250}, Support=new float[,]{{10,20},{30,40}} };
var dataRows=new[]{new MpStateRow{Kind=0,Id=7,Name="State",Values=schema.Capture(sourceState)}};
var dataPacket=MpStateCodec.Encode(schemas,dataRows);
var decodedRows=MpStateCodec.Decode(schemas,dataPacket);
var targetState=new StateFixture();schema.Apply(targetState,decodedRows[0].Values);
Check(targetState.Treasury==12345.5f && targetState.Officer==91 && targetState.Name==sourceState.Name && targetState.Active,"Campaign data scalar round trip");
Check(targetState.Recruits.SequenceEqual(sourceState.Recruits) && targetState.Support[1,1]==40,"Recruit pools and rectangular support arrays round trip");
targetState.Recruits[0]=999;schema.Apply(targetState,decodedRows[0].Values);
Check(targetState.Recruits[0]==400,"Applying arrays cannot mutate rollback baseline");
sourceState.Recruits[0]=555;schema.Apply(targetState,dataRows[0].Values);
Check(targetState.Recruits[0]==400,"Capturing data isolates it from simulation writes");
Reject(()=>MpStateCodec.Decode(new[]{new MpStateSchema(typeof(StateFixture),new[]{"Officer"})},dataPacket),"Different schema rejected");
Reject(()=>MpStateCodec.Decode(schemas,dataPacket.Take(dataPacket.Length/2).ToArray()),"Truncated state rejected");
Reject(()=>MpStateCodec.Decode(schemas,MpStateCodec.Encode(schemas,new[]{dataRows[0],dataRows[0]})),"Duplicate rows rejected");
Reject(()=>new MpStateSchema(typeof(StateFixture),new[]{"Missing"}),"Missing native field fails schema construction");
Reject(()=>new MpStateSchema(typeof(StateFixture),new[]{"Unsafe"}),"Arbitrary object graph rejected");
sourceState.Treasury=float.NaN;dataRows[0].Values=schema.Capture(sourceState);
Reject(()=>MpStateCodec.Encode(schemas,dataRows),"Nonfinite economy value rejected");
var nativeSchema=MpStateSchema.Scalars(typeof(StateFixture),"Support");
sourceState.Treasury=0;sourceState.Support[0,0]=float.NaN;
var nativeRows=new[]{new MpStateRow{Values=nativeSchema.Capture(sourceState)}};
var nativePacket=MpStateCodec.Encode(new[]{nativeSchema},nativeRows);
nativeSchema.Apply(targetState,MpStateCodec.Decode(new[]{nativeSchema},nativePacket)[0].Values);
Check(float.IsNaN(targetState.Support[0,0]),"Explicit native NaN history survives round trip");
Reject(()=>MpStateCodec.Decode(schemas,nativePacket),"NaN policy participates in schema identity");
sourceState.Support[0,0]=float.PositiveInfinity;nativeRows[0].Values=nativeSchema.Capture(sourceState);
Reject(()=>MpStateCodec.Encode(new[]{nativeSchema},nativeRows),"Native NaN exception does not allow infinity");
sourceState.Support[0,0]=0;sourceState.Treasury=float.NaN;nativeRows[0].Values=nativeSchema.Capture(sourceState);
Reject(()=>MpStateCodec.Encode(new[]{nativeSchema},nativeRows),"Native NaN exception remains field specific");
sourceState.Treasury=0;sourceState.Recruits=null;sourceState.Support=new float[0,2];sourceState.Name=null;
dataRows[0].Values=schema.Capture(sourceState);
schema.Apply(targetState,MpStateCodec.Decode(schemas,MpStateCodec.Encode(schemas,dataRows))[0].Values);
Check(targetState.Name==null && targetState.Recruits==null && targetState.Support.Length==0,"Null and empty arrays preserved");
sourceState.Recruits=new int[MpStateCodec.MaxArray+1];dataRows[0].Values=schema.Capture(sourceState);
Reject(()=>MpStateCodec.Encode(schemas,dataRows),"Oversized array rejected");
sourceState.Recruits=new[]{7,8};sourceState.Treasury=120;
MpStateRow Record(int id)=>new MpStateRow{Kind=0,Id=id,Values=schema.Capture(sourceState)};
var baseline=new MpStateBaseline(schemas);
var first=baseline.Prepare(1,0,baseline.Changes(new[]{Record(1),Record(2)}));
var firstHash=baseline.Fingerprint(first);
using(var packed=new MemoryStream(MpStateCodec.Encode(schemas,first)))
using(var gzip=new System.IO.Compression.GZipStream(packed,System.IO.Compression.CompressionMode.Decompress))
using(var raw=new MemoryStream()) {
    gzip.CopyTo(raw);
    Check(MpCheckpoint.Hash(raw.ToArray())==firstHash,"Canonical checksum matches uncompressed wire data without performing compression");
}
Check(baseline.Fingerprint(first.Reverse().ToArray())==firstHash,"Canonical checksum does not depend on enumeration order");
var workerCapture=new[]{Record(10)};
var capturedHash=baseline.Fingerprint(workerCapture);
sourceState.Recruits[0]=999;
Check(System.Threading.Tasks.Task.Run(()=>baseline.Fingerprint(workerCapture)).GetAwaiter().GetResult()==capturedHash,"Worker checksum uses captured arrays despite later simulation changes");
sourceState.Recruits[0]=7;
Check(baseline.Revision==0,"Preparing a revision does not acknowledge application");
baseline.Commit(1,first,firstHash);
Check(baseline.Changes(new[]{Record(2),Record(1)}).Length==0,"Stable records omit transmission regardless of enumeration order");
first[0].Values[0]=null;
Check(baseline.Changes(new[]{Record(1),Record(2)}).Length==0,"Committed baseline owns its own record copies");
sourceState.Treasury=121;
var delta=baseline.Changes(new[]{Record(2),Record(3)});
Check(delta.Length==3 && delta.Single(r=>r.Id==1).Deleted,"One transaction carries creation, modification and explicit deletion");
var wireDelta=MpStateCodec.Decode(schemas,MpStateCodec.Encode(schemas,delta));
var second=baseline.Prepare(2,1,wireDelta);
Check(second.Select(r=>r.Id).SequenceEqual(new[]{2,3}),"Delta reconstructs complete authoritative roster");
Reject(()=>baseline.Prepare(3,1,wireDelta),"Skipped revision rejected");
Reject(()=>baseline.Prepare(2,0,wireDelta),"Wrong base revision rejected");
Reject(()=>baseline.Prepare(2,1,wireDelta,"incorrect"),"Wrong final state checksum rejected before application");
Reject(()=>baseline.Prepare(2,1,new[]{new MpStateRow{Kind=0,Id=99,Deleted=true}}),"Deletion of unknown identity rejected");
Reject(()=>baseline.Prepare(2,1,new[]{Record(3),Record(3)}),"Duplicate transaction identities rejected");
Check(baseline.Revision==1 && baseline.Hash==firstHash,"Rejected transactions leave baseline unchanged");
baseline.Commit(2,second,baseline.Fingerprint(second));
Reject(()=>baseline.Commit(2,second,baseline.Hash),"Duplicate application cannot advance state twice");
Check(baseline.Changes(new[]{Record(3),Record(2)}).Length==0,"Acknowledged changes become next delta baseline");
var revisionMessage=MpCodec.Decode(MpCodec.Encode(new MpMessage{kind="data-offer",sequence=2,baseRevision=1,stateHash=baseline.Hash}));
Check(revisionMessage.baseRevision==1 && revisionMessage.stateHash==baseline.Hash,"Revision and application hash survive production wire codec");
var expectedValues=schema.Capture(sourceState); sourceState.Treasury++;
Check(MpStateBaseline.FirstDifference(schema,expectedValues,schema.Capture(sourceState))=="Treasury","Native readback identifies the divergent field");
sourceState.Treasury--;sourceState.Recruits[0]++;
Check(MpStateBaseline.FirstDifference(schema,expectedValues,schema.Capture(sourceState))=="Recruits","Native readback detects array element divergence");
var stationaryIndependent=new MpMessage{kind="commander",session="commands",sequence=1};
var regroup=new MpMessage{kind="regroup",session="commands",sequence=1,unit=5,x=7};
MpRules.Authorize(regroup,"commands",0,0,false,0);checks++;
Reject(()=>MpRules.Authorize(regroup,"commands",0,1,true,0),"Regroup cannot bypass source ownership");
Reject(()=>MpRules.Authorize(regroup,"commands",0,0,true,1),"Repeated regroup request rejected");
MpRules.Authorize(stationaryIndependent,"commands",0,0,false,0);checks++;
Reject(()=>MpRules.Authorize(stationaryIndependent,"commands",0,1,false,0),"Commander assignment never bypasses faction ownership");
stationaryIndependent.kind="move";
Reject(()=>MpRules.Authorize(stationaryIndependent,"commands",0,0,false,0),"Movement still requires movement eligibility");
MpFormationState Formation(int parent=0)=>new MpFormationState{Parent=parent,Side=0,Tier=16,Commander=0,Garrison=-1,Colors=new float[8],Contract=new float[6]};
var roster=new Dictionary<int,MpFormationState>{{1,Formation()},{2,Formation(1)}};
MpLifecycle.Validate(roster,1,0);checks++;
roster.Remove(2);roster[3]=Formation(1);MpLifecycle.Validate(roster,1,0);checks++;
Check(!roster.ContainsKey(2),"New lifecycle identity does not reuse deleted unit ID");
roster[1].Parent=3;Reject(()=>MpLifecycle.Validate(roster,1,0),"Lifecycle cycles rejected before native creation");
roster[1].Parent=0;roster[3].Parent=99;Reject(()=>MpLifecycle.Validate(roster,1,0),"Missing lifecycle parent rejected");
roster[3].Parent=1;roster[3].Commander=1;Reject(()=>MpLifecycle.Validate(roster,1,0),"Unknown lifecycle officer rejected");
roster[3].Commander=0;roster[3].X=float.NaN;Reject(()=>MpLifecycle.Validate(roster,1,0),"Nonfinite birth position rejected");
roster[3].X=0;roster[3].ParentRegiment=99;Reject(()=>MpLifecycle.Validate(roster,1,0),"Missing detached-unit parent rejected");
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
    units=new[]{new MpUnit{id=42,side=1,tier=18,name="ArmÃ©e of the Tennessee",commander="MG. Test",men=28988,x=1024.25f,z=-500.125f,targetX=2000,targetZ=-600,controllable=true,moving=true}},
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
    MpRules.WriteFrame(memory,"Unicode: armÃ©e / Tennessee"); memory.Position=0;
    Check(MpRules.ReadFrame(new FragmentedStream(memory))=="Unicode: armÃ©e / Tennessee","Fragmented Unicode frame");
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
    File.WriteAllText(Path.Combine(source,name),"fixture "+name+" é");
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

sealed class StateFixture
{
    public float Treasury;
    public int Officer;
    public string Name;
    public bool Active;
    public int[] Recruits;
    public float[,] Support;
    public object Unsafe=new object();
}
