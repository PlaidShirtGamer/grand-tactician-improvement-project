using GTIP;
using System.Net;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, name); }
var schema=MpStateSchema.Scalars(typeof(StateFixture));
checks+=FingerprintChecks.Run();
checks+=TransportQueueChecks.Run();
checks+=TransportHeartbeatChecks.Run();
checks+=TransportConnectChecks.Run();
checks+=SnapshotRosterChecks.Run();
checks+=DispatchOpenChecks.Run();
checks+=BattleEventChecks.Run();
checks+=ProductionChecks.Run();
checks+=MapPresentationModelChecks.Run();
checks+=PoseStreamChecks.Run();
checks+=PresentationFenceChecks.Run();
checks+=TransientChecks.Run();
Check(MpConstructionRules.Funded(true,false,false,-10),"Native army placement is independent of civilian debt-rating gates");
Check(!MpConstructionRules.Funded(false,false,false,0),"Civilian treasury construction retains its native rating gate");
Check(MpConstructionRules.Funded(false,false,true,0),"Fully funded company upgrades remain available at low debt ratings");
Check(!MpConstructionRules.Funded(false,false,true,-1),"Unfunded company upgrades cannot bypass a low debt rating");
Check(MpConstructionRules.Funded(false,true,true,-1),"Native extraordinary funding is available with an eligible rating");
var flowIds=new HashSet<int>();
for(int r=0;r<40;r++)for(int side=0;side<6;side++)for(int partner=0;partner<6;partner++) {
    int id=MpEconomyRules.FlowId(r,side,partner,6);
    if(!flowIds.Add(id)||!flowIds.Add(id+1)||id/2/6/6!=r||(id/2/6)%6!=side||(id/2)%6!=partner)throw new Exception("Trade history identity collision");
}
Check(flowIds.Count==2880,"All bilateral goods histories have distinct reversible import/export identities");
Reject(()=>MpEconomyRules.FlowId(0,6,0,6),"Unknown trade side rejected");
var visual=new MpVisualPosition();visual.Sample(0,0,0,0,false);
visual.Sample(10,2,4,1,false);Check(visual.X==0,"A received movement sample starts at the displayed endpoint");
visual.Sample(10,2,4,1.5f,false);Check(visual.X==5 && visual.Y==1 && visual.Z==2,"Visual movement interpolates every frame between host samples");
visual.Sample(20,2,4,1.5f,false);Check(visual.X==5,"An interrupted blend retains display continuity");
visual.Sample(20,2,4,2,false);Check(visual.X==20,"Visual movement reaches the received endpoint without overshoot");
visual.Sample(20,2,4,8,false);Check(visual.X==20,"Missing packets do not cause visual extrapolation");
visual.Sample(21,2,4,8,true);Check(visual.X==21,"Paused authoritative positions snap exactly");
visual.Sample(1000,2,4,9,false);Check(visual.X==1000,"Teleports are not animated across the map");
Reject(()=>visual.Sample(float.NaN,0,0,10,false),"Non-finite visual positions rejected");
var pathOrder=new MpPathOrder();Check(pathOrder.Accept(1,0) && pathOrder.Accept(1,2),"A priority host route supersedes the earlier full snapshot");
Check(!pathOrder.Accept(1,1),"An older in-flight snapshot cannot redraw a superseded route");
Check(pathOrder.Accept(1,2) && pathOrder.Accept(2,0),"Normal updates in the same route generation and other units remain independent");
pathOrder.Clear();Check(pathOrder.Accept(1,0),"New sessions do not inherit path generations");
var moveView=new MpMovementView{Unit=633,Order=10,X=100,Y=2,Z=300,Seconds=5,Minimized=true,Active=new[]{4,5},Disabled=new[]{3}};
MpMovementRules.View(moveView);checks++;
var movementSchema=new[]{MpStateSchema.Scalars(typeof(MpMovementView))};
var viewMessage=MpCodec.Decode(MpCodec.Encode(new MpMessage{kind="movement-panel",blob=MpStateCodec.Encode(movementSchema,new[]{new MpStateRow{Values=movementSchema[0].Capture(moveView)}})}));
var viewCopy=new MpMovementView();movementSchema[0].Apply(viewCopy,MpStateCodec.Decode(movementSchema,viewMessage.blob)[0].Values);
Check(viewCopy.Unit==633 && viewCopy.Order==10 && viewCopy.Minimized && viewCopy.Disabled.SequenceEqual(new[]{3}),"Native movement popup and disabled controls survive the production codecs");
MpMovementRules.Ticket(633,10,633,10);checks++;
Reject(()=>MpMovementRules.Ticket(633,9,633,10),"An old popup cannot cancel a newer route");
Reject(()=>MpMovementRules.Ticket(632,10,633,10),"A popup cannot target another fleet");
moveView.X=float.NaN;Reject(()=>MpMovementRules.View(moveView),"Non-finite native popup position rejected");moveView.X=100;
moveView.Disabled=new[]{99};Reject(()=>MpMovementRules.View(moveView),"Unknown native movement controls rejected");
var firstDecision=new MpFirstDecision();
Check(!firstDecision.Claim(0),"Engagement choice requires an open issue");firstDecision.Begin(1);
Check(firstDecision.Claim(1) && !firstDecision.Claim(1),"First accepted co-op engagement choice wins exactly once");firstDecision.Begin(2);
Check(!firstDecision.Claim(1) && !firstDecision.Resolved,"Delayed decision cannot resolve a newer engagement");
Check(firstDecision.Claim(2),"Next engagement can choose independently");Reject(()=>firstDecision.Begin(2),"Engagement identities cannot be reused");
firstDecision.Clear();Check(!firstDecision.Resolved && firstDecision.Serial==0,"Disconnect clears engagement arbitration");
var nativePath=new MpPathState{Lengths=new[]{2,1},Status=new[]{2,1},Corners=new[]{1f,2f,3f,4f,5f,6f,7f,8f,9f},BlockPath=new[]{0},BlockCorner=new[]{1},Destinations=new[]{7f,8f,9f},DestinationYaw=new[]{90f}};
MpWorldRules.Path(nativePath);Check(true,"Multi-segment native path accepted without client pathfinding");
var pathSchema=MpStateSchema.Scalars(typeof(MpPathState));
var pathCopy=new MpPathState();pathSchema.Apply(pathCopy,MpStateCodec.Decode(new[]{pathSchema},MpStateCodec.Encode(new[]{pathSchema},new[]{new MpStateRow{Values=pathSchema.Capture(nativePath)}}))[0].Values);
Check(pathCopy.Lengths.SequenceEqual(nativePath.Lengths) && pathCopy.Status.SequenceEqual(nativePath.Status) && pathCopy.Corners.SequenceEqual(nativePath.Corners),"Native path shape, order-delay status and coordinates round trip");
Reject(()=>MpWorldRules.Path(new MpPathState{Lengths=new[]{3},Status=new[]{2},Corners=new float[3],BlockPath=new int[0],BlockCorner=new int[0]}),"Truncated native path cannot be installed");
Reject(()=>MpWorldRules.Path(new MpPathState{Lengths=new[]{1},Status=new[]{2},Corners=new[]{float.NaN,1f,2f},BlockPath=new int[0],BlockCorner=new int[0]}),"Non-finite path cannot be installed");
var buildingRecord=new MpBuildingRecord{Type=22,Owner=0,Level=1,NativeIndex=3,X=500,Y=2,Z=800,Condition=1,Name="Washington Depot",Active=true,Garrison=25};
MpWorldRules.Building(buildingRecord,28,6);Check(true,"Native building and garrison identity accepted");
Check(MpWorldRules.SameBuilding(22,500.001f,800.001f,buildingRecord),"Checkpoint rounding preserves a building identity");
Check(!MpWorldRules.SameBuilding(22,501,800,buildingRecord),"Same-prefab buildings in different places cannot swap session identities");
Check(!MpWorldRules.SameBuilding(23,500,800,buildingRecord),"Co-located buildings of different types remain distinct");
var buildingSchema=MpStateSchema.Scalars(typeof(MpBuildingRecord));var buildingCopy=new MpBuildingRecord();
buildingSchema.Apply(buildingCopy,MpStateCodec.Decode(new[]{buildingSchema},MpStateCodec.Encode(new[]{buildingSchema},new[]{new MpStateRow{Id=17,Values=buildingSchema.Capture(buildingRecord)}}))[0].Values);
Check(buildingCopy.Garrison==25 && buildingCopy.NativeIndex==3 && buildingCopy.Name==buildingRecord.Name && buildingCopy.X==500,"Building identity and host list index survive replication");
buildingCopy.Owner=9;Reject(()=>MpWorldRules.Building(buildingCopy,28,6),"Unknown building owner rejected");buildingCopy.Owner=0;
buildingCopy.Condition=float.PositiveInfinity;Reject(()=>MpWorldRules.Building(buildingCopy,28,6),"Invalid building condition rejected");
foreach(float code in new[]{100f,101f,120f,121f,122f,123f}){MpWorldRules.UnitOrder("campaign-order",code,16,4,false);Check(true,"Registered native army order accepted: "+code);}
Reject(()=>MpWorldRules.UnitOrder("campaign-order",100,16,0,false),"Unready formation cannot request offensive stance");
Reject(()=>MpWorldRules.UnitOrder("campaign-order",121,16,1,false),"Unready formation cannot scout");
Reject(()=>MpWorldRules.UnitOrder("campaign-order",101,16,4,true),"Pending native order cannot be bypassed");
Reject(()=>MpWorldRules.UnitOrder("forced-march",1,16,2,false),"Forced march respects host readiness");
Reject(()=>MpWorldRules.UnitOrder("fleet-order",1,16,4,false),"Land formation cannot receive naval orders");
Reject(()=>MpWorldRules.UnitOrder("fleet-order",4,17,4,false),"Unknown naval order rejected");
Reject(()=>MpWorldRules.UnitOrder("arbitrary-method",0,16,4,false),"Peer cannot name arbitrary native methods");
Check(MpRules.NewClock(2,1),"Newer authoritative clock accepted");
Check(!MpRules.NewClock(1,2) && !MpRules.NewClock(2,2),"Delayed or duplicate clock cannot rewind pause state");
Check(!MpRules.NewClock(0,0) && !MpRules.NewClock(-1,0),"Invalid clock serial rejected");
Check(!MpRules.NativeSelectionCollider("Army","CommandRange",true),"Army range collider is not a selection target");
Check(!MpRules.NativeSelectionCollider("Corps","Range",false),"Corps root is not a selection target");
Check(!MpRules.NativeSelectionCollider("RegBlock","Coy",false),"Campaign company/range collider cannot select an icon");
Check(MpRules.NativeSelectionCollider("Untagged","UnitNameText",false),"Native unit name remains selectable");
Check(MpRules.NativeSelectionCollider("Untagged","UnitSymbol",true) && !MpRules.NativeSelectionCollider("Untagged","UnitSymbol",false),"Native symbol selection follows paper-map mode");
Check(MpFinanceRules.Normalize(0,1,.4f,5,8,10,20)==.4f,"Valid native tax slider value retained");
Check(Math.Abs(MpFinanceRules.Normalize(1,0,.26f,5,8,10,20)-.3f)<.00001f,"Political subsidy snaps to native policy capacity steps");
Check(MpFinanceRules.Normalize(2,0,1,5,8,10,20)==1,"Finance automation checkbox accepted");
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-.1f,1.1f})Reject(()=>MpFinanceRules.Normalize(0,0,bad,5,8,10,20),"Invalid financial value rejected: "+bad);
Reject(()=>MpFinanceRules.Normalize(0,5,0,5,8,10,20),"Out-of-range tax index rejected");
Reject(()=>MpFinanceRules.Normalize(1,8,0,5,8,10,20),"Out-of-range subsidy index rejected");
Reject(()=>MpFinanceRules.Normalize(2,0,.5f,5,8,10,20),"Automation requires a boolean choice");
Reject(()=>MpFinanceRules.Normalize(3,0,0,5,8,10,20),"Unregistered finance field rejected");
var alertSchema=MpStateSchema.Scalars(typeof(MpAlertState));
var alert=new MpAlertState{Count=9,Blink=true,Headline="Supply Alert: 9",Tooltip="Nine host alerts",Positions=Enumerable.Range(0,27).Select(i=>(float)i).ToArray()};
var alertRows=MpStateCodec.Decode(new[]{alertSchema},MpStateCodec.Encode(new[]{alertSchema},new[]{new MpStateRow{Id=849,Values=alertSchema.Capture(alert)}}));
var alertCopy=new MpAlertState();alertSchema.Apply(alertCopy,alertRows[0].Values);
Check(alertCopy.Count==9 && alertCopy.Blink && alertCopy.Positions.SequenceEqual(alert.Positions),"Authoritative supply alert count and navigation targets round trip");
Check(alertCopy.Headline==alert.Headline && alertCopy.Tooltip==alert.Tooltip,"Native alert tooltip content round trip");
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
foreach(float speed in new[]{1f,5f,20f,50f})Check(MpPresentationRules.ResumeNewspaper(true,false,false,speed),"Native stored newspaper speed can resume: "+speed);
Check(!MpPresentationRules.ResumeNewspaper(true,false,false,0),"Manual pause during newspaper never falls back to 50x");
Check(!MpPresentationRules.ResumeNewspaper(false,false,false,20),"Article may explicitly leave campaign paused");
Check(!MpPresentationRules.ResumeNewspaper(true,true,false,20),"Stay-paused preference honoured after both acknowledgements");
Check(!MpPresentationRules.ResumeNewspaper(true,false,true,20),"Another native modal prevents newspaper resuming");
var presentationSchemas=new[]{MpStateSchema.Scalars(typeof(MpChartPoints)),MpStateSchema.Scalars(typeof(MpMapNotice)),MpStateSchema.Scalars(typeof(MpAlertState))};
var chartPoints=new MpChartPoints{Time=new[]{1863f,1863.1f},Value=new[]{13.2f,14.1f}};
var notice=new MpMapNotice{Type=26,Reference=41,X=150,Z=130,Age=3,Text="Unit routed"};
var alerts=new MpAlertState{Count=2,Blink=true,Headline="Supply Alert: 2",Tooltip="First\nSecond",Positions=new[]{1f,2f,3f,4f,5f,6f}};
var presentationRows=new[]{new MpStateRow{Kind=0,Values=presentationSchemas[0].Capture(chartPoints)},new MpStateRow{Kind=1,Values=presentationSchemas[1].Capture(notice)},new MpStateRow{Kind=2,Values=presentationSchemas[2].Capture(alerts)}};
var presentationCopy=MpStateCodec.Decode(presentationSchemas,MpStateCodec.Encode(presentationSchemas,presentationRows));
var chartCopy=new MpChartPoints();presentationSchemas[0].Apply(chartCopy,presentationCopy[0].Values);MpPresentationRules.ValidateChart(chartCopy);
Check(chartCopy.Time.SequenceEqual(chartPoints.Time) && chartCopy.Value.SequenceEqual(chartPoints.Value),"Goods history points survive replication without recalculation");
var noticeCopy=new MpMapNotice();presentationSchemas[1].Apply(noticeCopy,presentationCopy[1].Values);
Check(noticeCopy.Reference==41 && noticeCopy.Age==3 && noticeCopy.Text=="Unit routed","Map notice identity/content/age survive replication");
var noticeAlertsCopy=new MpAlertState();presentationSchemas[2].Apply(noticeAlertsCopy,presentationCopy[2].Values);
Check(noticeAlertsCopy.Count==2 && noticeAlertsCopy.Tooltip==alerts.Tooltip && noticeAlertsCopy.Positions.SequenceEqual(alerts.Positions),"Alert counts, descriptions and locations come from one host record");
Reject(()=>MpPresentationRules.ValidateChart(new MpChartPoints{Time=new[]{1f,2f},Value=new[]{3f}}),"Mismatched chart axes rejected");
Reject(()=>MpPresentationRules.ValidateChart(new MpChartPoints{Time=new[]{2f,1f},Value=new[]{3f,4f}}),"Reversed goods history rejected");
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
var readinessSchema=new MpStateSchema(typeof(ReadinessFixture),new[]{"fullreadinessreached","Readiness","Treasury"},new[]{"fullreadinessreached"},new[]{"fullreadinessreached"});
float NativeReadinessEstimate(float current,float previous,float elapsed)=>1f/((current-previous)/elapsed)*(1f-current);
Check(float.IsPositiveInfinity(NativeReadinessEstimate(.5f,.5f,1f)),"Native unchanged partial readiness produces an infinite ETA");
Check(float.IsNaN(NativeReadinessEstimate(1f,1f,1f)),"Native unchanged full readiness produces an undefined ETA");
foreach(float estimate in new[]{0f,.25f,float.PositiveInfinity,float.NegativeInfinity,float.NaN}) {
    var input=new ReadinessFixture{fullreadinessreached=estimate,Readiness=.5f,Treasury=500};
    var sourceRows=new[]{new MpStateRow{Id=16,Name="19th wisconsin",Values=readinessSchema.Capture(input)}};
    var hostState=new MpStateBaseline(new[]{readinessSchema});var clientState=new MpStateBaseline(new[]{readinessSchema});
    var fullState=hostState.Prepare(1,0,hostState.Changes(sourceRows));var checksum=hostState.Fingerprint(fullState);
    var readinessDecoded=MpStateCodec.Decode(new[]{readinessSchema},MpStateCodec.Encode(new[]{readinessSchema},fullState));
    var clientFull=clientState.Prepare(1,0,readinessDecoded,checksum);var replicaState=new ReadinessFixture();readinessSchema.Apply(replicaState,clientFull[0].Values);
    hostState.Commit(1,fullState,checksum);clientState.Commit(1,clientFull,checksum);
    Check(replicaState.fullreadinessreached.Equals(estimate) && clientState.Hash==hostState.Hash,"Native readiness estimate survives full snapshot and acknowledgement: "+estimate);
    Check(MpStateCodec.InvalidNumbers(new[]{readinessSchema},sourceRows).Length==0,"Preflight accepts the registered readiness estimate: "+estimate);
}
var badReadiness=new ReadinessFixture{fullreadinessreached=float.PositiveInfinity,Readiness=float.NaN,Treasury=float.NegativeInfinity};
var badRows=new[]{new MpStateRow{Id=16,Name="19th wisconsin",Values=readinessSchema.Capture(badReadiness)}};
var numericIssues=MpStateCodec.InvalidNumbers(new[]{readinessSchema},badRows);
Check(numericIssues.Length==2 && numericIssues.Any(s=>s.Contains("Readiness")) && numericIssues.Any(s=>s.Contains("Treasury")),"Preflight reports all invalid fields while accepting only the ETA sentinel");
Reject(()=>MpStateCodec.Encode(new[]{readinessSchema},badRows),"ETA exception does not permit invalid readiness or treasury");
var nanOnly=new MpStateSchema(typeof(ReadinessFixture),new[]{"fullreadinessreached","Readiness","Treasury"},new[]{"fullreadinessreached"});
var infinityOnly=new MpStateSchema(typeof(ReadinessFixture),new[]{"fullreadinessreached","Readiness","Treasury"},infinityFields:new[]{"fullreadinessreached"});
var validReadiness=new ReadinessFixture{fullreadinessreached=float.PositiveInfinity};
var etaRows=new[]{new MpStateRow{Values=readinessSchema.Capture(validReadiness)}};
Reject(()=>MpStateCodec.Decode(new[]{nanOnly},MpStateCodec.Encode(new[]{readinessSchema},etaRows)),"Infinity permission participates in schema identity");
validReadiness.fullreadinessreached=float.NaN;etaRows[0].Values=infinityOnly.Capture(validReadiness);
Reject(()=>MpStateCodec.Encode(new[]{infinityOnly},etaRows),"Infinity permission alone does not allow NaN");
Reject(()=>new MpStateSchema(typeof(ReadinessFixture),new[]{"Readiness"},infinityFields:new[]{"Unknown"}),"Unknown float-policy field cannot silently bypass validation");
var canonicalA=new ReadinessFixture{fullreadinessreached=BitConverter.Int32BitsToSingle(unchecked((int)0x7fc00001))};
var canonicalB=new ReadinessFixture{fullreadinessreached=BitConverter.Int32BitsToSingle(unchecked((int)0xffc00002))};
var etaBaseline=new MpStateBaseline(new[]{readinessSchema});
Check(etaBaseline.Fingerprint(new[]{new MpStateRow{Values=readinessSchema.Capture(canonicalA)}})==etaBaseline.Fingerprint(new[]{new MpStateRow{Values=readinessSchema.Capture(canonicalB)}}),"NaN sign and payload variations have one canonical checksum");
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
sealed class ReadinessFixture {public float fullreadinessreached,Readiness,Treasury;}
