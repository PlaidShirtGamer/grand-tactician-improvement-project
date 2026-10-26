using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace GTIP.Installer;

public sealed class Installation
{
    public const string ManagedDirectory = "The Civil War (1861-1865)_Data/Managed";
    const string AudioFile = "main-menu.mp3";
    public static readonly string[] Files = ["Assembly-CSharp.dll", "GTIP.Runtime.dll", "campaignprefs.txt", "unitprefs.txt", "battleprefs.txt", AudioFile];
    readonly string root, data;
    public string Managed => Path.Combine(root, ManagedDirectory);
    string StatePath => Path.Combine(data, "state.json");
    string PendingPath => Path.Combine(data, "pending.json");
    public Action<string>? Progress { get; set; }
    // Fault injection for transaction regression tests; never set by the GUI.
    public Action<int>? AfterWrite { get; set; }
    public Installation(string directory)
    {
        root = Path.GetFullPath(directory);
        data = Path.Combine(root, ".gtip-installer");
        if (!Directory.Exists(Managed) || !Directory.Exists(Path.Combine(root, "Config")))
            throw new IOException("Select the game folder containing Config and The Civil War (1861-1865)_Data.");
        foreach (var file in Files) CheckPath(Target(file));
        CheckPath(data);
        foreach (var f in new[] { "state.json", "state.json.tmp", "pending.json", "pending.json.tmp", "original.dll", "lock.dat" })
            CheckPath(Path.Combine(data, f));
    }
    string Target(string file) => Path.Combine(file == AudioFile ? Path.Combine(root, "GTIP", "Audio") : file.EndsWith(".dll") ? Managed : Path.Combine(root, "Config"), file);
    void CheckPath(string path)
    {
        for (string? p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
        {
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Choose a game location without symbolic links or junctions: " + p);
        }
    }
    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static void Save<T>(string path, T value)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
    public sealed class State
    {
        public string Backup { get; set; } = "";
        public Dictionary<string, string?> Before { get; set; } = new();
        public Dictionary<string, string?> Installed { get; set; } = new();
    }
    State Read(string path)
    {
        var state = JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? throw new IOException("Invalid installer record.");
        if (!Guid.TryParseExact(state.Backup, "N", out _)) throw new IOException("Invalid backup identifier.");
        // Older installers tracked the five game/runtime/preferences files only.
        // Accept precisely that known prefix or the complete current set.
        if ((state.Before.Count != Files.Length && state.Before.Count != Files.Length - 1) || Files.Take(state.Before.Count).Any(f => !state.Before.ContainsKey(f)))
            throw new IOException("Incomplete backup record.");
        return state;
    }
    string BackupPath(State state, string file)
    {
        string p = Path.Combine(data, state.Backup, file); CheckPath(p); return p;
    }
    void CheckClosed()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.ProcessName.Contains("The Civil War", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Close Grand Tactician before installing or restoring GTIP.");
            }
        }
        foreach (var file in Files.Where(f => File.Exists(Target(f))))
            using (File.Open(Target(file), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    State Snapshot()
    {
        var s = new State { Backup = Guid.NewGuid().ToString("N") };
        Directory.CreateDirectory(Path.Combine(data, s.Backup));
        foreach (var f in Files)
        {
            s.Before[f] = File.Exists(Target(f)) ? Hash(Target(f)) : null;
            if (s.Before[f] != null) File.Copy(Target(f), BackupPath(s, f));
        }
        return s;
    }
    void ValidateBackup(State state)
    {
        foreach (var f in state.Before.Keys)
            if (state.Before[f] is string hash && (!File.Exists(BackupPath(state, f)) || Hash(BackupPath(state, f)) != hash))
                throw new IOException("Backup missing or changed: " + f);
    }
    void Restore(State state)
    {
        ValidateBackup(state);
        foreach (var f in state.Before.Keys)
        {
            if (state.Before[f] == null) File.Delete(Target(f));
            else { Directory.CreateDirectory(Path.GetDirectoryName(Target(f))!); File.Copy(BackupPath(state, f), Target(f), true); }
        }
    }
    void CheckInstalled(State state)
    {
        if (state.Installed.Count != state.Before.Count) throw new IOException("Incomplete installation record.");
        foreach (var f in state.Before.Keys)
            if (!state.Installed.TryGetValue(f, out var hash) || !File.Exists(Target(f)) || Hash(Target(f)) != hash)
                throw new IOException("File changed since GTIP installation: " + f + ". Preserve your changes and restore the installed version before continuing.");
    }
    void Recover()
    {
        if (!File.Exists(PendingPath)) return;
        Progress?.Invoke("Restoring files from an interrupted operation...");
        var pending = Read(PendingPath);
        Restore(pending);
        var prior = BackupPath(pending, "prior-state.json");
        if (File.Exists(prior)) File.Copy(prior, StatePath, true); else File.Delete(StatePath);
        File.Delete(PendingPath);
    }
    void Transaction(Action action)
    {
        var pending = Snapshot();
        if (File.Exists(StatePath)) File.Copy(StatePath, BackupPath(pending, "prior-state.json"));
        Save(PendingPath, pending);
        try { action(); File.Delete(PendingPath); }
        catch { Recover(); throw; }
    }
    public void Install(string? pristineAssembly = null)
    {
        CheckClosed();
        Directory.CreateDirectory(data);
        using var gate = File.Open(Path.Combine(data, "lock.dat"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Recover();
        State? existing = File.Exists(StatePath) ? Read(StatePath) : null;
        if (existing != null) { CheckInstalled(existing); ValidateBackup(existing); }
        string original = existing != null ? Path.Combine(data, "original.dll") : pristineAssembly ?? Target("Assembly-CSharp.dll");
        CheckPath(original);
        if (!File.Exists(original) || Hash(original) != PatchEngine.SupportedHash)
            throw new IOException("Unsupported or previously patched game assembly. For a manual GTIP installation, select its pristine Assembly-CSharp.dll backup in the optional field. Otherwise verify your game version.");
        string work = Path.Combine(data, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string runtime = Path.Combine(work, "GTIP.Runtime.dll");
        Progress?.Invoke("Compiling GTIP against your game files...");
        RuntimeCompiler.Compile(Managed, original, runtime);
        string staged = Path.Combine(work, "staged");
        Progress?.Invoke("Patching and verifying the game assembly...");
        PatchEngine.Stage(root, staged, runtime, original);
        foreach (var f in Files.Where(f => !f.EndsWith(".dll")))
        {
            using var source = RuntimeCompiler.Resources.GetManifestResourceStream((f == AudioFile ? "Audio." : "Config.") + f) ?? throw new IOException("Missing bundled file " + f);
            using var destination = File.Create(Path.Combine(staged, f)); source.CopyTo(destination);
        }
        // Recheck immediately before touching installed files.
        CheckClosed();
        if (existing != null) CheckInstalled(existing);
        CommitStaged(staged, existing, original);
    }
    internal void CommitStaged(string staged, State? existing, string original)
    {
        Directory.CreateDirectory(data);
        var baseline = existing ?? Snapshot();
        if (existing == null) File.Copy(original, Path.Combine(data, "original.dll"), true);
        Progress?.Invoke("Backing up and installing files...");
        Transaction(() => {
            if (!baseline.Before.ContainsKey(AudioFile))
            {
                // A five-file installation has never owned this optional file.
                // Preserve its current contents before the first managed update.
                baseline.Before[AudioFile] = File.Exists(Target(AudioFile)) ? Hash(Target(AudioFile)) : null;
                if (baseline.Before[AudioFile] != null) File.Copy(Target(AudioFile), BackupPath(baseline, AudioFile), true);
            }
            for (int i = 0; i < Files.Length; i++)
            {
                string f = Files[i];
                Directory.CreateDirectory(Path.GetDirectoryName(Target(f))!);
                File.Copy(Path.Combine(staged, f), Target(f), true);
                baseline.Installed[f] = Hash(Path.Combine(staged, f));
                if (Hash(Target(f)) != baseline.Installed[f]) throw new IOException("Verification failed: " + f);
                AfterWrite?.Invoke(i);
            }
            Save(StatePath, baseline);
        });
        Progress?.Invoke("Installed successfully. Backups: " + Path.Combine(data, baseline.Backup));
    }
    public void Uninstall()
    {
        CheckClosed(); Directory.CreateDirectory(data);
        using var gate = File.Open(Path.Combine(data, "lock.dat"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Recover();
        if (!File.Exists(StatePath)) throw new IOException("This folder has no installation recorded by the GTIP installer.");
        var state = Read(StatePath); CheckInstalled(state); ValidateBackup(state);
        Transaction(() => { Restore(state); File.Delete(StatePath); });
        Progress?.Invoke("Restored the files present before the first installer run. Backups have been retained.");
    }
}
