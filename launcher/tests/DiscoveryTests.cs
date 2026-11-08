using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace MfgLauncher
{
    public static class DiscoveryTests
    {
        static string Q(string text) { return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
        static string Manifest(string id,string name,string folder) { return "\"AppState\" { \"appid\" " + Q(id) + " \"name\" " + Q(name) + " \"installdir\" " + Q(folder) + " }"; }
        public static void Run(Action<bool,string> assert)
        {
            string temp=Path.Combine(Path.GetTempPath(),"MfgDiscoveryTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            try {
                string first=Path.Combine(temp,"Steam"),second=Path.Combine(temp,"OtherLibrary");
                string modern="// header\n\"libraryfolders\" { \"0\" { \"path\" "+Q(first)+" \"apps\" { \"10\" \"20\" } } \"1\" { \"path\" "+Q(second)+" } }";
                var paths=Discovery.ParseLibraryFolders(modern); assert(paths.Count==2 && paths.Contains(first)&&paths.Contains(second),"Modern VDF parses multiple library drives");
                var old=Discovery.ParseLibraryFolders("\"LibraryFolders\" { \"TimeNextStatsReport\" \"0\" \"1\" "+Q(second)+" }"); assert(old.Count==1&&old[0]==second,"Legacy VDF parses paths and ignores metadata");
                var parsed=Vdf.Parse("\"root\" { \"quoted\" \"a\\\"b\" \"path\" \"C:\\new\\test\" }"); assert(Vdf.Text(Vdf.Object(parsed,"root"),"quoted")=="a\"b"&&Vdf.Text(Vdf.Object(parsed,"root"),"path")==@"C:\new\test","VDF quotes and single-backslash Windows paths parse safely");
                bool malformed=false; try{Vdf.Parse("\"x\" { \"y\" \"z\"");}catch(InvalidDataException){malformed=true;} assert(malformed,"Malformed VDF rejected");
                bool traversal=false; try{Discovery.ParseManifest(Manifest("10","Escape","..\\escape"),first,"appmanifest_10.acf");}catch(InvalidDataException){traversal=true;} assert(traversal,"Manifest traversal rejected");
                bool mismatch=false; try{Discovery.ParseManifest(Manifest("11","Wrong ID","Game"),first,"appmanifest_10.acf");}catch(InvalidDataException){mismatch=true;} assert(mismatch,"Manifest filename ID mismatch rejected");
                string a=Path.Combine(first,"steamapps"),b=Path.Combine(second,"steamapps"); Directory.CreateDirectory(a);Directory.CreateDirectory(b);File.WriteAllText(Path.Combine(a,"libraryfolders.vdf"),modern);
                string root=Path.Combine(b,"common","FixtureGame"),runtime=Path.Combine(root,"Project","Binaries","Win64","FixtureGame-Win64-Shipping.exe");Directory.CreateDirectory(Path.GetDirectoryName(runtime));File.WriteAllText(runtime,"fixture only");File.WriteAllText(Path.Combine(root,"Launcher.exe"),"fixture only");File.WriteAllText(Path.Combine(Path.GetDirectoryName(runtime),"sl.dlss_g.dll"),"fixture only");File.WriteAllText(Path.Combine(b,"appmanifest_10.acf"),Manifest("10","Fixture Game","FixtureGame"));File.WriteAllText(Path.Combine(b,"appmanifest_20.acf"),Manifest("20","Missing Game","Missing"));
                string[] softwareIds = { "1812620", "993090", "228980" }; foreach (string id in softwareIds) { string softwareRoot = Path.Combine(b, "common", "Utility-" + id); Directory.CreateDirectory(softwareRoot); File.WriteAllText(Path.Combine(softwareRoot, "utility.exe"), "fixture only"); File.WriteAllText(Path.Combine(b, "appmanifest_" + id + ".acf"), Manifest(id, "Renamed utility " + id, "Utility-" + id)); }
                var result=Discovery.DiscoverAtLibraries(new[]{first,first,second},new[]{new ScanRoot{Path=root}},null,CancellationToken.None);assert(result.Libraries.Count==2&&result.Games.Count==1&&result.Games[0].SteamId=="10","Steam/custom duplicate roots deduplicate and retain Steam metadata");assert(result.Errors.Any(e=>e.Contains("Missing Game")),"Missing Steam installs reported");assert(!result.Games.Any(g => softwareIds.Contains(g.SteamId)), "Steam software and shared redistributables excluded by app ID"); var softwareAgain = Discovery.DiscoverAtLibraries(new[] { second }, new[] { new ScanRoot { Path = Path.Combine(b, "common", "Utility-1812620") } }, null, CancellationToken.None); assert(softwareAgain.Games.Count == 1 && softwareAgain.Games[0].SteamId == "10", "Custom roots do not reintroduce excluded Steam software"); var game=result.Games[0];assert(String.Equals(game.SelectedExe,runtime,StringComparison.OrdinalIgnoreCase),"Nested Unreal runtime preferred to launcher");assert(game.Files.Any(f=>f.EndsWith("sl.dlss_g.dll"))&&!game.ScanIncomplete,"Nested FG evidence found in bounded scan");
                string parent=Path.Combine(temp,"CustomGames");foreach(string n in new[]{"One","Two"}){Directory.CreateDirectory(Path.Combine(parent,n));File.WriteAllText(Path.Combine(parent,n,n+".exe"),"fixture only");}var children=Discovery.DiscoverAtLibraries(new string[0],new[]{new ScanRoot{Path=parent,IsParent=true}},null,CancellationToken.None);assert(children.Games.Count==2&&children.Games.All(g=>g.SelectedExe!=null),"Parent roots discover separate child games");
                string amb=Path.Combine(temp,"Ambiguous");Directory.CreateDirectory(amb);File.WriteAllText(Path.Combine(amb,"One.exe"),"fixture only");File.WriteAllText(Path.Combine(amb,"Two.exe"),"fixture only");var ambiguous=new Game{Name="Ambiguous",Root=amb};Discovery.ScanGame(ambiguous,CancellationToken.None);assert(ambiguous.SelectedExe==null&&ambiguous.Executables.Count==2,"Ambiguous executables remain unresolved");
                string witcherRoot=Path.Combine(temp,"Witcher");string dx12=Path.Combine(witcherRoot,"bin","x64_dx12","witcher3.exe");Directory.CreateDirectory(Path.GetDirectoryName(dx12));File.WriteAllText(dx12,"fixture");File.WriteAllText(Path.Combine(Path.GetDirectoryName(dx12),"witcher3.backup.exe"),"fixture");string legacy=Path.Combine(witcherRoot,"bin","x64","witcher3.exe");Directory.CreateDirectory(Path.GetDirectoryName(legacy));File.WriteAllText(legacy,"fixture");var witcher=new Game{Name="The Witcher 3",Root=witcherRoot};Discovery.ScanGame(witcher,CancellationToken.None);assert(witcher.SelectedExe==dx12,"Witcher selects exact DX12 runtime over legacy and backup executables");
                var missing=Discovery.DiscoverAtLibraries(new string[0],new[]{new ScanRoot{Path=Path.Combine(temp,"Absent")}},null,CancellationToken.None);assert(missing.Errors.Count==1&&missing.Games.Count==0,"Missing added roots reported");using(var cts=new CancellationTokenSource()){cts.Cancel();bool cancelled=false;try{Discovery.DiscoverAtLibraries(new[]{first},new ScanRoot[0],null,cts.Token);}catch(OperationCanceledException){cancelled=true;}assert(cancelled,"Discovery cancellation honored");}
            }finally{if(Util.IsWithin(Path.GetTempPath(),temp)&&Path.GetFileName(temp).StartsWith("MfgDiscoveryTests-",StringComparison.Ordinal))Directory.Delete(temp,true);}
        }
    }
}


