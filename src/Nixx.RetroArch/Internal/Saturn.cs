using System;
using System.IO;
using Unbroken.LaunchBox.Data;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Windows;
using Unbroken.LaunchBox.Windows.Data;

namespace Nixx.RetroArch.Internal
{
    /// <summary>Contexte et groupes Saturn (spec § 3.1.4).</summary>
    internal static class Saturn
    {
        /// <summary>Extensions Saturn par rang : .bcr (primaire), .bkr, .smpc.</summary>
        internal static readonly string[] Extensions = { ".bcr", ".bkr", ".smpc" };

        /// <summary>Rang de l'extension Saturn du chemin (sans casse ordinal), ou -1.</summary>
        internal static int Rank(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < Extensions.Length; i++)
            {
                if (string.Equals(extension, Extensions[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        internal static bool HasSaturnExtension(string path) => Rank(path) >= 0;

        internal static string GroupId(string? baseName)
            => (string.IsNullOrWhiteSpace(baseName) ? "saturn" : "saturn-" + baseName.Trim()).ToLowerInvariant();

        internal static bool IsContext(string core, IGame? game)
        {
            if (core.Contains("saturn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (game == null || string.IsNullOrWhiteSpace(game.Platform))
            {
                return false;
            }

            if (game.Platform.Contains("saturn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            DataManager dataManager = Root.DataManager;
            if (dataManager != null
                && dataManager.Platforms != null
                && dataManager.Platforms.TryGetValue(game.Platform, out PlatformBase? platform)
                && platform != null
                && !string.IsNullOrWhiteSpace(platform.ScrapeAs)
                && platform.ScrapeAs.Contains("saturn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
