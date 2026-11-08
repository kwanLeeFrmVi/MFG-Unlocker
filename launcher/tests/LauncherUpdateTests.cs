using System;
using System.IO;

namespace MfgLauncher
{
    public static class LauncherUpdateTests
    {
        public static void Run(Action<bool,string> assert)
        {
            const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            const string other = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
            const string repo = "kwanLeeFrmVi/MFG-Unlocker";
            const string tag = "launcher-v1.2.3";
            string prefix = "/" + repo + "/releases/download/" + tag + "/";
            assert(LauncherUpdates.ValidateRepository("  " + repo + "  ") == repo, "Repository accepts trimmed owner/name");
            assert(LauncherUpdates.ValidateRepository("owner.name/repo_name-2") == "owner.name/repo_name-2", "Repository accepts safe name punctuation");
            foreach (string invalid in new[] { "https://github.com/a/b", "owner/repo/extra", "../repo", "owner/..", "/owner/repo", "owner/repo?x=1", "owner/repo#tag", "owner/repo%2fother", "owner name/repo", "owner\\repo", "", null })
                assert(Throws(() => LauncherUpdates.ValidateRepository(invalid)), "Repository rejects injected or malformed value: " + invalid);
            assert(LauncherUpdates.IsNewer("launcher-v1.2.3", new Version(1, 0, 0, 0)), "Launcher-prefixed stable release compares newer");
            assert(LauncherUpdates.IsNewer("v2.0.0", new Version(1, 99, 99)), "Conventional v-prefixed stable release compares newer");
            assert(!LauncherUpdates.IsNewer("1.0.0+build.5", new Version(1, 0, 0, 0)), "Build metadata cannot force same-version update");
            assert(!LauncherUpdates.IsNewer("launcher-v1.0.0", new Version(1, 0, 0, 1)), "Assembly revision prevents same-tag downgrade");
            assert(!LauncherUpdates.IsNewer("0.9.9", new Version(1, 0)), "Older release does not update");
            foreach (string invalid in new[] { "v1.2.3-beta.1", "v01.2.3", "v1.2", "v1.2.3.4", "v2147483648.0.0", "release-v1.2.3", "launcher-v1.2.3/evil", "1.2.3+", "" })
                assert(Throws(() => LauncherUpdates.IsNewer(invalid, new Version(1, 0, 0))), "Version rejects non-stable or malformed tag: " + invalid);

            string html = "<ul><li><a href='" + prefix + "MFGLauncher.exe'>MFGLauncher.exe</a><clipboard-copy value='sha256:" + hash + "'></clipboard-copy></li></ul>";
            var release = LauncherUpdates.ParseAssets(repo, tag, html);
            assert(release.AssetName == "MFGLauncher.exe" && release.AssetUrl == "https://github.com" + prefix + "MFGLauncher.exe" && release.Sha256 == hash, "Asset parser binds exact executable, release, and same-item digest");
            assert(LauncherUpdates.ParseAssets(repo, tag, html.Replace(hash, hash.ToUpperInvariant())).Sha256 == hash, "Published SHA-256 normalizes hexadecimal case");
            assert(Throws(() => LauncherUpdates.ParseAssets(repo, tag, html.Replace("sha256:" + hash, "no digest") + "<li>sha256:" + hash + "</li>")), "Digest from another asset item cannot authenticate executable");
            assert(Throws(() => LauncherUpdates.ParseAssets(repo, tag, html.Replace("</li>", "sha256:" + other + "</li>"))), "Conflicting same-item digests are rejected");
            assert(Throws(() => LauncherUpdates.ParseAssets(repo, tag, html + html)), "Duplicate executable assets are ambiguous");
            assert(Throws(() => LauncherUpdates.ParseAssets(repo, tag, html.Replace(prefix, "/other/repo/releases/download/" + tag + "/"))), "Asset from another repository is rejected");
            assert(Throws(() => LauncherUpdates.ParseAssets(repo, tag, html.Replace(prefix, "https://github.com.evil.invalid" + prefix))), "Lookalike GitHub host is rejected");
            assert(Throws(() => LauncherUpdates.ParseAssets(repo, tag, html.Replace(prefix, "http://github.com" + prefix))), "HTTP downgrade is rejected");
            assert(Throws(() => LauncherUpdates.ParseAssets(repo, tag, html.Replace("MFGLauncher.exe", "MFGLauncher.exe.zip"))), "ZIP or differently named launcher cannot replace executable");
            string withChecksum = html.Replace("sha256:" + hash, "") + "<li><a href='" + prefix + "SHA256SUMS.txt'>SHA256SUMS.txt</a>sha256:" + other + "</li>";
            var fallback = LauncherUpdates.ParseAssets(repo, tag, withChecksum);
            assert(fallback.Sha256 == null && fallback.ChecksumAssetName == "SHA256SUMS.txt", "Checksum fallback does not reuse checksum asset's own digest for executable");
            assert(LauncherUpdates.ParseChecksum(hash + "  MFGLauncher.exe\n" + other + "  unrelated.dll", "MFGLauncher.exe") == hash, "SHA256SUMS binds hash to exact executable filename");
            assert(LauncherUpdates.ParseChecksum("SHA256 (MFGLauncher.exe) = " + hash, "MFGLauncher.exe") == hash, "BSD-style SHA256 checksum is supported");
            assert(LauncherUpdates.ParseChecksum("sha256:" + hash, "MFGLauncher.exe", true) == hash, "Per-file checksum asset supports a single bare digest");
            assert(Throws(() => LauncherUpdates.ParseChecksum(hash, "MFGLauncher.exe")), "Aggregate checksum requires filename binding");
            assert(Throws(() => LauncherUpdates.ParseChecksum(hash + "  Other.exe", "MFGLauncher.exe")), "Other-file checksum cannot authenticate launcher");
            assert(Throws(() => LauncherUpdates.ParseChecksum(hash + "  MFGLauncher.exe\n" + other + "  MFGLauncher.exe", "MFGLauncher.exe")), "Conflicting executable checksums are rejected");
            assert(Throws(() => LauncherUpdates.Download(new LauncherRelease { Repository = repo, Version = tag, AssetName = "MFGLauncher.exe", AssetUrl = "https://example.invalid/MFGLauncher.exe", Sha256 = hash })), "Download rejects unapproved source before network access");
            assert(Throws(() => LauncherUpdates.Download(new LauncherRelease { Repository = repo, Version = tag, AssetName = "MFGLauncher.exe", AssetUrl = "https://github.com" + prefix + "MFGLauncher.exe" })), "Download requires published checksum before network access");

            string root = Path.Combine(Path.GetTempPath(), "MfgLauncherUpdateTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try {
                string exe = Path.Combine(root, "MFGLauncher.exe");
                WritePe(exe, 0x8664, 0x0002); LauncherUpdates.ValidateDownloaded(exe, Util.HashFile(exe));
                assert(true, "Matching x64 executable checksum validates without executing it");
                assert(Throws(() => LauncherUpdates.ValidateDownloaded(exe, hash)), "Downloaded executable with mismatched digest is rejected");
                WritePe(exe, 0x8664, 0x2002);
                assert(Throws(() => LauncherUpdates.ValidateDownloaded(exe, Util.HashFile(exe))), "An x64 DLL renamed exe cannot replace the launcher");
                WritePe(exe, 0x014c, 0x0002);
                assert(Throws(() => LauncherUpdates.ValidateDownloaded(exe, Util.HashFile(exe))), "Non-x64 executable is rejected");
                File.WriteAllText(exe, "<html>download failed</html>");
                assert(Throws(() => LauncherUpdates.ValidateDownloaded(exe, Util.HashFile(exe))), "HTML download error cannot replace launcher");
                using (var file = File.Create(exe)) file.SetLength(20L * 1024 * 1024 + 1);
                assert(Throws(() => LauncherUpdates.ValidateDownloaded(exe, hash)), "Oversized launcher is rejected");
            } finally { Directory.Delete(root, true); }
        }
        static bool Throws(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
        static void WritePe(string path, ushort machine, ushort flags)
        {
            using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream)) {
                writer.Write(new byte[256]); stream.Position = 0; writer.Write((ushort)0x5a4d); stream.Position = 0x3c; writer.Write(0x80); stream.Position = 0x80; writer.Write((uint)0x4550); writer.Write(machine); stream.Position = 0x80 + 22; writer.Write(flags); stream.Position = 0x80 + 24; writer.Write((ushort)0x20b);
            }
        }
    }
}
