using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace MfgLauncher
{
    public static class Tests
    {
        static int passed, failed;
        static void Assert(bool condition, string name) { if (condition) { passed++; Console.WriteLine("PASS " + name); } else { failed++; Console.WriteLine("FAIL " + name); } }
        [STAThread] public static int Main(string[] args)
        {
            if (!String.Equals(Path.GetFileName(Application.ExecutablePath), "MFGLauncher.Tests.exe", StringComparison.OrdinalIgnoreCase)) { File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "launched.txt"), "working directory verified"); return 0; }
            try {
                if (args.Length > 0 && args[0] == "--probe-running") { System.Threading.Thread.Sleep(30000); return 0; }
                if (args.Length > 0 && args[0] == "--live-packages") { StateStore.DataDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "qa-fixtures", "live-package-state")); var pkg = PackageManager.Import(Path.GetFullPath(args[1]), "v1.7.0", "Official release test", "sha256:5bd19d03288e1ece1d0bb2030ac072b36758045028da192044dcaad73fd901dd"); Console.WriteLine("Official ZIP import: " + pkg.Version + " " + pkg.Sha256); var release = PackageManager.CheckLatest(); Console.WriteLine("Latest DLL release: " + release.Version + " " + release.AssetUrl); var app = LauncherUpdates.Check("kwanLeeFrmVi/MFG-Unlocker"); string staged = LauncherUpdates.Download(app); Console.WriteLine("Launcher release verified: " + app.Version + " " + Util.HashFile(staged)); return 0; }
                if (args.Length > 0 && args[0] == "--ui-smoke") { UiFixture(); return 0; }
                DiscoveryTests.Run(Assert); CompatibilityTests.Run(Assert); InstallationTests.Run(Assert); LauncherUpdateTests.Run(Assert); GuideTests.Run(Assert); SelfUpdaterSmoke.Run(Assert); LaunchTests();
            } catch (Exception ex) { failed++; Console.WriteLine("UNHANDLED " + ex); }
            Console.WriteLine("RESULT " + passed + " passed; " + failed + " failed"); return failed == 0 ? 0 : 1;
        }
        static void Throws(Action action, string name) { try { action(); Assert(false, name); } catch (Exception) { Assert(true, name); } }
        static void LaunchTests()
        {
            var steam = new Game { Name = "Steam fixture", Root = Path.GetTempPath(), SteamId = "292030" };
            Assert(LaunchService.StartInfo(steam).FileName == "steam://rungameid/292030" && LaunchService.StartInfo(steam).UseShellExecute, "Steam launch uses validated numeric URI");
            steam.SteamId = "292030&calc.exe"; Throws(() => LaunchService.StartInfo(steam), "Steam URI rejects command-like app IDs");
            steam.SteamId = "12\n"; Throws(() => LaunchService.StartInfo(steam), "Steam URI rejects trailing newline");
            string root = Path.Combine(Path.GetTempPath(), "MFG-LaunchTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try {
                string exe = Path.Combine(root, "Game & $name (probe).exe"); File.Copy(Application.ExecutablePath, exe);
                var game = new Game { Root = root, SelectedExe = exe };
                var info = LaunchService.StartInfo(game); Assert(!info.UseShellExecute && info.FileName == exe && info.WorkingDirectory == root, "Executable launch preserves special characters and sets working directory");
                var runningInfo = new ProcessStartInfo(Application.ExecutablePath, "--probe-running") { UseShellExecute = false, CreateNoWindow = true }; using (var running = Process.Start(runningInfo)) { try { var runningGame = new Game { Root = Path.GetDirectoryName(Application.ExecutablePath), SelectedExe = Application.ExecutablePath }; runningGame.Executables.Add(Application.ExecutablePath); Throws(() => InstallService.EnsureNotRunning(runningGame), "Running executable blocks game DLL changes"); } finally { if (!running.HasExited) { running.Kill(); running.WaitForExit(); } } }
                using (var process = Process.Start(info)) { bool exited = process.WaitForExit(10000); Assert(exited && process.ExitCode == 0 && File.Exists(Path.Combine(root, "launched.txt")), "Actual fixture process launches safely in its game directory"); }
                game.SelectedExe = Application.ExecutablePath; Throws(() => LaunchService.StartInfo(game), "Outside-root executable launch refused");
                game.SelectedExe = Path.Combine(root, "missing.exe"); Throws(() => LaunchService.StartInfo(game), "Missing executable launch refused");
                game.SelectedExe = null; Throws(() => LaunchService.StartInfo(game), "Unresolved executable asks for selection");
                Assert(!Util.IsWithin(root, root + "-outside/file.exe"), "Containment respects directory boundary");
                string settingsPath = Path.Combine(root, "settings.json"); var settings = new Settings(); settings.Roots.Add(new ScanRoot { Path = root, IsParent = true }); StateStore.Save(settingsPath, settings); settings.ExeChoices[root] = exe; StateStore.Save(settingsPath, settings);
                Assert(StateStore.Read<Settings>(settingsPath).ExeChoices[root] == exe, "Settings persist roots and choices using atomic replacement");
            } finally { Directory.Delete(root, true); }
        }
        static void UiFixture()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "qa-fixtures"); root = Path.GetFullPath(root); Directory.CreateDirectory(root); StateStore.DataDir = Path.Combine(root, "state");
            var result = new ScanResult(); string[] names = { "Cyberpunk 2077", "Hogwarts Legacy", "Unlisted FG adventure", "Grand Theft Auto V Enhanced", "An ordinary DLSS game", "Multiple executables" };
            for (int i = 0; i < names.Length; i++) {
                string folder = Path.Combine(root, names[i]); Directory.CreateDirectory(folder); string exe = Path.Combine(folder, "game.exe"); File.Copy(Application.ExecutablePath, exe, true);
                var game = new Game { Name = names[i], Root = folder, Source = "Test fixture", SelectedExe = exe }; game.Executables.Add(exe);
                if (i < 4 || i == 5) { File.WriteAllText(Path.Combine(folder, "sl.dlss_g.dll"), "FG fixture"); File.WriteAllText(Path.Combine(folder, "nvngx_dlssg.dll"), "FG fixture"); game.Files.Add("sl.dlss_g.dll"); game.Files.Add("nvngx_dlssg.dll"); } else game.Files.Add("nvngx_dlss.dll");
                if (i == 5) { string second = Path.Combine(folder, "second.exe"); File.Copy(exe, second, true); game.Executables.Add(second); game.SelectedExe = null; }
                GameEvaluation.Refresh(game); result.Games.Add(game);
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm(result));
        }
    }
}




