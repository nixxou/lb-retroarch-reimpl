using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace AbProbe;

// Section 13 de la matrice (option --network, accord de Mehdi du 09/10). Deux familles :
//   Real : vrai buildbot / GitHub, pour les cas dont la reponse ne se fabrique pas (version publiee, 404 reel...) ;
//   Fake : faux serveur local (NetGuard redirige les URL), pour les reponses construites (erreurs, JSON, archives
//          corrompues, delais) et pour les installations completes avec de petites archives faites par 7-Zip.
// 7-Zip est copie dans <racine factice>\ThirdParty\7-Zip depuis G:\LB1326\ThirdParty\7-Zip (lu seulement).
public static class Network
{
    public const string Section = "13. Reseau, telechargement, installation";
    public const string SevenZipSource = @"G:\LB1326\ThirdParty\7-Zip";

    private const string Buildbot = "/buildbot.libretro.com/stable/";
    private const string GitHubTags = "/api.github.com/repos/libretro/RetroArch/tags";

    public static string LatestReal { get; private set; }

    public static byte[] ProgramArchive { get; private set; }

    public static byte[] CoresArchive { get; private set; }

    public static FakeServer Server { get; set; }

    public static List<(string Id, string Reason)> NotPlayed()
    {
        var list = new List<(string, string)>();
        foreach (string id in new[] { "N10", "N21", "N22", "N40", "N41", "N46", "N47", "N48c" })
        {
            list.Add((id, "[LB] : LocalDb reelle ou session LaunchBox"));
        }

        foreach (string id in new[] { "N32", "N33", "N38", "N39" })
        {
            list.Add((id, "FireEmulatorImportAction interroge la LocalDb avant tout telechargement : exige la LocalDb reelle ([LB] de fait)"));
        }

        list.Add(("N43", "ouvre une boite modale de LaunchBox chez l'original : joue seulement avec --interactive (run par defaut)"));
        list.Add(("N43b", "idem N43 (racine null) : --interactive"));
        return list;
    }

    // Petites archives valides (dossier racine RetroArch-Win64), faites une fois par run avec le 7-Zip de LaunchBox.
    public static void BuildFixtures(string dir)
    {
        string staging = Path.Combine(dir, "staging");
        var random = new Random(42);
        byte[] Pad(int size)
        {
            var b = new byte[size];
            random.NextBytes(b);
            return b;
        }

        Directory.CreateDirectory(Path.Combine(staging, "p", "RetroArch-Win64"));
        File.WriteAllText(Path.Combine(staging, "p", "RetroArch-Win64", "retroarch.exe"), "FAKE RETROARCH EXE");
        File.WriteAllBytes(Path.Combine(staging, "p", "RetroArch-Win64", "pad.bin"), Pad(400 * 1024));
        Directory.CreateDirectory(Path.Combine(staging, "c", "RetroArch-Win64", "cores"));
        File.WriteAllText(Path.Combine(staging, "c", "RetroArch-Win64", "cores", "snes9x_libretro.dll"), "FAKE CORE");
        File.WriteAllBytes(Path.Combine(staging, "c", "RetroArch-Win64", "cores", "pad.bin"), Pad(300 * 1024));
        ProgramArchive = Make7z(Path.Combine(staging, "p"), Path.Combine(dir, "RetroArch.7z"));
        CoresArchive = Make7z(Path.Combine(staging, "c"), Path.Combine(dir, "RetroArch_cores.7z"));
    }

    private static byte[] Make7z(string cwd, string output)
    {
        var psi = new ProcessStartInfo(Path.Combine(SevenZipSource, "7z.exe"), $"a -t7z -mx=1 \"{output}\" RetroArch-Win64")
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi);
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("7-Zip : archive de test non creee (" + process.ExitCode + ")");
        }

        return File.ReadAllBytes(output);
    }

    // Monde par defaut du faux serveur ; les cas en remplacent des routes.
    private static Dictionary<string, FakeResponse> World(string version = "1.19.1")
    {
        return new Dictionary<string, FakeResponse>(StringComparer.Ordinal)
        {
            [Buildbot] = FakeResponse.Text(200, "<html><body>\n<a href=\"/stable/1.18.0/\">1.18.0/</a>\n<a href=\"/stable/1.19.1/\">1.19.1/</a>\n</body></html>\n"),
            [GitHubTags] = FakeResponse.Text(200, "[{\"name\":\"v1.19.1\"},{\"name\":\"v1.18.0\"}]"),
            [Buildbot + version + "/windows/x86_64/RetroArch.7z"] = new FakeResponse { Body = ProgramArchive },
            [Buildbot + version + "/windows/x86_64/RetroArch_cores.7z"] = new FakeResponse { Body = CoresArchive },
        };
    }

    private static void Serve(Dictionary<string, FakeResponse> routes)
        => Server.Handler = r => routes.TryGetValue(r.Path, out var resp) ? resp : null;

    private static Case Fake(string id, string input, Action<Env, Dictionary<string, FakeResponse>> setup, Func<Env, EmulatorPlugin, Side, object> act)
        => new(id, Section, "[faux serveur] " + input, e =>
        {
            Copy7z(e);
            var routes = World();
            setup?.Invoke(e, routes);
            Serve(routes);
        }, act) { Net = NetMode.Fake };

    private static Case Real(string id, string input, Action<Env> setup, Func<Env, EmulatorPlugin, Side, object> act)
        => new(id, Section, "[reseau reel] " + input, e =>
        {
            Copy7z(e);
            setup?.Invoke(e);
        }, act) { Net = NetMode.Real };

    private static void Copy7z(Env e)
    {
        string target = Path.Combine(e.L, "ThirdParty", "7-Zip");
        Directory.CreateDirectory(target);
        foreach (string f in new[] { "7z.exe", "7z.dll" })
        {
            File.Copy(Path.Combine(SevenZipSource, f), Path.Combine(target, f));
        }
    }

    private static IEmulator Existing(Env e, string path)
    {
        IEmulator existing = e.NewEmulator("existant", "emu9", path);
        e.Emus["emu9"] = existing;
        return existing;
    }

    // Appel d'installation avec suivi de la progression et de l'annulation.
    private static object Install(Env e, IEmulator existing, string version, Func<double?, bool> cancelWhen, Func<InstallEmulatorArgs, object> call, Action<double?> onProgress = null)
    {
        var labels = new List<string>();
        double? last = null;
        bool monotonic = true;
        bool anyProgress = false;
        Action<string, string, double?> progress = (a, b, v) =>
        {
            string label = a + " | " + b;
            if (!labels.Contains(label))
            {
                labels.Add(label);
            }

            if (last != null && v != null && v < last)
            {
                monotonic = false;
            }

            anyProgress = true;
            last = v;
            onProgress?.Invoke(v);
        };
        bool cancelAsked = false;
        Func<bool> cancel = () =>
        {
            bool c = cancelWhen != null && cancelWhen(last);
            cancelAsked |= c;
            return c;
        };
        var args = new InstallEmulatorArgs(Env.Snes, progress, cancel, existing, version);
        object response = Caught.Try(() => call(args));
        var rec = new Rec
        {
            { "response", response },
            { "progressLabels", labels },
            { "progressCalled", anyProgress },
            { "progressMonotonic", monotonic },
            { "cancelReturnedTrue", cancelAsked },
            { "existingApplicationPathAfter", existing == null ? "(aucun)" : AbProbe.Fake.Of(existing).P.GetValueOrDefault("ApplicationPath") },
        };
        if (cancelWhen == null)
        {
            rec.Add("lastProgress", last);
        }

        return rec;
    }

    private static object Call(Side side, object target, string method, params object[] args)
    {
        try
        {
            return side.PluginType.GetMethod(method).Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static object Versions(EmulatorPlugin p) => p.GetInstallableVersions();

    // Classe derivee du greffon qui surcharge InstallEmulator (N45, ecart E7).
    private static EmulatorPlugin Derived(Side side, IPluginPaths paths)
    {
        var asm = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("AbDerived_" + side.Name), AssemblyBuilderAccess.Run);
        var module = asm.DefineDynamicModule("m");
        var type = module.DefineType("DerivedRetroArchPlugin", TypeAttributes.Public | TypeAttributes.Class, side.PluginType);
        var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(IPluginPaths) });
        var il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, side.PluginType.GetConstructor(new[] { typeof(IPluginPaths) }));
        il.Emit(OpCodes.Ret);
        var install = type.DefineMethod("InstallEmulator", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(EmulatorInstallResponse), new[] { typeof(InstallEmulatorArgs) });
        il = install.GetILGenerator();
        il.Emit(OpCodes.Ldstr, "marqueur");
        il.Emit(OpCodes.Newobj, typeof(EmulatorInstallResponse).GetConstructor(new[] { typeof(string) }));
        il.Emit(OpCodes.Ret);
        Type created = type.CreateType();
        return (EmulatorPlugin)Activator.CreateInstance(created, paths);
    }

    public static IEnumerable<Case> All()
    {
        // ---- Versions ----
        yield return Real("N01", "GetInstallableVersions() sur le vrai buildbot", null, (e, p, s) =>
        {
            object versions = Caught.Try(() => Versions(p));
            if (s.Name == "original" && versions is IEnumerable<EmulatorControllerVersion> list)
            {
                LatestReal = list.FirstOrDefault()?.Label;
            }

            return versions;
        });
        yield return Fake("N02", "buildbot avec un lien nightly/, GitHub [{name: v1.2.3}]", (e, r) =>
        {
            r[Buildbot] = FakeResponse.Text(200, "<a href=\"/stable/1.19.1/\">1.19.1/</a>\n<a href=\"/stable/nightly/\">nightly/</a>\n");
            r[GitHubTags] = FakeResponse.Text(200, "[{\"name\":\"v1.2.3\"}]");
        }, (e, p, s) => Versions(p));
        yield return Fake("N03", "buildbot 500, GitHub OK", (e, r) => r[Buildbot] = FakeResponse.Text(500, "err"), (e, p, s) => Versions(p));
        yield return Fake("N04", "buildbot 500, GitHub 500", (e, r) =>
        {
            r[Buildbot] = FakeResponse.Text(500, "err");
            r[GitHubTags] = FakeResponse.Text(500, "err");
        }, (e, p, s) => Versions(p));
        yield return Fake("N05", "buildbot vide, GitHub []", (e, r) =>
        {
            r[Buildbot] = FakeResponse.Text(200, "");
            r[GitHubTags] = FakeResponse.Text(200, "[]");
        }, (e, p, s) => Versions(p));
        foreach (var (id, json) in new[]
                 {
                     ("N06", "[{\"nom\":\"x\"}]"), ("N25", "[{\"name\":\"V1.2.3\"}]"), ("N26", "[{\"name\":null}]"),
                     ("N27", "[{\"name\":123}]"), ("N28", "[{\"name\":{}}]"), ("N29", "[5]"), ("N30", "{}"), ("N36", "[[1]]"),
                 })
        {
            yield return Fake(id, "buildbot 500, GitHub " + json, (e, r) =>
            {
                r[Buildbot] = FakeResponse.Text(500, "err");
                r[GitHubTags] = FakeResponse.Text(200, json);
            }, (e, p, s) => Versions(p));
        }

        yield return Fake("N07", "en-tetes envoyes au buildbot et a GitHub (buildbot 500 pour forcer GitHub)",
            (e, r) => r[Buildbot] = FakeResponse.Text(500, "err"), (e, p, s) => Versions(p));
        yield return Fake("N24", "buildbot : liens valides puis un dernier lien tronque sans /\"", (e, r) =>
            r[Buildbot] = FakeResponse.Text(200, "<a href=\"/stable/1.18.0/\">x</a>\n<a href=\"/stable/1.19.1/\">x</a>\n<a href=\"/stable/1.99.0"),
            (e, p, s) => Versions(p));

        yield return Real("N09", "IsUpdateAvailable(EMU) herite, version locale = derniere version publiee (lue en N01)", e =>
        {
            if (LatestReal == null)
            {
                throw new InvalidOperationException("N01 n'a pas donne de version : N09 sans objet");
            }

            e.EMUf.P["ApplicationPath"] = e.MakeFakeExe($"Version: {LatestReal} (Git abc)\r\n");
        }, (e, p, s) =>
        {
            bool available = p.IsUpdateAvailable(e.EMU, out EmulatorControllerVersion version);
            return new Rec { { "available", available }, { "version", version } };
        });

        // ---- Installation ----
        yield return Fake("N08", "en-tetes envoyes pour les .7z (installation T\\MonRA 1.19.1, annulation au premier bloc)", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "1.19.1", last => last != null, p.InstallEmulator));
        yield return Fake("N11", "InstallEmulator, existant T\\RetroArch-Win64\\retroarch.exe, 1.19.1", null,
            (e, p, s) => Install(e, Existing(e, e.F("RetroArch-Win64\\retroarch.exe")), "1.19.1", null, p.InstallEmulator));
        yield return Real("N12", "InstallEmulator, existant T\\MonRA\\retroarch.exe, derniere version (archives reelles)", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), null, null, p.InstallEmulator));
        yield return Real("N13", "annulation pendant le telechargement du programme (au premier bloc), derniere version", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), null, last => last != null, p.InstallEmulator));
        yield return Fake("N14", "annulation pendant les coeurs (progression > 0.5)", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "1.19.1", last => last > 0.5, p.InstallEmulator));
        yield return Real("N15", "version 0.0.1 (404 reel), L\\Metadata absent", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "0.0.1", null, p.InstallEmulator));
        yield return Fake("N16", "archive du programme corrompue", (e, r) =>
            r[Buildbot + "1.19.1/windows/x86_64/RetroArch.7z"] = new FakeResponse { Body = Garbage(150 * 1024, 1) },
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "1.19.1", null, p.InstallEmulator));
        yield return Fake("N17", "archive des coeurs corrompue", (e, r) =>
            r[Buildbot + "1.19.1/windows/x86_64/RetroArch_cores.7z"] = new FakeResponse { Body = Garbage(150 * 1024, 2) },
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "1.19.1", null, p.InstallEmulator));
        yield return Fake("N18", "UpdateEmulator, existant sans ApplicationPath", null,
            (e, p, s) => Install(e, Existing(e, ""), null, null, a => Call(s, p, "UpdateEmulator", a)));
        yield return Real("N19", "UpdateEmulator, version locale = derniere version publiee (lue en N01)", e =>
        {
            if (LatestReal == null)
            {
                throw new InvalidOperationException("N01 n'a pas donne de version : N19 sans objet");
            }

            e.MakeFakeExe($"Version: {LatestReal} (Git abc)\r\n");
        }, (e, p, s) => Install(e, Existing(e, e.F("fake\\retroarch.exe")), null, null, a => Call(s, p, "UpdateEmulator", a)));
        yield return Fake("N20", "FireEmulatorImportAction, DownloadCores a Value=false", null, (e, p, s) =>
        {
            var action = new BooleanEmulatorImportAction("DownloadCores", "x", true);
            Env.Set(action, "Value", false);
            return p.FireEmulatorImportAction(new ImportActionArgs(action, new List<IGame> { e.G }, (a, b) => { }, () => false));
        });
        yield return Fake("N23", "delai HTTP depasse avant les en-tetes de RetroArch.7z (serveur muet 105 s), annulation fausse, T\\MonRA",
            (e, r) => r[Buildbot + "1.19.1/windows/x86_64/RetroArch.7z"] = new FakeResponse { StallBeforeHeadersMs = 105000 },
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "1.19.1", _ => false, p.InstallEmulator));
        yield return Fake("N31", "annulation juste apres le dernier bloc de RetroArch.7z (progression >= 0.5)", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "1.19.1", last => last >= 0.5, p.InstallEmulator));
        yield return Fake("N34", "existant ApplicationPath=\"retroarch.exe\" (sans dossier)", null,
            (e, p, s) => Install(e, Existing(e, "retroarch.exe"), "1.19.1", null, p.InstallEmulator));
        yield return Fake("N35", "existant a la racine d'un lecteur jetable (subst X: sur T\\drv), X:\\retroarch.exe", (e, r) =>
            {
                e.Subst("drv");
                e.Put("drv\\retroarch.exe", "OLD");
            },
            (e, p, s) => Install(e, Existing(e, e.DriveRoot + "retroarch.exe"), "1.19.1", null, p.InstallEmulator));
        yield return Fake("N37", "RootFolder = L\\ (separateur final), InstallEmulator sans emulateur existant", (e, r) => e.Root = e.L + "\\",
            (e, p, s) => Install(e, null, "1.19.1", null, p.InstallEmulator));
        yield return Fake("N42", "RootFolder=L sans Metadata, existant T\\MonRA, version 0.0.1 (404)", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "0.0.1", null, p.InstallEmulator));
        yield return Real("N45", "classe derivee qui surcharge InstallEmulator (\"marqueur\"), UpdateEmulator, exe existant absent (ecart E7)", null,
            (e, p, s) => Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), null, null, a => Call(s, Derived(s, e.Paths), "UpdateEmulator", a)));

        // ---- E11 : dernier segment du dossier de l'exe fait d'espaces ou de points ----
        yield return Fake("N48", "InstallEmulator, existant T\\Emu\\RA\\ \\retroarch.exe (T\\Emu\\RA existe), 1.19.1", (e, r) => e.Dir("Emu\\RA"),
            (e, p, s) => Install(e, Existing(e, e.F("Emu\\RA\\ \\retroarch.exe")), "1.19.1", null, p.InstallEmulator));
        yield return Fake("N48b", "UpdateEmulator, existant T\\Emu\\RA\\...\\retroarch.exe, exe absent", (e, r) => e.Dir("Emu\\RA"),
            (e, p, s) => Install(e, Existing(e, e.F("Emu\\RA\\...\\retroarch.exe")), null, null, a => Call(s, p, "UpdateEmulator", a)));
        yield return Fake("N48d", "sonde : InstallEmulator, existant T\\Emu\\RA\\.\\retroarch.exe", (e, r) => e.Dir("Emu\\RA"),
            (e, p, s) => Install(e, Existing(e, e.F("Emu\\RA\\.\\retroarch.exe")), "1.19.1", null, p.InstallEmulator));
        yield return Fake("N48e", "sonde : InstallEmulator, existant T\\Emu\\RA.\\retroarch.exe", (e, r) => e.Dir("Emu"),
            (e, p, s) => Install(e, Existing(e, e.F("Emu\\RA.\\retroarch.exe")), "1.19.1", null, p.InstallEmulator));

        // ---- N49 : partage du fichier temporaire, observe depuis le rappel de progression (aucune requete en plus) ----
        yield return Fake("N49", "pendant le telechargement de RetroArch.7z (serveur lent), ouverture du .7z temporaire en lecture avec partage lecture+ecriture", (e, r) =>
            r[Buildbot + "1.19.1/windows/x86_64/RetroArch.7z"] = new FakeResponse { Body = ProgramArchive, DelayPerChunkMs = 50 },
            (e, p, s) =>
            {
                string attempt = null;
                object result = Install(e, Existing(e, e.F("MonRA\\retroarch.exe")), "1.19.1", _ => attempt != null, p.InstallEmulator, v =>
                {
                    if (attempt != null)
                    {
                        return;
                    }

                    string temp = Path.Combine(e.L, "Metadata", "Temp");
                    string file = Directory.Exists(temp) ? Directory.GetFiles(temp, "*.7z").FirstOrDefault() : null;
                    if (file == null)
                    {
                        attempt = "aucun fichier .7z temporaire";
                        return;
                    }

                    try
                    {
                        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        attempt = "ouverture reussie";
                    }
                    catch (IOException ex)
                    {
                        attempt = "echec : " + ex.GetType().FullName + " HResult=0x" + ex.HResult.ToString("X8");
                    }
                });
                return new Rec { { "openAttempt", attempt }, { "install", result } };
            });
    }

    private static byte[] Garbage(int size, int seed)
    {
        var b = new byte[size];
        new Random(seed).NextBytes(b);
        return b;
    }
}
