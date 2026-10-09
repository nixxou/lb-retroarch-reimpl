using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unbroken.LaunchBox;
using Unbroken.LaunchBox.Data;
using Unbroken.LaunchBox.LocalDb;
using Unbroken.LaunchBox.LocalDb.DbModels;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Properties;
using Unbroken.LaunchBox.Windows;
using Unbroken.LaunchBox.Windows.Data;
using Unbroken.LaunchBox.Windows.Search;

namespace Nixx.RetroArch.Internal
{
    /// <summary>Installation, mise à jour et action d'import (spec § 2.13 à 2.15, § 6.4).</summary>
    internal static class Installer
    {
        private const string ArchiveRoot = "RetroArch-Win64";
        private const string NullGuid = "00000000-0000-0000-0000-000000000000";

        /// <summary>Renommage du dossier racine de l'archive puis extraction dans le parent du dossier cible (§ 2.13 étapes 6 et 7).</summary>
        private static void ExtractInto(string? archive, string targetFolder, string errorMessage)
        {
            string name = Path.GetFileName(targetFolder);
            if (!string.Equals(name, ArchiveRoot, StringComparison.Ordinal))
            {
                if (!SevenZip.RenameArchiveFolder(archive!, ArchiveRoot, name))
                {
                    throw new AccessViolationException(errorMessage);
                }
            }

            // Parent du chemin normalisé par Windows (un dernier segment d'espaces ou de points disparaît).
            if (!SevenZip.Extract(archive!, new DirectoryInfo(targetFolder).Parent?.FullName!))
            {
                throw new AccessViolationException(errorMessage);
            }
        }

        private static void DeleteArchive(string? archive)
        {
            try
            {
                File.Delete(archive!);
            }
            catch (Exception ex)
            {
                Root.Logging.PublishIgnoredException(ex);
            }
        }

        /// <summary>Étapes 3 à 8 (ou 9) pour une archive. Faux si l'appelant a annulé.</summary>
        private static bool InstallArchive(InstallEmulatorArgs? args, string url, string targetFolder, double offset, string errorMessage)
        {
            string? archive = RetroArchWeb.DownloadArchive(
                url,
                (done, total) => args?.ReportProgressAction?.Invoke(Strings.LabelDownloadingRetroArch, "", offset + (double)done / total * 0.5),
                () => ShouldCancel(args));

            if (ShouldCancel(args))
            {
                return false;
            }

            Directory.CreateDirectory(targetFolder);
            ExtractInto(archive, targetFolder, errorMessage);
            DeleteArchive(archive);
            return true;
        }

        private static bool ShouldCancel(InstallEmulatorArgs? args)
            => args?.ShouldCancelFunc != null && args.ShouldCancelFunc();

        internal static EmulatorInstallResponse InstallEmulator(InstallEmulatorArgs? args)
        {
            IEmulator? existing = args?.ExistingEmulator;
            string folder;
            if (existing != null && !string.IsNullOrWhiteSpace(existing.ApplicationPath))
            {
                folder = Path.GetDirectoryName(existing.ApplicationPath)!;
            }
            else
            {
                folder = Path.Combine(NamingHelper.RootFolder, "Emulators", "RetroArch");
            }

            string version = args?.Version ?? RetroArchWeb.GetLatestVersion();

            if (!InstallArchive(args, RetroArchWeb.ProgramUrl(version), folder, 0, Strings.ErrorRetroArchArchive))
            {
                return new EmulatorInstallResponse("");
            }

            if (!InstallArchive(args, RetroArchWeb.CoresUrl(version), folder, 0.5, Strings.ErrorCouldNotExtractRetroArchCores))
            {
                return new EmulatorInstallResponse("");
            }

            string applicationPath = FileHelper.GetRelativePath(Path.Combine(folder, "retroarch.exe"));
            if (existing != null)
            {
                existing.ApplicationPath = applicationPath;
                return new EmulatorInstallResponse(existing);
            }

            var emulator = new Emulator
            {
                Title = "RetroArch",
                ApplicationPath = applicationPath,
                CommandLine = "",
                NoQuotes = false,
                NoSpace = false,
                HideConsole = true,
                FileNameWithoutExtensionAndPath = false,
                AutoExtract = true,
                UseStartupScreen = true,
                DisableShutdownScreen = false,
                HideMouseCursorInGame = true,
                AggressiveWindowHiding = false,
                HideAllNonExclusiveFullscreenWindows = false,
                StartupLoadDelay = 5000,
                UsePauseScreen = true,
                SuspendProcessOnPause = true,
                ForcefulPauseScreenActivation = true,
                PauseAutoHotkeyScript = LocalDbEmulator.GetPauseAutoHotkeyScript("RetroArch"),
                ResumeAutoHotkeyScript = LocalDbEmulator.GetResumeAutoHotkeyScript("RetroArch"),
                ResetAutoHotkeyScript = LocalDbEmulator.GetResetAutoHotkeyScript("RetroArch"),
                SaveStateAutoHotkeyScript = LocalDbEmulator.GetSaveStateAutoHotkeyScript("RetroArch"),
                LoadStateAutoHotkeyScript = LocalDbEmulator.GetLoadStateAutoHotkeyScript("RetroArch"),
                SwapDiscsAutoHotkeyScript = LocalDbEmulator.GetSwapDiscsAutoHotkeyScript("RetroArch"),
                AutoHotkeyScript = LocalDbEmulator.GetAutoHotkeyScript("RetroArch"),
                ExitAutoHotkeyScript = LocalDbEmulator.GetExitAutoHotkeyScript("RetroArch"),
            };

            DbEmulator? db = LocalDbRetroArch.Get();
            if (db != null && db.Platforms != null)
            {
                foreach (DbEmulatorPlatform dbPlatform in db.Platforms)
                {
                    emulator.Platforms.Add(new EmulatorPlatform(
                        emulator.Id,
                        dbPlatform.Platform,
                        dbPlatform.CommandLine,
                        !Root.DataManager.GetDefaultEmulatorExistsForPlatform("RetroArch", dbPlatform.Platform),
                        false));

                    foreach (PlatformBase localPlatform in Root.DataManager.Platforms.Values)
                    {
                        if (dbPlatform.Platform == null || (localPlatform.ScrapeAs != dbPlatform.Platform && localPlatform.Name != dbPlatform.Platform))
                        {
                            continue;
                        }

                        foreach (GameBase game in Root.DataManager.Games.Values)
                        {
                            if (!game.Platform.Equals(localPlatform.Name, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (game.EmulatorId == NullGuid)
                            {
                                game.EmulatorId = emulator.Id;
                            }

                            if (game.AdditionalApplications != null)
                            {
                                foreach (AdditionalApplicationBase app in game.AdditionalApplications)
                                {
                                    if (app.EmulatorId == NullGuid)
                                    {
                                        app.EmulatorId = emulator.Id;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string? platform = args?.Platform;
            if (string.IsNullOrWhiteSpace(platform) || (db?.Platforms != null && db.Platforms.Any(p => p.Platform == platform)))
            {
                return new EmulatorInstallResponse(emulator);
            }

            Platform? local = Root.DataManager.GetPlatform(platform);
            string? commandLine = null;
            if (!string.IsNullOrWhiteSpace(local?.ScrapeAs))
            {
                commandLine = db?.Platforms?.FirstOrDefault(p => p.Platform.Equals(local!.ScrapeAs, StringComparison.Ordinal))?.CommandLine;
            }

            emulator.Platforms.Add(new EmulatorPlatform(
                emulator.Id,
                platform,
                commandLine ?? "",
                !Root.DataManager.GetDefaultEmulatorExistsForPlatform("RetroArch", platform),
                false));
            return new EmulatorInstallResponse(emulator);
        }

        internal static PluginResponse UpdateEmulator(InstallEmulatorArgs args, Func<string, string?> getCurrentVersion, Func<InstallEmulatorArgs, EmulatorInstallResponse> installEmulator)
        {
            if (args.ExistingEmulator == null || string.IsNullOrWhiteSpace(args.ExistingEmulator.ApplicationPath))
            {
                throw new InvalidOperationException(Strings.ErrorNoRetroArchAppPath);
            }

            string applicationPath = args.ExistingEmulator.ApplicationPath;
            if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(applicationPath)))
            {
                throw new InvalidOperationException(Strings.ErrorNoRetroArchAppPath);
            }

            string latest = RetroArchWeb.GetLatestVersion();
            try
            {
                if (File.Exists(applicationPath))
                {
                    string? current = getCurrentVersion(applicationPath);
                    if (string.Equals(current, latest, StringComparison.Ordinal))
                    {
                        return new PluginResponse(false, "You are already on the latest version of RetroArch!");
                    }
                }
            }
            catch (Exception ex)
            {
                Root.Logging.PublishIgnoredException(ex);
            }

            return installEmulator(args);
        }

        internal static PluginResponse FireImportAction(ImportActionArgs args)
        {
            EmulatorImportAction action = args.Action;
            if (action.Id != "DownloadCores" || !(action is BooleanEmulatorImportAction boolean) || !boolean.Value)
            {
                return new PluginResponse(true);
            }

            var emulatorIds = new List<string>();
            var platforms = new List<string>();
            foreach (IGame game in args.GamesImported)
            {
                if (game == null || string.IsNullOrWhiteSpace(game.EmulatorId) || string.IsNullOrWhiteSpace(game.Platform))
                {
                    continue;
                }

                if (!emulatorIds.Contains(game.EmulatorId))
                {
                    emulatorIds.Add(game.EmulatorId);
                }

                if (!platforms.Contains(game.Platform))
                {
                    platforms.Add(game.Platform);
                }
            }

            IDataManager dataManager = PluginHelper.DataManager;
            if (dataManager == null)
            {
                throw new NullReferenceException();
            }

            List<DbEmulatorPlatform> dbPlatforms = LocalDbRetroArch.Get()!.Platforms ?? new List<DbEmulatorPlatform>();

            foreach (string emulatorId in emulatorIds)
            {
                IEmulator? emulator = dataManager.GetEmulatorById(emulatorId);
                bool coreMissing = false;
                foreach (string platform in platforms)
                {
                    IEmulatorPlatform? line = null;
                    if (emulator != null)
                    {
                        line = emulator.GetAllEmulatorPlatforms()
                            .FirstOrDefault(p => p.Platform != null && p.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
                    }

                    if (line == null)
                    {
                        string? validName = GamesDb.GetValidPlatformNameAsync(platform).GetAwaiter().GetResult();
                        DbEmulatorPlatform? dbPlatform = string.IsNullOrWhiteSpace(validName)
                            ? null
                            : dbPlatforms.FirstOrDefault(p => p.Platform == validName);
                        if (dbPlatform == null)
                        {
                            continue;
                        }

                        if (emulator != null)
                        {
                            IEmulatorPlatform added = emulator.AddNewEmulatorPlatform();
                            added.Platform = platform;
                            added.CommandLine = dbPlatform.CommandLine;
                            added.IsDefault = !Root.DataManager.GetDefaultEmulatorExistsForPlatform(emulator.Title, platform);
                            added.M3uDiscLoadEnabled = false;
                        }
                    }

                    if (emulator != null && !string.IsNullOrWhiteSpace(emulator.ApplicationPath))
                    {
                        string? folder = Path.GetDirectoryName(emulator.ApplicationPath);
                        if (Directory.Exists(folder))
                        {
                            string coreFile = LocalDbWindows.GetRetroArchCoreFileName(line?.CommandLine!);
                            if (!File.Exists(Path.Combine(folder!, coreFile)))
                            {
                                coreMissing = true;
                            }
                        }
                    }
                }

                if (coreMissing)
                {
                    UpdateCores(emulator!, args);
                }
            }

            return new PluginResponse(true);
        }

        /// <summary>Mise à jour des cœurs d'un émulateur (§ 6.4).</summary>
        private static void UpdateCores(IEmulator emulator, ImportActionArgs args)
        {
            string version = RetroArchWeb.GetLatestVersion();
            string? archive = RetroArchWeb.DownloadArchive(
                RetroArchWeb.CoresUrl(version),
                (done, total) => args.ReportActionProgress(Strings.LabelDownloadingMissingCores, (double)done / total),
                args.ShouldCancelAction);

            if (args.ShouldCancelAction())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(emulator.ApplicationPath))
            {
                throw new InvalidOperationException(Strings.ErrorNoRetroArchAppPath);
            }

            string? folder = Path.GetDirectoryName(emulator.ApplicationPath);
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new AccessViolationException(Strings.ErrorCouldNotExtractRetroArchCores);
            }

            ExtractInto(archive, folder, Strings.ErrorCouldNotExtractRetroArchCores);
            DeleteArchive(archive);
        }
    }
}
