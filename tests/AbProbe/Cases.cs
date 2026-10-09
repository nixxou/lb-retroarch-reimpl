using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace AbProbe;

// Cas de docs\test-matrix.md (hors [LB] et section 13). Les entrees reprennent la matrice ; seule l'egalite
// original/clone est jugee.
public static class Cases
{
    private const string S1 = "1. Membres simples";
    private const string S2 = "2. Lecture du cfg pour les saves";
    private const string S3 = "3. Sous-dossiers de tri";
    private const string S4 = "4. Enumeration des saves";
    private const string S5 = "5. Enumeration des savestates";
    private const string S6 = "6. Applications additionnelles";
    private const string S7 = "7. Saturn dans GetSaves";
    private const string S8 = "8. AddSaveFile";
    private const string S9 = "9. AddSaveFile en contexte Saturn";
    private const string S10 = "10. Compagnons, groupes, signatures";
    private const string S11 = "11. BIOS";
    private const string S12 = "12. Version, coeurs, RetroAchievements";

    private const string Shy = "\u00AD";

    public static List<(string Id, string Reason)> NotPlayed()
    {
        var list = new List<(string, string)>();
        foreach (string id in new[] { "S16", "R10", "R15", "B01", "B02", "B12", "B13", "B14", "B25", "B27", "K03", "K04", "K05", "K06", "K07", "K10" })
        {
            list.Add((id, "[LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde)"));
        }

        for (int i = 1; i <= 41; i++)
        {
            list.Add(($"N{i:00}", "section 13 : jouee seulement par l'option --network (rapport last-run-network.md)"));
        }

        return list;
    }

    private static object R(params Func<object>[] calls) => calls.Select(Caught.Try).ToList();

    private static Case C(string id, string section, string input, Action<Env> setup, Func<Env, EmulatorPlugin, object> act)
        => new(id, section, input, setup, (e, p, s) => act(e, p));

    private static IEmulatorWithCores Cores(EmulatorPlugin p) => (IEmulatorWithCores)p;

    public static IEnumerable<Case> All()
    {
        // ---------------- 1. Membres simples ----------------
        yield return new Case("S01", S1, "new RetroArchPlugin(null)", null, (e, p, s) => s.Create(null)) { NoPlugin = true };
        yield return C("S02", S1, "EmulatorName", null, (e, p) => p.EmulatorName);
        yield return C("S03", S1, "SupportsSaveManagement()", null, (e, p) => p.SupportsSaveManagement());
        yield return C("S04", S1, "GetPotentialSaveSlots()", null, (e, p) => p.GetPotentialSaveSlots());
        yield return C("S05", S1, "GetImportActions() puis Add, Clear, Insert, Remove, RemoveAt, ecriture et lecture par indice (IList et IList<T>), IsReadOnly, IsFixedSize, IsSynchronized, seconde instance", null, (e, p) =>
        {
            var actions = p.GetImportActions();
            var l = actions as IList;
            var g = actions as IList<EmulatorImportAction>;
            object first = l != null && l.Count > 0 ? l[0] : null;
            var other = new BooleanEmulatorImportAction("x", "x", false);
            return new Rec
            {
                { "actions", actions },
                { "IList", l != null },
                { "IList<T>", g != null },
                { "IList.Add", Caught.Try(() => l.Add(other)) },
                { "IList.Clear", Caught.Try(() => { l.Clear(); return "ok"; }) },
                { "IList.Insert", Caught.Try(() => { l.Insert(0, other); return "ok"; }) },
                { "IList.Remove", Caught.Try(() => { l.Remove(first); return "ok"; }) },
                { "IList.RemoveAt", Caught.Try(() => { l.RemoveAt(0); return "ok"; }) },
                { "IList[0]=", Caught.Try(() => { l[0] = other; return "ok"; }) },
                { "IList[1]", Caught.Try(() => l[1]) },
                { "IList[-1]", Caught.Try(() => l[-1]) },
                { "IList.IsReadOnly", Caught.Try(() => l.IsReadOnly) },
                { "IList.IsFixedSize", Caught.Try(() => l.IsFixedSize) },
                { "IList.IsSynchronized", Caught.Try(() => l.IsSynchronized) },
                { "IList<T>.Add", Caught.Try(() => { g.Add(other); return "ok"; }) },
                { "IList<T>.Clear", Caught.Try(() => { g.Clear(); return "ok"; }) },
                { "IList<T>.Insert", Caught.Try(() => { g.Insert(0, other); return "ok"; }) },
                { "IList<T>.Remove", Caught.Try(() => g.Remove((EmulatorImportAction)first)) },
                { "IList<T>.RemoveAt", Caught.Try(() => { g.RemoveAt(0); return "ok"; }) },
                { "IList<T>[0]=", Caught.Try(() => { g[0] = other; return "ok"; }) },
                { "IList<T>[1]", Caught.Try(() => g[1]) },
                { "IList<T>[-1]", Caught.Try(() => g[-1]) },
                { "ICollection<T>.IsReadOnly", Caught.Try(() => g.IsReadOnly) },
                { "after", actions },
                { "secondCallSameInstance", ReferenceEquals(actions, p.GetImportActions()) },
            };
        });
        yield return C("S06", S1, "GetApplicableEmulators(null)", null, (e, p) => p.GetApplicableEmulators(null));
        yield return C("S07", S1, "GetApplicableEmulators([EMU, null, e(\"\"), e(T\\x\\RetroArch.EXE), e(T\\x\\retroarch_debug.exe), e(retroarch.exe)])", null, (e, p) =>
            p.GetApplicableEmulators(new[]
            {
                e.EMU, null, e.NewEmulator("e-vide", "x1", ""), e.NewEmulator("e-RetroArch.EXE", "x2", e.F("x\\RetroArch.EXE")),
                e.NewEmulator("e-debug", "x3", e.F("x\\retroarch_debug.exe")), e.NewEmulator("e-nu", "x4", "retroarch.exe"),
                e.NewEmulator("e-null", "x5", null),
            }));
        yield return C("S08", S1, "IsSecondarySaveFile sur null, \"\", a.bcr, a.BKR, a.smpc, a.srm, a.bkr.bak, a", null, (e, p) =>
            R(() => p.IsSecondarySaveFile(null), () => p.IsSecondarySaveFile(""), () => p.IsSecondarySaveFile("a.bcr"),
              () => p.IsSecondarySaveFile("a.BKR"), () => p.IsSecondarySaveFile("a.smpc"), () => p.IsSecondarySaveFile("a.srm"),
              () => p.IsSecondarySaveFile("a.bkr.bak"), () => p.IsSecondarySaveFile("a"), () => p.IsSecondarySaveFile("  .bkr")));
        yield return C("S09", S1, "UseSaveGroupIdForPersistedMatch sur null, null, \"  \", saturn, SATURN-x, xsaturn", null, (e, p) =>
            R(() => p.UseSaveGroupIdForPersistedMatch(null),
              () => p.UseSaveGroupIdForPersistedMatch(new GameSaveGame { SaveGroupId = null }),
              () => p.UseSaveGroupIdForPersistedMatch(new GameSaveGame { SaveGroupId = "  " }),
              () => p.UseSaveGroupIdForPersistedMatch(new GameSaveGame { SaveGroupId = "saturn" }),
              () => p.UseSaveGroupIdForPersistedMatch(new GameSaveState { SaveGroupId = "SATURN-x" }),
              () => p.UseSaveGroupIdForPersistedMatch(new GameSaveGame { SaveGroupId = "xsaturn" }),
              () => p.UseSaveGroupIdForPersistedMatch(new GameSaveGame { SaveGroupId = " saturn" })));
        yield return C("S10", S1, "GetCompanionSaveFiles sur null, \"\", T\\s\\a.srm, a.bcr", null, (e, p) =>
            R(() => p.GetCompanionSaveFiles(null), () => p.GetCompanionSaveFiles(""), () => p.GetCompanionSaveFiles(e.F("s\\a.srm")),
              () => p.GetCompanionSaveFiles("a.bcr")));
        yield return C("S11", S1, "GetAllAvailableCores sur null, \"\", retroarch.exe", null, (e, p) =>
            R(() => Cores(p).GetAllAvailableCores(null), () => Cores(p).GetAllAvailableCores(""), () => Cores(p).GetAllAvailableCores("retroarch.exe")));
        yield return C("S12", S1, "GetSuggestedCores(\"ignored\", null) et (\"ignored\", \"  \")", null, (e, p) =>
            R(() => Cores(p).GetSuggestedCores("ignored", null), () => Cores(p).GetSuggestedCores("ignored", "  ")));
        yield return C("S13", S1, "hors LaunchBox, LocalDb non initialisee : GetBiosFilesForPlatform(\"Sega CD\")", null,
            (e, p) => p.GetBiosFilesForPlatform("Sega CD"));
        yield return C("S14", S1, "types : GetApplicableEmulators(null), GetPotentialSaveSlots(), GetCompanionSaveFiles(\"x.srm\"), GetAllAvailableCores(\"\"), GetSuggestedCores(\"\",\"\")", null, (e, p) =>
        {
            object cores = Caught.Try(() => Cores(p).GetAllAvailableCores(""));
            object suggested = Caught.Try(() => Cores(p).GetSuggestedCores("", ""));
            object companions = Caught.Try(() => p.GetCompanionSaveFiles("x.srm"));
            return new Rec
            {
                { "GetApplicableEmulators(null)", Caught.Try(() => p.GetApplicableEmulators(null)) },
                { "GetPotentialSaveSlots()", Caught.Try(() => p.GetPotentialSaveSlots()) },
                { "GetCompanionSaveFiles(x.srm)", companions },
                { "  est Array.Empty<string>()", ReferenceEquals(companions, Array.Empty<string>()) },
                { "GetAllAvailableCores(\"\")", cores },
                { "  est Array.Empty<string>()", ReferenceEquals(cores, Array.Empty<string>()) },
                { "GetSuggestedCores(\"\",\"\")", suggested },
                { "  est Array.Empty<string>() ", ReferenceEquals(suggested, Array.Empty<string>()) },
            };
        });
        yield return C("S15", S1, "types : GetCompanionSaveFiles avec dossier valide sans compagnon ; GetAllAvailableCores sur cores vide ; BIOS 3 param., coeur absent", e =>
        {
            e.Dir("RA\\cores");
            e.Put("s\\a.bcr", "B");
        }, (e, p) =>
        {
            object companions = Caught.Try(() => p.GetCompanionSaveFiles(e.F("s\\a.bcr")));
            object cores = Caught.Try(() => Cores(p).GetAllAvailableCores(e.F("RA\\retroarch.exe")));
            return new Rec
            {
                { "GetCompanionSaveFiles", companions },
                { "GetAllAvailableCores", cores },
                { "  est Array.Empty<string>()", ReferenceEquals(cores, Array.Empty<string>()) },
                { "GetBiosFilesForPlatform", Caught.Try(() => p.GetBiosFilesForPlatform(e.F("RA\\retroarch.exe"), "x", "-L \"cores\\inexistant_libretro.dll\"")) },
            };
        });

        // ---------------- 2. Lecture du cfg ----------------
        foreach (Case c in CfgCases())
        {
            yield return c;
        }

        // ---------------- 3. Sous-dossiers de tri ----------------
        foreach (Case c in SortCases())
        {
            yield return c;
        }

        // ---------------- 4. Enumeration des saves ----------------
        foreach (Case c in EnumCases())
        {
            yield return c;
        }

        // ---------------- 5. Savestates ----------------
        foreach (Case c in StateCases())
        {
            yield return c;
        }

        // ---------------- 6. Applications additionnelles ----------------
        foreach (Case c in AppCases())
        {
            yield return c;
        }

        // ---------------- 7. Saturn ----------------
        foreach (Case c in SaturnCases())
        {
            yield return c;
        }

        // ---------------- 8 et 9. AddSaveFile ----------------
        foreach (Case c in AddCases())
        {
            yield return c;
        }

        // ---------------- 10. Compagnons ----------------
        foreach (Case c in CompanionCases())
        {
            yield return c;
        }

        // ---------------- 11. BIOS ----------------
        foreach (Case c in BiosCases())
        {
            yield return c;
        }

        // ---------------- 12. Version, coeurs, RA ----------------
        foreach (Case c in MiscCases())
        {
            yield return c;
        }

        // ---------------- J. Ecarts du juge de code (docs\code-judge.md) ----------------
        foreach (Case c in JudgeCases())
        {
            yield return c;
        }
    }

    // Les cas de la section 2 ont tous T\RA\saves\Game.srm (sauf mention) et T\RA\states\Game.state1.
    private static Case Cfg(string id, string input, Action<Env> setup, bool srm = true)
        => C(id, S2, input, e =>
        {
            if (srm)
            {
                e.Put("RA\\saves\\Game.srm", "S");
            }

            e.Put("RA\\states\\Game.state1", "ST");
            setup?.Invoke(e);
        }, (e, p) => e.Saves(p));

    private static IEnumerable<Case> CfgCases()
    {
        yield return Cfg("C01", "cfg standard", null);
        yield return Cfg("C02", "pas de retroarch.cfg", e => e.NoCfg = true);
        yield return Cfg("C03", "cfg + ligne vide a la fin", e => e.Cfg.Add(""));
        yield return Cfg("C04", "cfg avec # commentaire en tete", e => e.Cfg.Insert(0, "# commentaire"));
        yield return Cfg("C05", "cfg avec #include \"x.cfg\"", e => e.Cfg.Add("#include \"x.cfg\""));
        yield return Cfg("C06", "cfg avec video_driver = \"\"", e => e.Cfg.Add("video_driver = \"\""));
        yield return Cfg("C07", "cfg avec audio_latency = 64", e => e.Cfg.Add("audio_latency = 64"));
        yield return Cfg("C08", "cfg avec audio_latency = 6", e => e.Cfg.Add("audio_latency = 6"));
        yield return Cfg("C09", "cfg avec menu_x =", e => e.Cfg.Add("menu_x ="));
        yield return Cfg("C10", "savefile_directory=\":\\saves\" sans espaces", e => e.CfgSet("savefile_directory", "savefile_directory=\":\\saves\""));
        yield return Cfg("C11", "savefile_directory en double, la 2e vaut \":\\saves2\", fichier dans saves2", e =>
        {
            e.Cfg.Add("savefile_directory = \":\\saves2\"");
            e.Put("RA\\saves2\\Game.srm", "S2");
        }, srm: false);
        yield return Cfg("C12", "Savefile_Directory = \":\\saves2\" en plus, fichier dans saves", e =>
        {
            e.Cfg.Add("Savefile_Directory = \":\\saves2\"");
            e.Dir("RA\\saves2");
        });
        yield return Cfg("C13", "savefile_directory retiree", e => e.CfgRemove("savefile_directory"));
        yield return Cfg("C14", "savefile_directory absolu T\\RA\\saves", e => e.CfgSet("savefile_directory", $"savefile_directory = \"{e.F("RA\\saves")}\""));
        yield return Cfg("C15", "savefile_directory = \"default\", cwd T, T\\default\\Game.srm", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \"default\"");
            e.Put("default\\Game.srm", "D");
        });
        yield return Cfg("C16", "savefile_directory = \"default\", sans T\\default", e => e.CfgSet("savefile_directory", "savefile_directory = \"default\""));
        yield return Cfg("C17", "EMU relatif RA\\retroarch.exe, cwd T, RootFolder=T", e =>
        {
            e.EMUf.P["ApplicationPath"] = "RA\\retroarch.exe";
            e.Root = e.T;
        });
        yield return Cfg("C18", "dossier saves absent", e => e.Del("RA\\saves"), srm: false);
        yield return Cfg("C19", "savefiles_in_content_dir = \"true\", T\\roms\\snes\\Game.srm", e =>
        {
            e.CfgOn("savefiles_in_content_dir");
            e.Put("roms\\snes\\Game.srm", "C");
        });
        yield return Cfg("C20", "savefiles_in_content_dir = \"true\", seul Game.sfc", e => e.CfgOn("savefiles_in_content_dir"));
        yield return Cfg("C21", "savefiles_in_content_dir = \"TRUE\"", e =>
        {
            e.CfgSet("savefiles_in_content_dir", "savefiles_in_content_dir = \"TRUE\"");
            e.Put("roms\\snes\\Game.srm", "C");
        });
        yield return Cfg("C22", "savefiles_in_content_dir = true (sans guillemets)", e =>
        {
            e.CfgSet("savefiles_in_content_dir", "savefiles_in_content_dir = true");
            e.Put("roms\\snes\\Game.srm", "C");
        });
        yield return Cfg("C23", "cfg UTF-8 avec BOM", e => e.CfgBom = true);
        yield return Cfg("C24", "savefile_directory = \"x=y\"", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \"x=y\"");
            e.Put("x=y\\Game.srm", "X");
        });
        yield return Cfg("C25", "savefile_directory = \":\\..\\savesUp\", T\\savesUp\\Game.srm", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \":\\..\\savesUp\"");
            e.Put("savesUp\\Game.srm", "U");
        });
        yield return Cfg("C26", "savefile_directory = \":\\\", T\\RA\\Game.srm", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \":\\\"");
            e.Put("RA\\Game.srm", "R");
        });
        yield return Cfg("C27", "savefile_directory = U+00AD puis :\\saves", e => e.CfgSet("savefile_directory", "savefile_directory = \"" + Shy + ":\\saves\""));
        yield return Cfg("C28", "EMU relatif, cwd T, RootFolder=L", e => e.EMUf.P["ApplicationPath"] = "RA\\retroarch.exe");
        yield return Cfg("C29", "EMU relatif, cwd = autre dossier, RootFolder=T", e =>
        {
            e.EMUf.P["ApplicationPath"] = "RA\\retroarch.exe";
            e.Dir("..\\other");
            e.Cwd = Path.Combine(e.W, "other");
            e.Root = e.T;
        });
        yield return Cfg("C30", "EMU relatif, cwd T, RootFolder=L, savefile/savestate absolus", e =>
        {
            e.EMUf.P["ApplicationPath"] = "RA\\retroarch.exe";
            e.CfgSet("savefile_directory", $"savefile_directory = \"{e.F("RA\\saves")}\"");
            e.CfgSet("savestate_directory", $"savestate_directory = \"{e.F("RA\\states")}\"");
        });
        yield return Cfg("C31", "EMU retroarch.exe sans dossier, cwd T\\RA, RootFolder=T\\RA, sort_savefiles_enable, saves\\Snes9x\\Game.srm", e =>
        {
            e.EMUf.P["ApplicationPath"] = "retroarch.exe";
            e.Cwd = e.F("RA");
            e.Root = e.F("RA");
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
        });
        yield return Cfg("C32", "C31 avec RootFolder=T", e =>
        {
            e.EMUf.P["ApplicationPath"] = "retroarch.exe";
            e.Cwd = e.F("RA");
            e.Root = e.T;
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
        });
        yield return Cfg("C33", "savefile_directory = \":\\./a/b\", EMU RA\\retroarch.exe, cwd T, RootFolder=T\\LBR, T\\LBR\\RA\\a\\b\\Game.srm", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \":\\./a/b\"");
            e.EMUf.P["ApplicationPath"] = "RA\\retroarch.exe";
            e.Root = e.F("LBR");
            e.Put("LBR\\RA\\a\\b\\Game.srm", "L");
        });
        yield return Cfg("C34", "savefile_directory = \":\\a/b/\", EMU absolu, T\\RA\\a\\b\\Game.srm", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \":\\a/b/\"");
            e.Put("RA\\a\\b\\Game.srm", "AB");
        });
        yield return Cfg("C35", "savefile_directory = \":\\a\\..\\a\\b\\.\", EMU absolu, T\\RA\\a\\b\\Game.srm", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \":\\a\\..\\a\\b\\.\"");
            e.Put("RA\\a\\b\\Game.srm", "AB");
        });
    }

    private static Case Sort(string id, string input, Action<Env> setup)
        => C(id, S3, input, setup, (e, p) => e.Saves(p));

    private static void Mgba(Env e) => e.Put("RA\\info\\mgba_libretro.info", "display_name = \"Nintendo - Game Boy Advance (mGBA)\"\r\ncorename = \"mGBA\"\r\n");

    private static IEnumerable<Case> SortCases()
    {
        yield return Sort("D01", "sort_savefiles_by_content_enable, saves\\snes\\Game.srm", e =>
        {
            e.CfgOn("sort_savefiles_by_content_enable");
            e.Put("RA\\saves\\snes\\Game.srm", "S");
        });
        yield return Sort("D02", "sort par contenu, fichier dans saves seulement", e =>
        {
            e.CfgOn("sort_savefiles_by_content_enable");
            e.Put("RA\\saves\\Game.srm", "S");
        });
        yield return Sort("D03", "sort_savefiles_enable, saves\\Snes9x\\Game.srm", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
        });
        yield return Sort("D04", "les deux tris, saves\\snes\\Snes9x\\Game.srm", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.CfgOn("sort_savefiles_by_content_enable");
            e.Put("RA\\saves\\snes\\Snes9x\\Game.srm", "S");
        });
        yield return Sort("D05", "les deux tris, saves\\Snes9x\\snes\\Game.srm seulement", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.CfgOn("sort_savefiles_by_content_enable");
            e.Put("RA\\saves\\Snes9x\\snes\\Game.srm", "S");
        });
        yield return Sort("D06", "sort_savefiles_enable, G.CommandLine = mgba, saves\\mGBA\\Game.srm", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Gf.P["CommandLine"] = Env.MgbaLine;
            Mgba(e);
            e.Put("RA\\saves\\mGBA\\Game.srm", "S");
        });
        yield return Sort("D07", "sort_savefiles_enable, G.Platform=\"\" et G.CommandLine=\"\"", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Gf.P["Platform"] = "";
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
            e.Put("RA\\saves\\Game.srm", "S");
        });
        yield return Sort("D08", "sort_savefiles_enable, plateforme de G absente de EMU, EMU.CommandLine = snes9x", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.EmuPlatforms.Clear();
            e.EmuPlatforms.Add(e.NewPlatform("emu1", "Sega Genesis", Env.MgbaLine));
            e.EMUf.P["CommandLine"] = Env.Snes9xLine;
            Mgba(e);
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
            e.Put("RA\\saves\\mGBA\\Game.srm", "S");
        });
        yield return Sort("D09", "sort_savefiles_enable, plateforme ecrite en minuscules dans EMU, EMU.CommandLine=\"\"", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.EmuPlatforms.Clear();
            e.EmuPlatforms.Add(e.NewPlatform("emu1", Env.Snes.ToLowerInvariant(), Env.Snes9xLine));
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
        });
        yield return Sort("D10", "sort_savefiles_enable, .info absent", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Del("RA\\info\\snes9x_libretro.info");
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
            e.Put("RA\\saves\\Game.srm", "S");
        });
        yield return Sort("D11", "sort_savefiles_enable, corename=\"Snes9x\" sans espaces, saves\\es9x", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\info\\snes9x_libretro.info", "corename=\"Snes9x\"\r\n");
            e.Put("RA\\saves\\es9x\\Game.srm", "S");
        });
        yield return Sort("D12", "sort_savefiles_enable, corename = \"\"", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\info\\snes9x_libretro.info", "corename = \"\"\r\n");
            e.Put("RA\\saves\\Game.srm", "S");
        });
        yield return Sort("D13", "sort_savefiles_enable, ligne corename seule", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\info\\snes9x_libretro.info", "corename\r\n");
            e.Put("RA\\saves\\Game.srm", "S");
        });
        yield return Sort("D14", "sort_savestates_enable, G.CommandLine = mgba, effective snes9x, states\\Snes9x\\Game.state", e =>
        {
            e.CfgOn("sort_savestates_enable");
            e.Gf.P["CommandLine"] = Env.MgbaLine;
            Mgba(e);
            e.Put("RA\\states\\Snes9x\\Game.state", "ST");
            e.Put("RA\\states\\mGBA\\Game.state", "ST");
        });
        yield return Sort("D15", "deux tris des states, states\\snes\\Snes9x\\Game.state1", e =>
        {
            e.CfgOn("sort_savestates_enable");
            e.CfgOn("sort_savestates_by_content_enable");
            e.Put("RA\\states\\snes\\Snes9x\\Game.state1", "ST");
        });
        yield return Sort("D16", "ROM a la racine d'un lecteur absent (Q:\\Game.sfc), sort par contenu", e =>
        {
            e.CfgOn("sort_savefiles_by_content_enable");
            e.Gf.P["ApplicationPath"] = "Q:\\Game.sfc";
            e.Put("RA\\saves\\Game.srm", "S");
        });
        yield return Sort("D17", "sort_savefiles_enable, .info : U+00AD puis corename = \"Snes9x\"", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\info\\snes9x_libretro.info", Shy + "corename = \"Snes9x\"\r\n");
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
        });
    }

    private static Case Enum(string id, string input, string[] files, Action<Env> setup = null, Func<Env, EmulatorPlugin, object> act = null)
        => C(id, S4, input, e =>
        {
            foreach (string f in files)
            {
                e.Put("RA\\saves\\" + f, "S:" + f);
            }

            setup?.Invoke(e);
        }, act ?? ((e, p) => e.Saves(p)));

    private static IEnumerable<Case> EnumCases()
    {
        yield return Enum("E01", "aucun fichier", Array.Empty<string>());
        yield return Enum("E02", "Game.srm", new[] { "Game.srm" });
        yield return Enum("E03", "Game.sav", new[] { "Game.sav" });
        yield return Enum("E04", "Game.srm, Game.rtc", new[] { "Game.srm", "Game.rtc" });
        yield return Enum("E05", "Game.srm, Game.mcr", new[] { "Game.srm", "Game.mcr" });
        yield return Enum("E06", "Game.mcr, Game.rtc", new[] { "Game.mcr", "Game.rtc" });
        yield return Enum("E07", "Game.sav, Game.rtc", new[] { "Game.sav", "Game.rtc" });
        yield return Enum("E08", "Game.SRM, Game.rtc", new[] { "Game.SRM", "Game.rtc" });
        yield return Enum("E09", "Game.srm, Game (Rev 1).srm", new[] { "Game.srm", "Game (Rev 1).srm" });
        yield return Enum("E10", "Game (Rev 1).srm seul", new[] { "Game (Rev 1).srm" });
        yield return Enum("E11", "Game sans extension", new[] { "Game" });
        yield return Enum("E12", "Gam.srm", new[] { "Gam.srm" });
        yield return Enum("E13", "game.srm", new[] { "game.srm" });
        yield return Enum("E14", "savestate_directory = :\\saves, Game.srm + Game.state", new[] { "Game.srm", "Game.state" },
            e => e.CfgSet("savestate_directory", "savestate_directory = \":\\saves\""));
        yield return Enum("E15", "savestate_directory = :\\saves, seul Game.state", new[] { "Game.state" },
            e => e.CfgSet("savestate_directory", "savestate_directory = \":\\saves\""));
        yield return Enum("E16", "jeu deux fois dans Games", new[] { "Game.srm" }, null, (e, p) => e.Saves(p, new[] { e.G, e.G }));
        yield return Enum("E17", "Games=[null, jeu a ApplicationPath vide, G]", new[] { "Game.srm" }, null, (e, p) =>
        {
            IGame empty = e.NewGame("G-vide", "g0", "", Env.Snes, Env.Snes9xLine);
            return e.Saves(p, new[] { null, empty, e.G });
        });
        yield return Enum("E18", "Emulator=null", new[] { "Game.srm" }, null, (e, p) => e.Saves(p, nullEmulator: true));
        yield return Enum("E19", "app de meme ApplicationPath que G", new[] { "Game.srm" }, e =>
        {
            e.MakeA("roms\\snes\\Game.sfc", createFile: false);
            e.GApps.Add(e.A);
        });
        yield return Enum("E20", "app de chemin en casse differente", new[] { "Game.srm" }, e =>
        {
            e.MakeA("roms\\snes\\GAME.sfc", createFile: false);
            e.GApps.Add(e.A);
        });
        yield return Enum("E21", "args=null", new[] { "Game.srm" }, null, (e, p) => p.GetSaves(null));
        yield return Enum("E22", "G.GetEffectiveCommandLine() rend null", new[] { "Game.srm" }, e => e.SetEff(e.G, null));
        yield return Enum("E23", "G.GetAllAdditionalApplications() rend [null]", new[] { "Game.srm" }, e => e.GApps.Add(null));
    }

    private static Case State(string id, string input, string[] files, Action<Env> setup = null)
        => C(id, S5, input, e =>
        {
            foreach (string f in files)
            {
                e.Put("RA\\states\\" + f, "ST:" + f);
            }

            setup?.Invoke(e);
        }, (e, p) => e.Saves(p));

    private static IEnumerable<Case> StateCases()
    {
        yield return State("T01", "Game.state", new[] { "Game.state" });
        yield return State("T02", "Game.state1", new[] { "Game.state1" });
        yield return State("T03", "Game.state.auto", new[] { "Game.state.auto" });
        yield return State("T04", "Game.state.AUTO", new[] { "Game.state.AUTO" });
        yield return State("T05", "Game.STATE1", new[] { "Game.STATE1" });
        yield return State("T06", "Game.state1.png", new[] { "Game.state1.png" });
        yield return State("T07", "Game.state03", new[] { "Game.state03" });
        yield return State("T08", "Game.state-2", new[] { "Game.state-2" });
        yield return State("T09", "Game.state.bak", new[] { "Game.state.bak" });
        yield return State("T10", "Game.state99", new[] { "Game.state99" });
        yield return State("T11", "Game.stateX", new[] { "Game.stateX" });
        yield return State("T12", "Game.state, Game.state1, Game.state.auto, Game.state1.png", new[] { "Game.state", "Game.state1", "Game.state.auto", "Game.state1.png" });
        yield return State("T13", "Game.state, savestate_directory absente", new[] { "Game.state" }, e => e.CfgRemove("savestate_directory"));
        yield return State("T14", "Game.state1 dans T\\roms\\snes, savestates_in_content_dir", Array.Empty<string>(), e =>
        {
            e.CfgOn("savestates_in_content_dir");
            e.Put("roms\\snes\\Game.state1", "ST");
        });
        yield return State("T15", "Game.state.state3", new[] { "Game.state.state3" });
    }

    private static Case App(string id, string input, Action<Env> setup, Func<Env, EmulatorPlugin, object> act = null)
        => C(id, S6, input, e =>
        {
            e.MakeA();
            setup?.Invoke(e);
        }, act ?? ((e, p) => e.Saves(p, apps: new[] { e.A }, noGames: true)));

    private static IEnumerable<Case> AppCases()
    {
        yield return App("A01", "[A], Games=null, saves\\Game (Disc 2).srm", e => e.Put("RA\\saves\\Game (Disc 2).srm", "S"));
        yield return App("A02", "[A] et [G], Game.srm et Game (Disc 2).srm", e =>
        {
            e.Put("RA\\saves\\Game (Disc 2).srm", "S2");
            e.Put("RA\\saves\\Game.srm", "S");
        }, (e, p) => e.Saves(p, apps: new[] { e.A }));
        yield return App("A03", "[null, A]", e => e.Put("RA\\saves\\Game (Disc 2).srm", "S"),
            (e, p) => e.Saves(p, apps: new[] { null, e.A }, noGames: true));
        yield return App("A04", "A.GameId inconnu", e =>
        {
            e.Af.P["GameId"] = "zz";
            e.Put("RA\\saves\\Game (Disc 2).srm", "S");
            e.Put("RA\\states\\Game (Disc 2).state", "ST");
        });
        yield return App("A05", "A.CommandLine = mgba, sort_savefiles_enable, saves\\mGBA\\Game (Disc 2).srm", e =>
        {
            e.Af.P["CommandLine"] = Env.MgbaLine;
            e.CfgOn("sort_savefiles_enable");
            Mgba(e);
            e.Put("RA\\saves\\mGBA\\Game (Disc 2).srm", "S");
            e.Put("RA\\saves\\Snes9x\\Game (Disc 2).srm", "S");
        });
        yield return App("A06", "A effective \"\", sort_savestates_enable, states\\Snes9x\\Game (Disc 2).state", e =>
        {
            e.SetEff(e.A, "");
            e.CfgOn("sort_savestates_enable");
            e.Put("RA\\states\\Snes9x\\Game (Disc 2).state", "ST");
        });
        yield return App("A07", "A.ApplicationPath=\"\"", e =>
        {
            e.Af.P["ApplicationPath"] = "";
            e.Put("RA\\saves\\Game (Disc 2).srm", "S");
        });
        yield return App("A08", "A effective sans -L", e =>
        {
            e.SetEff(e.A, "--verbose");
            e.Put("RA\\saves\\Game (Disc 2).srm", "S");
            e.Put("RA\\states\\Game (Disc 2).state2", "ST");
        });
    }

    private static Case Sat(string id, string input, string[] files, Action<Env> setup = null, bool snes = false)
        => C(id, S7, input, e =>
        {
            foreach (string f in files)
            {
                e.Put("RA\\saves\\" + f, "S:" + f);
            }

            setup?.Invoke(e);
        }, (e, p) => e.Saves(p, snes ? new[] { e.G } : new[] { e.GS }));

    private static IEnumerable<Case> SaturnCases()
    {
        yield return Sat("R01", "Panzer.bcr, .bkr, .smpc", new[] { "Panzer.bcr", "Panzer.bkr", "Panzer.smpc" });
        yield return Sat("R02", "Panzer.bkr, .smpc", new[] { "Panzer.bkr", "Panzer.smpc" });
        yield return Sat("R03", "Panzer.smpc", new[] { "Panzer.smpc" });
        yield return Sat("R04", "Panzer.bcr, Panzer.srm", new[] { "Panzer.bcr", "Panzer.srm" });
        yield return Sat("R05", "Panzer.bcr, Panzer (J).bcr", new[] { "Panzer.bcr", "Panzer (J).bcr" });
        yield return Sat("R06", "PANZER.BCR, panzer.bkr", new[] { "PANZER.BCR", "panzer.bkr" });
        yield return Sat("R07", "G (SNES, snes9x) avec Game.bcr, Game.bkr", new[] { "Game.bcr", "Game.bkr" }, null, snes: true);
        yield return Sat("R08", "G effective yabause, Game.bcr seul", new[] { "Game.bcr" },
            e => e.SetEff(e.G, "-L \"cores\\yabause_libretro.dll\""), snes: true);
        yield return Sat("R09", "Platform=\"My Saturn Games\", coeur snes9x, x.bcr + x.bkr", new[] { "x.bcr", "x.bkr" }, e =>
        {
            e.GSf.P["Platform"] = "My Saturn Games";
            e.GSf.P["ApplicationPath"] = e.F("roms\\saturn\\x.cue");
            e.SetEff(e.GS, Env.Snes9xLine);
        });
        yield return Sat("R11", "Panzer.bcr + states\\Panzer.state1", new[] { "Panzer.bcr" }, e => e.Put("RA\\states\\Panzer.state1", "ST"));
        yield return Sat("R12", "\"Panzer .bcr\" (espace avant l'extension)", new[] { "Panzer .bcr" });
        yield return Sat("R13", "jeu Saturn T\\roms\\sat\\Stra.cue ; Strax.bcr, Stra U+00AD x.bkr, Stra U+0130.smpc",
            new[] { "Strax.bcr", "Stra" + Shy + "x.bkr", "Stra\u0130.smpc" }, Stra);
        yield return Sat("R14", "jeu Saturn Stra.cue ; Strai.bcr, StraI.bkr, Stra U+0131.smpc",
            new[] { "Strai.bcr", "StraI.bkr", "Stra\u0131.smpc" }, Stra);
    }

    private static void Stra(Env e)
    {
        e.GSf.P["ApplicationPath"] = e.F("roms\\sat\\Stra.cue");
        e.Put("roms\\sat\\Stra.cue", "CUE");
    }

    private static Case W(string id, string section, string input, Action<Env> setup, Func<Env, EmulatorPlugin, object> act)
        => C(id, section, input, e =>
        {
            e.Put("src\\x.srm", "NEW");
            setup?.Invoke(e);
        }, act);

    private static IEnumerable<Case> AddCases()
    {
        yield return W("W01", S8, "SaveToAdd=null", null, (e, p) => e.Add(p, null));
        yield return W("W02", S8, "FileLocation absent", null, (e, p) => e.Add(p, Env.SG("g1", e.F("src\\absent.srm"))));
        yield return W("W03", S8, "GameId inconnu", null, (e, p) => e.Add(p, Env.SG("zz", e.F("src\\x.srm"))));
        yield return W("W04", S8, "AdditionalApplicationId=\"zz\" absent", null, (e, p) =>
        {
            var s = Env.SG("g1", e.F("src\\x.srm"));
            Env.Set(s, "AdditionalApplicationId", "zz");
            return e.Add(p, s);
        });
        yield return W("W05", S8, "G.EmulatorId=\"\"", e => e.Gf.P["EmulatorId"] = "", (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W06", S8, "G.EmulatorId=\"nope\"", e => e.Gf.P["EmulatorId"] = "nope", (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W07", S8, "G.ApplicationPath=\"\"", e => e.Gf.P["ApplicationPath"] = "", (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W08", S8, "GameSaveGame T\\src\\x.srm", null, (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W09", S8, "W08 avec SaveGroupName, Title, SaveGroupId, MatchLineageId et autres champs", null, (e, p) =>
        {
            var s = Env.SG("g1", e.F("src\\x.srm"));
            Env.Set(s, "SaveGroupName", "Perso");
            Env.Set(s, "Title", "T");
            Env.Set(s, "SaveGroupId", "gid");
            Env.Set(s, "MatchLineageId", "m");
            Env.Set(s, "MigrationFamilyId", "mf");
            Env.Set(s, "DisplayChipText", "chip");
            Env.Set(s, "OriginalFileName", "orig.srm");
            Env.Set(s, "EmulatorCore", "core0");
            Env.Set(s, "EmulatorFileName", "ef.exe");
            Env.Set(s, "ReportedFileSizeBytes", (long?)5L);
            Env.Set(s, "IsDirectory", false);
            return e.Add(p, s);
        });
        yield return W("W10", S8, "GameSaveGame T\\src\\x sans extension", e => e.Put("src\\x", "NEW"), (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x"))));
        foreach (var (id, slot) in new[] { ("W11", (int?)null), ("W12", 0), ("W13", 4), ("W14", -1), ("W15", -7) })
        {
            yield return W(id, S8, $"GameSaveState slot {(slot?.ToString() ?? "null")}", e => e.Put("src\\s.state", "ST"),
                (e, p) => e.Add(p, Env.SS("g1", e.F("src\\s.state"), slot)));
        }

        yield return W("W16", S8, "destination existe, ShouldOverwriteFunc=null", e => e.Put("RA\\saves\\Game.srm", "OLD"),
            (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W17", S8, "destination existe, func -> false", e => e.Put("RA\\saves\\Game.srm", "OLD"),
            (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm")), 0));
        yield return W("W18", S8, "destination existe, func -> true", e => e.Put("RA\\saves\\Game.srm", "OLD"),
            (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm")), 1));
        yield return W("W19", S8, "dossier saves absent", e => e.Del("RA\\saves"), (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W20", S8, "savefile_directory absente", e => e.CfgRemove("savefile_directory"), (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W21", S8, "sort_savefiles_enable, .info absent", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Del("RA\\info\\snes9x_libretro.info");
        }, (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W22", S8, "source = destination, func -> true", e => e.Put("RA\\saves\\Game.srm", "SAME"),
            (e, p) => e.Add(p, Env.SG("g1", e.F("RA\\saves\\Game.srm")), 1));
        yield return W("W23", S8, "destination en lecture seule, func -> true", e =>
        {
            e.Put("RA\\saves\\Game.srm", "OLD");
            e.AfterFinish.Add(() => File.SetAttributes(e.F("RA\\saves\\Game.srm"), FileAttributes.ReadOnly));
        }, (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm")), 1));
        yield return W("W24", S8, "application A (emu1), AdditionalApplicationId=a1", e =>
        {
            e.MakeA();
            e.GApps.Add(e.A);
        }, (e, p) =>
        {
            var s = Env.SG("g1", e.F("src\\x.srm"));
            Env.Set(s, "AdditionalApplicationId", "a1");
            return e.Add(p, s);
        });
        yield return W("W25", S8, "sort par contenu, saves\\snes absent", e => e.CfgOn("sort_savefiles_by_content_enable"),
            (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return W("W26", S8, "args=null", null, (e, p) => Caught.Try(() => p.AddSaveFile(null)));
        yield return W("W27", S8, "app A (Game2.sfc, effective null), GameSaveState slot 1", e =>
        {
            e.MakeA("roms\\snes\\Game2.sfc");
            e.SetEff(e.A, null);
            e.GApps.Add(e.A);
            e.Put("src\\s.state1", "ST");
        }, (e, p) => e.Add(p, AppState(e)));
        yield return W("W27b", S8, "W27 avec sort_savestates_enable, states\\Snes9x absent", e =>
        {
            e.MakeA("roms\\snes\\Game2.sfc");
            e.SetEff(e.A, null);
            e.GApps.Add(e.A);
            e.Put("src\\s.state1", "ST");
            e.CfgOn("sort_savestates_enable");
        }, (e, p) => e.Add(p, AppState(e)));
        yield return W("W28", S8, "W27 + ligne effective du jeu null, states absent", e =>
        {
            e.MakeA("roms\\snes\\Game2.sfc");
            e.SetEff(e.A, null);
            e.SetEff(e.G, null);
            e.GApps.Add(e.A);
            e.Put("src\\s.state1", "ST");
            e.Del("RA\\states");
        }, (e, p) => e.Add(p, AppState(e)));
        yield return W("W29", S8, "AdditionalApplicationId=a1, GetAllAdditionalApplications() rend null", e => e.GApps = null, (e, p) =>
        {
            var s = Env.SG("g1", e.F("src\\x.srm"));
            Env.Set(s, "AdditionalApplicationId", "a1");
            return e.Add(p, s);
        });
        yield return W("W30", S8, "GetAllAdditionalApplications() rend [null, A]", e =>
        {
            e.MakeA();
            e.GApps.Add(null);
            e.GApps.Add(e.A);
        }, (e, p) =>
        {
            var s = Env.SG("g1", e.F("src\\x.srm"));
            Env.Set(s, "AdditionalApplicationId", "a1");
            return e.Add(p, s);
        });
        yield return W("W31", S8, "app A a ligne effective \"\", sort_savestates_enable, GameSaveState", e =>
        {
            e.MakeA();
            e.SetEff(e.A, "");
            e.GApps.Add(e.A);
            e.CfgOn("sort_savestates_enable");
            e.Put("src\\s.state1", "ST");
        }, (e, p) => e.Add(p, AppState(e)));

        // ---- 9. Saturn ----
        yield return W("WS1", S9, "GS, source Old.bcr avec Old.bkr et Old.smpc", e => SrcFiles(e, "Old.bcr", "Old.bkr", "Old.smpc"),
            (e, p) => e.Add(p, Env.SG("gs", e.F("src\\Old.bcr"))));
        yield return W("WS2", S9, "GS, source Old.bkr (+ Old.bcr)", e => SrcFiles(e, "Old.bcr", "Old.bkr"),
            (e, p) => e.Add(p, Env.SG("gs", e.F("src\\Old.bkr"))));
        yield return W("WS3", S9, "GS, source Old.bcr seul", e => SrcFiles(e, "Old.bcr"),
            (e, p) => e.Add(p, Env.SG("gs", e.F("src\\Old.bcr"))));
        yield return W("WS4", S9, "GS, saves\\Panzer.bkr present, source .bcr + .bkr", e =>
        {
            SrcFiles(e, "Old.bcr", "Old.bkr");
            e.Put("RA\\saves\\Panzer.bkr", "OLD");
        }, (e, p) => e.Add(p, Env.SG("gs", e.F("src\\Old.bcr"))));
        yield return W("WS5", S9, "GS, GameSaveState slot 2 s.state2 avec s.bcr voisin", e => SrcFiles(e, "s.state2", "s.bcr"),
            (e, p) => e.Add(p, Env.SS("gs", e.F("src\\s.state2"), 2)));
        yield return W("WS6", S9, "GS, GameSaveState slot -1, pas de voisins", e => SrcFiles(e, "s.auto"),
            (e, p) => e.Add(p, Env.SS("gs", e.F("src\\s.auto"), -1)));
        yield return W("WS7", S9, "GS, source Old.BCR (+ Old.bkr)", e => SrcFiles(e, "Old.BCR", "Old.bkr"),
            (e, p) => e.Add(p, Env.SG("gs", e.F("src\\Old.BCR"))));
        yield return W("WS8", S9, "G (SNES), source x.bcr, SaveGroupId=sg", e => SrcFiles(e, "x.bcr", "x.bkr"), (e, p) =>
        {
            var s = Env.SG("g1", e.F("src\\x.bcr"));
            Env.Set(s, "SaveGroupId", "sg");
            return e.Add(p, s);
        });
        yield return W("WS9", S9, "GS, cwd T\\src, source Old.bcr sans dossier, Old.bkr voisin", e =>
        {
            SrcFiles(e, "Old.bcr", "Old.bkr");
            e.Cwd = e.F("src");
        }, (e, p) => e.Add(p, Env.SG("gs", "Old.bcr")));
    }

    private static GameSaveState AppState(Env e)
    {
        var s = Env.SS("g1", e.F("src\\s.state1"), 1);
        Env.Set(s, "AdditionalApplicationId", "a1");
        return s;
    }

    private static void SrcFiles(Env e, params string[] names)
    {
        foreach (string n in names)
        {
            e.Put("src\\" + n, "SRC:" + n);
        }
    }

    private static Case X(string id, string input, string[] files, Func<Env, EmulatorPlugin, object> act, Action<Env> setup = null)
        => C(id, S10, input, e =>
        {
            foreach (string f in files)
            {
                e.Put("s\\" + f, "X:" + f);
            }

            setup?.Invoke(e);
        }, act);

    private static object Group(EmulatorPlugin p, GameSaveBase save, string primary)
    {
        bool ok = p.TryGetSaveGroupInfo(save, primary, out string id, out string name);
        return new Rec { { "result", ok }, { "saveGroupId", id }, { "saveGroupName", name } };
    }

    private static object Sig(EmulatorPlugin p, GameSaveBase save, string emu = null)
    {
        bool ok = p.TryComputeSaveSignature(save, emu, out string sig);
        return new Rec { { "result", ok }, { "signature", sig } };
    }

    private static GameSaveBase Core(GameSaveBase s, string core)
    {
        Env.Set(s, "EmulatorCore", core);
        return s;
    }

    private static IEnumerable<Case> CompanionCases()
    {
        yield return X("X01", "GetCompanionSaveFiles(T\\s\\a.bcr), a.bcr a.bkr a.smpc", new[] { "a.bcr", "a.bkr", "a.smpc" }, (e, p) => p.GetCompanionSaveFiles(e.F("s\\a.bcr")));
        yield return X("X02", "T\\s\\a.bkr, a.bcr a.bkr", new[] { "a.bcr", "a.bkr" }, (e, p) => p.GetCompanionSaveFiles(e.F("s\\a.bkr")));
        yield return X("X03", "T\\s\\a.BKR, a.bcr a.bkr", new[] { "a.bcr", "a.bkr" }, (e, p) => p.GetCompanionSaveFiles(e.F("s\\a.BKR")));
        yield return X("X04", "T\\s\\a.bcr seul", new[] { "a.bcr" }, (e, p) => p.GetCompanionSaveFiles(e.F("s\\a.bcr")));
        yield return X("X05", "T\\s\\a.bcr absent, a.bkr present", new[] { "a.bkr" }, (e, p) => p.GetCompanionSaveFiles(e.F("s\\a.bcr")));
        yield return X("X06", "TryGetSaveGroupInfo(null, ...)", Array.Empty<string>(), (e, p) => Group(p, null, e.F("s\\a.bcr")));
        yield return X("X07", "save Panzer.bkr, primaire \"\"", new[] { "Panzer.bkr" }, (e, p) => Group(p, Env.SG("gs", e.F("s\\Panzer.bkr")), ""));
        yield return X("X08", "save quelconque, primaire T\\s\\ Zz .bcr", Array.Empty<string>(), (e, p) => Group(p, Env.SG("g1", e.F("s\\q.srm")), e.F("s\\ Zz .bcr")));
        yield return X("X09", "Game.srm, coeur mednafen_saturn_libretro", Array.Empty<string>(), (e, p) => Group(p, Core(Env.SG("g1", e.F("s\\Game.srm")), "mednafen_saturn_libretro"), null));
        yield return X("X10", "Game.state.auto, coeur Saturn", Array.Empty<string>(), (e, p) => Group(p, Core(Env.SS("g1", e.F("s\\Game.state.auto"), -1), "mednafen_saturn_libretro"), null));
        yield return X("X11", "Game.srm, coeur snes9x", Array.Empty<string>(), (e, p) => Group(p, Core(Env.SG("g1", e.F("s\\Game.srm")), "snes9x_libretro"), null));
        yield return C("X12", S10, "TryComputeSaveSignature, a.bcr (01), a.bkr (02 03)", e =>
        {
            e.PutBytes("s\\a.bcr", new byte[] { 1 });
            e.PutBytes("s\\a.bkr", new byte[] { 2, 3 });
        }, (e, p) => Sig(p, Env.SG("gs", e.F("s\\a.bcr")), "ignored"));
        yield return C("X13", S10, "idem avec A.BCR sur disque", e =>
        {
            e.PutBytes("s\\A.BCR", new byte[] { 1 });
            e.PutBytes("s\\a.bkr", new byte[] { 2, 3 });
        }, (e, p) => Sig(p, Env.SG("gs", e.F("s\\A.BCR"))));
        yield return X("X14", "FileLocation absent", new[] { "a.bkr" }, (e, p) => Sig(p, Env.SG("gs", e.F("s\\a.bcr"))));
        yield return X("X15", "Game.srm + coeur Saturn, aucun .bcr/.bkr/.smpc", new[] { "Game.srm" },
            (e, p) => Sig(p, Core(Env.SG("gs", e.F("s\\Game.srm")), "mednafen_saturn_libretro")));
        yield return C("X16", S10, "Game.srm + coeur Saturn + Game.bkr", e =>
        {
            e.Put("s\\Game.srm", "SRM");
            e.PutBytes("s\\Game.bkr", new byte[] { 9, 8, 7 });
        }, (e, p) => Sig(p, Core(Env.SG("gs", e.F("s\\Game.srm")), "mednafen_saturn_libretro")));
        yield return C("X17", S10, "a.bcr ouvert en ecriture partagee", e =>
        {
            e.PutBytes("s\\a.bcr", new byte[] { 1 });
            e.PutBytes("s\\a.bkr", new byte[] { 2, 3 });
            e.LockFile(e.F("s\\a.bcr"), FileShare.ReadWrite);
        }, (e, p) => Sig(p, Env.SG("gs", e.F("s\\a.bcr"))));
        yield return C("X18", S10, "a.bkr ouvert en exclusif", e =>
        {
            e.PutBytes("s\\a.bcr", new byte[] { 1 });
            e.PutBytes("s\\a.bkr", new byte[] { 2, 3 });
            e.LockFile(e.F("s\\a.bkr"), FileShare.None);
        }, (e, p) => Sig(p, Env.SG("gs", e.F("s\\a.bcr"))));
        yield return X("X19", "GetCompanionSaveFiles(\"T/s/a.bcr\") avec des /", new[] { "a.bcr", "a.bkr" }, (e, p) => p.GetCompanionSaveFiles(e.F("s\\a.bcr").Replace('\\', '/')));
        yield return X("X20", "GetCompanionSaveFiles(\"T\\s\\.\\a.bcr\")", new[] { "a.bcr", "a.bkr" }, (e, p) => p.GetCompanionSaveFiles(e.F("s\\.\\a.bcr")));
        yield return C("X21", S10, "GetCompanionSaveFiles(\"X:\\a.bcr\") sur un lecteur jetable (subst), X:\\a.bkr present", e =>
        {
            e.Subst("drv");
            e.Put("drv\\a.bkr", "BKR");
        }, (e, p) => p.GetCompanionSaveFiles(e.DriveRoot + "a.bcr"));
    }

    private static Case B(string id, string input, Action<Env> setup, string platform, string cmd, Func<Env, string> exe = null)
        => C(id, S11, input, setup, (e, p) => p.GetBiosFilesForPlatform(exe != null ? exe(e) : e.F("RA\\retroarch.exe"), platform, cmd));

    private static IEnumerable<Case> BiosCases()
    {
        const string fourDo = "-L \"cores\\4do_libretro.dll\" \"%romfile%\"";
        yield return B("B03", "T\\RA\\retroarch.exe, Panasonic 3DO, 4do", null, "Panasonic 3DO", fourDo);
        yield return B("B04", "idem, panasonic 3do", null, "panasonic 3do", fourDo);
        yield return B("B05", "idem, Sega Saturn", null, "Sega Saturn", fourDo);
        yield return B("B06", "mednafen_saturn, Sega Saturn", null, "Sega Saturn", Env.SaturnLine);
        yield return B("B07", "ep128emu_core_libretro, plateforme x", null, "x", "-L \"cores\\ep128emu_core_libretro.dll\"");
        yield return B("B08", "puae, Commodore Amiga CD32", null, "Commodore Amiga CD32", "-L \"cores\\puae_libretro.dll\"");
        yield return B("B09", "puae, Commodore CDTV", null, "Commodore CDTV", "-L \"cores\\puae_libretro.dll\"");
        yield return B("B10", "CoreName.dll, plateforme Platform (en-tete)", null, "Platform", "-L \"cores\\CoreName.dll\"");
        yield return B("B11", "inexistant_libretro.dll", null, "x", "-L \"cores\\inexistant_libretro.dll\"");
        yield return B("B15", "system_directory = \"D:\\Bios\"", e => e.CfgSet("system_directory", "system_directory = \"D:\\Bios\""), "Panasonic 3DO", fourDo);
        yield return B("B16", "system_directory = \"default\"", e => e.CfgSet("system_directory", "system_directory = \"default\""), "Panasonic 3DO", fourDo);
        yield return B("B17", "cfg sans system_directory", e => e.CfgRemove("system_directory"), "Panasonic 3DO", fourDo);
        yield return B("B18", "system_directory_extra = \"Z\" avant system_directory", e => e.Cfg.Insert(0, "system_directory_extra = \"Z\""), "Panasonic 3DO", fourDo);
        yield return B("B19", "cfg ouvert en exclusif", e => e.LockFile(e.F("RA\\retroarch.cfg"), FileShare.None), "Panasonic 3DO", fourDo);
        yield return B("B20", "-L \"cores\\a.b.dll\" x.dll\"", null, "x", "-L \"cores\\a.b.dll\" x.dll\"");
        yield return B("B21", "U+00AD puis system_directory = \"D:\\Bios\", 4do, x",
            e => e.CfgSet("system_directory", Shy + "system_directory = \"D:\\Bios\""), "x", fourDo);
        yield return B("B22", "system_directory = U+00AD:\\sys",
            e => e.CfgSet("system_directory", "system_directory = \"" + Shy + ":\\sys\""), "Panasonic 3DO", fourDo);
        yield return B("B23", "exe relatif, cwd T, system_directory = \"REL\", coeur inexistant",
            e => e.CfgSet("system_directory", "system_directory = \"REL\""), "x", "-L \"cores\\inexistant.dll\"", e => "RA\\retroarch.exe");
        yield return B("B24", "B23 avec un autre repertoire courant", e =>
        {
            e.CfgSet("system_directory", "system_directory = \"REL\"");
            e.Dir("..\\other");
            e.Cwd = Path.Combine(e.W, "other");
        }, "x", "-L \"cores\\inexistant.dll\"", e => "RA\\retroarch.exe");
        yield return C("B26", S11, "exe a la racine d'un lecteur jetable (subst X: sur T\\drv), X:\\retroarch.cfg avec system_directory = \"Z\"", e =>
        {
            e.Subst("drv");
            e.Put("drv\\retroarch.exe", "");
            e.Put("drv\\retroarch.cfg", "system_directory = \"Z\"\r\n");
        }, (e, p) => p.GetBiosFilesForPlatform(e.DriveRoot + "retroarch.exe", "Panasonic 3DO", fourDo));
        yield return B("B90", "sonde : 4do, plateforme vide (colonne vide seulement)", null, "", fourDo);
        yield return B("B91", "sonde : 4do, plateforme null", null, null, fourDo);
        yield return new Case("BF1", S11, "SystemFolder=X1, new BiosFile(f.bin,true,d,m,null), SystemFolder=X2", null, (e, p, s) =>
        {
            s.SetSystemFolder("X1");
            object bios = s.NewBios("f.bin", true, "d", "m", null);
            s.SetSystemFolder("X2");
            return bios;
        });
        yield return new Case("BF2", S11, "SystemFolder=abc puis null", null, (e, p, s) =>
        {
            s.SetSystemFolder("abc");
            string a = s.GetSystemFolder();
            s.SetSystemFolder(null);
            return new Rec { { "abc", a }, { "null", s.GetSystemFolder() } };
        });
        yield return new Case("BF3", S11, "new BiosFile(null, false)", null, (e, p, s) => s.NewBios(null, false));
    }

    private static Case V(string id, string input, string stdout, string stderr = null, bool informative = false)
        => new(id, S12, input, e => e.MakeFakeExe(stdout, stderr), (e, p, s) => p.GetCurrentVersion(e.F("fake\\retroarch.exe"))) { Informative = informative };

    private static object Inject(EmulatorPlugin p, InjectRetroAchievementsCredentialsArgs args)
        => new Rec { { "inject", Caught.Try(() => p.InjectRetroAchievementsCredentials(args)) }, { "supports", Caught.Try(() => p.SupportsRetroAchievements("x")) } };

    private static IEnumerable<Case> MiscCases()
    {
        yield return C("V01", S12, "GetCurrentVersion(null), (\"\"), chemin absent", null, (e, p) =>
            R(() => p.GetCurrentVersion(null), () => p.GetCurrentVersion(""), () => p.GetCurrentVersion(e.F("fake\\absent.exe"))));
        yield return V("V02", "sortie Version: 1.19.1 (Git abc)", "Version: 1.19.1 (Git abc)\r\n");
        yield return V("V03", "sortie Version: 1.9.0 (Git abc)", "Version: 1.9.0 (Git abc)\r\n");
        yield return V("V04", "sortie Version: 1.9.0", "Version: 1.9.0\r\n");
        yield return V("V05", "sortie Version: 1.10.10", "Version: 1.10.10\r\n");
        yield return V("V06", "stderr Version: 1.20.0", null, "Version: 1.20.0\r\n");
        yield return V("V07", "sortie version: 1.19.1", "version: 1.19.1\r\n");
        yield return C("V08", S12, "fichier texte renomme .exe", e => e.Put("fake\\retroarch.exe", "not an exe"), (e, p) => p.GetCurrentVersion(e.F("fake\\retroarch.exe")));
        yield return V("V09", "sortie Version: 1.2.34", "Version: 1.2.34\r\n");
        yield return V("V10", "sortie U+0000 puis Version: 1.2.3 (x)", "\0Version: 1.2.3 (x)\r\n");
        yield return V("V11", "sortie RetroArch / Version: 1.20.0 (Git 1) / foo", "RetroArch\r\nVersion: 1.20.0 (Git 1)\r\nfoo\r\n");
        yield return V("V12", "deux lignes Version (informatif)", "Version: 1.11.1 a\r\nVersion: 1.22.2 b\r\n", informative: true);
        yield return V("V13", "sonde : sortie vide", "");
        yield return V("V14", "sonde : U+00AD puis Version: 1.2.3 (x), sans saut final", Shy + "Version: 1.2.3 (x)");

        yield return C("K01", S12, "GetAllAvailableCores(T\\RA\\retroarch.exe), cores absent", null, (e, p) => Cores(p).GetAllAvailableCores(e.F("RA\\retroarch.exe")));
        yield return C("K02", S12, "cores : a_libretro.dll, b_libretro.dll, c.txt, d.dll_old, e.dllx", CoreFiles,
            (e, p) => Cores(p).GetAllAvailableCores(e.F("RA\\retroarch.exe")));
        yield return C("K08", S12, "GetAllAvailableCores(\"RA\\retroarch.exe\"), cwd T, RootFolder=L", CoreFiles,
            (e, p) => Cores(p).GetAllAvailableCores("RA\\retroarch.exe"));
        yield return C("K09", S12, "K08 avec RootFolder=T", e =>
        {
            CoreFiles(e);
            e.Root = e.T;
        }, (e, p) => Cores(p).GetAllAvailableCores("RA\\retroarch.exe"));

        yield return C("Q01", S12, "SupportsRetroAchievements(\"x\"), fichier absent", null, (e, p) => p.SupportsRetroAchievements("x"));
        yield return C("Q02", S12, "Inject(u, t, hardcore false) puis Supports", null, (e, p) =>
            Inject(p, new InjectRetroAchievementsCredentialsArgs(e.F("RA\\retroarch.exe"), "u", "t", true, false)));
        yield return C("Q03", S12, "Inject(hardcore null, enable false) puis Supports", null, (e, p) =>
            Inject(p, new InjectRetroAchievementsCredentialsArgs(e.F("RA\\retroarch.exe"), "u", "t", false, null)));
        yield return C("Q04", S12, "fichier : cheevos_enable = \"untrue\" puis cheevos_token = \"a=b\\\"c\"", e =>
            RaFile(e, "cheevos_enable = \"untrue\"\r\ncheevos_token = \"a=b\\\"c\"\r\n"), (e, p) => p.SupportsRetroAchievements("x"));
        yield return C("Q05", S12, "fichier verrouille en exclusif", e =>
        {
            RaFile(e, "cheevos_enable = \"true\"\r\ncheevos_username = \"u\"\r\n");
            e.LockFile(Path.Combine(e.Data, "RetroAchievements.cfg"), FileShare.None);
        }, (e, p) => p.SupportsRetroAchievements("x"));
        yield return C("Q06", S12, "PrepareEmulatorForLaunch sans identifiants", null, (e, p) =>
            p.PrepareEmulatorForLaunch(new PrepareForLaunchArgs(e.EMU, e.G, "cmd", null, null)));
        yield return C("Q07", S12, "Prepare u/t, EnableHardcoreAchievements=true, ligne -L \"cores\\x.dll\" \"rom\"", e =>
            e.EMUf.P["EnableHardcoreAchievements"] = true, (e, p) =>
            p.PrepareEmulatorForLaunch(new PrepareForLaunchArgs(e.EMU, e.G, "-L \"cores\\x.dll\" \"rom\"", null, new RetroAchievementCredentials("u", "t"))));
        yield return C("Q08", S12, "Prepare, DataDirectory sous un fichier (creation du dossier impossible)", e =>
        {
            File.WriteAllText(Path.Combine(e.W, "afile"), "f");
            e.PathsF.P["DataDirectory"] = Path.Combine(e.W, "afile", "data");
        }, (e, p) => p.PrepareEmulatorForLaunch(new PrepareForLaunchArgs(e.EMU, e.G, "cmd", null, new RetroAchievementCredentials("u", "t"))));
        yield return C("Q09", S12, "fichier : U+00AD puis cheevos_enable = \"true\"", e => RaFile(e, Shy + "cheevos_enable = \"true\"\r\n"),
            (e, p) => p.SupportsRetroAchievements("x"));
        yield return C("Q10", S12, "InjectRetroAchievementsCredentials(null)", null, (e, p) => p.InjectRetroAchievementsCredentials(null));
        yield return C("Q11", S12, "Inject username null, token null", null, (e, p) =>
            Inject(p, new InjectRetroAchievementsCredentialsArgs(null, null, null, null, null)));
        yield return C("Q12", S12, "PrepareEmulatorForLaunch(null)", null, (e, p) => p.PrepareEmulatorForLaunch(null));
        yield return C("Q13", S12, "DataDirectory=null, SupportsRetroAchievements(\"x\")", e => e.PathsF.P["DataDirectory"] = null,
            (e, p) => p.SupportsRetroAchievements("x"));
        yield return C("Q14", S12, "fichier : U+00AD puis cheevos_token = \"t\"", e => RaFile(e, Shy + "cheevos_token = \"t\"\r\n"),
            (e, p) => p.SupportsRetroAchievements("x"));
        yield return C("Q15", S12, "sonde : Prepare avec app et hardcore false, identifiants vides", null, (e, p) =>
            p.PrepareEmulatorForLaunch(new PrepareForLaunchArgs(e.EMU, e.G, null, e.MakeA(createFile: false), new RetroAchievementCredentials("", ""))));
        yield return C("Q16", S12, "sonde : Prepare, EmulatorBeingLaunched null", null, (e, p) =>
            p.PrepareEmulatorForLaunch(new PrepareForLaunchArgs(null, e.G, "c", null, new RetroAchievementCredentials("u\u00e9", "t\"q"))));
    }

    private const string SJ = "J. Ecarts du juge de code (docs\\code-judge.md)";

    private static object Install(Env e, EmulatorPlugin p, string version)
    {
        IEmulator existing = e.NewEmulator("EMU-MonRA", "emu2", e.F("MonRA\\retroarch.exe"));
        var args = new InstallEmulatorArgs(Env.Snes, (a, b, c) => { }, () => false, existing, version);
        return p.InstallEmulator(args);
    }

    private static IEnumerable<Case> JudgeCases()
    {
        // E1 : dossier <racine>\Metadata\Temp cree avant la requete (requete bloquee par la garde).
        yield return new Case("N42", SJ, "E1 : RootFolder=L sans Metadata, InstallEmulator(existant T\\MonRA\\retroarch.exe, version 0.0.1), requete bloquee",
            null, (e, p, s) => Install(e, p, "0.0.1")) { NeedsNetGuard = true };

        // E2 : temporaire impossible -> exception brute sans requete. L'original ouvre alors une boite modale de
        // LaunchBox (« A directory could not be created »), observee le 09/10 : ces deux cas exigent --interactive.
        yield return new Case("N43", SJ, "E2 : L\\Metadata est un fichier, InstallEmulator(existant T\\MonRA\\retroarch.exe, version 1.19.1)",
            e => File.WriteAllText(Path.Combine(e.L, "Metadata"), "f"), (e, p, s) => Install(e, p, "1.19.1")) { NeedsNetGuard = true, Interactive = true };
        yield return new Case("N43b", SJ, "E2 : RootFolder=null, InstallEmulator(existant T\\MonRA\\retroarch.exe, version 1.19.1)",
            e => e.Root = null, (e, p, s) => Install(e, p, "1.19.1")) { NeedsNetGuard = true, Interactive = true };

        // E3 : nom de base vide.
        yield return W("WS10", SJ, "E3 : GS, source T\\src\\.bcr (01), T\\src\\.bkr (02), T\\src.bkr (03)", e =>
        {
            e.PutBytes("src\\.bcr", new byte[] { 1 });
            e.PutBytes("src\\.bkr", new byte[] { 2 });
            e.PutBytes("src.bkr", new byte[] { 3 });
        }, (e, p) => e.Add(p, Env.SG("gs", e.F("src\\.bcr"))));
        yield return W("WS11", SJ, "E3 : jeu Saturn de contenu T\\roms\\saturn\\.cue, sources T\\src\\Old.bcr et Old.bkr", e =>
        {
            e.GSf.P["ApplicationPath"] = e.F("roms\\saturn\\.cue");
            e.Put("roms\\saturn\\.cue", "CUE");
            SrcFiles(e, "Old.bcr", "Old.bkr");
        }, (e, p) => e.Add(p, Env.SG("gs", e.F("src\\Old.bcr"))));

        // E4 : reste du prefixe :\ relatif a un lecteur ou enracine sans lecteur.
        yield return C("C36", SJ, "E4 : savefile_directory = \":\\<lettre de T>:saves\", cwd T, T\\RA\\saves\\Game.srm, T\\saves absent", e =>
        {
            e.CfgSet("savefile_directory", "savefile_directory = \":\\" + e.T[0] + ":saves\"");
            e.Put("RA\\saves\\Game.srm", "S");
        }, (e, p) => e.Saves(p));
        yield return W("W32", SJ, "E4 : C36 en AddSaveFile (GameSaveGame T\\src\\x.srm)",
            e => e.CfgSet("savefile_directory", "savefile_directory = \":\\" + e.T[0] + ":saves\""),
            (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));
        yield return C("C37", SJ, "E4 : exe X:\\retroarch.exe (subst X: sur T\\x), cwd T, savefile_directory = \":\\\\saves\", T\\x\\saves\\Game.srm", e =>
        {
            e.Subst("x");
            e.NoCfg = true;
            e.Put("x\\retroarch.exe", "");
            var lines = Env.StandardCfg.ToList();
            lines[0] = "savefile_directory = \":\\\\saves\"";
            e.Put("x\\retroarch.cfg", string.Join("\r\n", lines) + "\r\n");
            e.Put("x\\saves\\Game.srm", "S");
            e.EMUf.P["ApplicationPath"] = e.DriveRoot + "retroarch.exe";
        }, (e, p) => e.Saves(p));

        // E5 : lignes de plateforme null.
        yield return C("D18", SJ, "E5 : sort_savefiles_enable, G.CommandLine=\"\", GetAllEmulatorPlatforms()=null, EMU.CommandLine=snes9x, saves\\Snes9x\\Game.srm", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.EmuPlatformsNull = true;
            e.EMUf.P["CommandLine"] = Env.Snes9xLine;
            e.Put("RA\\saves\\Snes9x\\Game.srm", "S");
        }, (e, p) => e.Saves(p));
        yield return W("W33", SJ, "E5 : D18 en AddSaveFile (GameSaveGame T\\src\\x.srm)", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.EmuPlatformsNull = true;
            e.EMUf.P["CommandLine"] = Env.Snes9xLine;
        }, (e, p) => e.Add(p, Env.SG("g1", e.F("src\\x.srm"))));

        // E6 : action d'import avec PluginHelper.DataManager null, LocalDb non initialisee.
        yield return new Case("N44", SJ, "E6 : DataManager null, LocalDb non initialisee, DownloadCores vrai, GamesImported=[G]",
            e => e.DataManagerNull = true, (e, p, s) =>
            {
                var action = new BooleanEmulatorImportAction("DownloadCores", "x", true);
                Env.Set(action, "Value", true);
                return p.FireEmulatorImportAction(new ImportActionArgs(action, new List<IGame> { e.G }, (a, b) => { }, () => false));
            }) { NeedsNetGuard = true };
    }

    private static void CoreFiles(Env e)
    {
        foreach (string f in new[] { "a_libretro.dll", "b_libretro.dll", "c.txt", "d.dll_old", "e.dllx" })
        {
            e.Put("RA\\cores\\" + f, "C");
        }
    }

    private static void RaFile(Env e, string content)
    {
        Directory.CreateDirectory(e.Data);
        File.WriteAllText(Path.Combine(e.Data, "RetroAchievements.cfg"), content, new System.Text.UTF8Encoding(false));
    }
}
