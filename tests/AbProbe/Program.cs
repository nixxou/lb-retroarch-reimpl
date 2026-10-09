using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace AbProbe;

// Point d'entree. Les assemblies de LaunchBox (SDK compris) sont resolues depuis le dossier Core de LaunchBox,
// lu seulement. Aucun type du SDK ne doit etre touche avant l'abonnement a Resolving.
public static class Program
{
    public static string LbCore { get; private set; }

    public static int Main(string[] args)
    {
        LbCore = Environment.GetEnvironmentVariable("ABPROBE_LBCORE")
            ?? typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .First(a => a.Key == "LbCore").Value;

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string candidate = Path.Combine(LbCore, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        return Run(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args) => Runner.Run(args);
}
