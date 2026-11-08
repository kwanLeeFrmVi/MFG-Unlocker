using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace MfgLauncher
{
    public static class SelfUpdater
    {
        static string Quote(string path) { if (path.IndexOf('"') >= 0) throw new InvalidDataException("Invalid update path."); return "\"" + path + "\""; }
        public static void Begin(string staged, string sha256)
        {
            string target = Application.ExecutablePath; string root = Path.Combine(StateStore.DataDir, "launcher-updates");
            if (!Util.IsWithin(root, staged) || Util.HashFile(staged) != sha256 || !Util.IsX64Pe(staged, false)) throw new InvalidDataException("The launcher update failed validation.");
            Util.NoReparse(target); Util.NoReparse(staged); LauncherUpdates.ValidateDownloaded(staged, sha256);
            string helper = Path.Combine(root, "update-helper-" + Guid.NewGuid().ToString("N") + ".exe"); Directory.CreateDirectory(root); File.Copy(target, helper, false);
            var start = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = root, Arguments = "--apply-update " + Quote(target) + " " + Quote(staged) + " " + Quote(sha256) + " " + Process.GetCurrentProcess().Id + " " + Quote(Util.HashFile(target)) };
            Process.Start(start);
        }
        public static bool ApplyIfRequested(string[] args)
        {
            if (args.Length == 0 || args[0] != "--apply-update") return false;
            string temp = null, target = null, backup = null; bool replaced = false;
            try {
                if (args.Length != 6) throw new InvalidDataException("Invalid update request.");
                target = Util.Canonical(args[1]); string staged = Util.Canonical(args[2]); string expected = args[3]; int pid;
                if (!Int32.TryParse(args[4], out pid) || expected.Length != 64 || args[5].Length != 64) throw new InvalidDataException("Invalid update request.");
                string updateRoot = Path.Combine(StateStore.DataDir, "launcher-updates");
                if (!Util.IsWithin(updateRoot, Application.ExecutablePath) || !Util.IsWithin(updateRoot, staged) || !String.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase) || String.Equals(target, staged, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update paths are outside their expected locations.");
                Util.NoReparse(target); Util.NoReparse(staged); LauncherUpdates.ValidateDownloaded(staged, expected);
                if (Util.HashFile(Application.ExecutablePath) != args[5] || Util.HashFile(target) != args[5] || Util.HashFile(staged) != expected || !Util.IsX64Pe(staged, false)) throw new InvalidDataException("The update or existing launcher has changed.");
                try { using (var parent = Process.GetProcessById(pid)) if (!parent.WaitForExit(120000)) throw new IOException("The launcher did not close; update deferred."); } catch (ArgumentException) { }
                if (Util.HashFile(target) != args[5]) throw new IOException("The launcher changed while the update was waiting.");
                temp = Path.Combine(Path.GetDirectoryName(target), ".mfg-update-" + Guid.NewGuid().ToString("N") + ".tmp"); backup = target + ".previous-" + Guid.NewGuid().ToString("N") + ".bak";
                File.Copy(staged, temp, false); if (Util.HashFile(temp) != expected) throw new IOException("Staged update hash mismatch.");
                File.Replace(temp, target, backup); replaced = true;
                if (Util.HashFile(target) != expected) throw new IOException("Updated launcher hash mismatch.");
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target) });
            } catch (Exception ex) {
                if (replaced && target != null && backup != null && File.Exists(backup)) { try { File.Replace(backup, target, null); } catch { } }
                MessageBox.Show("The launcher update could not finish. The previous version was preserved.\n\n" + ex.Message, "MFG Launcher update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } finally { if (temp != null && File.Exists(temp)) { try { File.Delete(temp); } catch { } } }
            return true;
        }
    }
    public sealed class UpdateSettingsDialog : Form
    {
        readonly CheckBox dll = new CheckBox(), launcher = new CheckBox(); readonly TextBox repository = Theme.TextBox(); readonly Settings settings;
        public UpdateSettingsDialog(Settings value)
        {
            settings = value; Theme.Form(this); Text = "Automatic updates"; ClientSize = new System.Drawing.Size(660, 375); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            var title = Theme.Label("Keep the launcher and mod current", 18, Theme.Text, true); title.SetBounds(24, 18, 610, 40); Controls.Add(title);
            dll.Text = "Automatically update owned game DLLs from dashdogy/MFG-Unlocker"; dll.Checked = settings.AutoUpdateDll; dll.ForeColor = Theme.Text; dll.SetBounds(24, 78, 610, 35); Controls.Add(dll);
            launcher.Text = "Automatically update and restart this launcher"; launcher.Checked = settings.AutoUpdateLauncher; launcher.ForeColor = Theme.Text; launcher.SetBounds(24, 123, 610, 35); Controls.Add(launcher);
            var label = Theme.Label("Launcher release repository (owner/name)", 10, Theme.Text, true); label.SetBounds(24, 180, 610, 25); Controls.Add(label);
            repository.Text = settings.LauncherRepository ?? ""; repository.AccessibleName = "Launcher release repository"; repository.SetBounds(24, 211, 610, 30); Controls.Add(repository);
            var note = Theme.Label("Checks run at startup and every six hours while open. Running games and\naltered installations are skipped. Only verified app releases can replace the launcher.", 10, Theme.Muted); note.SetBounds(24, 260, 610, 55); Controls.Add(note);
            var save = Theme.Button("Save", true); save.SetBounds(526, 324, 108, 38); save.Click += delegate { try { if (!String.IsNullOrWhiteSpace(repository.Text)) LauncherUpdates.ValidateRepository(repository.Text.Trim()); settings.AutoUpdateDll = dll.Checked; settings.AutoUpdateLauncher = launcher.Checked; settings.LauncherRepository = repository.Text.Trim(); DialogResult = DialogResult.OK; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Update source", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }; Controls.Add(save); AcceptButton = save;
            var cancel = Theme.Button("Cancel"); cancel.SetBounds(402, 324, 108, 38); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
        }
    }
}


