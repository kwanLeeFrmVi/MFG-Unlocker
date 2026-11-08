using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
namespace MfgLauncher
{
    public static class SelfUpdaterSmoke
    {
        public static void Run(Action<bool,string> assert)
        {
            string probe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MFGLauncher.UpdateProbe.exe"); assert(File.Exists(probe), "Self-update probe is built beside the test runner"); if (!File.Exists(probe)) return;
            string root = Path.Combine(Path.GetTempPath(), "MfgSelfUpdaterSmoke-" + Guid.NewGuid().ToString("N")); string state = Path.Combine(root, "state"); string updateRoot = Path.Combine(state, "launcher-updates"); string stageDir = Path.Combine(updateRoot, "staged"); Directory.CreateDirectory(stageDir);
            string helper = Path.Combine(updateRoot, "update-helper-probe.exe"), target = Path.Combine(root, "MFGLauncher.exe"), staged = Path.Combine(stageDir, "MFGLauncher.exe"); Process worker = null;
            try {
                File.Copy(probe, helper, false); File.Copy(probe, target, false); File.Copy(probe, staged, false); using (var stream = new FileStream(staged, FileMode.Append, FileAccess.Write, FileShare.None)) stream.WriteByte(0x7f);
                string oldHash = Util.HashFile(target), newHash = Util.HashFile(staged); assert(oldHash != newHash, "Smoke update has distinct old and staged hashes");
                var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root, Arguments = "--apply-update " + Quote(target) + " " + Quote(staged) + " " + Quote(newHash) + " 2147483647 " + Quote(oldHash) }; start.EnvironmentVariables["MFG_UPDATER_TEST_DATA"] = state; worker = Process.Start(start);
                bool completed = worker != null && worker.WaitForExit(10000); if (!completed && worker != null) { try { worker.Kill(); worker.WaitForExit(1000); } catch { } } assert(completed, "Self-update helper completes within ten seconds"); if (!completed) return;
                assert(worker.ExitCode == 0, "Self-update helper exits successfully"); assert(Util.HashFile(target) == newHash, "Helper atomically replaces target with verified staged executable");
                string[] backups = Directory.GetFiles(root, "MFGLauncher.exe.previous-*.bak"); assert(backups.Length == 1 && Util.HashFile(backups[0]) == oldHash, "Self-update preserves the previous executable as a rollback backup");
                string marker = Path.Combine(root, "updated.txt"); var wait = Stopwatch.StartNew(); while (!File.Exists(marker) && wait.ElapsedMilliseconds < 3000) Thread.Sleep(50); assert(File.Exists(marker) && File.ReadAllText(marker) == "updated", "Helper restarts the updated executable in its own directory");
            } finally {
                if (worker != null) { try { if (!worker.HasExited) { worker.Kill(); worker.WaitForExit(1000); } } catch { } worker.Dispose(); }
                if (!Util.IsWithin(Path.GetTempPath(), root) || !new DirectoryInfo(root).Name.StartsWith("MfgSelfUpdaterSmoke-", StringComparison.Ordinal)) throw new IOException("Unsafe smoke fixture cleanup path.");
                for (int attempt = 0; Directory.Exists(root); attempt++) { try { Directory.Delete(root, true); } catch (IOException) { if (attempt >= 19) throw; Thread.Sleep(50); } catch (UnauthorizedAccessException) { if (attempt >= 19) throw; Thread.Sleep(50); } }
            }
        }
        static string Quote(string value) { if (value.IndexOf('"') >= 0) throw new InvalidDataException("Invalid smoke fixture argument."); return "\"" + value + "\""; }
    }
}
