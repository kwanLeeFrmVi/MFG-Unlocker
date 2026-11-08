using System;
using System.IO;
namespace MfgLauncher
{
    public static class UpdateProbe
    {
        [STAThread] public static void Main(string[] args)
        {
            string data = Environment.GetEnvironmentVariable("MFG_UPDATER_TEST_DATA");
            if (String.IsNullOrWhiteSpace(data)) { Environment.ExitCode = 2; return; }
            StateStore.DataDir = Path.GetFullPath(data); if (SelfUpdater.ApplyIfRequested(args)) return;
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "updated.txt"), "updated");
        }
    }
}
