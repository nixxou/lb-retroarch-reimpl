using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unbroken.LaunchBox.Plugins;

namespace AbProbe;

public static class Runner
{
    public const string OriginalDll =
        @"G:\LB1326\System\Plugins\RetroArch LaunchBox Integration\Unbroken.LaunchBox.Windows.RetroArch.dll";

    public const string ReplayCommand = "dotnet run -c Release --project tests\\AbProbe";

    public const string NetworkReplayCommand = "dotnet run -c Release --project tests\\AbProbe -- --network";

    // Verdicts imposes apres rejeu (INSTABLE).
    public static readonly Dictionary<string, string> Overrides = new(StringComparer.Ordinal);

    public static long RealBytes { get; private set; }

    private static FakeServer server;

    private static string probeDir;
    private static string work;
    private static PropertyInfo namingRoot;

    public static int Run(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string configuration = typeof(Runner).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "Configuration").Value;
        string repo = FindRepoRoot(baseDir);
        probeDir = Path.Combine(repo, "tests", "AbProbe");
        string cloneDll = Path.Combine(repo, "src", "Nixx.RetroArch", "bin", configuration, "net10.0-windows", "Nixx.RetroArch.dll");
        string fakeSource = Path.Combine(baseDir, "fake");
        string filter = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

        work = Path.Combine(Path.GetTempPath(), "nixx-ra-ab", "w");
        string lb = Path.GetFullPath(Path.Combine(Program.LbCore, ".."));
        if (work.StartsWith(lb, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Dossier de travail dans LaunchBox : refuse.");
        }

        var original = new Side("original", OriginalDll, "Unbroken.LaunchBox.Windows.RetroArch.RetroArchPlugin");
        var clone = new Side("clone", cloneDll, "Nixx.RetroArch.RetroArchPlugin");

        Type naming = AppDomain.CurrentDomain.GetAssemblies().Concat(new[] { System.Reflection.Assembly.Load("Unbroken.LaunchBox") })
            .Select(a => a.GetType("Unbroken.LaunchBox.NamingHelper")).First(t => t != null);
        namingRoot = naming.GetProperty("RootFolder", BindingFlags.Public | BindingFlags.Static);

        bool network = args.Contains("--network");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long freeBefore = new DriveInfo(Path.GetPathRoot(Path.GetTempPath())).AvailableFreeSpace;
        long nicBefore = NicBytes();
        WindowWatch.Start();
        if (network)
        {
            server = new FakeServer();
            Network.Server = server;
            Network.BuildFixtures(Path.Combine(Path.GetTempPath(), "nixx-ra-ab", "fixtures"));
        }

        NetGuard.Install(network, server);
        RemoveStaleSubst();
        WarmUp(original, clone, fakeSource);
        if (network && NetGuard.Armed)
        {
            // Chauffe de l'installation (7-Zip, journal de LaunchBox...) sur le faux serveur, hors comparaison.
            Case warm = Network.All().First(c => c.Id == "N11");
            RunSide(warm, original, fakeSource);
            RunSide(warm, clone, fakeSource);
        }

        List<Case> cases = network ? Network.All().ToList() : Cases.All().Concat(Fuzz.All()).ToList();
        if (filter != null)
        {
            cases = cases.Where(c => c.Id.StartsWith(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var results = new List<(Case Case, Outcome Orig, Outcome Clone)>();
        var skipped = new List<(string Id, string Reason)>();
        foreach (Case c in cases)
        {
            if (c.Interactive && !args.Contains("--interactive"))
            {
                skipped.Add((c.Id, "ouvre une boite modale de LaunchBox chez l'original (observe le 09/10) : joue seulement avec --interactive"));
                Console.WriteLine($"{c.Id,-8} NON JOUE (interactif)");
                continue;
            }

            if (c.Net == NetMode.Real && args.Contains("--no-real"))
            {
                skipped.Add((c.Id, "option --no-real (mise au point)"));
                continue;
            }

            if (c.Net == NetMode.Fake && !NetGuard.Armed)
            {
                skipped.Add((c.Id, "redirection vers le faux serveur inoperante (" + NetGuard.SelfTest + ")"));
                Console.WriteLine($"{c.Id,-8} NON JOUE (faux serveur)");
                continue;
            }

            if (c.NeedsNetGuard && !NetGuard.Armed)
            {
                skipped.Add((c.Id, "garde reseau inoperante (" + NetGuard.SelfTest + ") : non joue pour ne rien envoyer"));
                Console.WriteLine($"{c.Id,-8} NON JOUE (garde reseau)");
                continue;
            }

            Outcome o = RunSide(c, original, fakeSource);
            Outcome k = RunSide(c, clone, fakeSource);
            if (c.Net == NetMode.Real && Verdict(c, o, k) == "ROUGE" && o.NetPrint != k.NetPrint)
            {
                // Reponses du reseau differentes entre les deux appels : un seul rejeu.
                Console.WriteLine($"{c.Id,-8} reponses reseau differentes, rejeu");
                o = RunSide(c, original, fakeSource);
                k = RunSide(c, clone, fakeSource);
                if (Verdict(c, o, k) == "ROUGE" && o.NetPrint != k.NetPrint)
                {
                    Overrides[c.Id] = "INSTABLE";
                }
            }

            results.Add((c, o, k));
            Console.WriteLine($"{c.Id,-8} {Verdict(c, o, k)}");
            if (args.Contains("--dump"))
            {
                Console.WriteLine("--- original\n" + o.Value + "SystemFolder=" + o.SystemFolder + "\n" + o.Tree);
                Console.WriteLine("--- clone\n" + k.Value + "SystemFolder=" + k.SystemFolder + "\n" + k.Tree + "\n");
            }
        }

        long freeMin = new DriveInfo(Path.GetPathRoot(Path.GetTempPath())).AvailableFreeSpace;
        DeleteTree(Path.Combine(Path.GetTempPath(), "nixx-ra-ab"));
        server?.Dispose();

        string header = null;
        if (network)
        {
            header = $"- Mode `--network` (section 13) : garde {NetGuard.SelfTest}.\n"
                     + $"- Place libre sur {Path.GetPathRoot(Path.GetTempPath())} au debut : {freeBefore / (1024 * 1024)} Mio.\n"
                     + $"- Volume annonce par le vrai reseau (Content-Length cumules des cas reels, rejeux compris) : {RealBytes / (1024 * 1024)} Mio ;"
                     + $" octets recus par les cartes reseau pendant le run (toute la machine) : {(NicBytes() - nicBefore) / (1024 * 1024)} Mio.\n"
                     + $"- Fenetres visibles du processus pendant le run : {WindowWatch.Seen}.";
            Console.WriteLine($"duree {clock.Elapsed}");
        }

        List<(string Id, string Reason)> notPlayed = (network ? Network.NotPlayed() : Cases.NotPlayed()).Concat(skipped).ToList();
        string report = Report.Build(results, notPlayed, original, clone, filter, network, header);
        if (filter == null)
        {
            File.WriteAllText(Path.Combine(probeDir, network ? "last-run-network.md" : "last-run.md"), report.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
        }
        else
        {
            Console.WriteLine(report);
        }

        int red = results.Count(r => Verdict(r.Case, r.Orig, r.Clone) == "ROUGE");
        Console.WriteLine($"joues {results.Count}, rouges {red}");
        return red == 0 ? 0 : 1;
    }

    public static string Verdict(Case c, Outcome o, Outcome k)
    {
        if (Overrides.TryGetValue(c.Id, out string forced))
        {
            return forced;
        }

        if (o.ProbeError != null || k.ProbeError != null)
        {
            return "ERREUR-SONDE";
        }

        bool same = o.Value == k.Value && o.SystemFolder == k.SystemFolder && o.Tree == k.Tree;
        return same ? "VERT" : c.Informative ? "JAUNE" : "ROUGE";
    }

    // Les initialisations statiques de LaunchBox (Root, Strings, FileHelper...) ne doivent pas tomber dans le
    // premier cas joue : on les declenche une fois, hors comparaison, dans un dossier jetable.
    private static void WarmUp(Side original, Side clone, string fakeSource)
    {
        var warm = new Case("warmup", "", "", e =>
        {
            e.CfgOn("sort_savefiles_enable");
            e.Put("RA\\saves\\Game.srm", "S");
            e.Put("RA\\saves\\Game.bcr", "S");
        }, (e, p, s) => new Rec
        {
            { "saves", Caught.Try(() => e.Saves(p)) },
            { "add", Caught.Try(() => e.Add(p, Env.SG("g1", e.F("RA\\saves\\Game.bcr")), 1)) },
            { "bios", Caught.Try(() => p.GetBiosFilesForPlatform(e.F("RA\\retroarch.exe"), "x", "-L \"cores\\4do_libretro.dll\"")) },
            { "cores", Caught.Try(() => ((IEmulatorWithCores)p).GetAllAvailableCores("RA\\retroarch.exe")) },
            { "actions", Caught.Try(() => p.GetImportActions()) },
            { "ra", Caught.Try(() => p.SupportsRetroAchievements("x")) },
        });
        RunSide(warm, original, fakeSource);
        RunSide(warm, clone, fakeSource);
    }

    private static Outcome RunSide(Case c, Side side, string fakeSource)
    {
        DeleteTree(work);
        Directory.CreateDirectory(work);
        Env e;
        try
        {
            e = new Env(work, fakeSource);
            c.Setup?.Invoke(e);
            e.Finish();
        }
        catch (Exception ex)
        {
            Directory.SetCurrentDirectory(probeDir);
            RemoveStaleSubst();
            return new Outcome { ProbeError = "montage : " + ex };
        }

        object value;
        try
        {
            Directory.SetCurrentDirectory(e.Cwd);
            namingRoot.SetValue(null, e.Root);
            PluginHelper.DataManager = e.DataManagerNull ? null : e.DM;
            NetGuard.Take();
            server?.TakeLog();
            NetGuard.Mode = c.Net ?? NetMode.Block;
            side.SetSystemFolder(e.SystemFolderInit);
            EmulatorPlugin plugin = c.NoPlugin ? null : side.Create(e.Paths);
            value = c.Act(e, plugin, side);
        }
        catch (Exception ex)
        {
            value = new Caught(ex);
        }

        NetGuard.Mode = NetMode.Block;
        string netPrint = "";
        if (c.NeedsNetGuard)
        {
            value = new Rec { { "value", value }, { "requestsBlocked", NetGuard.Take().Select(r => r.Request).ToList() } };
        }
        else if (c.Net != null)
        {
            List<NetEvent> events = NetGuard.Take();
            if (c.Net == NetMode.Real)
            {
                netPrint = string.Join("\n", events.Select(r => r.Request + " -> " + r.Response));
                RealBytes += events.Where(r => r.ContentLength > 0).Sum(r => r.ContentLength);
            }

            var rec = new Rec { { "value", value }, { "requests", events } };
            if (c.Net == NetMode.Fake)
            {
                rec.Add("fakeServerLog", server.TakeLog());
            }

            value = rec;
        }

        string dump;
        try
        {
            dump = Dumper.Dump(value);
        }
        catch (Exception ex)
        {
            dump = "rendu impossible : " + new Caught(ex).Text;
        }

        string systemFolder = Dumper.Q(side.GetSystemFolder());
        if (e.Drive is char drive)
        {
            dump = dump.Replace(drive + ":\\\\", "X:\\\\", StringComparison.Ordinal);
            systemFolder = systemFolder.Replace(drive + ":\\\\", "X:\\\\", StringComparison.Ordinal);
        }
        foreach (IDisposable l in e.Locks)
        {
            l.Dispose();
        }

        Directory.SetCurrentDirectory(probeDir);
        foreach (Action cleanup in e.Cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception)
            {
                // Le lecteur restant est retire par RemoveStaleSubst au prochain cas ou au prochain run.
            }
        }

        PluginHelper.DataManager = null;
        dump = dump.Replace(Dumper.Q(work).Trim('"'), "<W>", StringComparison.OrdinalIgnoreCase);
        dump = dump.Replace(work, "<W>", StringComparison.OrdinalIgnoreCase);
        if (server != null)
        {
            dump = dump.Replace("127.0.0.1:" + server.Port, "<faux-serveur>", StringComparison.Ordinal);
        }

        return new Outcome
        {
            Value = Guids.Replace(dump, "<GUID>"),
            SystemFolder = systemFolder,
            Tree = Guids.Replace(Snapshot(work), "<GUID>"),
            NetPrint = netPrint,
        };
    }

    private static readonly System.Text.RegularExpressions.Regex Guids =
        new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    private static long NicBytes()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .Sum(n => n.GetIPStatistics().BytesReceived);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string Snapshot(string root)
    {
        var lines = new List<string>();
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            lines.Add("D " + Path.GetRelativePath(root, dir));
        }

        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            string hash;
            try
            {
                // Prefixe \\?\ : les dossiers dont le nom finit par une espace ou un point (cas N48) restent lisibles.
                using var stream = new FileStream(@"\\?\" + file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                hash = Convert.ToHexString(SHA256.HashData(stream)).Substring(0, 16);
            }
            catch (Exception ex)
            {
                hash = "illisible " + ex.GetType().Name;
            }

            string ro = info.Attributes.HasFlag(FileAttributes.ReadOnly) ? " RO" : "";
            lines.Add($"F {Path.GetRelativePath(root, file)} {info.Length} {hash}{ro}");
        }

        lines.Sort(StringComparer.Ordinal);
        return string.Join("\n", lines.Select(l => Dumper.Q(l).Trim('"').Replace("\\\\", "\\")));
    }

    // Retire tout lecteur subst laisse par un run interrompu et pointant dans le dossier de travail.
    private static void RemoveStaleSubst()
    {
        foreach (string line in Env.RunSubst("").Split('\n'))
        {
            if (line.Length > 2 && line[1] == ':' && line.Contains("nixx-ra-ab", StringComparison.OrdinalIgnoreCase))
            {
                Env.RunSubst(line.Substring(0, 2) + " /D");
            }
        }
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(@"\\?\" + file, FileAttributes.Normal);
        }

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(@"\\?\" + path, true);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                System.Threading.Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 20)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }

    private static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "nixx-retroarch.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Racine du depot introuvable depuis " + start);
    }
}

public static class Report
{
    public static string Build(List<(Case Case, Outcome Orig, Outcome Clone)> results, List<(string Id, string Reason)> notPlayed, Side original, Side clone, string filter, bool network = false, string header = null)
    {
        var sb = new StringBuilder();
        var verdicts = results.Select(r => (r.Case, r.Orig, r.Clone, V: Runner.Verdict(r.Case, r.Orig, r.Clone))).ToList();
        int green = verdicts.Count(v => v.V == "VERT");
        int red = verdicts.Count(v => v.V == "ROUGE");
        int yellow = verdicts.Count(v => v.V == "JAUNE");
        int err = verdicts.Count(v => v.V == "ERREUR-SONDE");
        int matrix = verdicts.Count(v => !v.Case.Id.StartsWith("FZ", StringComparison.Ordinal));

        sb.AppendLine("# Sonde A/B : original contre clone (greffon RetroArch)");
        sb.AppendLine();
        sb.AppendLine("Rapport produit par `tests\\AbProbe` ; ne pas l'editer a la main.");
        sb.AppendLine();
        sb.AppendLine($"- Rejouer depuis la racine du depot : `{(network ? Runner.NetworkReplayCommand : Runner.ReplayCommand)}` (construit aussi le clone).");
        if (header != null)
        {
            sb.AppendLine(header);
        }
        sb.AppendLine($"- Original : `{original.Path}` (lecture seule).");
        sb.AppendLine("- Clone : `src\\Nixx.RetroArch\\bin\\<config>\\net10.0-windows\\Nixx.RetroArch.dll`.");
        sb.AppendLine("- Juge : egalite stricte des deux greffons (valeur rendue en profondeur, exception type + message, arbre du");
        sb.AppendLine("  dossier de cas avec tailles et hash, `BiosFile.SystemFolder` apres l'appel). `<W>` = dossier de cas jetable.");
        if (!network)
        {
            sb.AppendLine($"- Garde reseau : {NetGuard.SelfTest}. Toute requete HTTP est interceptee avant connexion (proxy de secours : port ferme local).");
        }
        if (filter != null)
        {
            sb.AppendLine($"- Filtre : `{filter}`.");
        }

        sb.AppendLine();
        sb.AppendLine("## Resume");
        sb.AppendLine();
        sb.AppendLine($"- Cas joues : {verdicts.Count} ({matrix} de la matrice, {verdicts.Count - matrix} de fuzzing).");
        sb.AppendLine($"- VERTS : {green}. ROUGES : {red}. JAUNES (informatif, egalite non exigee) : {yellow}. Erreurs de sonde : {err}.");
        sb.AppendLine($"- Non joues : {notPlayed.Count} (liste en fin de rapport).");
        sb.AppendLine();

        foreach (var group in verdicts.GroupBy(v => v.Case.Section))
        {
            sb.AppendLine("## " + group.Key);
            sb.AppendLine();
            sb.AppendLine("| Cas | Verdict | Entree |");
            sb.AppendLine("|---|---|---|");
            foreach (var v in group)
            {
                sb.AppendLine($"| {v.Case.Id} | {v.V} | {Cell(v.Case.Input)} |");
            }

            sb.AppendLine();
        }

        var bad = verdicts.Where(v => v.V != "VERT").ToList();
        if (bad.Count > 0)
        {
            sb.AppendLine("## Details des ecarts");
            sb.AppendLine();
            foreach (var v in bad)
            {
                sb.AppendLine($"### {v.Case.Id} : {v.V}");
                sb.AppendLine();
                sb.AppendLine("Entree : " + v.Case.Input);
                sb.AppendLine();
                if (v.V == "ERREUR-SONDE")
                {
                    Block(sb, "Original", v.Orig.ProbeError ?? "(pas d'erreur)");
                    Block(sb, "Clone", v.Clone.ProbeError ?? "(pas d'erreur)");
                    continue;
                }

                if (v.Orig.Value != v.Clone.Value)
                {
                    Block(sb, "Valeur rendue, original", v.Orig.Value);
                    Block(sb, "Valeur rendue, clone", v.Clone.Value);
                }

                if (v.Orig.SystemFolder != v.Clone.SystemFolder)
                {
                    sb.AppendLine($"`BiosFile.SystemFolder` apres l'appel : original {v.Orig.SystemFolder}, clone {v.Clone.SystemFolder}.");
                    sb.AppendLine();
                }

                if (v.Orig.Tree != v.Clone.Tree)
                {
                    var o = v.Orig.Tree.Split('\n');
                    var k = v.Clone.Tree.Split('\n');
                    Block(sb, "Disque : seulement chez l'original", string.Join("\n", o.Except(k)));
                    Block(sb, "Disque : seulement chez le clone", string.Join("\n", k.Except(o)));
                }
            }
        }

        sb.AppendLine("## Non joues");
        sb.AppendLine();
        sb.AppendLine("| Cas | Raison |");
        sb.AppendLine("|---|---|");
        foreach (var (id, reason) in notPlayed)
        {
            sb.AppendLine($"| {id} | {reason} |");
        }

        return sb.ToString();
    }

    private static string Cell(string s) => s.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");

    private static void Block(StringBuilder sb, string title, string text)
    {
        var lines = text.TrimEnd('\n').Split('\n');
        sb.AppendLine(title + " :");
        sb.AppendLine();
        sb.AppendLine("```text");
        foreach (string line in lines.Take(120))
        {
            sb.AppendLine(line);
        }

        if (lines.Length > 120)
        {
            sb.AppendLine($"... ({lines.Length - 120} lignes de plus)");
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }
}
