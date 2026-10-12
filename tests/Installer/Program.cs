using GTIP.Installer;
using System.Text.Json;

void Check(bool test, string text) { if (!test) throw new Exception(text); Console.WriteLine("PASS " + text); }
void Reject(Action action, string text) { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } Check(rejected, text); }
string sandbox = Path.Combine(Path.Combine(Environment.CurrentDirectory, "artifacts"), "GTIP-installer-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(sandbox);
string Fixture(string name)
{
    string path = Path.Combine(sandbox, name);
    Directory.CreateDirectory(Path.Combine(path, Installation.ManagedDirectory)); Directory.CreateDirectory(Path.Combine(path, "Config"));
    foreach (var f in Installation.Files.Where(f => f != "GTIP.Runtime.dll"))
        File.WriteAllText(Path.Combine(path, f.EndsWith(".dll") ? Installation.ManagedDirectory : "Config", f), "before-" + f);
    return path;
}
string FileAt(string root, string f) => Path.Combine(root, f.EndsWith(".dll") ? Installation.ManagedDirectory : "Config", f);
string stage = Path.Combine(sandbox, "stage"); Directory.CreateDirectory(stage);
foreach (var f in Installation.Files) File.WriteAllText(Path.Combine(stage, f), "installed-" + f);
string game = Fixture("transaction"); var engine = new Installation(game);
engine.CommitStaged(stage, null, FileAt(game, "Assembly-CSharp.dll"));
Check(Installation.Files.All(f => File.ReadAllText(FileAt(game, f)) == "installed-" + f), "All five files installed");
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
    engine.Install();
    engine.Uninstall();
    Check(Installation.Hash(FileAt(game, "Assembly-CSharp.dll")) == PatchEngine.SupportedHash && !File.Exists(FileAt(game, "GTIP.Runtime.dll")), "Real-game update and uninstall restore pristine assembly");
}
Console.WriteLine("Fixtures retained for inspection: " + sandbox);
