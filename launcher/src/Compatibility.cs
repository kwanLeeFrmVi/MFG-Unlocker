using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MfgLauncher
{
    public static class Compatibility
    {
        public const string RulesSource = "https://github.com/dashdogy/MFG-Unlocker/blob/main/docs/compatibility.md";
        public const string RulesDate = "2026-11-08";
        static readonly HashSet<string> Supported = new HashSet<string>(new[] {
            "version.dll", "dinput8.dll", "winmm.dll", "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll", "dxgi.dll", "dsound.dll", "wininet.dll", "winhttp.dll",
            "xinput1_1.dll", "xinput1_2.dll", "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll", "xinputuap.dll"
        }, StringComparer.OrdinalIgnoreCase);
        sealed class Rule
        {
            public string Name;
            public string[] Proxies;
            public Rule(string name, params string[] proxies) { Name = name; Proxies = proxies; }
        }
        static readonly Dictionary<string, Rule> Rules = BuildRules();
        static readonly HashSet<string> AntiCheat = BuildNames(new[] {
            "Battlefield 6", "Battlefield 6 RedSec", "Battlefield RedSec", "RedSec", "BF6", "Forza Horizon 6", "Gears of War E-Day", "Gears E-Day",
            "Grand Theft Auto V Enhanced", "GTA V Enhanced", "GTAV Enhanced", "GTA5 Enhanced", "Zenless Zone Zero", "ZenlessZoneZero",
            "The First Descendant", "First Descendant", "M1-Win64-Shipping", "AION 2", "AION2", "Wuthering Waves", "WutheringWaves", "Client-Win64-Shipping-WutheringWaves",
            "Where Winds Meet", "WhereWindsMeet", "Call of Duty", "Call of Duty HQ", "cod", "Call of Duty Modern Warfare 4", "Call of Duty Modern Warfare IV",
            "Fortnite", "FortniteClient-Win64-Shipping", "Apex Legends", "r5apex", "Destiny 2", "Destiny2", "PUBG Battlegrounds", "PUBG", "TslGame", "Valorant", "VALORANT-Win64-Shipping"
        });
        static readonly HashSet<string> OptiOnly = BuildNames(new[] {
            "007 First Light", "Arknights Endfield", "Clair Obscur Expedition 33", "Expedition 33", "Forza Motorsport", "GTA The Trilogy", "Grand Theft Auto The Trilogy",
            "Grand Theft Auto San Andreas The Definitive Edition", "Grand Theft Auto Vice City The Definitive Edition", "Hitman World of Assassination", "Hitman 3", "Marvel's Spider-Man Remastered", "Spider-Man Remastered", "Neverness to Everness",
            "No Man's Sky", "No Rest for the Wicked", "Red Dead Redemption 2", "RDR2", "Tainted Grail The Fall of Avalon", "The Ascent", "X4 Foundations"
        });

        static HashSet<string> BuildNames(string[] names) { return new HashSet<string>(names.Select(Normalize), StringComparer.Ordinal); }
        static Dictionary<string, Rule> BuildRules()
        {
            var result = new Dictionary<string, Rule>(StringComparer.Ordinal);
            Add(result, new Rule("Ace Combat 8", "version.dll"), "Ace Combat 8");
            Add(result, new Rule("Alan Wake 2", "winmm.dll", "dinput8.dll"), "Alan Wake 2", "AlanWake2");
            Add(result, new Rule("Assassin's Creed IV Black Flag Resynced", "version.dll", "dinput8.dll"), "Assassin's Creed IV Black Flag Resynced", "Assassin's Creed 4 Black Flag Resynced");
            Add(result, new Rule("Black Myth: Wukong", "version.dll", "dsound.dll", "dxgi.dll"), "Black Myth Wukong", "b1", "b1-Win64-Shipping");
            Add(result, new Rule("Control Resonant", "version.dll", "winmm.dll"), "Control Resonant");
            Add(result, new Rule("Crimson Desert", "d3d12.dll"), "Crimson Desert", "CrimsonDesert");
            Add(result, new Rule("Cyberpunk 2077", "xinput9_1_0.dll", "dxgi.dll", "d3d12.dll"), "Cyberpunk 2077", "Cyberpunk2077");
            Add(result, new Rule("DOOM: The Dark Ages (Vulkan)", "winmm.dll"), "DOOM The Dark Ages", "DOOM The Dark Ages Vulkan", "DOOMTheDarkAges");
            Add(result, new Rule("Dragon's Dogma 2", "winmm.dll"), "Dragon's Dogma 2", "DD2");
            Add(result, new Rule("Dying Light 2", "winmm.dll", "version.dll"), "Dying Light 2", "Dying Light 2 Stay Human", "DyingLightGame_x64_rwdi");
            Add(result, new Rule("Dying Light: The Beast", "winmm.dll"), "Dying Light The Beast", "DyingLightTheBeast");
            Add(result, new Rule("Enshrouded (Vulkan)", "winmm.dll"), "Enshrouded", "Enshrouded Vulkan");
            Add(result, new Rule("Farming Simulator 25", "dinput8.dll", "version.dll"), "Farming Simulator 25", "FarmingSimulator2025Game");
            Add(result, new Rule("Final Fantasy VII Rebirth", "version.dll", "dxgi.dll"), "Final Fantasy VII Rebirth", "Final Fantasy 7 Rebirth", "ff7rebirth");
            Add(result, new Rule("Gothic 1 Remake", "version.dll", "winmm.dll"), "Gothic 1 Remake", "Gothic Remake");
            Add(result, new Rule("Hogwarts Legacy", "version.dll"), "Hogwarts Legacy", "HogwartsLegacy", "HogwartsLegacy-Win64-Shipping");
            Add(result, new Rule("Indiana Jones and the Great Circle (Vulkan)", "dinput8.dll"), "Indiana Jones and the Great Circle", "Indiana Jones and the Great Circle Vulkan", "TheGreatCircle");
            Add(result, new Rule("Marvel's Spider-Man 2", "version.dll"), "Marvel's Spider-Man 2", "Spider-Man 2", "Spider-Man2");
            Add(result, new Rule("Microsoft Flight Simulator 2020", "version.dll"), "Microsoft Flight Simulator", "Microsoft Flight Simulator 2020", "FlightSimulator");
            Add(result, new Rule("Microsoft Flight Simulator 2024", "version.dll", "dinput8.dll", "d3d12.dll"), "Microsoft Flight Simulator 2024", "FlightSimulator2024");
            Add(result, new Rule("Monster Hunter Wilds", "version.dll", "winmm.dll"), "Monster Hunter Wilds", "MonsterHunterWilds");
            Add(result, new Rule("Onimusha: Way of the Sword", "winmm.dll"), "Onimusha Way of the Sword");
            Add(result, new Rule("Phantom Blade Zero", "version.dll", "dxgi.dll"), "Phantom Blade Zero", "PhantomBladeZero");
            Add(result, new Rule("Planet Zoo 2", "winmm.dll"), "Planet Zoo 2");
            Add(result, new Rule("Pragmata", "version.dll"), "Pragmata");
            Add(result, new Rule("Resident Evil Requiem", "version.dll", "dxgi.dll"), "Resident Evil Requiem", "RERequiem", "re9");
            Add(result, new Rule("S.T.A.L.K.E.R. 2", "version.dll", "d3d12.dll"), "S.T.A.L.K.E.R. 2", "S.T.A.L.K.E.R. 2 Heart of Chornobyl", "Stalker2", "Stalker2-Win64-Shipping");
            Add(result, new Rule("Silent Hill: Townfall", "version.dll"), "Silent Hill Townfall");
            Add(result, new Rule("Star Wars: Galactic Racer", "version.dll", "dxgi.dll"), "Star Wars Galactic Racer");
            Add(result, new Rule("Subnautica 2", "version.dll"), "Subnautica 2");
            Add(result, new Rule("The Blood of Dawnwalker", "version.dll"), "The Blood of Dawnwalker", "Blood of Dawnwalker");
            Add(result, new Rule("The Last of Us Part II", "dinput8.dll"), "The Last of Us Part II", "The Last of Us Part II Remastered", "The Last of Us Part 2", "tlou-ii");
            Add(result, new Rule("The Witcher 3", "winmm.dll", "version.dll", "d3d11.dll"), "The Witcher 3", "The Witcher 3 Wild Hunt", "Witcher3");
            Add(result, new Rule("AION 2", "d3d12.dll", "dxgi.dll"), "AION 2", "AION2");
            Add(result, new Rule("Call of Duty: Modern Warfare 4", "dxgi.dll"), "Call of Duty Modern Warfare 4", "Call of Duty Modern Warfare IV");
            Add(result, new Rule("The First Descendant", "dxgi.dll"), "The First Descendant", "First Descendant", "M1-Win64-Shipping");
            Add(result, new Rule("Where Winds Meet", "winmm.dll"), "Where Winds Meet", "WhereWindsMeet");
            Add(result, new Rule("Wuthering Waves", "version.dll", "dxgi.dll"), "Wuthering Waves", "WutheringWaves", "Client-Win64-Shipping-WutheringWaves");
            return result;
        }
        static void Add(Dictionary<string, Rule> rules, Rule rule, params string[] aliases) { foreach (string alias in aliases) rules[Normalize(alias)] = rule; }
        static string Normalize(string text)
        {
            var normalized = new StringBuilder();
            foreach (char c in (text ?? "").Normalize(NormalizationForm.FormD))
                if (Char.IsLetterOrDigit(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) normalized.Append(Char.ToLowerInvariant(c));
            return normalized.ToString();
        }
        static IEnumerable<string> Identity(Game game)
        {
            yield return Normalize(game.Name);
            yield return Normalize(Path.GetFileName((game.Root ?? "").TrimEnd('\\', '/')));
            foreach (string exe in game.Executables ?? new List<string>()) yield return Normalize(Path.GetFileNameWithoutExtension(exe));
        }
        public static bool IsSupportedProxy(string name) { return name != null && Supported.Contains(name); }
        public static string StatusText(GameStatus status)
        {
            switch (status) {
                case GameStatus.Reported: return "Reported compatible";
                case GameStatus.Candidate: return "Candidate";
                case GameStatus.Blocked: return "Blocked";
                case GameStatus.Review: return "Needs review";
                case GameStatus.Installed: return "Installed";
                default: return "No FG evidence";
            }
        }
        public static void Evaluate(Game game) { EvaluateCore(game, null, false); }
        public static void EvaluateForInstall(Game game, bool antiCheatAcknowledged) { EvaluateCore(game, null, antiCheatAcknowledged); }
        public static void EvaluateOwnedUpdate(Game game, string ownedProxy, bool antiCheatAcknowledged = false)
        {
            if (!IsSupportedProxy(ownedProxy)) throw new InvalidDataException("Unsupported owned proxy."); EvaluateCore(game, ownedProxy, antiCheatAcknowledged);
            if ((game.Status == GameStatus.Reported || game.Status == GameStatus.Candidate || game.CanOverrideAntiCheat) && !game.SuggestedProxies.Contains(ownedProxy, StringComparer.OrdinalIgnoreCase)) { game.CanOverrideAntiCheat = false; game.Status = game.HasAntiCheat && !antiCheatAcknowledged ? GameStatus.Blocked : GameStatus.Review; game.Evidence.Add("The owned proxy is no longer a supported route for this game. Review before updating."); }
        }
        static void EvaluateCore(Game game, string ownedProxy, bool antiCheatAcknowledged)
        {
            if (game == null) throw new ArgumentNullException("game");
            game.Evidence = new List<string>(); game.SuggestedProxies = new List<string>(); game.KnownRule = null; game.BlockReason = null; game.HasAntiCheat = false; game.CanOverrideAntiCheat = false;
            game.Status = GameStatus.Unsupported;
            var identities = Identity(game).Where(s => s.Length > 0).ToList();
            string blockedName = identities.FirstOrDefault(AntiCheat.Contains);
            string marker = (game.Files ?? new List<string>()).FirstOrDefault(IsAntiCheatFile);
            game.HasAntiCheat = blockedName != null || marker != null;
            string antiCheatReason = game.HasAntiCheat ? (marker == null ? "This title is known to use anti-cheat." : "Anti-cheat component found: " + marker) : null;
            if (game.HasAntiCheat) { game.Evidence.Add(antiCheatReason); game.Evidence.Add("MFG Unlocker is unsupported with anti-cheat. A manual installation requires explicit risk acknowledgment; the launcher does not disable or bypass anti-cheat."); }
            Rule rule = null;
            foreach (string identity in identities) { if (Rules.TryGetValue(identity, out rule)) break; }
            if (rule == null && identities.Any(OptiOnly.Contains)) {
                game.Status = GameStatus.Blocked; game.BlockReason = "Upstream reports this title through OptiScaler only. The OptiScaler plugin route is not supported by this launcher.";
                game.Evidence.Add(game.BlockReason); return;
            }
            if (rule != null) {
                game.KnownRule = rule.Name;
                game.Evidence.Add("Upstream game report: " + rule.Name + " (rules checked " + RulesDate + ").");
                game.SuggestedProxies.AddRange(rule.Proxies);
                if (rule.Name == "Microsoft Flight Simulator 2020" && String.IsNullOrEmpty(game.SteamId) && IsStoreEdition(game)) {
                    game.SuggestedProxies.Clear(); game.SuggestedProxies.Add("dinput8.dll");
                    game.Evidence.Add("Microsoft Store route uses dinput8.dll; Steam uses version.dll.");
                }
            }
            var names = new HashSet<string>((game.Files ?? new List<string>()).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
            bool streamlineFg = names.Contains("sl.dlss_g.dll"), dlssFg = names.Contains("nvngx_dlssg.dll");
            if (streamlineFg) game.Evidence.Add("Found sl.dlss_g.dll: Streamline DLSS Frame Generation plugin.");
            if (dlssFg) game.Evidence.Add("Found nvngx_dlssg.dll: DLSS Frame Generation runtime.");
            if (names.Contains("nvngx_dlss.dll")) game.Evidence.Add("Found DLSS Super Resolution; this alone does not establish Frame Generation support.");
            if (!streamlineFg || !dlssFg) {
                game.Status = rule != null || streamlineFg || dlssFg ? GameStatus.Review : GameStatus.Unsupported;
                game.Evidence.Add("Normal DLSS installation requires existing Streamline Frame Generation. Both sl.dlss_g.dll and nvngx_dlssg.dll were not found; installation is disabled.");
            } else {
                game.Status = rule == null ? GameStatus.Candidate : GameStatus.Reported;
                if (rule == null) { game.SuggestedProxies.Add("version.dll"); game.SuggestedProxies.Add("winmm.dll"); game.Evidence.Add("Frame Generation files found, but this title has no matching upstream report. A proxy filename requires your explicit review."); }
                game.Evidence.Add("File detection cannot confirm that Frame Generation works in this game build or on your GPU.");
            }
            if (rule != null && rule.Name == "Microsoft Flight Simulator 2020" && String.IsNullOrEmpty(game.SteamId) && !IsStoreEdition(game)) { game.Status = GameStatus.Review; game.SuggestedProxies.Clear(); game.Evidence.Add("Flight Simulator 2020 has different Steam and Store proxy routes. Discover the Steam installation or add its XboxGames/Store folder to establish the edition."); }
            if (String.IsNullOrEmpty(game.SelectedExe)) {
                game.Status = GameStatus.Review;
                game.Evidence.Add(game.Executables != null && game.Executables.Count > 1 ? "Choose the actual runtime executable in Details; several executable candidates were found." : "No actual runtime executable has been identified.");
            } else {
                try {
                    string actual = Util.Canonical(game.SelectedExe);
                    if (!Util.IsWithin(game.Root, actual) || !File.Exists(actual) || !Util.IsX64Pe(actual, false) || (game.Executables != null && !game.Executables.Any(p => String.Equals(Util.Canonical(p), actual, StringComparison.OrdinalIgnoreCase)))) {
                        game.Status = GameStatus.Review; game.Evidence.Add("The selected executable must be an existing scanned x64 game executable inside this game folder.");
                    } else {
                        game.Evidence.Add("Runtime executable: " + actual.Substring(Util.Canonical(game.Root).Length + 1));
                        if (rule != null && rule.Name == "The Witcher 3" && !String.Equals(actual, Path.Combine(Util.Canonical(game.Root), @"bin\x64_dx12\witcher3.exe"), StringComparison.OrdinalIgnoreCase) && !(Util.Canonical(game.Root).EndsWith(@"\bin\x64_dx12", StringComparison.OrdinalIgnoreCase) && String.Equals(Path.GetFileName(actual), "witcher3.exe", StringComparison.OrdinalIgnoreCase) && String.Equals(Path.GetDirectoryName(actual), Util.Canonical(game.Root), StringComparison.OrdinalIgnoreCase))) {
                            game.Status = GameStatus.Review; game.SuggestedProxies.Clear();
                            game.Evidence.Add("The Witcher 3 requires its DirectX 12 runtime. Select bin\\x64_dx12\\witcher3.exe in the main game folder; DirectX 11 and backup executables are not supported installation targets.");
                        }
                        var available = game.SuggestedProxies.Where(p => String.Equals(p, ownedProxy, StringComparison.OrdinalIgnoreCase) || (!File.Exists(Path.Combine(Path.GetDirectoryName(actual), p)) && !Directory.Exists(Path.Combine(Path.GetDirectoryName(actual), p)))).ToList();
                        foreach (string occupied in game.SuggestedProxies.Except(available, StringComparer.OrdinalIgnoreCase)) game.Evidence.Add("Occupied proxy preserved: " + occupied);
                        game.SuggestedProxies = available;
                        if ((game.Status == GameStatus.Reported || game.Status == GameStatus.Candidate) && available.Count == 0) { game.Status = GameStatus.Review; game.Evidence.Add("Every supported proxy route for this game is occupied. Existing files will never be overwritten."); }
                    }
                } catch (Exception ex) {
                    if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)) throw; game.Status = GameStatus.Review; game.Evidence.Add("Executable could not be verified: " + ex.Message);
                }
            }
            if (game.ScanIncomplete) { game.Status = GameStatus.Review; game.Evidence.Add("The scan was incomplete. Resolve scan errors and rescan before installing."); }
            if (game.HasAntiCheat) {
                game.CanOverrideAntiCheat = game.Status == GameStatus.Reported || game.Status == GameStatus.Candidate;
                if (!antiCheatAcknowledged) { game.Status = GameStatus.Blocked; game.BlockReason = antiCheatReason + " Read the warning and acknowledge the risk for this action before installing."; }
                else if (game.CanOverrideAntiCheat) { game.Status = GameStatus.Candidate; game.Evidence.Add("Anti-cheat risk acknowledged for this action only. This installation remains unsupported and unverified."); }
            }
        }
        static bool IsStoreEdition(Game game)
        {
            string source = game.Source ?? "";
            return source.IndexOf("Store", StringComparison.OrdinalIgnoreCase) >= 0 || source.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) >= 0 || (game.Root ?? "").Replace('\\', '/').Split('/').Any(p => String.Equals(p, "XboxGames", StringComparison.OrdinalIgnoreCase));
        }
        static bool IsAntiCheatFile(string path)
        {
            string normalized = (path ?? "").Replace('\\', '/').ToLowerInvariant();
            string name = Path.GetFileName(normalized);
            return normalized.Contains("easyanticheat") || normalized.Contains("battleye") || normalized.Contains("anticheatexpert") || normalized.Contains("eaanticheat") || normalized.Contains("xigncode") || normalized.Contains("gameguard") || normalized.Contains("nprotect") || normalized.Contains("equ8") || normalized.Contains("/vanguard/") ||
                name == "start_protected_game.exe" || name == "beclient_x64.dll" || name == "beclient.dll" || name == "beservice.exe" || name == "vgc.exe" || name == "vgk.sys" || name == "ace-base.sys" || name == "anticheat.exe";
        }
    }
}







