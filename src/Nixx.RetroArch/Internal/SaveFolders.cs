using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unbroken.LaunchBox;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Windows;

namespace Nixx.RetroArch.Internal
{
    /// <summary>Lecture du retroarch.cfg et résolution des dossiers de saves et de savestates (spec § 3.1.1 à 3.1.3).</summary>
    internal static class SaveFolders
    {
        private sealed class Keys
        {
            internal Keys(string inContentDir, string directory, string sortByContent, string sortByCore)
            {
                InContentDir = inContentDir;
                Directory = directory;
                SortByContent = sortByContent;
                SortByCore = sortByCore;
            }

            internal string InContentDir { get; }
            internal string Directory { get; }
            internal string SortByContent { get; }
            internal string SortByCore { get; }
        }

        private static readonly Keys SaveKeys = new Keys("savefiles_in_content_dir", "savefile_directory", "sort_savefiles_by_content_enable", "sort_savefiles_enable");
        private static readonly Keys StateKeys = new Keys("savestates_in_content_dir", "savestate_directory", "sort_savestates_by_content_enable", "sort_savestates_enable");

        /// <summary>Dossier des saves de jeu (§ 3.1.2), ou null.</summary>
        internal static string? GetSaveFolder(IEmulator emulator, IGame? game, IAdditionalApplication? app, bool create)
            => Resolve(emulator, game, app, create, SaveKeys, states: false);

        /// <summary>Dossier des savestates (§ 3.1.3), ou null.</summary>
        internal static string? GetStateFolder(IEmulator emulator, IGame? game, IAdditionalApplication? app, bool create)
            => Resolve(emulator, game, app, create, StateKeys, states: true);

        /// <summary>Cfg décodé selon § 3.1.1, ou null s'il manque ou s'il est invalide.</summary>
        private static Dictionary<string, string>? ReadCfg(string raDir)
        {
            string path = Path.Combine(raDir, "retroarch.cfg");
            if (!File.Exists(path))
            {
                return null;
            }

            var values = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(path))
            {
                int first = line.IndexOf('=');
                if (first < 0)
                {
                    return null;
                }

                string key = line.Substring(0, first).Trim();
                string rest = line.Substring(first + 1);
                int second = rest.IndexOf('=');
                string value = (second < 0 ? rest : rest.Substring(0, second)).Trim();
                if (value.Length < 2)
                {
                    return null;
                }

                values[key] = value.Substring(1, value.Length - 2);
            }

            return values;
        }

        private static bool IsTrue(Dictionary<string, string> cfg, string key)
            => cfg.TryGetValue(key, out string? value) && value == "true";

        private static string? Resolve(IEmulator emulator, IGame? game, IAdditionalApplication? app, bool create, Keys keys, bool states)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(emulator.ApplicationPath))
                {
                    return null;
                }

                string? content = !string.IsNullOrWhiteSpace(app?.ApplicationPath) ? app!.ApplicationPath : game?.ApplicationPath;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }

                string raDir = Path.GetDirectoryName(emulator.ApplicationPath)!;
                Dictionary<string, string>? cfg = ReadCfg(raDir);
                if (cfg == null)
                {
                    return null;
                }

                string result;
                if (IsTrue(cfg, keys.InContentDir))
                {
                    result = Path.GetDirectoryName(content)!;
                }
                else
                {
                    if (!cfg.TryGetValue(keys.Directory, out string? directory))
                    {
                        return null;
                    }

                    if (directory.StartsWith(":\\"))
                    {
                        string exe = Path.GetFullPath(emulator.ApplicationPath, NamingHelper.RootFolder);
                        result = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory.Substring(2), Path.GetDirectoryName(exe)!));
                    }
                    else
                    {
                        result = directory;
                    }
                }

                if (IsTrue(cfg, keys.SortByContent))
                {
                    result = Path.Combine(result, Path.GetFileName(Path.GetDirectoryName(content))!);
                }

                if (IsTrue(cfg, keys.SortByCore))
                {
                    string? commandLine;
                    if (!states)
                    {
                        if (!string.IsNullOrWhiteSpace(app?.CommandLine))
                        {
                            commandLine = app!.CommandLine;
                        }
                        else if (!string.IsNullOrWhiteSpace(game?.CommandLine))
                        {
                            commandLine = game!.CommandLine;
                        }
                        else if (game == null || string.IsNullOrWhiteSpace(game.Platform))
                        {
                            return null;
                        }
                        else
                        {
                            IEmulatorPlatform? line = emulator.GetAllEmulatorPlatforms()?.FirstOrDefault(p => p.Platform == game.Platform);
                            commandLine = line != null && !string.IsNullOrWhiteSpace(line.CommandLine) ? line.CommandLine : emulator.CommandLine;
                        }
                    }
                    else
                    {
                        string? effective = null;
                        if (app != null)
                        {
                            effective = app.GetEffectiveCommandLine();
                        }

                        if (effective == null)
                        {
                            effective = game?.GetEffectiveCommandLine();
                        }

                        commandLine = effective;
                    }

                    if (string.IsNullOrWhiteSpace(commandLine))
                    {
                        return null;
                    }

                    string? core = CommandLines.FindCoreName(commandLine);
                    if (core == null)
                    {
                        return null;
                    }

                    string info = Path.Combine(raDir, "info", core + ".info");
                    if (!File.Exists(info))
                    {
                        return null;
                    }

                    string? coreLine = File.ReadAllLines(info).FirstOrDefault(l => l.StartsWith("corename"));
                    if (coreLine == null || coreLine.Length < 12)
                    {
                        return null;
                    }

                    string coreName = coreLine.Substring(12, coreLine.Length - 13);
                    if (string.IsNullOrWhiteSpace(coreName))
                    {
                        return null;
                    }

                    result = Path.Combine(result, coreName);
                }

                if (string.IsNullOrWhiteSpace(result))
                {
                    return null;
                }

                if (!Directory.Exists(result))
                {
                    if (!create)
                    {
                        return null;
                    }

                    Directory.CreateDirectory(result);
                }

                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
