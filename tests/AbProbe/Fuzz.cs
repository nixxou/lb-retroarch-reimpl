using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace AbProbe;

// Fuzzing leger et deterministe (graines fixes) : retroarch.cfg et noms de fichiers de saves. Le juge est l'egalite
// des deux greffons, pas la matrice.
public static class Fuzz
{
    private const string SectionCfg = "F1. Fuzzing du retroarch.cfg";
    private const string SectionNames = "F2. Fuzzing des noms de saves";
    private const int CfgCount = 60;
    private const int NameCount = 60;

    private static readonly string[] Keys =
    {
        "savefile_directory", "savestate_directory", "system_directory", "savefiles_in_content_dir",
        "savestates_in_content_dir", "sort_savefiles_enable", "sort_savestates_enable", "sort_savefiles_by_content_enable",
        "sort_savestates_by_content_enable", "Savefile_Directory", "system_directory_extra", "savefile_directory_x",
        "video_driver", "cheevos_enable",
    };

    private static readonly string[] DirValues =
    {
        ":\\saves", ":\\saves2", ":\\states", "default", ":\\", ":\\..\\savesUp", "{T}\\RA\\saves", "", "x=y", ":\\säves",
        ":\\saves ", "­:\\saves", ":/saves", "{T}\\RA\\states", ":\\saves\\snes", "  :\\saves", ":\\éè 🎮",
    };

    private static readonly string[] BoolValues = { "true", "false", "TRUE", " true", "true ", "1", "" };

    private static readonly string[] Seps = { " = ", "=", "  =  ", " =", "= ", "\t=\t", " == " };

    private static readonly string[] Prefixes = { "", "", "", "", " ", "\t", "­", "﻿", "​" };

    private static readonly string[] Junk =
    {
        "", "# commentaire", "#include \"x.cfg\"", "garbage", "é = \"è\"", "k = \"🎮\"", "a = 6", "menu_x =",
        "a = \"x\" # c", "audio_latency = 64", "=\"v\"", " = ", "\"quoted key\" = \"v\"", "k = \"\"\"\"", "k = 'ç'",
    };

    public static IEnumerable<Case> All()
    {
        for (int i = 0; i < CfgCount; i++)
        {
            yield return CfgCase(i);
        }

        for (int i = 0; i < NameCount; i++)
        {
            yield return NameCase(i);
        }
    }

    private static string Quote(Random r, string v)
    {
        switch (r.Next(10))
        {
            case 0: return v;
            case 1: return "'" + v + "'";
            case 2: return "\"" + v;
            case 3: return "“" + v + "”";
            case 4: return "\"" + v + "\" ";
            default: return "\"" + v + "\"";
        }
    }

    private static string RandomLine(Random r)
    {
        if (r.Next(6) == 0)
        {
            return Junk[r.Next(Junk.Length)];
        }

        string key = Keys[r.Next(Keys.Length)];
        string value = key.Contains("dir", StringComparison.Ordinal) && !key.Contains("in_content", StringComparison.Ordinal)
            ? DirValues[r.Next(DirValues.Length)]
            : BoolValues[r.Next(BoolValues.Length)];
        return Prefixes[r.Next(Prefixes.Length)] + key + Seps[r.Next(Seps.Length)] + Quote(r, value);
    }

    private static Case CfgCase(int index)
    {
        var r = new Random(1000 + index);
        var lines = Env.StandardCfg.ToList();
        if (r.Next(3) == 0)
        {
            lines = lines.OrderBy(_ => r.Next()).ToList();
        }

        if (r.Next(4) == 0)
        {
            lines.RemoveAt(r.Next(lines.Count));
        }

        // Activer quelques options de facon valide pour atteindre les sous-dossiers.
        foreach (string key in Keys.Take(9))
        {
            if (key.Contains("_enable", StringComparison.Ordinal) || key.Contains("in_content", StringComparison.Ordinal))
            {
                if (r.Next(4) == 0)
                {
                    int i = lines.FindIndex(l => l.StartsWith(key + " =", StringComparison.Ordinal));
                    if (i >= 0)
                    {
                        lines[i] = key + " = \"true\"";
                    }
                }
            }
        }

        int extra = r.Next(0, 5);
        for (int k = 0; k < extra; k++)
        {
            lines.Insert(r.Next(lines.Count + 1), RandomLine(r));
        }

        bool bom = r.Next(5) == 0;
        string newline = r.Next(5) == 0 ? "\n" : "\r\n";
        int slot = r.Next(-2, 5);
        string input = (bom ? "[BOM] " : "") + (newline == "\n" ? "[LF] " : "") + "cfg = " +
                       string.Join(" / ", lines.Select(Dumper.Q)) + $" ; GetSaves([G]), AddSaveFile(x.srm et state slot {slot}), BIOS 4do";

        return new Case($"FZC{index:00}", SectionCfg, input, e =>
        {
            e.Cfg = lines.Select(l => l.Replace("{T}", e.T, StringComparison.Ordinal)).ToList();
            e.CfgBom = bom;
            e.CfgNewline = newline;
            foreach (string f in new[]
                     {
                         "RA\\saves\\Game.srm", "RA\\saves2\\Game.srm", "RA\\säves\\Game.srm", "savesUp\\Game.srm",
                         "RA\\states\\Game.state", "RA\\states\\Game.state1", "RA\\saves\\snes\\Game.srm",
                         "RA\\saves\\Snes9x\\Game.srm", "RA\\saves\\snes\\Snes9x\\Game.srm", "default\\Game.srm",
                         "roms\\snes\\Game.srm", "roms\\snes\\Game.state2", "RA\\Game.srm", "RA\\states\\Snes9x\\Game.state3",
                     })
            {
                e.Put(f, "F:" + f);
            }

            e.Put("src\\x.srm", "NEW");
            e.Put("src\\s.st", "ST");
        }, (e, p, s) => new Rec
        {
            { "saves", Caught.Try(() => e.Saves(p)) },
            { "addGame", Caught.Try(() => e.Add(p, Env.SG("g1", e.F("src\\x.srm")), 1)) },
            { "addState", Caught.Try(() => e.Add(p, Env.SS("g1", e.F("src\\s.st"), slot), 1)) },
            { "bios", Caught.Try(() => p.GetBiosFilesForPlatform(e.F("RA\\retroarch.exe"), "Panasonic 3DO", "-L \"cores\\4do_libretro.dll\"")) },
            { "systemFolder", s.GetSystemFolder() },
        });
    }

    private static readonly string[] Bases =
    {
        "Game", "game", "GAME", "Game (Rev 1)", "Game ", "Gamé", "Gam", "Game  x", "Game_ä", "Game.state", "Game.bcr",
        " Game", "Game­", "Gamex",
    };

    private static readonly string[] Exts =
    {
        ".srm", ".SRM", ".mcr", ".sav", ".rtc", "", ".bcr", ".BKR", ".bkr", ".smpc", ".state", ".state1", ".state.auto",
        ".state-2", ".state03", ".state.state3", ".state1.png", ".STATE", ".state 4", ".state+5", ".state٣",
        ".staté", ".state­1", ".bcr.bak", ".state.AUTO", ".state2147483648", ".state -1", ".Smpc",
    };

    private static Case NameCase(int index)
    {
        var r = new Random(5000 + index);
        bool saturn = r.Next(2) == 0;
        bool sameDir = r.Next(4) == 0;
        var files = new List<string>();
        int count = r.Next(1, 7);
        for (int k = 0; k < count; k++)
        {
            string name = Bases[r.Next(Bases.Length)] + Exts[r.Next(Exts.Length)];
            if (name.EndsWith(" ", StringComparison.Ordinal) || name.EndsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            string dir = r.Next(3) == 0 ? "states" : "saves";
            files.Add(dir + "\\" + name);
        }

        string srcName = Bases[r.Next(Bases.Length)].Trim() + Exts[r.Next(Exts.Length)];
        bool srcState = r.Next(3) == 0;
        int? slot = r.Next(4) == 0 ? null : r.Next(-2, 12);
        string input = (saturn ? "jeu Saturn Game.cue" : "jeu SNES G") + (sameDir ? ", states = saves" : "") +
                       " ; fichiers " + string.Join(", ", files.Select(Dumper.Q)) +
                       $" ; AddSaveFile({(srcState ? "state slot " + (slot?.ToString() ?? "null") : "game")} {Dumper.Q(srcName)} avec voisins .bcr/.bkr)";

        return new Case($"FZN{index:00}", SectionNames, input, e =>
        {
            if (sameDir)
            {
                e.CfgSet("savestate_directory", "savestate_directory = \":\\saves\"");
            }

            if (saturn)
            {
                e.GSf.P["ApplicationPath"] = e.F("roms\\saturn\\Game.cue");
                e.Put("roms\\saturn\\Game.cue", "CUE");
            }

            foreach (string f in files)
            {
                e.Put("RA\\" + f, "N:" + f);
            }

            e.Put("src\\" + srcName, "SRC");
            string stem = System.IO.Path.GetFileNameWithoutExtension(srcName);
            e.Put("src\\" + stem + ".bkr", "SRCBKR");
            e.Put("src\\" + stem + ".smpc", "SRCSMPC");
        }, (e, p, s) =>
        {
            IGame game = saturn ? e.GS : e.G;
            string core = saturn ? "mednafen_saturn_libretro" : "snes9x_libretro";
            var rec = new Rec { { "saves", Caught.Try(() => e.Saves(p, new[] { game })) } };
            foreach (string f in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string path = e.F("RA\\" + f);
                rec.Add("secondary " + f, Caught.Try(() => p.IsSecondarySaveFile(path)));
                rec.Add("companions " + f, Caught.Try(() => p.GetCompanionSaveFiles(path)));
                rec.Add("group " + f, Caught.Try(() =>
                {
                    bool ok = p.TryGetSaveGroupInfo(new GameSaveGame { FileLocation = path, EmulatorCore = core }, null, out string id, out string name);
                    return new Rec { { "ok", ok }, { "id", id }, { "name", name } };
                }));
                rec.Add("signature " + f, Caught.Try(() =>
                {
                    bool ok = p.TryComputeSaveSignature(new GameSaveGame { FileLocation = path, EmulatorCore = core }, null, out string sig);
                    return new Rec { { "ok", ok }, { "sig", sig } };
                }));
            }

            GameSaveBase src = srcState
                ? Env.SS(saturn ? "gs" : "g1", e.F("src\\" + srcName), slot)
                : Env.SG(saturn ? "gs" : "g1", e.F("src\\" + srcName));
            rec.Add("add", Caught.Try(() => e.Add(p, src, 1)));
            rec.Add("savesAfterAdd", Caught.Try(() => e.Saves(p, new[] { game })));
            return rec;
        });
    }
}
