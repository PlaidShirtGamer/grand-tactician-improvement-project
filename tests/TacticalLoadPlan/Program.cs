using GTIP;

int checks=0;
void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
void Reject(Action action,string message){bool rejected=false;try{action();}catch(Exception e)when(e is IOException || e is InvalidDataException || e is ArgumentException || e is InvalidOperationException){rejected=true;}Check(rejected,message);}
string sandbox=Path.Combine(Environment.CurrentDirectory,"artifacts","tactical-load-plan-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sandbox);
var rootFiles="addcommanders.dat AICampaign.dat AlarmInfos.dat AllObjects.dat armygrouprefs.dat attributes.dat battledata.dat buildings.dat CampaignObjectives.dat ChartDatabase.dat commanders.txt garrisonrefs.dat groups.dat IIPsTowns.dat nations.dat paths.dat Railroads.dat regiments.dat scenario.dat ships.dat shiptype.dat spottings.dat version.dat weapons.dat".Split(' ');
var battleFiles="armygrouprefs.dat artillery.dat attributes.dat battledata.dat battleunits.dat buildings.dat commanders.txt entrypoints.dat fallenlayers.dat frontlines.dat groups.dat objectives.dat obstacles.dat paths.dat pontoons.dat regiments.dat scenario.dat sideinfo.dat version.dat".Split(' ');
string[] Scenario(bool battle)=>new[]{"0","Union","4","10","1861","25","10","2026","15","55","False",battle?"-2":"4","001/A",battle?"212":"","False","0",battle?"Campaign: Test":"Campaign","0.1875","12","4","1861","False","0","1.5","Test","Test battlefield","True","False","False","True","-1"};
(string Install,string Save) Fixture(string name) {
 string install=Path.Combine(sandbox,name),campaign=Path.Combine(install,"Campaigns","001","A"),save=Path.Combine(campaign,"SaveGTIPTest");
 Directory.CreateDirectory(save);Directory.CreateDirectory(Path.Combine(save,"Battle"));Directory.CreateDirectory(Path.Combine(install,"Battlefields","212","A"));Directory.CreateDirectory(Path.Combine(install,"Config"));
 foreach(string file in rootFiles)File.WriteAllText(Path.Combine(save,file),"0\n");
 foreach(string file in battleFiles)File.WriteAllText(Path.Combine(save,"Battle",file),"0\n");
 File.WriteAllLines(Path.Combine(save,"scenario.dat"),Scenario(false));File.WriteAllLines(Path.Combine(save,"Battle","scenario.dat"),Scenario(true));
 File.WriteAllText(Path.Combine(save,"version.dat"),"1.142\n");File.WriteAllText(Path.Combine(save,"Battle","version.dat"),"1.142\n");
 foreach(string name2 in new[]{"unitprefs.txt","campaignprefs.txt","battleprefs.txt","weapons.txt","weaponclasses.dat","weapongroups.dat","shiptypes.dat","buildingtypes.dat","weather.txt","ranks.txt"})File.WriteAllText(Path.Combine(install,"Config",name2),"native "+name2);
 File.WriteAllText(Path.Combine(campaign,"nations.dat"),"base nations");
 foreach(string file in new[]{"battledata.dat","nations.dat"})File.WriteAllText(Path.Combine(install,"Battlefields","212","A",file),"battlefield "+file);
 string data=Path.Combine(install,"The Civil War (1861-1865)_Data");Directory.CreateDirectory(Path.Combine(data,"StreamingAssets"));File.WriteAllText(Path.Combine(data,"globalgamemanagers"),"supported registered native scenes");
 foreach(string file in new[]{"campaignmap","battlemap","campaignmap.manifest","battlemap.manifest"})File.WriteAllText(Path.Combine(data,"StreamingAssets",file),"native descriptor "+file);
 return(install,save);
}
MpTacticalLoadPlan Read((string Install,string Save) f,bool returning=false,int side=0)=>MpTacticalLoadPlan.Read(f.Install,f.Save,"001/A",side,returning,1.142f);
void Change((string Install,string Save) f,bool battle,int index,string value){string path=Path.Combine(f.Save,battle?"Battle/":"","scenario.dat");var lines=File.ReadAllLines(path);lines[index]=value;File.WriteAllLines(path,lines);}
var fixture=Fixture("valid");var plan=Read(fixture);
Check(plan.Scenario=="001/A" && plan.Folder=="SaveGTIPTest" && plan.Battlefield=="212" && plan.BattleSublevel=="A" && !plan.Returning && plan.Side==0,"Native battle plan selects the saved battlefield, campaign and isolated folder");
Check(plan.President==4 && plan.Version==1.142f && Convert.FromBase64String(plan.ContentHash).Length==32,"Native plan retains parent campaign president/version and a SHA-256 content descriptor");
Check(Read(fixture).ContentHash==plan.ContentHash,"Unchanged installed gameplay content has a stable descriptor hash");
var returning=Read(fixture,true);Check(returning.Returning && returning.Level=="001" && returning.Sublevel=="A" && returning.Battlefield=="","A plain campaign reload uses campaign metadata without a tactical scene or results");
Reject(()=>Read(fixture,false,1),"A checkpoint from another faction cannot enter the co-op scene");
Reject(()=>MpTacticalLoadPlan.Read(fixture.Install,fixture.Save,"../A",0,false,1.142f),"Campaign identifiers cannot escape the installed campaign tree");
Reject(()=>MpTacticalLoadPlan.Read(fixture.Install,fixture.Save,"001/A/other",0,false,1.142f),"Campaign identifiers cannot smuggle additional paths");
Reject(()=>MpTacticalLoadPlan.Read(fixture.Install,fixture.Save,"001/A",0,false,float.NaN),"Nonfinite native game versions are rejected");
Reject(()=>MpTacticalLoadPlan.Read(fixture.Install,fixture.Save,"001/A",0,false,1.143f),"Save versions must match the running supported game version");
var outside=Fixture("outside");string moved=Path.Combine(outside.Install,"OtherSave");Directory.Move(outside.Save,moved);
Reject(()=>MpTacticalLoadPlan.Read(outside.Install,moved,"001/A",0,false,1.142f),"The extracted save must be a direct child of its agreed campaign");

foreach(var item in new[]{(true,12,"002/A","mismatched campaign"),(false,0,"1","mismatched parent faction"),(true,13,"999","generated placeholder battlefield"),(true,13,"../212","battlefield traversal"),(true,13,"212/A","battlefield subpath"),(true,13,"9999","oversized battlefield ID"),(true,2,"32","invalid game date"),(true,5,"32","invalid save date"),(true,8,"24","invalid save hour"),(true,10,"maybe","invalid boolean"),(true,17,"NaN","nonfinite combat adjustment"),(true,17,"-1","negative combat adjustment"),(true,21,"True","tutorial scenario"),(true,23,"0","zero sprite ratio"),(true,23,"Infinity","infinite sprite ratio"),(true,30,"0","unsupported hierarchy")}) {
 var f=Fixture("invalid-"+checks);Change(f,item.Item1,item.Item2,item.Item3);Reject(()=>Read(f),"Preflight rejects "+item.Item4+" before native imports");
}
var shortMeta=Fixture("short-metadata");File.WriteAllLines(Path.Combine(shortMeta.Save,"Battle","scenario.dat"),Scenario(true).Take(30));Reject(()=>Read(shortMeta),"Truncated scenario metadata is rejected before native state changes");
var excessive=Fixture("long-metadata");File.AppendAllText(Path.Combine(excessive.Save,"Battle","scenario.dat"),"unknown field\n");Reject(()=>Read(excessive),"Unexpected native scenario shape is rejected");
var malformed=Fixture("nul-metadata");Change(malformed,true,1,"Union\0");Reject(()=>Read(malformed),"Embedded NUL characters cannot reach native metadata parsing");
var wrongVersion=Fixture("different-battle-version");File.WriteAllText(Path.Combine(wrongVersion.Save,"Battle","version.dat"),"1.141\n");Reject(()=>Read(wrongVersion),"Battle and parent versions must both match the running build");
var noTactical=Fixture("return-only");Directory.Delete(Path.Combine(noTactical.Save,"Battle"),true);Check(Read(noTactical,true).Returning,"Campaign restoration does not require a Battle subdirectory");Reject(()=>Read(noTactical),"Battle loading requires the native tactical save files");
foreach(string file in new[]{"weapons.dat","shiptype.dat","AllObjects.dat","addcommanders.dat","garrisonrefs.dat"}) {
 var f=Fixture("missing-root-"+checks);File.Delete(Path.Combine(f.Save,file));Reject(()=>Read(f),"Native load completeness requires "+file+" beyond the legacy checkpoint minimum");
}
var counts=Fixture("bad-count");File.WriteAllText(Path.Combine(counts.Save,"Battle","regiments.dat"),"20001\n");Reject(()=>Read(counts),"Tactical roster headers are bounded before native allocation");
var table=Fixture("short-table");File.WriteAllText(Path.Combine(table.Save,"Battle","commanders.txt"),"1\n");Reject(()=>Read(table),"A nonempty native table requires data following its count");
var missingAsset=Fixture("missing-scene-definition");File.Delete(Path.Combine(missingAsset.Install,"Battlefields","212","A","battledata.dat"));Reject(()=>Read(missingAsset),"Missing native battlefield definition data is rejected");
var missingTerrain=Fixture("missing-terrain");File.Delete(Path.Combine(missingTerrain.Install,"The Civil War (1861-1865)_Data","StreamingAssets","battlemap"));Reject(()=>Read(missingTerrain),"Missing installed terrain bundle is rejected");

var config=Fixture("config-digest");var original=Read(config).ContentHash;
File.WriteAllText(Path.Combine(config.Install,"Config","prefs.dat"),"local music and UI options");Check(Read(config).ContentHash==original,"Local music/UI preference files are not part of gameplay compatibility");
File.WriteAllText(Path.Combine(config.Install,"Config","unitprefs.txt"),"changed unit game rules");Check(Read(config).ContentHash!=original,"Different effective unit rules change installed gameplay compatibility");
var effective=Fixture("effective-rules");var effectiveHash=Read(effective).ContentHash;
File.Copy(Path.Combine(effective.Install,"Config","unitprefs.txt"),Path.Combine(effective.Install,"Campaigns","001","A","unitprefs.txt"));Check(Read(effective).ContentHash==effectiveHash,"Identical effective scenario overrides do not depend on installation paths");
File.WriteAllText(Path.Combine(effective.Install,"Config","unitprefs.txt"),"ignored fallback change");Check(Read(effective).ContentHash==effectiveHash,"An inactive unit-rule fallback cannot cause a false compatibility mismatch");
File.WriteAllText(Path.Combine(effective.Install,"Config","ranks0.txt"),"changed optional rank rules");Check(Read(effective).ContentHash!=effectiveHash,"Optional installed alliance rank overrides affect compatibility");
var descriptor=Fixture("scene-descriptor");var descriptorHash=Read(descriptor).ContentHash;
File.WriteAllText(Path.Combine(descriptor.Install,"The Civil War (1861-1865)_Data","globalgamemanagers"),"different registered scenes");Check(Read(descriptor).ContentHash!=descriptorHash,"Changed Unity scene descriptors affect compatibility");

// Drive the production scene adapter while replacing only native/Unity calls.
string priorDirectory=Environment.CurrentDirectory;
void ResetNative((string Install,string Save) f) {
 Environment.CurrentDirectory=f.Install;UnityEngine.Application.dataPath=Path.Combine(f.Install,"The Civil War (1861-1865)_Data");UnityEngine.Application.Available=_=>true;
 SceneManagement.currentoperation=2;SceneManagement.currentcampaignscenario="001/A";SceneManagement.batchoperation.Clear();
 Saving.latestfolder="old-save";Saving.priorloadedfolder="old-save";GameVars.playeralliance=0;BattlefieldSetup.Clears=0;NativeTrace.Calls.Clear();ImportExportUnitData.Imports.Clear();BattleController.quitbattlescene=CampaignController.quitcampaignscene=true;
}
try {
 var native=Fixture("native-load");ResetNative(native);var archive=MpTacticalCheckpoint.Pack(native.Save);var request=new MpTacticalLoadRequest(1,false,MpTacticalCheckpoint.Hash(archive),archive);
 string sourceBefore=File.ReadAllText(Path.Combine(native.Save,"scenario.dat"));var ticket=MpTacticalNativeScene.Load(request,"001/A",0);
 Check(ReferenceEquals(ticket.Request,request) && ticket.Scenario=="001/A" && ticket.Battlefield=="212" && !ticket.Returning && ticket.Folder.StartsWith("SaveGTIPBattle"),"Production native adapter returns the exact scoped request and fresh battle-load ticket");
 Check(SceneManagement.currentoperation==8 && SceneManagement.currentcampaignscenario=="001/A" && Saving.latestfolder==ticket.Folder && Saving.priorloadedfolder==ticket.Folder,"Battle adapter selects native saved-campaign-battle operation eight and isolated save globals");
 Check(ImportExportUnitData.Imports.Count==1 && ImportExportUnitData.Imports[0].Realism && ImportExportUnitData.Imports[0].Path.Replace('\\','/')=="Campaigns/001/A/"+ticket.Folder+"/Battle/","Production adapter imports the verified Battle scenario with host realism settings");
 Check(NativeTrace.Calls.SequenceEqual(new[]{"clear","import"}) && BattlefieldSetup.Clears==1,"Old native battlefield data is cleared before loading verified metadata");
 Check(SceneManagement.batchoperation.Select(b=>b.operationid).SequenceEqual(new[]{14,4,2,13,15}) && SceneManagement.batchoperation.Select(b=>b.startoffset).SequenceEqual(new[]{0,1,2,3,3}),"Battle scene batches preserve native black/rebuild/loading/op8/hide ordering and offsets");
 var savedBattle=SceneManagement.batchoperation[3];Check((string)savedBattle.Arguments[0]=="212" && (string)savedBattle.Arguments[1]=="A" && (string)savedBattle.Arguments[2]=="001/A","Saved battle batch retains battlefield scene, sublevel and parent campaign context");
 Check(SceneManagement.batchoperation.All(b=>b.Arguments[5]==null) && !BattleController.quitbattlescene && !CampaignController.quitcampaignscene,"Proof loading carries no battle result and clears stale native quit requests");
 Check(File.ReadAllText(Path.Combine(native.Save,"scenario.dat"))==sourceBefore && Directory.Exists(Path.Combine(native.Install,"Campaigns","001","A",ticket.Folder)),"Native adapter creates a fresh load folder and preserves the existing source save");

 var restore=Fixture("native-return");ResetNative(restore);var rootArchive=MpCheckpoint.Pack(restore.Save);var restoreRequest=new MpTacticalLoadRequest(1,true,MpCheckpoint.Hash(rootArchive),rootArchive);var restoredTicket=MpTacticalNativeScene.Load(restoreRequest,"001/A",0);
 Check(restoredTicket.Returning && SceneManagement.currentoperation==6 && SceneManagement.batchoperation.Select(b=>b.operationid).SequenceEqual(new[]{14,4,2,6,15}),"Campaign restoration queues a plain operation-six load without native result processing");
 Check(ImportExportUnitData.Imports.Single().Path.Replace('\\','/')=="Campaigns/001/A/"+restoredTicket.Folder+"/" && (int)SceneManagement.batchoperation[3].Arguments[2]==4,"Campaign return imports its root metadata and native president ID");
 Check(SceneManagement.batchoperation[2].Arguments[5]==null && SceneManagement.batchoperation[3].Arguments[4]==null,"Campaign return cannot carry an independently calculated battle result into native loading");

 var unavailable=Fixture("native-unavailable-scene");ResetNative(unavailable);UnityEngine.Application.Available=s=>s!="212";var badScene=MpTacticalCheckpoint.Pack(unavailable.Save);var badSceneRequest=new MpTacticalLoadRequest(2,false,MpTacticalCheckpoint.Hash(badScene),badScene);
 Reject(()=>MpTacticalNativeScene.Load(badSceneRequest,"001/A",0),"An unavailable Unity scene is refused by the production adapter");
 Check(BattlefieldSetup.Clears==0 && ImportExportUnitData.Imports.Count==0 && SceneManagement.batchoperation.Count==0 && Saving.latestfolder=="old-save" && SceneManagement.currentoperation==2,"Failed scene availability preflight does not clear native objects, import metadata, queue work or change save globals");
 var invalid=Fixture("native-invalid-preflight");ResetNative(invalid);Change(invalid,true,13,"../212");var invalidArchive=MpTacticalCheckpoint.Pack(invalid.Save);var invalidRequest=new MpTacticalLoadRequest(2,false,MpTacticalCheckpoint.Hash(invalidArchive),invalidArchive);
 Reject(()=>MpTacticalNativeScene.Load(invalidRequest,"001/A",0),"Invalid checkpoint metadata is rejected by the production adapter before native calls");
 Check(NativeTrace.Calls.Count==0 && SceneManagement.batchoperation.Count==0 && Saving.latestfolder=="old-save","Native load preflight failure preserves native objects and save globals");
 var busy=Fixture("native-busy");ResetNative(busy);SceneManagement.batchoperation.Add(new SceneManagement.BatchOperation(777));var busyArchive=MpTacticalCheckpoint.Pack(busy.Save);int directories=Directory.GetDirectories(Path.GetDirectoryName(busy.Save)!).Length;
 Reject(()=>MpTacticalNativeScene.Load(new MpTacticalLoadRequest(1,false,MpTacticalCheckpoint.Hash(busyArchive),busyArchive),"001/A",0),"A pending native scene operation cannot be crossed by another tactical load");
 Check(Directory.GetDirectories(Path.GetDirectoryName(busy.Save)!).Length==directories && NativeTrace.Calls.Count==0,"Busy-scene rejection happens before extraction or native mutation");
 ResetNative(busy);Environment.CurrentDirectory=priorDirectory;
 Reject(()=>MpTacticalNativeScene.Load(new MpTacticalLoadRequest(1,false,MpTacticalCheckpoint.Hash(busyArchive),busyArchive),"001/A",0),"An unexpected process working directory cannot load or save another game's relative paths");
} finally {Environment.CurrentDirectory=priorDirectory;}

if(args.Length==2) {
 string install=Path.GetFullPath(args[0]),save=Path.GetFullPath(args[1]);var actual=MpTacticalLoadPlan.Read(install,save,"001/A",0,false,1.142f);
 Check(actual.Battlefield=="212" && actual.Scenario=="001/A","An actual native campaign battle save passes preflight with its installed scene and content descriptors");
 var rootPlan=MpTacticalLoadPlan.Read(install,save,"001/A",0,true,1.142f);Check(rootPlan.Returning && rootPlan.President>=0,"The actual save's campaign root independently passes authoritative return preflight");
 Console.WriteLine("Actual native content descriptor: "+actual.ContentHash);
}
Console.WriteLine($"Tactical native load plan: {checks} checks. Native scene calls and full terrain-bundle byte parity are not exercised. Fixtures: {sandbox}");
