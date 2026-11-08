using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MfgLauncher
{
    public sealed class TextDialog : Form
    {
        public TextDialog(string title, string text)
        {
            Theme.Form(this); Text = title; Size = new Size(790, 590); MinimumSize = new Size(550, 400); Padding = new Padding(22);
            var box = Theme.TextBox(); box.Multiline = true; box.ReadOnly = true; box.ScrollBars = ScrollBars.Both; box.WordWrap = true; box.Dock = DockStyle.Fill; box.Text = text; box.AccessibleName = title; Controls.Add(box);
            var close = Theme.Button("Close"); close.Dock = DockStyle.Bottom; close.DialogResult = DialogResult.OK; Controls.Add(close); AcceptButton = close; CancelButton = close;
        }
    }
    public sealed class RootModeDialog : Form
    {
        readonly RadioButton parent;
        public bool IsParent { get { return parent.Checked; } }
        public RootModeDialog(string path)
        {
            Theme.Form(this); Text = "Add folder"; ClientSize = new Size(580, 280); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            var label = Theme.Label(path, 10, Theme.Muted); label.SetBounds(24, 20, 530, 55); Controls.Add(label);
            var single = new RadioButton { Text = "This is one game's folder", Checked = true, ForeColor = Theme.Text }; single.SetBounds(24, 90, 510, 35); Controls.Add(single);
            parent = new RadioButton { Text = "This folder contains several game folders", ForeColor = Theme.Text }; parent.SetBounds(24, 135, 520, 35); Controls.Add(parent);
            var add = Theme.Button("Add folder", true); add.SetBounds(412, 215, 140, 40); add.DialogResult = DialogResult.OK; Controls.Add(add); AcceptButton = add;
            var cancel = Theme.Button("Cancel"); cancel.SetBounds(288, 215, 108, 40); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
        }
    }
    public sealed class RootsDialog : Form
    {
        readonly ListBox list = new ListBox { BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, HorizontalScrollbar = true, DisplayMember = "Path" };
        public List<ScanRoot> Roots { get; private set; }
        public RootsDialog(List<ScanRoot> roots)
        {
            Roots = roots.ToList(); Theme.Form(this); Text = "Added game folders"; ClientSize = new Size(720, 420); MinimumSize = new Size(600, 380);
            var note = Theme.Label("Removing a folder changes the library only. Game files are kept.\nSteam libraries are always discovered automatically.", 10, Theme.Muted); note.SetBounds(22, 16, 665, 60); note.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; Controls.Add(note);
            list.SetBounds(22, 90, 670, 240); list.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom; list.AccessibleName = "Added folders"; foreach (var root in Roots) list.Items.Add(root); Controls.Add(list);
            var remove = Theme.Button("Remove folder"); remove.SetBounds(22, 356, 145, 40); remove.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; remove.Click += delegate { if (list.SelectedIndex < 0) return; int index = list.SelectedIndex; Roots.RemoveAt(index); list.Items.RemoveAt(index); }; Controls.Add(remove);
            var save = Theme.Button("Save", true); save.SetBounds(584, 356, 108, 40); save.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; save.DialogResult = DialogResult.OK; Controls.Add(save); AcceptButton = save;
            var cancel = Theme.Button("Cancel"); cancel.SetBounds(460, 356, 108, 40); cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
        }
    }
    public sealed class InstallDialog : Form
    {
        readonly ComboBox proxies = Theme.Combo(); readonly CheckBox reviewed;
        public string Proxy { get { return proxies.SelectedItem as string; } }
        public bool AntiCheatAcknowledged { get; private set; }
        public InstallDialog(Game game, PackageInfo package, bool updating = false)
        {
            bool risk = game.HasAntiCheat;
            Theme.Form(this); Text = updating ? "Update MFG Unlocker" : "Install MFG Unlocker"; ClientSize = new Size(700, risk ? 600 : 490); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            var title = Theme.Label(game.Name, 19, Theme.Text, true); title.SetBounds(24, 18, 650, 42); Controls.Add(title);
            var info = Theme.Label(package.Version + "\nSource: " + package.Source + "\n" + (package.DigestVerified ? "Published SHA-256 verified." : "No published archive digest verified.") + "\n\nTarget: " + Path.GetDirectoryName(game.SelectedExe), 10, Theme.Muted); info.SetBounds(24, 75, 650, 155); Controls.Add(info);
            var caption = Theme.Label("Proxy filename", 10, Theme.Text, true); caption.SetBounds(24, 239, 640, 23); Controls.Add(caption);
            proxies.SetBounds(24, 269, 650, 30);
            if (updating && game.Installed != null) { proxies.Items.Add(game.Installed.ProxyName); proxies.Enabled = false; }
            else foreach (var name in game.SuggestedProxies) if (!File.Exists(Path.Combine(Path.GetDirectoryName(game.SelectedExe), name))) proxies.Items.Add(name);
            if (proxies.Items.Count > 0) proxies.SelectedIndex = 0; Controls.Add(proxies);
            var note = Theme.Label("Close the game before installing. Enable Frame Generation in-game.\nBackspace opens the mod menu. Game updates can change compatibility.", 10, Theme.Muted); note.SetBounds(24, 317, 650, 55); Controls.Add(note);
            if (risk) { var warning = Theme.Label("Anti-cheat risk\nMFG Unlocker does not support anti-cheat games. Installing can prevent the\ngame from launching, trigger detections, or lead to account penalties or bans.\nThis launcher does not disable or bypass anti-cheat. Proceed at your own risk.", 10, Theme.Bad); warning.SetBounds(24, 380, 650, 108); Controls.Add(warning); }
            reviewed = new CheckBox { Text = risk ? "I acknowledge these risks and accept responsibility for this installation." : "I reviewed the evidence and want to test this unverified candidate.", ForeColor = Theme.Text, Checked = !risk && game.Status != GameStatus.Candidate, Visible = risk || game.Status == GameStatus.Candidate };
            reviewed.SetBounds(24, risk ? 490 : 380, 650, 35); reviewed.AccessibleName = reviewed.Text; Controls.Add(reviewed);
            var install = Theme.Button(updating ? "Update" : "Install", true); install.SetBounds(554, ClientSize.Height - 59, 120, 40); install.Enabled = proxies.Items.Count > 0 && reviewed.Checked; reviewed.CheckedChanged += delegate { install.Enabled = proxies.Items.Count > 0 && reviewed.Checked; }; install.Click += delegate { AntiCheatAcknowledged = risk && reviewed.Checked; DialogResult = DialogResult.OK; }; Controls.Add(install); AcceptButton = install;
            var cancel = Theme.Button("Cancel"); cancel.SetBounds(430, ClientSize.Height - 59, 108, 40); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
        }
    }
    public sealed class DetailsDialog : Form
    {
        public bool RequestedInstall { get; private set; }
        public event EventHandler EvidenceChanged;
        readonly Button install = Theme.Button("Install", true);
        bool updating, closePending; readonly Game game; readonly ComboBox executables = Theme.Combo(); readonly TextBox evidence = Theme.TextBox(); readonly Label status = Theme.Label("", 11, Theme.Text, true); readonly Button update = Theme.Button("Update release", true); readonly PackageInfo package;
        public DetailsDialog(Game selected, PackageInfo currentPackage)
        {
            game = selected; package = currentPackage; FormClosing += delegate(object sender, FormClosingEventArgs e) { if (updating) { e.Cancel = true; closePending = true; } }; Theme.Form(this); Text = "Game details"; ClientSize = new Size(820, 690); MinimumSize = new Size(710, 630);
            var title = Theme.Label(game.Name, 22, Theme.Text, true); title.SetBounds(24, 18, 760, 45); title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; Controls.Add(title);
            status.SetBounds(24, 74, 760, 28); Controls.Add(status);
            var path = Theme.Label(game.Root, 10, Theme.Muted); path.SetBounds(24, 111, 760, 40); path.AutoEllipsis = true; path.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; Controls.Add(path);
            var exeLabel = Theme.Label("Game executable", 10, Theme.Text, true); exeLabel.SetBounds(24, 165, 760, 24); Controls.Add(exeLabel);
            executables.SetBounds(24, 199, 760, 30); executables.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; executables.AccessibleName = "Choose game executable"; executables.Items.Add("Choose the actual game executable"); foreach (var exe in game.Executables) executables.Items.Add(exe); executables.SelectedIndex = String.IsNullOrEmpty(game.SelectedExe) ? 0 : Math.Max(0, executables.Items.IndexOf(game.SelectedExe)); Controls.Add(executables);
            executables.SelectedIndexChanged += delegate { game.SelectedExe = executables.SelectedIndex > 0 ? executables.SelectedItem as string : null; GameEvaluation.Refresh(game); RefreshEvidence(); if (EvidenceChanged != null) EvidenceChanged(this, EventArgs.Empty); };
            var caption = Theme.Label("Compatibility evidence", 10, Theme.Text, true); caption.SetBounds(24, 250, 760, 25); Controls.Add(caption);
            evidence.Multiline = true; evidence.ReadOnly = true; evidence.ScrollBars = ScrollBars.Vertical; evidence.SetBounds(24, 285, 760, 275); evidence.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom; evidence.AccessibleName = "Compatibility evidence"; Controls.Add(evidence);
            var note = Theme.Label("Detection does not guarantee support. Anti-cheat overrides require risk acknowledgment.\nDirect DLL installation only; Bink and OptiScaler plugin routes are excluded.", 9, Theme.Muted); note.SetBounds(24, 577, 760, 45); note.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; Controls.Add(note);
            var launch = Theme.Button("Launch"); launch.SetBounds(24, 634, 100, 38); launch.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; launch.Click += delegate { Try(() => LaunchService.Launch(game)); }; Controls.Add(launch);
            var open = Theme.Button("Open folder"); open.SetBounds(137, 634, 120, 38); open.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; open.Click += delegate { Try(() => LaunchService.OpenFolder(game)); }; Controls.Add(open);
            install.SetBounds(428, 634, 108, 38); install.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; install.Click += delegate { RequestedInstall = true; DialogResult = DialogResult.OK; }; Controls.Add(install);
            update.SetBounds(270, 634, 145, 38); update.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; update.Click += async delegate { await UpdateRelease(); }; Controls.Add(update);
            var close = Theme.Button("Close"); close.SetBounds(676, 634, 108, 38); close.Anchor = AnchorStyles.Right | AnchorStyles.Bottom; close.DialogResult = DialogResult.OK; Controls.Add(close); CancelButton = close;
            RefreshEvidence();
        }
        void RefreshEvidence()
        {
            status.Text = game.HasAntiCheat ? "Anti-cheat warning" : Theme.StatusText(game.Status); status.ForeColor = Theme.StatusColor(game.Status);
            evidence.Text = String.Join("\r\n\r\n", game.Evidence) + "\r\n\r\n" + (game.Installed != null ? "Installed: " + game.Installed.Version + "\r\nProxy: " + game.Installed.DllPath + "\r\nSHA-256: " + game.Installed.Sha256 : "Suggested filenames: " + String.Join(", ", game.SuggestedProxies));
            install.Enabled = package != null && game.Installed == null && (game.Status == GameStatus.Reported || game.Status == GameStatus.Candidate || game.CanOverrideAntiCheat);
            update.Enabled = game.Installed != null && package != null && (game.Status != GameStatus.Blocked || game.CanOverrideAntiCheat) && game.Installed.Sha256 != package.Sha256 && !String.IsNullOrEmpty(game.SelectedExe) && String.Equals(Util.Canonical(game.SelectedExe), Util.Canonical(game.Installed.ExePath), StringComparison.OrdinalIgnoreCase);
        }
        async Task UpdateRelease()
        {
            bool antiCheatAcknowledged = false;
            if (game.HasAntiCheat) { using (var review = new InstallDialog(game, package, true)) { if (review.ShowDialog(this) != DialogResult.OK) return; antiCheatAcknowledged = review.AntiCheatAcknowledged; } }
            else if (MessageBox.Show(this, "Update to " + package.Version + " from " + package.Source + "?\n\nThe previous owned DLL and receipt will be retained in the launcher's rollback folder.", "Update MFG Unlocker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            updating = true; Enabled = false;
            try { await Task.Run(() => InstallService.Update(game, package, antiCheatAcknowledged)); GameEvaluation.Refresh(game); RefreshEvidence(); MessageBox.Show(this, "Updated. Previous version retained in:\n" + Path.Combine(StateStore.DataDir, "rollback"), "MFG Launcher"); } catch (Exception ex) { GameEvaluation.Refresh(game); RefreshEvidence(); MessageBox.Show(this, ex.Message, "MFG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); } finally { updating = false; Enabled = true; if (closePending) Close(); }
        }
        void Try(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "MFG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    }
}




