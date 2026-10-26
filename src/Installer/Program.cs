using GTIP.Installer;
using System.Security.Cryptography;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--self-test")
        {
            try
            {
                var names = RuntimeCompiler.Resources.GetManifestResourceNames();
                if (names.Count(n => n.StartsWith("Runtime.")) < 8 || names.Count(n => n.StartsWith("Config.")) != 3 || names.Count(n => n.StartsWith("Licenses.")) < 4)
                    throw new Exception("Incomplete embedded payload");
                using var audio = RuntimeCompiler.Resources.GetManifestResourceStream("Audio.main-menu.mp3")
                    ?? throw new Exception("Missing bundled main-menu music");
                if (audio.Length != 1786320 || Convert.ToHexString(SHA256.HashData(audio)) != "368B10C209D23C3146267049C903503D6E9DF19FD504109784753B0948D29196")
                    throw new Exception("Bundled main-menu music failed integrity verification");
                RuntimeCompiler.VerifyCompiler();
                File.WriteAllText(args[1], "PASS: self-contained compiler, patcher, runtime source, configs, main-menu MP3 and licenses load; bundled audio size and SHA-256 verified.");
                return 0;
            }
            catch (Exception e) { File.WriteAllText(args[1], e.ToString()); return 1; }
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm(args.Length == 2 && args[0] == "--folder" ? args[1] : ""));
        return 0;
    }
}

sealed class SetupForm : Form
{
    readonly TextBox folder = new() { Dock = DockStyle.Fill };
    readonly TextBox original = new() { Dock = DockStyle.Fill };
    readonly TextBox status = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    readonly Button install = new() { Text = "Install / Update", AutoSize = true };
    readonly Button restore = new() { Text = "Uninstall / Restore", AutoSize = true };
    bool busy;
    public SetupForm(string initialFolder)
    {
        folder.Text = initialFolder;
        Text = "GTIP Setup"; ClientSize = new Size(760, 470); MinimumSize = new Size(650, 450);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var intro = new Label { Text = "Install GTIP in your Grand Tactician game folder. Existing files are backed up.\nIncludes main-menu music and three preferences replacing your campaign, unit, and battle settings.", AutoSize = true, Margin = new Padding(0, 0, 0, 16) };
        layout.Controls.Add(intro, 0, 0); layout.SetColumnSpan(intro, 2);
        layout.Controls.Add(new Label { Text = "Game installation folder", AutoSize = true }, 0, 1);
        layout.Controls.Add(folder, 0, 2);
        var browse = new Button { Text = "Browse…", AutoSize = true };
        browse.Click += (_, _) => { using var dialog = new FolderBrowserDialog(); if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; };
        layout.Controls.Add(browse, 1, 2);
        layout.Controls.Add(new Label { Text = "Pristine Assembly-CSharp.dll backup (only for an earlier manual installation)", AutoSize = true }, 0, 3);
        layout.Controls.Add(original, 0, 4);
        var find = new Button { Text = "Browse…", AutoSize = true };
        find.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = "Game assembly|Assembly-CSharp.dll" }; if (dialog.ShowDialog(this) == DialogResult.OK) original.Text = dialog.FileName; };
        layout.Controls.Add(find, 1, 4);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(install); buttons.Controls.Add(restore); layout.Controls.Add(buttons, 0, 5);
        var licenses = new Button { Text = "Third-party notices", AutoSize = true };
        licenses.Click += (_, _) => {
            using var dialog = new Form { Text = "Third-party notices", Size = new Size(760, 520), StartPosition = FormStartPosition.CenterParent };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both };
            foreach (var name in RuntimeCompiler.Resources.GetManifestResourceNames().Where(n => n.StartsWith("Licenses.")))
            { using var reader = new StreamReader(RuntimeCompiler.Resources.GetManifestResourceStream(name)!); text.AppendText(name + Environment.NewLine + reader.ReadToEnd() + Environment.NewLine); }
            dialog.Controls.Add(text); dialog.ShowDialog(this);
        };
        buttons.Controls.Add(licenses);
        var elevate = new Button { Text = "Run as administrator", AutoSize = true };
        elevate.Click += (_, _) => {
            if (busy) return;
            try {
                var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
                info.ArgumentList.Add("--folder"); info.ArgumentList.Add(folder.Text);
                if (System.Diagnostics.Process.Start(info) != null) Close();
            } catch (Exception e) { status.AppendText(e.Message + Environment.NewLine); }
        };
        buttons.Controls.Add(elevate);
        var note = new Label { Text = "Uninstall restores the files from before the first installer run. Use a compatible pre-mod save.", AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
        layout.Controls.Add(note, 0, 6); layout.SetColumnSpan(note, 2);
        layout.Controls.Add(status, 0, 7); layout.SetColumnSpan(status, 2);
        Controls.Add(layout);
        install.Click += async (_, _) => await Run(false);
        restore.Click += async (_, _) => await Run(true);
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
    }
    async Task Run(bool uninstall)
    {
        if (busy) return;
        string game = folder.Text.Trim(), backup = original.Text.Trim();
        busy = true; install.Enabled = restore.Enabled = false;
        void Log(string text) => BeginInvoke(new Action(() => status.AppendText(text + Environment.NewLine)));
        try
        {
            await Task.Run(() => {
                var engine = new Installation(game) { Progress = Log };
                if (uninstall) engine.Uninstall(); else engine.Install(backup.Length == 0 ? null : backup);
            });
        }
        catch (UnauthorizedAccessException) { Log("Windows denied access to this folder. Use Run as administrator and try again."); }
        catch (Exception error) { Log(error.Message); }
        finally { busy = false; install.Enabled = restore.Enabled = true; }
    }
}
