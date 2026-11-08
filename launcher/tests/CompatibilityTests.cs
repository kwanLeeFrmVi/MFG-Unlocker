using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MfgLauncher
{
    public static class CompatibilityTests
    {
        public static void Run(Action<bool,string> assert)
        {
            string root = Path.Combine(Path.GetTempPath(), "MfgCompatibilityTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {
                Game known = Fixture(root, "Cyberpunk 2077", "cyberpunk");
                Fg(known); Compatibility.Evaluate(known);
                assert(known.Status == GameStatus.Reported, "Known title and full FG evidence is reported compatible");
                assert(known.SuggestedProxies.SequenceEqual(new[] { "xinput9_1_0.dll", "dxgi.dll", "d3d12.dll" }), "Cyberpunk uses its documented proxies, never version/winmm");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(known.SelectedExe), "xinput9_1_0.dll"), "other mod");
                Compatibility.Evaluate(known);
                assert(known.Status == GameStatus.Reported && known.SuggestedProxies.First() == "dxgi.dll", "Occupied primary proxy selects documented available alternative");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(known.SelectedExe), "dxgi.dll"), "other mod");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(known.SelectedExe), "d3d12.dll"), "other mod");
                Compatibility.Evaluate(known);
                assert(known.Status == GameStatus.Review && known.SuggestedProxies.Count == 0, "All proxy conflicts require review without overwriting");
                assert(File.ReadAllText(Path.Combine(Path.GetDirectoryName(known.SelectedExe), "dxgi.dll")) == "other mod", "Compatibility evaluation preserves unrelated mod files");

                Compatibility.EvaluateOwnedUpdate(known, "xinput9_1_0.dll"); assert(known.Status == GameStatus.Reported && known.SuggestedProxies.Contains("xinput9_1_0.dll"), "Owned proxy remains available for update"); known.Files.Add("EasyAntiCheat/EasyAntiCheat_EOS_Setup.exe"); Compatibility.EvaluateOwnedUpdate(known, "xinput9_1_0.dll"); assert(known.Status == GameStatus.Blocked, "Owned install cannot bypass anti-cheat update blocking"); known.Files.Remove("EasyAntiCheat/EasyAntiCheat_EOS_Setup.exe");
                Game sr = Fixture(root, "Unknown SR game", "sr"); sr.Files.Add("nvngx_dlss.dll"); Compatibility.Evaluate(sr);
                assert(sr.Status == GameStatus.Unsupported && sr.SuggestedProxies.Count == 0, "Super Resolution alone is not compatibility evidence");
                Game partial = Fixture(root, "Unknown partial game", "partial"); partial.Files.Add("sl.dlss_g.dll"); Compatibility.Evaluate(partial);
                assert(partial.Status == GameStatus.Review, "Partial FG integration requires review");
                Game unknown = Fixture(root, "Unknown FG game", "unknown"); Fg(unknown); Compatibility.Evaluate(unknown);
                assert(unknown.Status == GameStatus.Candidate && unknown.SuggestedProxies.Contains("version.dll"), "Unknown full FG integration remains an unverified candidate");
                Game noFg = Fixture(root, "Hogwarts Legacy", "nofg"); Compatibility.Evaluate(noFg);
                assert(noFg.Status == GameStatus.Review, "Known report alone cannot establish local FG compatibility");

                foreach (string blockedTitle in new[] { "Grand Theft Auto V Enhanced", "Battlefield 6", "Forza Horizon 6", "Gears of War: E-Day", "Zenless Zone Zero", "AION 2", "The First Descendant", "Where Winds Meet", "Wuthering Waves" }) {
                    Game blocked = Fixture(root, blockedTitle, "blocked-" + Guid.NewGuid().ToString("N")); Fg(blocked); Compatibility.Evaluate(blocked);
                    assert(blocked.Status == GameStatus.Blocked && blocked.SuggestedProxies.Count == 0, "Known anti-cheat title blocked without marker: " + blockedTitle);
                }
                foreach (string marker in new[] { "EasyAntiCheat/EasyAntiCheat_EOS_Setup.exe", "BattlEye/BEClient_x64.dll", "AntiCheatExpert/ACE-Base.sys", "start_protected_game.exe", "vgk.sys" }) {
                    Game blocked = Fixture(root, "Cyberpunk 2077", "marker-" + Guid.NewGuid().ToString("N")); Fg(blocked); blocked.Files.Add(marker); Compatibility.Evaluate(blocked);
                    assert(blocked.Status == GameStatus.Blocked, "Local anti-cheat evidence takes precedence: " + marker);
                }
                Game opti = Fixture(root, "Red Dead Redemption 2", "opti"); Fg(opti); Compatibility.Evaluate(opti);
                assert(opti.Status == GameStatus.Blocked && opti.BlockReason.Contains("OptiScaler"), "OptiScaler-only report does not authorize normal proxy installation");
                Game substring = Fixture(root, "The Witcher 3 Soundtrack", "unrelated"); Fg(substring); Compatibility.Evaluate(substring);
                assert(substring.Status == GameStatus.Candidate && substring.KnownRule == null, "Unrelated title substring cannot match upstream rule");
                Game ambiguous = Fixture(root, "Hogwarts Legacy", "ambiguous"); Fg(ambiguous); ambiguous.SelectedExe = null; Compatibility.Evaluate(ambiguous);
                assert(ambiguous.Status == GameStatus.Review, "Unresolved runtime executable prevents installation");
                ambiguous.SelectedExe = ambiguous.Executables[0]; Compatibility.Evaluate(ambiguous);
                assert(ambiguous.Status == GameStatus.Reported, "Explicit valid executable selection resolves ambiguity");
                ambiguous.ScanIncomplete = true; Compatibility.Evaluate(ambiguous);
                assert(ambiguous.Status == GameStatus.Review, "Incomplete scan prevents installation");
                Game invalid = Fixture(root, "Hogwarts Legacy", "invalid"); Fg(invalid); File.WriteAllText(invalid.SelectedExe, "not a PE"); Compatibility.Evaluate(invalid);
                assert(invalid.Status == GameStatus.Review, "Invalid or non-x64 runtime executable requires review");
                Game outside = Fixture(root, "Hogwarts Legacy", "outside"); Fg(outside); outside.SelectedExe = sr.SelectedExe; outside.Executables.Add(sr.SelectedExe); Compatibility.Evaluate(outside);
                assert(outside.Status == GameStatus.Review, "Runtime outside game folder cannot qualify");
                Game flight = Fixture(root, "Microsoft Flight Simulator 2020", "flight"); Fg(flight); flight.SteamId = "1250410"; Compatibility.Evaluate(flight);
                assert(flight.SuggestedProxies.First() == "version.dll", "Flight Simulator Steam route uses version.dll");
                flight.SteamId = null; Compatibility.Evaluate(flight);
                assert(flight.Status == GameStatus.Review && flight.SuggestedProxies.Count == 0, "Manually added Flight Simulator folder cannot guess Store edition");
                flight.Source = "Microsoft Store"; Compatibility.Evaluate(flight);
                assert(flight.SuggestedProxies.First() == "dinput8.dll", "Flight Simulator Store route uses dinput8.dll");
                assert(!Compatibility.IsSupportedProxy("binkw64.dll") && !Compatibility.IsSupportedProxy("bink2w64.dll") && !Compatibility.IsSupportedProxy("../version.dll") && Compatibility.IsSupportedProxy("WINMM.DLL"), "Proxy allowlist excludes Bink routes and path traversal");
            } finally { Directory.Delete(root, true); }
        }
        static Game Fixture(string root, string name, string folder)
        {
            string dir = Path.Combine(root, folder); Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "game.exe");
            using (var stream = File.Create(exe)) using (var writer = new BinaryWriter(stream)) {
                writer.Write(new byte[256]); stream.Position = 0; writer.Write((ushort)0x5a4d); stream.Position = 0x3c; writer.Write(0x80); stream.Position = 0x80; writer.Write((uint)0x4550); writer.Write((ushort)0x8664); stream.Position = 0x80 + 22; writer.Write((ushort)0x0002); stream.Position = 0x80 + 24; writer.Write((ushort)0x20b);
            }
            var game = new Game { Root = dir, Name = name, Source = "Folder", SelectedExe = exe };
            game.Executables.Add(exe); game.Files.Add("game.exe"); return game;
        }
        static void Fg(Game game) { game.Files.Add("plugins/sl.dlss_g.dll"); game.Files.Add("nvngx_dlssg.dll"); }
    }
}


