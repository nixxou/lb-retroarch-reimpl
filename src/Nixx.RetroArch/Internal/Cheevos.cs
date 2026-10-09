using System;
using System.IO;
using System.Text;
using Unbroken.LaunchBox.Plugins;

namespace Nixx.RetroArch.Internal
{
    /// <summary>Fichier RetroAchievements.cfg du greffon (spec § 2.16 à 2.18).</summary>
    internal static class Cheevos
    {
        internal static string FilePath(IPluginPaths pluginPaths)
            => Path.Combine(pluginPaths.DataDirectory, "RetroAchievements.cfg");

        private static string Content(string? token, string? username, bool hardcore)
            => "cheevos_enable = \"true\"\r\n"
               + "cheevos_token = \"" + token + "\"\r\n"
               + "cheevos_username = \"" + username + "\"\r\n"
               + "cheevos_hardcore_mode_enable = \"" + (hardcore ? "true" : "false") + "\"\r\n";

        internal static PrepareForLaunchResponse Prepare(IPluginPaths pluginPaths, PrepareForLaunchArgs args)
        {
            if (!args.RetroAchievementCredentials.HasValue)
            {
                return new PrepareForLaunchResponse(true);
            }

            string file = FilePath(pluginPaths);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            try
            {
                RetroAchievementCredentials credentials = args.RetroAchievementCredentials.Value;
                bool hardcore = args.EmulatorBeingLaunched != null && args.EmulatorBeingLaunched.EnableHardcoreAchievements;
                File.WriteAllText(file, Content(credentials.Token, credentials.Username, hardcore), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                return new PrepareForLaunchResponse(false, ex.Message);
            }

            return new PrepareForLaunchResponse(true)
            {
                NewCommandLine = args.CurrentCommandLine + " --appendconfig \"" + file + "\"",
            };
        }

        internal static RetroAchievementSupportResponse Read(IPluginPaths pluginPaths)
        {
            string file = FilePath(pluginPaths);
            if (!File.Exists(file))
            {
                return new RetroAchievementSupportResponse(true);
            }

            try
            {
                bool enabled = false;
                bool hardcore = false;
                string? username = null;
                string? token = null;
                foreach (string line in File.ReadAllLines(file))
                {
                    if (line.StartsWith("cheevos_enable"))
                    {
                        enabled = line.Contains("true");
                    }
                    else if (line.StartsWith("cheevos_hardcore_mode_enable"))
                    {
                        hardcore = line.Contains("true");
                    }
                    else if (line.StartsWith("cheevos_token"))
                    {
                        token = line.Replace("cheevos_token", "").Replace("=", "").Replace("\"", "").Trim();
                    }
                    else if (line.StartsWith("cheevos_username"))
                    {
                        username = line.Replace("cheevos_username", "").Replace("=", "").Replace("\"", "").Trim();
                    }
                }

                return new RetroAchievementSupportResponse(true, enabled, hardcore, username, token);
            }
            catch
            {
                return new RetroAchievementSupportResponse(true);
            }
        }

        internal static PluginResponse Inject(IPluginPaths pluginPaths, InjectRetroAchievementsCredentialsArgs args)
        {
            string file = FilePath(pluginPaths);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            try
            {
                bool hardcore = args.HardcoreEnabled != false;
                File.WriteAllText(file, Content(args.Token, args.Username, hardcore), new UTF8Encoding(false));
                return new PluginResponse(true);
            }
            catch (Exception ex)
            {
                return new PluginResponse(false, ex.Message);
            }
        }
    }
}
