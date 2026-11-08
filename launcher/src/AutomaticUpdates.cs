using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MfgLauncher
{
    public sealed partial class MainForm
    {
        Timer updateTimer;
        void ConfigureUpdates()
        {
            bool oldDll = settings.AutoUpdateDll, oldLauncher = settings.AutoUpdateLauncher; string oldRepo = settings.LauncherRepository;
            using (var dialog = new UpdateSettingsDialog(settings)) if (dialog.ShowDialog(this) == DialogResult.OK) { try { StateStore.Save(StateStore.SettingsPath, settings); SetStatus("Update preferences saved. Use Check for updates to check now."); } catch (Exception ex) { settings.AutoUpdateDll = oldDll; settings.AutoUpdateLauncher = oldLauncher; settings.LauncherRepository = oldRepo; ShowError(ex); } }
        }
        void StartUpdateTimer()
        {
            updateTimer = new Timer { Interval = 6 * 60 * 60 * 1000 };
            updateTimer.Tick += async delegate { if (!busy && OwnedForms.Length == 0) await CheckUpdates(false); }; updateTimer.Start(); FormClosed += delegate { updateTimer.Dispose(); };
        }
        async Task CheckUpdates(bool manual)
        {
            if (busy || IsDisposed || (!manual && !settings.AutoUpdateDll && !settings.AutoUpdateLauncher)) return;
            SetBusy(true); var notices = new List<string>(); int updated = 0; string stagedLauncher = null, launcherHash = null;
            try {
                if (manual || settings.AutoUpdateDll) {
                    try {
                        SetStatus("Checking the official MFG Unlocker release...");
                        var release = await Task.Run(() => PackageManager.CheckLatest());
                        if (package == null || package.Version != release.Version || !package.DigestVerified) {
                            package = await Task.Run(() => PackageManager.DownloadLatest(SetStatus)); StateStore.Save(StateStore.PackagePath, package); UpdatePackage();
                        }
                        if (settings.AutoUpdateDll && package != null) foreach (var game in library.Games.Where(g => g.Installed != null).ToList()) {
                            try {
                                var receipt = await Task.Run(() => InstallService.ReadReceipt(game)); if (receipt == null || receipt.Sha256 == package.Sha256) continue;
                                Version installedVersion, availableVersion; if (!Version.TryParse((receipt.Version ?? "").TrimStart('v', 'V'), out installedVersion) || !Version.TryParse((package.Version ?? "").TrimStart('v', 'V'), out availableVersion) || installedVersion > availableVersion) { notices.Add(game.Name + ": automatic update skipped; its version is newer or cannot be compared. Use Details for a manual update."); continue; }
                                if (game.ScanIncomplete || game.Status == GameStatus.Blocked || String.IsNullOrEmpty(game.SelectedExe) || !String.Equals(Util.Canonical(game.SelectedExe), Util.Canonical(receipt.ExePath), StringComparison.OrdinalIgnoreCase)) { notices.Add(game.Name + ": automatic DLL update skipped; review its executable/scan evidence."); continue; }
                                SetStatus("Updating the owned DLL in " + game.Name + "..."); await Task.Run(() => InstallService.Update(game, package)); updated++;
                            } catch (Exception ex) { notices.Add(game.Name + ": DLL update deferred. " + ex.Message); }
                            finally { GameEvaluation.Refresh(game); }
                        }
                    } catch (Exception ex) { notices.Add("DLL release check: " + ex.Message); }
                }
                if (settings.AutoUpdateLauncher && !String.IsNullOrWhiteSpace(settings.LauncherRepository)) {
                    try {
                        SetStatus("Checking launcher releases in " + settings.LauncherRepository + "...");
                        var release = await Task.Run(() => LauncherUpdates.Check(settings.LauncherRepository));
                        if (LauncherUpdates.IsNewer(release.Version, Assembly.GetExecutingAssembly().GetName().Version)) {
                            SetStatus("Downloading and verifying launcher " + release.Version + "..."); stagedLauncher = await Task.Run(() => LauncherUpdates.Download(release)); launcherHash = release.Sha256;
                        }
                    } catch (Exception ex) { notices.Add("Launcher release check: " + ex.Message); }
                }
                library.Errors.AddRange(notices); RenderGames(); errorsButton.Text = library.Errors.Count == 0 ? "Scan report" : "Scan report (" + library.Errors.Count + ")";
                SetStatus("Update check finished. " + updated + " game DLLs updated." + (notices.Count > 0 ? " " + notices.Count + " notices in Scan report." : " Releases are current."));
                if (manual && notices.Count > 0) using (var report = new TextDialog("Update report", String.Join(Environment.NewLine + Environment.NewLine, notices))) report.ShowDialog(this);
                if (stagedLauncher != null) { SetStatus("Restarting to apply the verified launcher update..."); SelfUpdater.Begin(stagedLauncher, launcherHash); closePending = true; }
            } catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false); }
        }
    }
}

