using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nixx.RetroArch.Models;
using Unbroken.LaunchBox.LocalDb.DbModels;
using Unbroken.LaunchBox.Plugins;

namespace Nixx.RetroArch.Internal
{
    /// <summary>Dossier « system » (§ 2.10), table BIOS (§ 5) et repli LocalDb.</summary>
    internal static class Bios
    {
        private const string ResourceName = "Nixx.RetroArch.Bios_Requirements.txt";

        /// <summary>Met à jour BiosFile.SystemFolder à partir du dossier racine (§ 2.10, lecture du dossier system).</summary>
        internal static void UpdateSystemFolder(string? root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                BiosFile.SystemFolder = "system";
                return;
            }

            string cfg = Path.Combine(root, "retroarch.cfg");
            if (!File.Exists(cfg))
            {
                BiosFile.SystemFolder = "system";
                return;
            }

            string[] lines = File.ReadAllLines(cfg);
            string? line = lines.FirstOrDefault(l => l.StartsWith("system_directory"));
            if (line == null || !line.Contains('='))
            {
                BiosFile.SystemFolder = "system";
                return;
            }

            string value = line.Substring(line.IndexOf('=') + 1).Replace("\"", "").Trim();
            if (value.StartsWith(":\\"))
            {
                value = value.Substring(2);
            }

            BiosFile.SystemFolder = value;
        }

        /// <summary>Lignes de la table pour un fichier de cœur et une plateforme (§ 5).</summary>
        internal static List<EmulatorBiosFile> ReadTable(string coreFile, string? platform)
        {
            string text;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)!)
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            var result = new List<EmulatorBiosFile>();
            var groups = new Dictionary<string, EmulatorBiosGroup>();
            foreach (string row in text.Split("\r\n"))
            {
                string[] cols = row.Split('\t');
                if (!cols[0].Equals(coreFile, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cols[1]) && !cols[1].Equals(platform, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                bool required = bool.TryParse(cols[5], out bool r) && r;
                string? md5 = string.IsNullOrWhiteSpace(cols[4]) ? null : cols[4];

                EmulatorBiosGroup? group = null;
                if (!string.IsNullOrWhiteSpace(cols[6]))
                {
                    if (!groups.TryGetValue(cols[6], out group))
                    {
                        bool groupRequired = bool.TryParse(cols[8], out bool gr) && gr;
                        group = new EmulatorBiosGroup(cols[6], cols[7], groupRequired, cols[9]);
                        groups.Add(cols[6], group);
                    }
                }

                result.Add(new BiosFile(cols[3], required, cols[2], md5, group));
            }

            return result;
        }

        /// <summary>Repli LocalDb (§ 2.10.a), avec la Location du SystemFolder courant.</summary>
        internal static IEnumerable<EmulatorBiosFile> FromLocalDb(string? platform)
        {
            DbEmulator? db = LocalDbRetroArch.Get();
            if (db == null || db.Platforms == null)
            {
                return Array.Empty<EmulatorBiosFile>();
            }

            DbEmulatorPlatform? entry = db.Platforms.FirstOrDefault(p => string.Equals(p.Platform, platform, StringComparison.OrdinalIgnoreCase));
            if (entry == null || string.IsNullOrWhiteSpace(entry.RequiredBiosFile))
            {
                return Array.Empty<EmulatorBiosFile>();
            }

            return [new BiosFile(entry.RequiredBiosFile, true)];
        }
    }
}
