using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;
namespace MfgLauncher
{
    public static class Vdf
    {
        public static Dictionary<string, object> Parse(string text)
        {
            if (text == null || text.Length > 4 * 1024 * 1024) throw new InvalidDataException("VDF input is missing or exceeds 4 MB.");
            int position = 0; return ReadObject(text, ref position, false, 0);
        }
        static Dictionary<string, object> ReadObject(string text, ref int position, bool nested, int depth)
        {
            if (depth > 32) throw new InvalidDataException("VDF nesting is too deep.");
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (true) {
                string key = Token(text, ref position);
                if (key == null) { if (nested) throw new InvalidDataException("Unclosed VDF object."); return values; }
                if (key == "}") { if (!nested) throw new InvalidDataException("Unexpected VDF closing brace."); return values; }
                if (key == "{") throw new InvalidDataException("Expected a VDF key.");
                string value = Token(text, ref position);
                if (value == null || value == "}") throw new InvalidDataException("Missing VDF value for " + key);
                values[key] = value == "{" ? (object)ReadObject(text, ref position, true, depth + 1) : value;
            }
        }
        static string Token(string text, ref int position)
        {
            while (position < text.Length) {
                if (Char.IsWhiteSpace(text[position]) || text[position] == '\ufeff') { position++; continue; }
                if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/') { while (position < text.Length && text[position] != '\n') position++; continue; }
                break;
            }
            if (position == text.Length) return null;
            char first = text[position++];
            if (first == '{' || first == '}') return first.ToString();
            if (first == '"') {
                var value = new StringBuilder();
                while (position < text.Length) {
                    char c = text[position++];
                    if (c == '"') return value.ToString();
                    if (c == '\\' && position < text.Length) { char next = text[position]; if (next == '\\' || next == '"') { value.Append(next); position++; continue; } }
                    value.Append(c);
                }
                throw new InvalidDataException("Unclosed VDF string.");
            }
            int start = position - 1;
            while (position < text.Length && !Char.IsWhiteSpace(text[position]) && text[position] != '{' && text[position] != '}') position++;
            return text.Substring(start, position - start);
        }
        public static string Text(Dictionary<string, object> value, string key) { object found; return value.TryGetValue(key, out found) ? found as string : null; }
        public static Dictionary<string, object> Object(Dictionary<string, object> value, string key) { object found; return value.TryGetValue(key, out found) ? found as Dictionary<string, object> : null; }
    }
    public static class Discovery
    {
        const int MaxFiles = 30000, MaxDepth = 12, MaxFolders = 6000;
        static readonly HashSet<string> IgnoredDirectories = new HashSet<string>(new[] { ".git", ".svn", "_CommonRedist", "shadercache", "DirectX", "DotNet", "vcredist", "__Installer" }, StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> ExcludedSteamApps = new HashSet<string>(new[] { "1812620", "993090", "228980" }, StringComparer.Ordinal);
        public static List<string> ParseLibraryFolders(string text)
        {
            var parsed = Vdf.Parse(text); var folders = Vdf.Object(parsed, "libraryfolders") ?? parsed; var paths = new List<string>();
            foreach (var entry in folders) {
                uint index; if (!UInt32.TryParse(entry.Key, out index)) continue;
                var obj = entry.Value as Dictionary<string, object>; string path = obj == null ? entry.Value as string : Vdf.Text(obj, "path");
                if (!String.IsNullOrWhiteSpace(path) && System.IO.Path.IsPathRooted(path)) paths.Add(path);
            }
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static Game ParseManifest(string text, string library, string manifestPath)
        {
            var state = Vdf.Object(Vdf.Parse(text), "AppState"); if (state == null) throw new InvalidDataException("Missing AppState object.");
            string appid = Vdf.Text(state, "appid"), name = Vdf.Text(state, "name"), folder = Vdf.Text(state, "installdir"); uint number;
            if (!UInt32.TryParse(appid, out number) || number == 0) throw new InvalidDataException("Invalid Steam app ID.");
            if (!String.IsNullOrEmpty(manifestPath) && !String.Equals(System.IO.Path.GetFileName(manifestPath), "appmanifest_" + number + ".acf", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Manifest filename and app ID disagree.");
            if (String.IsNullOrWhiteSpace(folder) || System.IO.Path.IsPathRooted(folder) || folder.Split('\\', '/').Any(p => p == ".." || p == "." || p.Length == 0) || folder.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0 || folder.IndexOf(':') >= 0) throw new InvalidDataException("Invalid Steam install directory.");
            string common = System.IO.Path.Combine(Util.Canonical(library), "steamapps", "common"); string root = Util.Canonical(System.IO.Path.Combine(common, folder));
            if (!Util.IsWithin(common, root)) throw new InvalidDataException("Steam install directory escapes its library.");
            if (String.IsNullOrWhiteSpace(name)) name = folder;
            return new Game { Name = new string(name.Where(c => !Char.IsControl(c)).ToArray()), Root = root, SteamId = number.ToString(), Source = "Steam" };
        }
        public static ScanResult Discover(List<ScanRoot> roots, Action<string> progress, CancellationToken token)
        {
            var errors = new List<string>(); var seeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> add = p => { if (!String.IsNullOrWhiteSpace(p)) { try { seeds.Add(Util.Canonical(p)); } catch (Exception ex) { errors.Add("Steam path: " + ex.Message); } } };
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 }) foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine }) {
                try { using (var registry = RegistryKey.OpenBaseKey(hive, view)) using (var steam = registry.OpenSubKey(@"Software\Valve\Steam")) {
                    if (steam == null) continue; add(steam.GetValue("SteamPath") as string); add(steam.GetValue("InstallPath") as string);
                    var exe = steam.GetValue("SteamExe") as string; if (!String.IsNullOrEmpty(exe)) add(System.IO.Path.GetDirectoryName(exe));
                } } catch (Exception ex) { errors.Add("Steam registry: " + ex.Message); }
            }
            foreach (string programFiles in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }) if (!String.IsNullOrEmpty(programFiles)) add(System.IO.Path.Combine(programFiles, "Steam"));
            foreach (var drive in DriveInfo.GetDrives()) {
                token.ThrowIfCancellationRequested();
                try { if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue; foreach (string suffix in new[] { "", "Steam", "SteamLibrary", @"Games\Steam", @"Games\SteamLibrary", @"Program Files\Steam", @"Program Files (x86)\Steam" }) add(System.IO.Path.Combine(drive.RootDirectory.FullName, suffix)); }
                catch (Exception ex) { errors.Add("Drive " + drive.Name + ": " + ex.Message); }
            }
            var result = DiscoverAtLibraries(seeds, roots, progress, token); result.Errors.InsertRange(0, errors); return result;
        }
        public static ScanResult DiscoverAtLibraries(IEnumerable<string> libraries, IEnumerable<ScanRoot> roots, Action<string> progress, CancellationToken token)
        {
            var excludedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var result = new ScanResult(); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var games = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(libraries ?? Enumerable.Empty<string>());
            while (queue.Count > 0) {
                token.ThrowIfCancellationRequested(); string raw = queue.Dequeue(), library;
                try {
                    library = Util.Canonical(raw); if (!visited.Add(library)) continue; if (visited.Count > 256) throw new InvalidDataException("Too many Steam libraries in discovery metadata.");
                    string apps = System.IO.Path.Combine(library, "steamapps"); if (!Directory.Exists(apps)) continue; Util.NoReparse(apps); result.Libraries.Add(library);
                    if (progress != null) progress("Reading Steam library: " + library);
                    string vdf = System.IO.Path.Combine(apps, "libraryfolders.vdf");
                    if (File.Exists(vdf)) { try { foreach (string additional in ParseLibraryFolders(ReadSmall(vdf))) { if (!Directory.Exists(System.IO.Path.Combine(additional, "steamapps"))) result.Errors.Add("Steam library is missing or inaccessible: " + additional); if (!visited.Contains(additional)) queue.Enqueue(additional); } } catch (Exception ex) { result.Errors.Add(vdf + ": " + ex.Message); } }
                    foreach (string manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly)) {
                        token.ThrowIfCancellationRequested();
                        try {
                            var game = ParseManifest(ReadSmall(manifest), library, manifest);
                            if (ExcludedSteamApps.Contains(game.SteamId)) { excludedRoots.Add(game.Root); continue; }
                            if (!Directory.Exists(game.Root)) { result.Errors.Add(game.Name + ": install folder is missing (stale Steam manifest)."); continue; }
                            Util.NoReparse(game.Root); if (!games.ContainsKey(game.Root)) games.Add(game.Root, game);
                        } catch (OperationCanceledException) { throw; } catch (Exception ex) { result.Errors.Add(manifest + ": " + ex.Message); }
                    }
                } catch (OperationCanceledException) { throw; } catch (Exception ex) { result.Errors.Add(raw + ": " + ex.Message); }
            }
            foreach (var root in roots ?? Enumerable.Empty<ScanRoot>()) {
                token.ThrowIfCancellationRequested(); if (root == null) continue;
                try {
                    string path = Util.Canonical(root.Path); if (!Directory.Exists(path)) { result.Errors.Add(path + ": added folder is missing or inaccessible."); continue; } Util.NoReparse(path);
                    IEnumerable<string> folders = root.IsParent ? Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly) : new[] { path }; int count = 0;
                    foreach (string folder in folders) {
                        token.ThrowIfCancellationRequested(); if (++count > 2000) { result.Errors.Add(path + ": stopped after 2,000 child folders; add smaller roots."); break; }
                        try { Util.NoReparse(folder); string canonical = Util.Canonical(folder); if (!excludedRoots.Contains(canonical) && !games.ContainsKey(canonical)) games.Add(canonical, new Game { Name = System.IO.Path.GetFileName(canonical), Root = canonical, Source = "Added folder" }); }
                        catch (Exception ex) { result.Errors.Add(folder + ": " + ex.Message); }
                    }
                } catch (OperationCanceledException) { throw; } catch (Exception ex) { result.Errors.Add((root.Path ?? "Added folder") + ": " + ex.Message); }
            }
            foreach (Game game in games.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)) {
                token.ThrowIfCancellationRequested(); if (progress != null) progress("Scanning " + game.Name + "..."); ScanGame(game, token);
                if (game.ScanIncomplete) result.Errors.Add(game.Name + ": scan incomplete; review Details before installing."); if (game.Source == "Added folder" && game.Executables.Count == 0 && !game.ScanIncomplete) { result.Errors.Add("No game executable found: " + game.Root); continue; } result.Games.Add(game);
            }
            return result;
        }
        static string ReadSmall(string path) { if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Manifest exceeds 4 MB."); return File.ReadAllText(path); }
        public static void ScanGame(Game game, CancellationToken token)
        {
            if (game == null) throw new ArgumentNullException("game");
            game.Files = new List<string>(); game.Executables = new List<string>(); game.SelectedExe = null; game.ScanIncomplete = false; if (game.Evidence == null) game.Evidence = new List<string>(); string root;
            try { root = Util.Canonical(game.Root); Util.NoReparse(root); if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Game folder is missing."); }
            catch (Exception ex) { game.ScanIncomplete = true; game.Evidence.Add("Scan: " + ex.Message); return; }
            var folders = new Stack<KeyValuePair<string, int>>(); folders.Push(new KeyValuePair<string, int>(root, 0)); int visited = 0;
            while (folders.Count > 0) {
                token.ThrowIfCancellationRequested(); var entry = folders.Pop();
                if (++visited > MaxFolders) { game.ScanIncomplete = true; game.Evidence.Add("Scan stopped after " + MaxFolders + " directories; use a smaller game folder."); break; }
                try {
                    if ((File.GetAttributes(entry.Key) & FileAttributes.ReparsePoint) != 0) { game.ScanIncomplete = true; game.Evidence.Add("Skipped a linked folder: " + entry.Key); continue; }
                    foreach (string file in Directory.EnumerateFiles(entry.Key, "*", SearchOption.TopDirectoryOnly)) {
                        token.ThrowIfCancellationRequested(); if (game.Files.Count >= MaxFiles) { game.ScanIncomplete = true; game.Evidence.Add("Scan stopped after " + MaxFiles + " files; use a smaller game folder."); break; }
                        try {
                            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) { game.ScanIncomplete = true; continue; }
                            string relative = file.Substring(root.TrimEnd('\\', '/').Length + 1); game.Files.Add(relative);
                            if (String.Equals(System.IO.Path.GetExtension(file), ".exe", StringComparison.OrdinalIgnoreCase)) game.Executables.Add(Util.Canonical(file));
                        } catch (Exception ex) { game.ScanIncomplete = true; game.Evidence.Add("Could not inspect file " + file + ": " + ex.Message); }
                    }
                    if (game.Files.Count >= MaxFiles) break;
                    foreach (string folder in Directory.EnumerateDirectories(entry.Key, "*", SearchOption.TopDirectoryOnly)) {
                        token.ThrowIfCancellationRequested(); if (IgnoredDirectories.Contains(System.IO.Path.GetFileName(folder))) continue;
                        if (entry.Value >= MaxDepth) { game.ScanIncomplete = true; game.Evidence.Add("Scan depth limit reached: " + folder); continue; } if (folders.Count + visited >= MaxFolders) { game.ScanIncomplete = true; game.Evidence.Add("Directory scan limit reached."); break; } folders.Push(new KeyValuePair<string, int>(folder, entry.Value + 1));
                    }
                } catch (OperationCanceledException) { throw; } catch (Exception ex) { game.ScanIncomplete = true; game.Evidence.Add("Cannot read " + entry.Key + ": " + ex.Message); }
            }
            game.Executables = game.Executables.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            var ranked = game.Executables.Select(p => new { Path = p, Score = ExecutableScore(root, p) }).Where(p => p.Score >= 0).OrderByDescending(p => p.Score).ToList();
            if (ranked.Count == 1 || (ranked.Count > 1 && ranked[0].Score >= 80 && ranked[0].Score > ranked[1].Score)) game.SelectedExe = ranked[0].Path;
            if (ranked.Count > 1 && game.SelectedExe == null) game.Evidence.Add("Multiple runtime executables found; select the game executable in Details."); if (ranked.Count == 0) game.Evidence.Add("No runtime executable identified in the scanned folder.");
        }
        static int ExecutableScore(string root, string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant(); string relative = path.Substring(root.TrimEnd('\\', '/').Length + 1).Replace('/', '\\').ToLowerInvariant();
            string[] tools = { "unins", "uninstall", "crash", "report", "updater", "updatehelper", "launcher", "bootstrap", "setup", "install", "redist", "benchmark", "editor", "cefsubprocess", "webhelper", "helper", "easyanticheat", "battleye", "anticheat" };
            if (tools.Any(t => name.Contains(t))) return -1;
            if (relative.Contains(@"\engine\") || relative.StartsWith(@"engine\") || relative.Contains(@"\tools\") || relative.Contains(@"\support\")) return -1;
            if (relative == @"bin\x64_dx12\witcher3.exe") return 220;
            int score = 10; if (relative.Contains(@"binaries\win64\")) score += 100; if (relative.Contains(@"bin\x64_dx12\")) score += 120; if (name.EndsWith("-win64-shipping")) score += 80;
            if (relative.Contains(@"bin\x64\")) score += 20; if (name.EndsWith("_dx12") || name.EndsWith("-dx12")) score += 40; if (relative.Contains(@"\win32\") || name.Contains("dx11")) score -= 5; return score;
        }
    }
}




