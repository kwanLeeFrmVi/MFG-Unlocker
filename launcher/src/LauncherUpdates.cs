using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MfgLauncher
{
    public sealed class LauncherRelease
    {
        public string Version { get; set; }
        public string Repository { get; set; }
        public string AssetUrl { get; set; }
        public string AssetName { get; set; }
        public string Digest { get; set; }
        public string Sha256 { get; set; }
        public string ChecksumUrl { get; set; }
        public string ChecksumAssetName { get; set; }
        public string PageUrl { get; set; }
    }
    public static class LauncherUpdates
    {
        const int MaxBinaryBytes = 20 * 1024 * 1024;
        const int MaxMetadataBytes = 4 * 1024 * 1024;
        const string BinaryName = "MFGLauncher.exe";
        static Regex Pattern(string pattern, RegexOptions options) { return new Regex(pattern, options, TimeSpan.FromSeconds(2)); }
        public static string ValidateRepository(string repository)
        {
            string value = (repository ?? "").Trim();
            if (!Pattern(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,99}/[A-Za-z0-9][A-Za-z0-9._-]{0,99}\z", RegexOptions.None).IsMatch(value))
                throw new InvalidDataException("Enter a GitHub repository as owner/name, using letters, numbers, dots, hyphens or underscores.");
            return value;
        }
        static Version ParseVersion(string version)
        {
            var match = Pattern(@"\A(?:launcher-v|[vV])?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.None).Match(version ?? "");
            int major, minor, patch;
            if (!match.Success || !Int32.TryParse(match.Groups[1].Value, out major) || !Int32.TryParse(match.Groups[2].Value, out minor) || !Int32.TryParse(match.Groups[3].Value, out patch))
                throw new InvalidDataException("Launcher release tags must be stable semantic versions such as launcher-v1.2.3.");
            return new Version(major, minor, patch, 0);
        }
        public static bool IsNewer(string version, Version current)
        {
            if (current == null) throw new ArgumentNullException("current");
            return ParseVersion(version).CompareTo(new Version(current.Major, current.Minor, Math.Max(0, current.Build), Math.Max(0, current.Revision))) > 0;
        }
        public static LauncherRelease Check(string repository)
        {
            string repo = ValidateRepository(repository);
            var page = Fetch(new Uri("https://github.com/" + repo + "/releases/latest"), MaxMetadataBytes, false);
            string prefix = "/" + repo + "/releases/tag/";
            if (!page.Final.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The repository has no accessible latest release.");
            string tag = Uri.UnescapeDataString(page.Final.AbsolutePath.Substring(prefix.Length));
            ParseVersion(tag);
            var assets = Fetch(new Uri("https://github.com/" + repo + "/releases/expanded_assets/" + Uri.EscapeDataString(tag)), MaxMetadataBytes, false);
            var release = ParseAssets(repo, tag, Encoding.UTF8.GetString(assets.Bytes));
            release.PageUrl = "https://github.com/" + repo + "/releases/tag/" + Uri.EscapeDataString(tag);
            return release;
        }
        static bool AssetPath(Uri uri, string repo, string tag, string name)
        {
            if (!Allowed(uri, false) || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
            string[] parts = uri.AbsolutePath.Split('/').Select(Uri.UnescapeDataString).ToArray();
            string[] repository = repo.Split('/');
            return parts.Length == 7 && parts[0] == "" && String.Equals(parts[1], repository[0], StringComparison.OrdinalIgnoreCase) && String.Equals(parts[2], repository[1], StringComparison.OrdinalIgnoreCase)
                && parts[3] == "releases" && parts[4] == "download" && parts[5] == tag && parts[6] == name;
        }
        sealed class Asset
        {
            public string Name, Url, Hash;
        }
        public static LauncherRelease ParseAssets(string repository, string tag, string html)
        {
            string repo = ValidateRepository(repository); ParseVersion(tag);
            if (html == null || html.Length > MaxMetadataBytes) throw new InvalidDataException("Release asset metadata is missing or too large.");
            var assets = new List<Asset>();
            foreach (Match item in Pattern(@"<li\b[^>]*>([\s\S]*?)</li\s*>", RegexOptions.IgnoreCase).Matches(html)) {
                foreach (Match link in Pattern("<a\\b[^>]*\\bhref\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase).Matches(item.Value)) {
                    Uri uri; string href = WebUtility.HtmlDecode(link.Groups[1].Value);
                    if (!Uri.TryCreate(new Uri("https://github.com/"), href, out uri)) continue;
                    string name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
                    if (name != BinaryName && name != "MFGLauncher.exe.sha256" && name != "MFGLauncher.sha256" && name != "SHA256SUMS.txt" && name != "SHA256SUMS" && name != "checksums.txt") continue;
                    if (!AssetPath(uri, repo, tag, name)) continue;
                    var hashes = Pattern(@"sha256:([A-Fa-f0-9]{64})(?![A-Fa-f0-9])", RegexOptions.None).Matches(item.Value).Cast<Match>().Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToArray();
                    if (hashes.Length > 1) throw new InvalidDataException("The release asset contains conflicting SHA-256 digests.");
                    assets.Add(new Asset { Name = name, Url = uri.AbsoluteUri, Hash = hashes.FirstOrDefault() });
                }
            }
            var binaries = assets.Where(a => a.Name == BinaryName).ToList();
            if (binaries.Count != 1) throw new InvalidDataException("The release must contain exactly one MFGLauncher.exe asset.");
            var binary = binaries[0];
            Asset checksum = null;
            foreach (string name in new[] { "MFGLauncher.exe.sha256", "MFGLauncher.sha256", "SHA256SUMS.txt", "SHA256SUMS", "checksums.txt" }) {
                var matches = assets.Where(a => a.Name == name).ToList();
                if (matches.Count > 1) throw new InvalidDataException("The release contains ambiguous checksum assets.");
                if (matches.Count == 1) { checksum = matches[0]; break; }
            }
            if (binary.Hash == null && checksum == null) throw new InvalidDataException("Automatic launcher updates require a published SHA-256 digest or checksum asset.");
            return new LauncherRelease { Version = tag, Repository = repo, AssetName = BinaryName, AssetUrl = binary.Url, Sha256 = binary.Hash, Digest = binary.Hash == null ? null : "sha256:" + binary.Hash, ChecksumUrl = checksum == null ? null : checksum.Url, ChecksumAssetName = checksum == null ? null : checksum.Name };
        }
        public static string ParseChecksum(string text, string assetName) { return ParseChecksum(text, assetName, false); }
        public static string ParseChecksum(string text, string assetName, bool allowBareHash)
        {
            if (assetName != BinaryName || text == null || text.Length > 128 * 1024) throw new InvalidDataException("Invalid launcher checksum file.");
            var hashes = new List<string>();
            foreach (string raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                string line = raw.Trim().TrimStart('\ufeff');
                var match = Pattern(@"\A([A-Fa-f0-9]{64})\s+\*?" + Regex.Escape(assetName) + @"\z", RegexOptions.None).Match(line);
                if (!match.Success) match = Pattern(@"\ASHA256\s*\(" + Regex.Escape(assetName) + @"\)\s*=\s*([A-Fa-f0-9]{64})\z", RegexOptions.IgnoreCase).Match(line);
                if (!match.Success && allowBareHash) match = Pattern(@"\A(?:sha256:)?([A-Fa-f0-9]{64})\z", RegexOptions.None).Match(line);
                if (match.Success) hashes.Add(match.Groups[1].Value.ToLowerInvariant());
            }
            var distinct = hashes.Distinct().ToArray();
            if (distinct.Length != 1) throw new InvalidDataException("The checksum asset must contain one unambiguous SHA-256 for MFGLauncher.exe.");
            return distinct[0];
        }
        static string NormalizeHash(string hash)
        {
            string value = hash ?? "";
            if (value.StartsWith("sha256:", StringComparison.Ordinal)) value = value.Substring(7);
            if (!Pattern(@"\A[A-Fa-f0-9]{64}\z", RegexOptions.None).IsMatch(value)) throw new InvalidDataException("A published SHA-256 digest is required for automatic launcher updates.");
            return value.ToLowerInvariant();
        }
        public static void ValidateDownloaded(string path, string expectedHash)
        {
            string expected = NormalizeHash(expectedHash);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxBinaryBytes || !Util.IsX64Pe(path, false) || !IsExecutable(path)) throw new InvalidDataException("The launcher update must be an x64 Windows executable no larger than 20 MB.");
            if (!String.Equals(Util.HashFile(path), expected, StringComparison.Ordinal)) throw new InvalidDataException("Launcher update SHA-256 does not match the published checksum.");
        }
        static bool IsExecutable(string path)
        {
            using (var stream = File.OpenRead(path)) using (var reader = new BinaryReader(stream)) { stream.Position = 0x3c; int header = reader.ReadInt32(); stream.Position = header + 22; ushort flags = reader.ReadUInt16(); return (flags & 0x0002) != 0 && (flags & 0x2000) == 0; }
        }
        public static string Download(LauncherRelease release)
        {
            if (release == null) throw new ArgumentNullException("release");
            string repo = ValidateRepository(release.Repository); ParseVersion(release.Version);
            Uri asset;
            if (release.AssetName != BinaryName || !Uri.TryCreate(release.AssetUrl, UriKind.Absolute, out asset) || !AssetPath(asset, repo, release.Version, BinaryName)) throw new InvalidDataException("The launcher update asset must belong to the configured GitHub release.");
            string expected = null;
            if (!String.IsNullOrEmpty(release.Sha256)) expected = NormalizeHash(release.Sha256);
            if (!String.IsNullOrEmpty(release.Digest)) {
                string digest = NormalizeHash(release.Digest);
                if (expected != null && expected != digest) throw new InvalidDataException("Conflicting launcher update digests.");
                expected = digest;
            }
            if (expected == null) {
                Uri checksum;
                if (!Uri.TryCreate(release.ChecksumUrl, UriKind.Absolute, out checksum) || !AssetPath(checksum, repo, release.Version, release.ChecksumAssetName ?? "")) throw new InvalidDataException("No valid published launcher checksum asset was found.");
                if (!new[] { "MFGLauncher.exe.sha256", "MFGLauncher.sha256", "SHA256SUMS.txt", "SHA256SUMS", "checksums.txt" }.Contains(release.ChecksumAssetName)) throw new InvalidDataException("Unsupported launcher checksum asset.");
                bool allowBare = release.ChecksumAssetName == "MFGLauncher.exe.sha256" || release.ChecksumAssetName == "MFGLauncher.sha256";
                expected = ParseChecksum(Encoding.UTF8.GetString(Fetch(checksum, 128 * 1024, true).Bytes), BinaryName, allowBare);
            }
            var download = Fetch(asset, MaxBinaryBytes, true);
            string parent = Path.Combine(StateStore.DataDir, "launcher-updates");
            Util.NoReparse(parent); Directory.CreateDirectory(parent); Util.NoReparse(parent);
            string directory = Path.Combine(parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); Util.NoReparse(directory);
            string staged = Path.Combine(directory, BinaryName);
            try {
                using (var file = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(download.Bytes, 0, download.Bytes.Length); file.Flush(true); }
                ValidateDownloaded(staged, expected);
                release.Sha256 = expected; release.Digest = "sha256:" + expected;
                return staged;
            } catch {
                if (File.Exists(staged)) File.Delete(staged);
                if (Directory.Exists(directory)) Directory.Delete(directory);
                throw;
            }
        }
        sealed class Response
        {
            public Uri Final;
            public byte[] Bytes;
        }
        static bool Allowed(Uri uri, bool assets)
        {
            return uri != null && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0
                && (String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) || (assets && String.Equals(uri.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase)));
        }
        static Response Fetch(Uri uri, int maxBytes, bool assets) { for (int attempt = 0; ; attempt++) { try { return FetchOnce(uri, maxBytes, assets); } catch (HttpRequestException) { if (attempt >= 2) throw; Thread.Sleep(500 * (attempt + 1)); } } }
        static Response FetchOnce(Uri uri, int maxBytes, bool assets)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            using (var client = new HttpClient(handler))
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90))) {
                client.Timeout = TimeSpan.FromSeconds(90); client.DefaultRequestHeaders.UserAgent.ParseAdd("MFG-Launcher/1.0");
                Uri current = uri;
                for (int redirects = 0; redirects <= 6; redirects++) {
                    if (!Allowed(current, assets)) throw new InvalidDataException("Launcher update redirected outside the approved GitHub hosts.");
                    using (var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Get, current), HttpCompletionOption.ResponseHeadersRead, cancellation.Token).GetAwaiter().GetResult()) {
                        int status = (int)response.StatusCode;
                        if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308) {
                            if (response.Headers.Location == null) throw new InvalidDataException("GitHub returned an invalid release redirect.");
                            current = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location); continue;
                        }
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maxBytes) throw new InvalidDataException("Launcher update response exceeds its size limit.");
                        using (var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        using (var destination = new MemoryStream()) {
                            byte[] buffer = new byte[65536]; int read;
                            while ((read = source.ReadAsync(buffer, 0, buffer.Length, cancellation.Token).GetAwaiter().GetResult()) != 0) {
                                if (destination.Length + read > maxBytes) throw new InvalidDataException("Launcher update response exceeds its size limit.");
                                destination.Write(buffer, 0, read);
                            }
                            return new Response { Final = current, Bytes = destination.ToArray() };
                        }
                    }
                }
                throw new InvalidDataException("Too many GitHub release redirects.");
            }
        }
    }
}


