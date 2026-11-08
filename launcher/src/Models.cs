using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace MfgLauncher
{
    public enum GameStatus { Reported, Candidate, Blocked, Review, Unsupported, Installed }
    public sealed class ScanRoot
    {
        public string Path { get; set; }
        public bool IsParent { get; set; }
    }
    public sealed class Game
    {
        public string Id { get { return Util.HashText(Root.ToLowerInvariant()); } }
        public string Name { get; set; }
        public string Root { get; set; }
        public string SteamId { get; set; }
        public string Source { get; set; }
        public List<string> Executables { get; set; }
        public string SelectedExe { get; set; }
        public List<string> Files { get; set; }
        public List<string> Evidence { get; set; }
        public List<string> SuggestedProxies { get; set; }
        public bool ScanIncomplete { get; set; }
        public GameStatus Status { get; set; }
        public string BlockReason { get; set; }
        public string KnownRule { get; set; }
        public Receipt Installed { get; set; }
        public Game() { Executables = new List<string>(); Files = new List<string>(); Evidence = new List<string>(); SuggestedProxies = new List<string>(); }
    }
    public sealed class ScanResult
    {
        public List<Game> Games { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Libraries { get; set; }
        public ScanResult() { Games = new List<Game>(); Errors = new List<string>(); Libraries = new List<string>(); }
    }
    public sealed class Settings
    {
        public List<ScanRoot> Roots { get; set; }
        public Dictionary<string,string> ExeChoices { get; set; }
        public bool AutoUpdateDll { get; set; }
        public bool AutoUpdateLauncher { get; set; }
        public string LauncherRepository { get; set; }
        public Settings() { AutoUpdateDll = true; AutoUpdateLauncher = true; LauncherRepository = "kwanLeeFrmVi/MFG-Unlocker"; Roots = new List<ScanRoot>(); ExeChoices = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); }
    }
    public sealed class Receipt
    {
        public int Format { get; set; }
        public string Owner { get; set; }
        public string GameRoot { get; set; }
        public string ExePath { get; set; }
        public string DllPath { get; set; }
        public string ProxyName { get; set; }
        public string Version { get; set; }
        public string Sha256 { get; set; }
        public string InstalledUtc { get; set; }
        public string Source { get; set; }
    }
    public sealed class PackageInfo
    {
        public string DllPath { get; set; }
        public string Version { get; set; }
        public string Sha256 { get; set; }
        public string Source { get; set; }
        public bool DigestVerified { get; set; }
    }
    public sealed class ReleaseInfo
    {
        public string Version { get; set; }
        public string AssetUrl { get; set; }
        public string AssetName { get; set; }
        public string Digest { get; set; }
        public long Size { get; set; }
        public string PageUrl { get; set; }
    }
    public static class StateStore
    {
        public static string DataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MFG Launcher");
        public static T Read<T>(string path) where T : class, new()
        {
            if (!File.Exists(path)) return new T();
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Settings file is too large: " + path);
            return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        public static void Save(string path, object value)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temp, new JavaScriptSerializer().Serialize(value), new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static string SettingsPath { get { return System.IO.Path.Combine(DataDir, "settings.json"); } }
        public static string PackagePath { get { return System.IO.Path.Combine(DataDir, "package.json"); } }
    }
    public static class Util
    {
        public static string HashText(string text) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        public static string HashFile(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        public static string Canonical(string path) { string full = System.IO.Path.GetFullPath(path); return full.Length == System.IO.Path.GetPathRoot(full).Length ? full : full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar); }
        public static bool IsWithin(string root, string path) { string prefix = Canonical(root).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar; return Canonical(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase); }
        public static void NoReparse(string path)
        {
            string current = System.IO.Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current)) { if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Symbolic links and junctions cannot be installation targets: " + current); current = System.IO.Path.GetDirectoryName(current); }
        }
        public static bool IsX64Pe(string path, bool requireDll)
        {
            using (var file = File.OpenRead(path)) using (var reader = new BinaryReader(file)) {
                if (file.Length < 128 || reader.ReadUInt16() != 0x5a4d) return false;
                file.Position = 0x3c; int offset = reader.ReadInt32(); if (offset < 64 || offset > file.Length - 26) return false;
                file.Position = offset; if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664) return false;
                file.Position = offset + 22; ushort characteristics = reader.ReadUInt16();
                file.Position = offset + 24; return reader.ReadUInt16() == 0x20b && (!requireDll || (characteristics & 0x2000) != 0);
            }
        }
    }
}



