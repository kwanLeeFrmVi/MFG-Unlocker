using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace MfgLauncher
{
    public static class PackageManager
    {
        const long MaxArchive = 100L * 1024 * 1024, MaxExpanded = 200L * 1024 * 1024;
        const string Repository = "dashdogy/MFG-Unlocker";
        sealed class Response { public Uri Final; public byte[] Bytes; }
        static Regex Pattern(string value) { return new Regex(value, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)); }
        static bool Allowed(Uri uri, bool assets)
        {
            return uri != null && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && (String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) || assets && String.Equals(uri.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase));
        }
        static Response Fetch(Uri uri, long limit, bool assets) { for (int attempt = 0; ; attempt++) { try { return FetchOnce(uri, limit, assets); } catch (HttpRequestException) { if (attempt >= 2) throw; Thread.Sleep(500 * (attempt + 1)); } } }
        static Response FetchOnce(Uri uri, long limit, bool assets)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            using (var client = new HttpClient(handler)) using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(assets ? 120 : 25))) {
                client.Timeout = TimeSpan.FromSeconds(assets ? 120 : 25); client.DefaultRequestHeaders.UserAgent.ParseAdd("MFG-Launcher/1.0"); Uri current = uri;
                for (int redirects = 0; redirects <= 6; redirects++) {
                    if (!Allowed(current, assets)) throw new InvalidDataException("The release redirected outside approved GitHub download hosts.");
                    using (var message = new HttpRequestMessage(HttpMethod.Get, current)) using (var response = client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).GetAwaiter().GetResult()) {
                        int code = (int)response.StatusCode;
                        if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308) { if (response.Headers.Location == null) throw new InvalidDataException("GitHub returned an invalid redirect."); current = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location); continue; }
                        response.EnsureSuccessStatusCode(); if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > limit) throw new InvalidDataException("The release response exceeds its size limit.");
                        using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()) using (var memory = new MemoryStream()) {
                            byte[] buffer = new byte[65536]; int read;
                            while ((read = stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).GetAwaiter().GetResult()) > 0) { if (memory.Length + read > limit) throw new InvalidDataException("The release response exceeds its size limit."); memory.Write(buffer, 0, read); }
                            return new Response { Final = current, Bytes = memory.ToArray() };
                        }
                    }
                }
                throw new InvalidDataException("Too many release redirects.");
            }
        }
        public static ReleaseInfo CheckLatest()
        {
            var latest = Fetch(new Uri("https://github.com/" + Repository + "/releases/latest"), 4 * 1024 * 1024, false); const string prefix = "/dashdogy/MFG-Unlocker/releases/tag/";
            if (!latest.Final.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("No official latest MFG Unlocker release was found.");
            string tag = Uri.UnescapeDataString(latest.Final.AbsolutePath.Substring(prefix.Length)); ValidateVersion(tag);
            var assets = Fetch(new Uri("https://github.com/" + Repository + "/releases/expanded_assets/" + Uri.EscapeDataString(tag)), 4 * 1024 * 1024, false); return ParseAssets(tag, Encoding.UTF8.GetString(assets.Bytes));
        }
        static void ValidateVersion(string version) { if (!Pattern(@"\Av?[0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.-]+)?\z").IsMatch(version ?? "")) throw new InvalidDataException("The package version must be a release version such as v1.7.0."); }
        public static ReleaseInfo ParseAssets(string tag, string html)
        {
            ValidateVersion(tag); if (html == null || html.Length > 4 * 1024 * 1024) throw new InvalidDataException("Release metadata is missing or too large.");
            string expected = "MFGUnlocker-" + (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag : "v" + tag) + ".zip"; string expectedPath = "/" + Repository + "/releases/download/" + tag + "/" + expected; var results = new List<ReleaseInfo>();
            foreach (Match item in Pattern(@"<li\b[^>]*>[\s\S]*?</li\s*>").Matches(html)) foreach (Match link in Pattern("<a\\b[^>]*\\bhref\\s*=\\s*[\"']([^\"']+)[\"']").Matches(item.Value)) {
                Uri uri; if (!Uri.TryCreate(new Uri("https://github.com/"), WebUtility.HtmlDecode(link.Groups[1].Value), out uri)) continue;
                if (!Allowed(uri, false) || Uri.UnescapeDataString(uri.AbsolutePath) != expectedPath || uri.Query.Length != 0 || uri.Fragment.Length != 0) continue;
                var digests = Pattern(@"sha256:([a-f0-9]{64})(?![a-f0-9])").Matches(item.Value).Cast<Match>().Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToArray(); if (digests.Length > 1) throw new InvalidDataException("Release metadata contains conflicting digests.");
                results.Add(new ReleaseInfo { Version = tag, AssetName = expected, AssetUrl = uri.AbsoluteUri, Digest = digests.Length == 1 ? "sha256:" + digests[0] : null, PageUrl = "https://github.com/" + Repository + "/releases/tag/" + Uri.EscapeDataString(tag) });
            }
            if (results.Count != 1) throw new InvalidDataException("The official release must contain one unambiguous MFGUnlocker ZIP asset."); return results[0];
        }
        public static PackageInfo DownloadLatest(Action<string> progress)
        {
            if (progress != null) progress("Checking the official MFG Unlocker release..."); var release = CheckLatest(); if (progress != null) progress("Downloading " + release.AssetName + " from GitHub..."); var download = Fetch(new Uri(release.AssetUrl), MaxArchive, true);
            string directory = Path.Combine(StateStore.DataDir, "downloads"); Util.NoReparse(directory); Directory.CreateDirectory(directory); Util.NoReparse(directory); string archive = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".zip");
            try { using (var file = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(download.Bytes, 0, download.Bytes.Length); file.Flush(true); } if (progress != null) progress("Validating the release package..."); return Import(archive, release.Version, release.AssetUrl, release.Digest); }
            finally { if (File.Exists(archive)) File.Delete(archive); }
        }
        static string Digest(string digest) { string value = digest ?? ""; if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) value = value.Substring(7); if (!Pattern(@"\A[a-f0-9]{64}\z").IsMatch(value)) throw new InvalidDataException("Invalid published SHA-256 digest."); return value.ToLowerInvariant(); }
        static string ZipPath(ZipArchiveEntry entry)
        {
            string name = entry.FullName.Replace('\\', '/'); if (String.IsNullOrEmpty(name) || name.Length > 240 || name.StartsWith("/", StringComparison.Ordinal) || name.IndexOf(':') >= 0 || name.IndexOf('\0') >= 0) throw new InvalidDataException("The archive contains an unsafe path.");
            string trimmed = name.TrimEnd('/'); foreach (string segment in trimmed.Split('/')) { string stem = segment.Split('.')[0]; if (segment.Length == 0 || segment == "." || segment == ".." || segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal) || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Pattern(@"\A(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z").IsMatch(stem)) throw new InvalidDataException("The archive contains an unsafe Windows path."); }
            int unixType = (entry.ExternalAttributes >> 16) & 0xf000; if (unixType == 0xa000 || (entry.ExternalAttributes & 0x400) != 0) throw new InvalidDataException("Archive links and reparse points are unsupported."); return trimmed;
        }
        public static PackageInfo Import(string zipPath, string version, string source, string digest)
        {
            ValidateVersion(version); Util.NoReparse(zipPath); var info = new FileInfo(zipPath); if (!info.Exists || info.Length == 0 || info.Length > MaxArchive) throw new InvalidDataException("Choose a ZIP package no larger than 100 MB.");
            bool verified = !String.IsNullOrEmpty(digest); if (verified && Util.HashFile(zipPath) != Digest(digest)) throw new InvalidDataException("The package SHA-256 does not match the published release digest."); byte[] dll;
            using (var zip = ZipFile.OpenRead(zipPath)) {
                if (zip.Entries.Count == 0 || zip.Entries.Count > 2000) throw new InvalidDataException("The archive contains too many entries or is empty."); long total = 0; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); ZipArchiveEntry target = null;
                foreach (var entry in zip.Entries) {
                    string relative = ZipPath(entry); if (!seen.Add(relative)) throw new InvalidDataException("The archive contains duplicate paths."); if (entry.Length < 0 || entry.Length > MaxArchive || total > MaxExpanded - entry.Length) throw new InvalidDataException("The expanded archive exceeds its size limit."); total += entry.Length;
                    if (String.Equals(Path.GetFileName(relative), "MFGUnlocker.dll", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith("/", StringComparison.Ordinal)) { if (target != null) throw new InvalidDataException("The archive contains more than one MFGUnlocker.dll."); target = entry; }
                }
                if (target == null || target.Length < 128) throw new InvalidDataException("The archive does not contain MFGUnlocker.dll.");
                using (var stream = target.Open()) using (var memory = new MemoryStream()) { byte[] buffer = new byte[65536]; int read; while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) { if (memory.Length + read > MaxArchive || memory.Length + read > target.Length) throw new InvalidDataException("The DLL exceeds its declared size."); memory.Write(buffer, 0, read); } if (memory.Length != target.Length) throw new InvalidDataException("The DLL size does not match its archive entry."); dll = memory.ToArray(); }
            }
            string directory = Path.Combine(StateStore.DataDir, "packages", Guid.NewGuid().ToString("N")); Util.NoReparse(directory); Directory.CreateDirectory(directory); Util.NoReparse(directory); string path = Path.Combine(directory, "MFGUnlocker.dll");
            try { using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(dll, 0, dll.Length); file.Flush(true); } if (!Util.IsX64Pe(path, true)) throw new InvalidDataException("MFGUnlocker.dll must be an x64 PE DLL."); var package = new PackageInfo { DllPath = path, Version = version, Sha256 = Util.HashFile(path), Source = source ?? zipPath, DigestVerified = verified }; StateStore.Save(StateStore.PackagePath, package); return package; }
            catch { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory)) Directory.Delete(directory); throw; }
        }
    }
    public static class InstallService
    {
        public const string ReceiptName = ".mfg-launcher.json";
        internal static Action<string> Step = null;
        static void Checkpoint(string step) { if (Step != null) Step(step); }
        static string ReceiptPath(Game game) { return Path.Combine(Util.Canonical(game.Root), ReceiptName); }
        static bool Same(string left, string right) { return String.Equals(Util.Canonical(left), Util.Canonical(right), StringComparison.OrdinalIgnoreCase); }
        static void ValidateGame(Game game)
        {
            if (game == null || String.IsNullOrEmpty(game.Root) || !Directory.Exists(game.Root)) throw new InvalidDataException("The game folder is missing. Rescan the library."); Util.NoReparse(game.Root); string exe = game.SelectedExe;
            if (String.IsNullOrEmpty(exe) || !Util.IsWithin(game.Root, exe) || !File.Exists(exe) || !String.Equals(Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase) || !Util.IsX64Pe(exe, false)) throw new InvalidDataException("Choose a valid x64 game executable inside the game folder."); Util.NoReparse(exe);
        }
        static void ValidatePackage(PackageInfo package)
        {
            if (package == null || String.IsNullOrEmpty(package.DllPath) || !File.Exists(package.DllPath)) throw new InvalidDataException("Download or import an MFG Unlocker package first."); Util.NoReparse(package.DllPath);
            if (new FileInfo(package.DllPath).Length > 100L * 1024 * 1024 || !Util.IsX64Pe(package.DllPath, true) || !Regex.IsMatch(package.Sha256 ?? "", @"\A[a-fA-F0-9]{64}\z") || !String.Equals(Util.HashFile(package.DllPath), package.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The cached package changed or is not a valid x64 DLL. Download or import it again."); if (String.IsNullOrEmpty(package.Version)) throw new InvalidDataException("The package has no release version.");
        }
        static void RefreshEligibility(Game game, string ownedProxy)
        {
            string selected = game.SelectedExe; Discovery.ScanGame(game, CancellationToken.None); game.SelectedExe = selected; if (ownedProxy == null) Compatibility.Evaluate(game); else Compatibility.EvaluateOwnedUpdate(game, ownedProxy);
            if (game.Status != GameStatus.Reported && game.Status != GameStatus.Candidate) throw new InvalidOperationException(game.BlockReason ?? "This game needs a complete compatibility review before its DLL can be changed. Rescan and check Details."); ValidateGame(game);
        }
        public static void EnsureNotRunning(Game game)
        {
            var executableNames = new HashSet<string>((game.Executables ?? new List<string>()).Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase); if (!String.IsNullOrEmpty(game.SelectedExe)) executableNames.Add(Path.GetFileNameWithoutExtension(game.SelectedExe));
            foreach (var process in Process.GetProcesses()) using (process) {
                string name = ""; try { name = process.ProcessName; } catch (InvalidOperationException) { continue; }
                try { string path = process.MainModule == null ? null : process.MainModule.FileName; if (path != null && Util.IsWithin(game.Root, path)) throw new InvalidOperationException("Close the game and its launcher before changing MFG Unlocker: " + name); }
                catch (System.ComponentModel.Win32Exception) { if (executableNames.Contains(name)) throw new InvalidOperationException("A matching game process cannot be inspected. Close it before changing MFG Unlocker: " + name); }
                catch (NotSupportedException) { if (executableNames.Contains(name)) throw new InvalidOperationException("A matching game process cannot be inspected. Close it first: " + name); }
                catch (InvalidOperationException ex) { if (!process.HasExited || ex.Message.StartsWith("Close the game", StringComparison.Ordinal)) throw; }
            }
        }
        public static Receipt ReadReceipt(Game game)
        {
            if (game == null || String.IsNullOrEmpty(game.Root)) throw new InvalidDataException("The game folder is missing."); string path = ReceiptPath(game); Util.NoReparse(path);
            if (Directory.Exists(path)) throw new InvalidDataException("The ownership receipt path is occupied by a folder."); if (!File.Exists(path)) return null; var receipt = StateStore.Read<Receipt>(path);
            if (receipt.Format != 1 || receipt.Owner != "MFG Launcher" || String.IsNullOrEmpty(receipt.GameRoot) || !Same(game.Root, receipt.GameRoot) || String.IsNullOrEmpty(receipt.ExePath) || !Util.IsWithin(game.Root, receipt.ExePath) || !File.Exists(receipt.ExePath) || !String.Equals(Path.GetExtension(receipt.ExePath), ".exe", StringComparison.OrdinalIgnoreCase) || !Util.IsX64Pe(receipt.ExePath, false)) throw new InvalidDataException("The ownership receipt is invalid or belongs to another game folder.");
            if (!Compatibility.IsSupportedProxy(receipt.ProxyName) || String.IsNullOrEmpty(receipt.DllPath) || !Same(receipt.DllPath, Path.Combine(Path.GetDirectoryName(receipt.ExePath), receipt.ProxyName)) || !Util.IsWithin(game.Root, receipt.DllPath)) throw new InvalidDataException("The ownership receipt contains an invalid DLL destination.");
            Util.NoReparse(receipt.ExePath); Util.NoReparse(receipt.DllPath);
            if (!File.Exists(receipt.DllPath) || !Regex.IsMatch(receipt.Sha256 ?? "", @"\A[a-fA-F0-9]{64}\z") || !String.Equals(Util.HashFile(receipt.DllPath), receipt.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The installed DLL is missing or has changed. It will be preserved; review the game folder manually."); return receipt;
        }
        static Receipt MakeReceipt(Game game, PackageInfo package, string proxy) { return new Receipt { Format = 1, Owner = "MFG Launcher", GameRoot = Util.Canonical(game.Root), ExePath = Util.Canonical(game.SelectedExe), DllPath = Path.Combine(Path.GetDirectoryName(Util.Canonical(game.SelectedExe)), proxy), ProxyName = proxy, Version = package.Version, Sha256 = package.Sha256.ToLowerInvariant(), InstalledUtc = DateTime.UtcNow.ToString("o"), Source = package.Source }; }
        static void WriteExclusive(string path, byte[] bytes) { using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); } }
        static byte[] Json(object value) { return new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(value)); }
        static void DeleteMatching(string path, string hash) { if (File.Exists(path) && String.Equals(Util.HashFile(path), hash, StringComparison.OrdinalIgnoreCase)) File.Delete(path); }
        static Mutex Lock(Game game)
        {
            var mutex = new Mutex(false, "Local\\MFGLauncher-" + game.Id); bool acquired; try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; } if (!acquired) { mutex.Dispose(); throw new InvalidOperationException("Another launcher operation is already changing this game. Try again when it finishes."); } return mutex;
        }
        public static void Install(Game game, PackageInfo package, string proxy)
        {
            ValidateGame(game); ValidatePackage(package);
            using (var mutex = Lock(game)) { try {
                RefreshEligibility(game, null); if (!Compatibility.IsSupportedProxy(proxy) || !game.SuggestedProxies.Contains(proxy, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose an available proxy filename recommended for this game.");
                string receiptPath = ReceiptPath(game); if (File.Exists(receiptPath) || Directory.Exists(receiptPath)) throw new IOException("An ownership receipt already exists. Review or uninstall the existing installation first.");
                var receipt = MakeReceipt(game, package, proxy); Util.NoReparse(receipt.DllPath); if (File.Exists(receipt.DllPath) || Directory.Exists(receipt.DllPath)) throw new IOException("The proxy destination is occupied. Existing game DLLs and mods are never overwritten."); EnsureNotRunning(game);
                bool dllCreated = false, receiptCreated = false; string partialHash = null, receiptHash = null; string receiptTemp = Path.Combine(game.Root, ".mfg-receipt-" + Guid.NewGuid().ToString("N") + ".tmp");
                try {
                    using (var source = new FileStream(package.DllPath, FileMode.Open, FileAccess.Read, FileShare.Read)) using (var target = new FileStream(receipt.DllPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) {
                        dllCreated = true;
                        try {
                            source.CopyTo(target); target.Flush(true); target.Position = 0; using (var sha = SHA256.Create()) partialHash = BitConverter.ToString(sha.ComputeHash(target)).Replace("-", "").ToLowerInvariant(); if (partialHash != receipt.Sha256) throw new InvalidDataException("The package changed while being copied."); Checkpoint("install-dll-written");
                            byte[] bytes = Json(receipt); using (var sha = SHA256.Create()) receiptHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); WriteExclusive(receiptTemp, bytes); File.Move(receiptTemp, receiptPath); receiptCreated = true; Checkpoint("install-receipt-written");
                        } catch { if (partialHash == null) { target.Position = 0; using (var sha = SHA256.Create()) partialHash = BitConverter.ToString(sha.ComputeHash(target)).Replace("-", "").ToLowerInvariant(); } throw; }
                    }
                    game.Installed = receipt;
                } catch { if (receiptCreated && receiptHash != null) DeleteMatching(receiptPath, receiptHash); if (dllCreated && partialHash != null) DeleteMatching(receipt.DllPath, partialHash); throw; }
                finally { if (File.Exists(receiptTemp)) File.Delete(receiptTemp); }
            } finally { mutex.ReleaseMutex(); } }
        }
        public static void Update(Game game, PackageInfo package)
        {
            ValidateGame(game); ValidatePackage(package);
            using (var mutex = Lock(game)) { try {
                var old = ReadReceipt(game); if (old == null) throw new InvalidOperationException("Only a verified owned installation can be updated."); if (!Same(game.SelectedExe, old.ExePath)) throw new InvalidOperationException("Select the executable belonging to this installation before updating it."); RefreshEligibility(game, old.ProxyName);
                if (!game.SuggestedProxies.Contains(old.ProxyName, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("The installed proxy is no longer supported for this game. Review it before updating."); EnsureNotRunning(game); if (String.Equals(old.Sha256, package.Sha256, StringComparison.OrdinalIgnoreCase)) return;
                string backup = Backup(game, old), receiptPath = ReceiptPath(game), originalReceiptHash = Util.HashFile(receiptPath); string stage = Path.Combine(Path.GetDirectoryName(old.DllPath), ".mfg-launcher-" + Guid.NewGuid().ToString("N") + ".tmp"), receiptStage = Path.Combine(game.Root, ".mfg-launcher-" + Guid.NewGuid().ToString("N") + ".tmp");
                var next = MakeReceipt(game, package, old.ProxyName); byte[] nextJson = Json(next); string nextReceiptHash; using (var sha = SHA256.Create()) nextReceiptHash = BitConverter.ToString(sha.ComputeHash(nextJson)).Replace("-", "").ToLowerInvariant(); bool dllReplaced = false, receiptReplaced = false;
                try {
                    WriteExclusive(stage, File.ReadAllBytes(package.DllPath)); if (!String.Equals(Util.HashFile(stage), package.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The package changed while being staged."); WriteExclusive(receiptStage, nextJson);
                    var current = ReadReceipt(game); if (current == null || current.Sha256 != old.Sha256 || Util.HashFile(receiptPath) != originalReceiptHash) throw new IOException("The owned installation changed during this update. No update was applied."); EnsureNotRunning(game); Checkpoint("update-staged");
                    File.Replace(stage, old.DllPath, null); dllReplaced = true; Checkpoint("update-dll-replaced"); File.Replace(receiptStage, receiptPath, null); receiptReplaced = true; Checkpoint("update-receipt-replaced"); game.Installed = next;
                } catch (Exception error) {
                    bool restored = true;
                    try {
                        if (dllReplaced) { if (File.Exists(old.DllPath) && String.Equals(Util.HashFile(old.DllPath), next.Sha256, StringComparison.OrdinalIgnoreCase)) { WriteExclusive(stage, File.ReadAllBytes(Path.Combine(backup, "MFGUnlocker.dll"))); File.Replace(stage, old.DllPath, null); } else restored = false; }
                        if (receiptReplaced) { if (File.Exists(receiptPath) && Util.HashFile(receiptPath) == nextReceiptHash) StateStore.Save(receiptPath, old); else restored = false; }
                    } catch { restored = false; }
                    if (!restored) throw new IOException("The update failed and a changed file was preserved. Rollback data is saved at " + backup, error); throw;
                } finally { if (File.Exists(stage)) File.Delete(stage); if (File.Exists(receiptStage)) File.Delete(receiptStage); }
            } finally { mutex.ReleaseMutex(); } }
        }
        static string Backup(Game game, Receipt receipt)
        {
            string directory = Path.Combine(StateStore.DataDir, "rollback", game.Id, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")); Util.NoReparse(directory); Directory.CreateDirectory(directory); Util.NoReparse(directory);
            string dll = Path.Combine(directory, "MFGUnlocker.dll"), receiptCopy = Path.Combine(directory, ReceiptName); File.Copy(receipt.DllPath, dll, false); File.Copy(ReceiptPath(game), receiptCopy, false); if (!String.Equals(Util.HashFile(dll), receipt.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("The owned DLL changed while saving rollback data. Nothing was changed in the game folder."); return directory;
        }
        public static void Uninstall(Game game)
        {
            if (game == null || String.IsNullOrEmpty(game.Root)) throw new InvalidDataException("The game folder is missing."); Util.NoReparse(game.Root);
            using (var mutex = Lock(game)) { try {
                var old = ReadReceipt(game); if (old == null) throw new InvalidOperationException("No verified owned installation was found. Unrelated files will be preserved."); EnsureNotRunning(game); string backup = Backup(game, old), receiptPath = ReceiptPath(game), receiptHash = Util.HashFile(receiptPath); bool deletedDll = false;
                try {
                    var current = ReadReceipt(game); if (current == null || current.Sha256 != old.Sha256 || Util.HashFile(receiptPath) != receiptHash) throw new IOException("The installation changed while preparing removal. Rescan and review it."); Checkpoint("uninstall-prepared"); File.Delete(old.DllPath); deletedDll = true; Checkpoint("uninstall-dll-deleted"); if (Util.HashFile(receiptPath) != receiptHash) throw new IOException("The ownership receipt changed during removal."); File.Delete(receiptPath); game.Installed = null;
                } catch (Exception error) { if (deletedDll && !File.Exists(old.DllPath) && !Directory.Exists(old.DllPath)) { try { WriteExclusive(old.DllPath, File.ReadAllBytes(Path.Combine(backup, "MFGUnlocker.dll"))); } catch { throw new IOException("Removal failed. Rollback data is saved at " + backup, error); } } throw; }
            } finally { mutex.ReleaseMutex(); } }
        }
    }
}


