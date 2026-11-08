using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
namespace MfgLauncher
{
    public static class LaunchService
    {
        public static ProcessStartInfo StartInfo(Game game)
        {
            if (!String.IsNullOrEmpty(game.SteamId)) {
                if (!Regex.IsMatch(game.SteamId, @"\A[0-9]{1,12}\z")) throw new InvalidDataException("Invalid Steam app ID.");
                return new ProcessStartInfo("steam://rungameid/" + game.SteamId) { UseShellExecute = true };
            }
            string exe = game.SelectedExe;
            if (String.IsNullOrEmpty(exe)) throw new InvalidOperationException("Choose the game executable in Details first.");
            if (!Util.IsWithin(game.Root, exe) || !String.Equals(Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(exe)) throw new InvalidDataException("The selected executable is missing or outside the game folder. Rescan the library.");
            Util.NoReparse(exe);
            return new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) };
        }
        public static void Launch(Game game) { Process.Start(StartInfo(game)); }
        public static void OpenFolder(Game game) { if (!Directory.Exists(game.Root)) throw new DirectoryNotFoundException(game.Root); Process.Start(new ProcessStartInfo(game.Root) { UseShellExecute = true }); }
        public static void OpenDocs() { Process.Start(new ProcessStartInfo("https://github.com/dashdogy/MFG-Unlocker/blob/main/docs/compatibility.md") { UseShellExecute = true }); }
    }
    public static class GameEvaluation
    {
        public static void Refresh(Game game)
        {
            game.Installed = null; Compatibility.Evaluate(game);
            try {
                var receipt = InstallService.ReadReceipt(game);
                if (receipt != null) {
                    game.Installed = receipt;
                    game.Evidence.Add("Owned installation: " + receipt.Version + " as " + receipt.ProxyName + ". SHA-256 matches.");
                    if (game.Status != GameStatus.Blocked) { if (String.IsNullOrEmpty(game.SelectedExe) || !String.Equals(Util.Canonical(game.SelectedExe), Util.Canonical(receipt.ExePath), StringComparison.OrdinalIgnoreCase)) { game.Status = GameStatus.Review; game.Evidence.Add("The owned mod belongs to a different executable. Select " + receipt.ExePath + " to launch that installation, or uninstall it before changing runtimes."); } else game.Status = GameStatus.Installed; }
                }
            } catch (Exception ex) { game.Evidence.Add("Installation needs review: " + ex.Message); if (game.Status != GameStatus.Blocked) game.Status = GameStatus.Review; }
        }
    }
}

