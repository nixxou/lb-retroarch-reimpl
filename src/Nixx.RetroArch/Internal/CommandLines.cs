using System.Text.RegularExpressions;

namespace Nixx.RetroArch.Internal
{
    /// <summary>Formats reconnus dans les lignes de commande (spec § 4).</summary>
    internal static class CommandLines
    {
        // § 4.1 : nom sans point (sauts de ligne permis), un caractère libre (hors saut de ligne), puis dll".
        private static readonly Regex CoreNameRegex = new Regex(@"-L ""cores\\([^.]+).dll""");

        // § 4.2 : au moins un caractère hors saut de ligne, jusqu'au dernier .dll" de la même ligne.
        private static readonly Regex CoreFileRegex = new Regex(@"-L ""cores\\(.+)\.dll""");

        /// <summary>Nom de cœur (§ 4.1), ou null. Ligne null : ArgumentNullException.</summary>
        internal static string? FindCoreName(string? commandLine)
        {
            Match m = CoreNameRegex.Match(commandLine!);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Nom de fichier de cœur (§ 4.2), ou null.</summary>
        internal static string? FindCoreFile(string commandLine)
        {
            Match m = CoreFileRegex.Match(commandLine);
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
