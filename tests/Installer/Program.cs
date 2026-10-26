using GTIP.Installer;
using System.Text.Json;

int checks=0;
void Check(bool test, string text) { if (!test) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
void Reject(Action action, string text) { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } Check(rejected, text); }
string sandbox = Path.Combine(Path.Combine(Environment.CurrentDirectory, "artifacts"), "GTIP-installer-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(sandbox);
string stage = Path.Combine(sandbox, "stage"); Directory.CreateDirectory(stage);
string Fixture(string name, bool audio = true)
{
    string path = Path.Combine(sandbox, name);
    Directory.CreateDirectory(Path.Combine(path, Installation.ManagedDirectory)); Directory.CreateDirectory(Path.Combine(path, "Config"));
    foreach (var f in Installation.Files.Where(f => f != "GTIP.Runtime.dll" && (audio || f != "main-menu.mp3")))
    { Directory.CreateDirectory(Path.GetDirectoryName(FileAt(path,f))!); File.WriteAllText(FileAt(path,f), "before-" + f); }
    return path;
}
string FileAt(string root, string f) => Path.Combine(root, f == "main-menu.mp3" ? "GTIP/Audio" : f.EndsWith(".dll") ? Installation.ManagedDirectory : "Config", f);
Installation.State ReadState(string path)=>JsonSerializer.Deserialize<Installation.State>(File.ReadAllText(Path.Combine(path,".gtip-installer/state.json")))!;
Installation.State LegacyInstall(string path)
{
    var record=new Installation.State{Backup=Guid.NewGuid().ToString("N")};string data=Path.Combine(path,".gtip-installer"),backup=Path.Combine(data,record.Backup);
    Directory.CreateDirectory(backup);File.Copy(FileAt(path,"Assembly-CSharp.dll"),Path.Combine(data,"original.dll"));
    foreach(var f in Installation.Files.Take(5)) {
        record.Before[f]=File.Exists(FileAt(path,f))?Installation.Hash(FileAt(path,f)):null;
        if(record.Before[f]!=null)File.Copy(FileAt(path,f),Path.Combine(backup,f));
        File.Copy(Path.Combine(stage,f),FileAt(path,f),true);record.Installed[f]=Installation.Hash(FileAt(path,f));
    }
    File.WriteAllText(Path.Combine(data,"state.json"),JsonSerializer.Serialize(record));return record;
}
foreach (var f in Installation.Files) File.WriteAllText(Path.Combine(stage, f), "installed-" + f);
string game = Fixture("transaction"); var engine = new Installation(game);
engine.CommitStaged(stage, null, FileAt(game, "Assembly-CSharp.dll"));
Check(Installation.Files.All(f => File.ReadAllText(FileAt(game, f)) == "installed-" + f), "All six files installed, including GTIP/Audio/main-menu.mp3");
string statePath = Path.Combine(game, ".gtip-installer/state.json");
var state = JsonSerializer.Deserialize<Installation.State>(File.ReadAllText(statePath))!;
foreach (var f in Installation.Files) File.WriteAllText(Path.Combine(stage, f), "updated-" + f);
engine.CommitStaged(stage, state, Path.Combine(game, ".gtip-installer/original.dll"));
engine.Uninstall();
Check(Installation.Files.Where(f => f != "GTIP.Runtime.dll").All(f => File.ReadAllText(FileAt(game, f)) == "before-" + f) && !File.Exists(FileAt(game, "GTIP.Runtime.dll")), "Update then uninstall restores initial files and removes added runtime");
game = Fixture("failure"); engine = new Installation(game) { AfterWrite = i => { if (i == 2) throw new IOException("Injected failure"); } };
Reject(() => engine.CommitStaged(stage, null, FileAt(game, "Assembly-CSharp.dll")), "Injected mid-copy failure reported");
Check(Installation.Files.Where(f => f != "GTIP.Runtime.dll").All(f => File.ReadAllText(FileAt(game, f)) == "before-" + f) && !File.Exists(FileAt(game, "GTIP.Runtime.dll")) && !File.Exists(Path.Combine(game, ".gtip-installer/state.json")), "Failed installation rolls every file back");
game = Fixture("changed"); engine = new Installation(game); engine.CommitStaged(stage, null, FileAt(game, "Assembly-CSharp.dll"));
File.WriteAllText(FileAt(game, "unitprefs.txt"), "user customization");
Reject(engine.Uninstall, "Uninstall refuses files modified after installation");
Check(File.ReadAllText(FileAt(game, "unitprefs.txt")) == "user customization", "Modified preferences preserved");
game = Fixture("unsupported"); engine = new Installation(game);
Reject(() => engine.Install(), "Unsupported game hash rejected before patching");
Check(File.ReadAllText(FileAt(game, "Assembly-CSharp.dll")) == "before-Assembly-CSharp.dll", "Unsupported assembly unchanged");
Reject(() => new Installation(Path.Combine(sandbox, "missing")), "Invalid game folder rejected");
game = Fixture("interrupted"); engine = new Installation(game); engine.CommitStaged(stage, null, FileAt(game, "Assembly-CSharp.dll"));
var pending = new Installation.State { Backup = Guid.NewGuid().ToString("N") };
string data = Path.Combine(game, ".gtip-installer"), recovery = Path.Combine(data, pending.Backup);
Directory.CreateDirectory(recovery);
foreach (var f in Installation.Files) { pending.Before[f] = Installation.Hash(FileAt(game, f)); File.Copy(FileAt(game, f), Path.Combine(recovery, f)); }
File.Copy(Path.Combine(data, "state.json"), Path.Combine(recovery, "prior-state.json"));
File.WriteAllText(Path.Combine(data, "pending.json"), JsonSerializer.Serialize(pending));
File.WriteAllText(FileAt(game, "Assembly-CSharp.dll"), "interrupted replacement");
File.Delete(Path.Combine(data, "state.json"));
engine.Uninstall();
Check(File.ReadAllText(FileAt(game, "Assembly-CSharp.dll")) == "before-Assembly-CSharp.dll" && !File.Exists(Path.Combine(data, "pending.json")), "Interrupted transaction recovered before uninstall");
game = Fixture("corrupt-backup"); engine = new Installation(game); engine.CommitStaged(stage, null, FileAt(game, "Assembly-CSharp.dll"));
state = JsonSerializer.Deserialize<Installation.State>(File.ReadAllText(Path.Combine(game, ".gtip-installer/state.json")))!;
File.WriteAllText(Path.Combine(game, ".gtip-installer", state.Backup, "Assembly-CSharp.dll"), "corrupted");
Reject(engine.Uninstall, "Corrupt backup rejected before restore");
Check(File.ReadAllText(FileAt(game, "Assembly-CSharp.dll")) == "updated-Assembly-CSharp.dll", "Corrupt backup cannot overwrite installed assembly");

using(var audio=RuntimeCompiler.Resources.GetManifestResourceStream("Audio.main-menu.mp3")) {
    Check(audio!=null && audio.Length>0 && !RuntimeCompiler.Resources.GetManifestResourceNames().Contains("Config.main-menu.mp3"),"The installer embeds the MP3 as its own Audio resource rather than a preference");
    string asset=Path.Combine(Environment.CurrentDirectory,"assets/audio/main-menu.mp3");
    Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(audio!))==Installation.Hash(asset),"The embedded MP3 exactly matches the supplied repository asset");
}
game=Fixture("audio-absent",false);engine=new Installation(game);engine.CommitStaged(stage,null,FileAt(game,"Assembly-CSharp.dll"));
Check(ReadState(game).Before["main-menu.mp3"]==null && File.Exists(FileAt(game,"main-menu.mp3")),"A fresh install records an absent audio baseline and creates its target directory");
engine.Uninstall();Check(!File.Exists(FileAt(game,"main-menu.mp3")) && !File.Exists(FileAt(game,"GTIP.Runtime.dll")),"Fresh uninstall removes newly added audio and runtime files");

game=Fixture("audio-changed");engine=new Installation(game);engine.CommitStaged(stage,null,FileAt(game,"Assembly-CSharp.dll"));
File.WriteAllText(FileAt(game,"main-menu.mp3"),"user replacement music");
Reject(engine.Uninstall,"Uninstall refuses managed audio modified since installation");
Reject(()=>engine.Install(),"Update refuses managed audio modified since installation before compiling");
Check(File.ReadAllText(FileAt(game,"main-menu.mp3"))=="user replacement music","Refused operations preserve modified audio");

foreach(bool audio in new[]{false,true}) {
    game=Fixture("audio-write-failure-"+audio,audio);engine=new Installation(game){AfterWrite=i=>{if(i==5)throw new IOException("Injected audio write failure");}};
    Reject(()=>engine.CommitStaged(stage,null,FileAt(game,"Assembly-CSharp.dll")),"A fault immediately after writing audio is reported with baseline present="+audio);
    Check(Installation.Files.Take(5).Where(f=>f!="GTIP.Runtime.dll").All(f=>File.ReadAllText(FileAt(game,f))=="before-"+f) &&
        !File.Exists(FileAt(game,"GTIP.Runtime.dll")) && (audio?File.ReadAllText(FileAt(game,"main-menu.mp3"))=="before-main-menu.mp3":!File.Exists(FileAt(game,"main-menu.mp3"))) &&
        !File.Exists(Path.Combine(game,".gtip-installer/state.json")),"An audio-write fault rolls all six targets back with baseline present="+audio);
}

game=Fixture("legacy-uninstall");state=LegacyInstall(game);engine=new Installation(game);engine.Uninstall();
Check(File.ReadAllText(FileAt(game,"main-menu.mp3"))=="before-main-menu.mp3" && !File.Exists(FileAt(game,"GTIP.Runtime.dll")),"A legacy five-file uninstall restores its own files and leaves unmanaged audio alone");

foreach(bool audio in new[]{false,true}) {
    game=Fixture("legacy-upgrade-"+audio,audio);state=LegacyInstall(game);string retained=state.Backup;engine=new Installation(game);
    engine.CommitStaged(stage,state,Path.Combine(game,".gtip-installer/original.dll"));state=ReadState(game);
    Check(state.Backup==retained && state.Before.Count==6 && state.Installed.Count==6 && (state.Before["main-menu.mp3"]!=null)==audio,
        "A five-file upgrade retains original game backups and captures the unmanaged audio baseline present="+audio);
    engine.CommitStaged(stage,state,Path.Combine(game,".gtip-installer/original.dll"));engine.Uninstall();
    Check(Installation.Files.Take(5).Where(f=>f!="GTIP.Runtime.dll").All(f=>File.ReadAllText(FileAt(game,f))=="before-"+f) &&
        (audio?File.ReadAllText(FileAt(game,"main-menu.mp3"))=="before-main-menu.mp3":!File.Exists(FileAt(game,"main-menu.mp3"))),
        "Later updates retain the first audio baseline and uninstall restores its contents or absence present="+audio);
}

game=Fixture("legacy-upgrade-failure");state=LegacyInstall(game);engine=new Installation(game){AfterWrite=i=>{if(i==5)throw new IOException("Injected migration failure");}};
Reject(()=>engine.CommitStaged(stage,state,Path.Combine(game,".gtip-installer/original.dll")),"A first audio migration can roll back after its last file write");
Check(ReadState(game).Before.Count==5 && File.ReadAllText(FileAt(game,"main-menu.mp3"))=="before-main-menu.mp3" &&
    Installation.Files.Take(5).All(f=>File.ReadAllText(FileAt(game,f))=="updated-"+f),"Failed migration restores the legacy five-file record, installed files and unmanaged audio");
File.WriteAllText(FileAt(game,"main-menu.mp3"),"custom after failed migration");engine=new Installation(game);
engine.CommitStaged(stage,ReadState(game),Path.Combine(game,".gtip-installer/original.dll"));engine.Uninstall();
Check(File.ReadAllText(FileAt(game,"main-menu.mp3"))=="custom after failed migration","Retrying migration captures current unmanaged audio rather than an orphan backup from the failed attempt");

game=Fixture("legacy-interrupted");state=LegacyInstall(game);data=Path.Combine(game,".gtip-installer");
pending=new Installation.State{Backup=Guid.NewGuid().ToString("N")};recovery=Path.Combine(data,pending.Backup);Directory.CreateDirectory(recovery);
foreach(var f in Installation.Files.Take(5)){pending.Before[f]=Installation.Hash(FileAt(game,f));File.Copy(FileAt(game,f),Path.Combine(recovery,f));}
File.Copy(Path.Combine(data,"state.json"),Path.Combine(recovery,"prior-state.json"));File.WriteAllText(Path.Combine(data,"pending.json"),JsonSerializer.Serialize(pending));
File.WriteAllText(FileAt(game,"Assembly-CSharp.dll"),"interrupted legacy replacement");File.Delete(Path.Combine(data,"state.json"));
File.WriteAllText(FileAt(game,"main-menu.mp3"),"unmanaged during legacy recovery");engine=new Installation(game);engine.Uninstall();
Check(!File.Exists(Path.Combine(data,"pending.json")) && File.ReadAllText(FileAt(game,"Assembly-CSharp.dll"))=="before-Assembly-CSharp.dll" &&
    File.ReadAllText(FileAt(game,"main-menu.mp3"))=="unmanaged during legacy recovery","An interrupted five-file journal recovers and uninstalls without touching unrecorded audio");

game=Fixture("invalid-record");state=LegacyInstall(game);state.Before.Remove("battleprefs.txt");state.Before["main-menu.mp3"]=null;
File.WriteAllText(Path.Combine(game,".gtip-installer/state.json"),JsonSerializer.Serialize(state));engine=new Installation(game);
Reject(engine.Uninstall,"Legacy compatibility accepts only the exact known five-file set");
// A real-game integration run is optional locally; no game binaries are needed by CI.
if (args.Length == 2)
{
    game = Fixture("real-game");
    foreach (string file in Directory.GetFiles(Path.Combine(args[0], Installation.ManagedDirectory), "*.dll"))
        File.Copy(file, Path.Combine(game, Installation.ManagedDirectory, Path.GetFileName(file)), true);
    File.Copy(args[1], FileAt(game, "Assembly-CSharp.dll"), true);
    File.Delete(FileAt(game, "GTIP.Runtime.dll"));
    engine = new Installation(game) { Progress = Console.WriteLine };
    engine.Install();
    Check(Installation.Hash(FileAt(game, "Assembly-CSharp.dll")) != PatchEngine.SupportedHash, "Real game compiled and patched in disposable fixture");
    Check(Installation.Hash(FileAt(game,"main-menu.mp3"))==Installation.Hash(Path.Combine(Environment.CurrentDirectory,"assets/audio/main-menu.mp3")) &&
        ReadState(game).Installed.Count==6,"Real installer stages the embedded MP3 at its managed audio target");
    engine.Install();
    engine.Uninstall();
    Check(Installation.Hash(FileAt(game, "Assembly-CSharp.dll")) == PatchEngine.SupportedHash && !File.Exists(FileAt(game, "GTIP.Runtime.dll")) &&
        File.ReadAllText(FileAt(game,"main-menu.mp3"))=="before-main-menu.mp3", "Real-game update and uninstall restore pristine assembly and original custom audio");
}
Console.WriteLine($"Installer transactions: {checks} checks.");
Console.WriteLine("Fixtures retained for inspection: " + sandbox);
