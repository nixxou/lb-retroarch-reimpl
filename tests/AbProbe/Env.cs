using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace AbProbe;

// ---------------------------------------------------------------------------------------------------------------
// Montage commun de la matrice (section 0), recree a neuf pour chaque cas et chaque greffon :
//   W = dossier du cas ; T = W\T (dossier de travail, repertoire courant) ; L = W\lbroot (NamingHelper.RootFolder) ;
//   W\data = DataDirectory du greffon ; W\plugin = InstallDirectory.
// ---------------------------------------------------------------------------------------------------------------
public sealed class Env
{
    public const string Snes = "Super Nintendo Entertainment System";
    public const string Snes9xLine = "-L \"cores\\snes9x_libretro.dll\"";
    public const string MgbaLine = "-L \"cores\\mgba_libretro.dll\"";
    public const string SaturnLine = "-L \"cores\\mednafen_saturn_libretro.dll\"";

    public static readonly string[] StandardCfg =
    {
        "savefile_directory = \":\\saves\"",
        "savestate_directory = \":\\states\"",
        "system_directory = \":\\system\"",
        "savefiles_in_content_dir = \"false\"",
        "savestates_in_content_dir = \"false\"",
        "sort_savefiles_enable = \"false\"",
        "sort_savestates_enable = \"false\"",
        "sort_savefiles_by_content_enable = \"false\"",
        "sort_savestates_by_content_enable = \"false\"",
    };

    public Env(string w, string fakeSource)
    {
        W = w;
        T = Path.Combine(w, "T");
        L = Path.Combine(w, "lbroot");
        Data = Path.Combine(w, "data");
        Inst = Path.Combine(w, "plugin");
        FakeSource = fakeSource;
        Directory.CreateDirectory(T);
        Directory.CreateDirectory(L);
        Directory.CreateDirectory(Inst);
        Cwd = T;
        Root = L;

        Put("RA\\retroarch.exe", "");
        Put("RA\\info\\snes9x_libretro.info",
            "display_name = \"Nintendo - SNES / SFC (Snes9x - Current)\"\r\ncorename = \"Snes9x\"\r\n");
        Put("roms\\snes\\Game.sfc", "ROM");
        Put("roms\\saturn\\Panzer.cue", "CUE");
        Dir("RA\\saves");
        Dir("RA\\states");
        Cfg = StandardCfg.ToList();

        PathsF = Fake.Of(Paths = Fake.Make<IPluginPaths>("paths"));
        PathsF.P["DataDirectory"] = Data;
        PathsF.P["InstallDirectory"] = Inst;

        EMU = NewEmulator("EMU", "emu1", F("RA\\retroarch.exe"));
        EmuPlatforms.Add(NewPlatform("emu1", Snes, Snes9xLine));
        EMUf.M["GetAllEmulatorPlatforms"] = _ => EmuPlatformsNull ? null : EmuPlatforms.ToArray();
        Emus["emu1"] = EMU;

        G = NewGame("G", "g1", F("roms\\snes\\Game.sfc"), Snes, Snes9xLine);
        GApps = new List<IAdditionalApplication>();
        Fake.Of(G).M["GetAllAdditionalApplications"] = _ => GApps?.ToArray();
        Games["g1"] = G;

        GS = NewGame("GS", "gs", F("roms\\saturn\\Panzer.cue"), "Sega Saturn", SaturnLine);
        Games["gs"] = GS;

        var dmProxy = Fake.Make<IDataManager>("DataManager");
        Fake.Of(dmProxy).M["GetGameById"] = a => a[0] is string id && Games.TryGetValue(id, out var g) ? g : null;
        Fake.Of(dmProxy).M["GetEmulatorById"] = a => a[0] is string id && Emus.TryGetValue(id, out var e) ? e : null;
        DM = dmProxy;
    }

    public string W { get; }

    public string T { get; }

    public string L { get; }

    public string Data { get; }

    public string Inst { get; }

    public string FakeSource { get; }

    // Etat applique juste avant l'appel.
    public string Cwd { get; set; }

    public string Root { get; set; }

    public string SystemFolderInit { get; set; } = "system";

    // retroarch.cfg : ecrit par Finish() (UTF-8 sans BOM, CRLF, saut final), sauf NoCfg.
    public List<string> Cfg { get; set; }

    public bool NoCfg { get; set; }

    public bool CfgBom { get; set; }

    public string CfgNewline { get; set; } = "\r\n";

    public IPluginPaths Paths { get; }

    public Fake PathsF { get; }

    public IEmulator EMU { get; }

    public Fake EMUf => Fake.Of(EMU);

    public List<IEmulatorPlatform> EmuPlatforms { get; } = new();

    public bool EmuPlatformsNull { get; set; }

    public IGame G { get; }

    public Fake Gf => Fake.Of(G);

    public List<IAdditionalApplication> GApps { get; set; }

    public IGame GS { get; }

    public Fake GSf => Fake.Of(GS);

    public IAdditionalApplication A { get; private set; }

    public Fake Af => Fake.Of(A);

    public Dictionary<string, IGame> Games { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, IEmulator> Emus { get; } = new(StringComparer.Ordinal);

    public IDataManager DM { get; }

    // PluginHelper.DataManager laisse a null pendant l'appel (E6).
    public bool DataManagerNull { get; set; }

    // Actions jouees apres l'ecriture du cfg et la fixation des dates (verrous, lecture seule...).
    public List<Action> AfterFinish { get; } = new();

    public List<IDisposable> Locks { get; } = new();

    // Lecteur jetable (subst) : lettre libre choisie au montage, retiree apres l'appel ; rendue "X" dans le rapport.
    public char? Drive { get; private set; }

    public string DriveRoot => Drive + ":\\";

    public List<Action> Cleanup { get; } = new();

    public void Subst(string rel)
    {
        Dir(rel);
        var used = new HashSet<char>(DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])));
        char letter = "XWVUTSRQPONMLKJ".First(c => !used.Contains(c));
        RunSubst($"{letter}: \"{F(rel)}\"");
        Drive = letter;
        Cleanup.Add(() => RunSubst($"{letter}: /D"));
    }

    public static string RunSubst(string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("subst.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = System.Diagnostics.Process.Start(psi);
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    public string F(string rel) => Path.Combine(T, rel);

    public void Put(string rel, string content) => PutBytes(rel, new UTF8Encoding(false).GetBytes(content));

    public void PutBytes(string rel, byte[] content)
    {
        string path = F(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, content);
    }

    public void Dir(string rel) => Directory.CreateDirectory(F(rel));

    public void Del(string rel)
    {
        string path = F(rel);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    public void CfgSet(string key, string line)
    {
        int i = Cfg.FindIndex(l => l.StartsWith(key + " =", StringComparison.Ordinal));
        if (i < 0)
        {
            Cfg.Add(line);
        }
        else
        {
            Cfg[i] = line;
        }
    }

    public void CfgOn(string key) => CfgSet(key, key + " = \"true\"");

    public void CfgRemove(string key) => Cfg.RemoveAll(l => l.StartsWith(key + " =", StringComparison.Ordinal));

    public void SetEff(object proxy, string line) => Fake.Of(proxy).M["GetEffectiveCommandLine"] = _ => line;

    public IEmulator NewEmulator(string label, string id, string path)
    {
        var e = Fake.Make<IEmulator>(label);
        var f = Fake.Of(e);
        f.P["Id"] = id;
        f.P["Title"] = "RetroArch";
        f.P["ApplicationPath"] = path;
        f.P["CommandLine"] = "";
        f.M["GetAllEmulatorPlatforms"] = _ => Array.Empty<IEmulatorPlatform>();
        return e;
    }

    public IEmulatorPlatform NewPlatform(string emulatorId, string platform, string commandLine)
    {
        var p = Fake.Make<IEmulatorPlatform>("EP:" + platform);
        var f = Fake.Of(p);
        f.P["EmulatorId"] = emulatorId;
        f.P["Platform"] = platform;
        f.P["CommandLine"] = commandLine;
        return p;
    }

    public IGame NewGame(string label, string id, string path, string platform, string effective)
    {
        var g = Fake.Make<IGame>(label);
        var f = Fake.Of(g);
        f.P["Id"] = id;
        f.P["ApplicationPath"] = path;
        f.P["CommandLine"] = "";
        f.P["Platform"] = platform;
        f.P["EmulatorId"] = "emu1";
        f.M["GetEffectiveCommandLine"] = _ => effective;
        f.M["GetAllAdditionalApplications"] = _ => Array.Empty<IAdditionalApplication>();
        return g;
    }

    // Application additionnelle A de la section 6.
    public IAdditionalApplication MakeA(string rel = "roms\\snes\\Game (Disc 2).sfc", bool createFile = true)
    {
        A = Fake.Make<IAdditionalApplication>("A");
        Af.P["Id"] = "a1";
        Af.P["GameId"] = "g1";
        Af.P["ApplicationPath"] = rel == null ? null : F(rel);
        Af.P["CommandLine"] = "";
        Af.P["EmulatorId"] = "emu1";
        Af.M["GetEffectiveCommandLine"] = _ => Snes9xLine;
        if (createFile && rel != null)
        {
            Put(rel, "ROM2");
        }

        return A;
    }

    // Faux retroarch.exe (cas V) dans T\fake.
    public string MakeFakeExe(string stdout, string stderr = null)
    {
        Dir("fake");
        foreach (string file in Directory.GetFiles(FakeSource))
        {
            File.Copy(file, F("fake\\" + Path.GetFileName(file)));
        }

        if (stdout != null)
        {
            PutBytes("fake\\out.bin", new UTF8Encoding(false).GetBytes(stdout));
        }

        if (stderr != null)
        {
            PutBytes("fake\\err.bin", new UTF8Encoding(false).GetBytes(stderr));
        }

        return F("fake\\retroarch.exe");
    }

    public void LockFile(string path, FileShare share, FileAccess access = FileAccess.ReadWrite)
        => AfterFinish.Add(() => Locks.Add(new FileStream(path, FileMode.Open, access, share)));

    public void Finish()
    {
        if (!NoCfg)
        {
            var text = new StringBuilder();
            foreach (string line in Cfg)
            {
                text.Append(line).Append(CfgNewline);
            }

            byte[] body = new UTF8Encoding(false).GetBytes(text.ToString());
            if (CfgBom)
            {
                body = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray();
            }

            PutBytes("RA\\retroarch.cfg", body);
        }

        foreach (string file in Directory.GetFiles(W, "*", SearchOption.AllDirectories))
        {
            File.SetCreationTimeUtc(file, Dumper.Fixed);
            File.SetLastWriteTimeUtc(file, Dumper.Fixed);
            File.SetLastAccessTimeUtc(file, Dumper.Fixed);
        }

        foreach (Action action in AfterFinish)
        {
            action();
        }
    }

    // ---- appels courants ----

    public object Saves(EmulatorPlugin p, IGame[] games = null, IAdditionalApplication[] apps = null, bool noGames = false, IEmulator emulator = null, bool nullEmulator = false)
        => p.GetSaves(new GetSavesArgs
        {
            Emulator = nullEmulator ? null : emulator ?? EMU,
            Games = noGames ? null : games ?? new[] { G },
            AdditionalApplications = apps,
        });

    public object Add(EmulatorPlugin p, GameSaveBase save, int overwrite = -1)
    {
        int calls = 0;
        Func<bool> func = overwrite < 0 ? null : () =>
        {
            calls++;
            return overwrite == 1;
        };
        object response = Caught.Try(() => p.AddSaveFile(new AddSaveArgs { SaveToAdd = save, ShouldOverwriteFunc = func }));
        return new Rec { { "response", response }, { "overwriteCalls", calls }, { "sourceAfter", save } };
    }

    // Les proprietes de GameSaveBase sont en init : la sonde les ecrit par reflexion apres coup.
    public static void Set(object target, string property, object value)
        => target.GetType().GetProperty(property).SetValue(target, value);

    public static GameSaveGame SG(string gameId, string path) => new() { GameId = gameId, FileLocation = path };

    public static GameSaveState SS(string gameId, string path, int? slot) => new() { GameId = gameId, FileLocation = path, Slot = slot };
}
