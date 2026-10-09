using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Nixx.RetroArch.Internal;
using Nixx.RetroArch.Models;
using Unbroken.LaunchBox.LocalDb.DbModels;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Properties;
using Unbroken.LaunchBox.Windows;

namespace Nixx.RetroArch
{
    /// <summary>Intégration RetroArch pour LaunchBox (clone comportemental, voir docs\spec.md).</summary>
    public class RetroArchPlugin : EmulatorPlugin, IEmulatorWithCores
    {
        private readonly IPluginPaths _pluginPaths;

        // § 2.11 : lignes reçues de --version ; partagé par l'instance (deux appels simultanés se voient).
        private ConcurrentBag<string> _versionLines = new ConcurrentBag<string>();

        public RetroArchPlugin(IPluginPaths pluginPaths)
        {
            _pluginPaths = pluginPaths ?? throw new ArgumentNullException(nameof(pluginPaths));
        }

        // § 2.2
        public override string EmulatorName => "RetroArch";

        // § 2.3
        public override bool SupportsSaveManagement() => true;

        // § 2.4
        public override IReadOnlyDictionary<int, string> GetPotentialSaveSlots()
        {
            var slots = new Dictionary<int, string> { { -1, "Auto" } };
            for (int i = 0; i <= 9; i++)
            {
                slots.Add(i, i.ToString());
            }

            return slots;
        }

        // § 2.5
        public override IEnumerable<IEmulator> GetApplicableEmulators(IEnumerable<IEmulator> emulators)
        {
            var result = new List<IEmulator>();
            if (emulators == null)
            {
                return result;
            }

            foreach (IEmulator emulator in emulators)
            {
                if (emulator != null
                    && !string.IsNullOrWhiteSpace(emulator.ApplicationPath)
                    && string.Equals(Path.GetFileName(emulator.ApplicationPath), "retroarch.exe", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(emulator);
                }
            }

            return result;
        }

        // § 2.6
        public override EmulatorSupportResponse IsPlatformSupported(string platform)
        {
            DbEmulator? db = LocalDbRetroArch.Get();
            if (db == null || db.Platforms == null)
            {
                return new EmulatorSupportResponse(false, false);
            }

            List<DbEmulatorPlatform> matches = db.Platforms
                .Where(p => p.Platform.Equals(platform, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            DbEmulatorPlatform? chosen = matches.FirstOrDefault(p => p.Recommended) ?? matches.FirstOrDefault();
            if (chosen == null)
            {
                return new EmulatorSupportResponse(false, false);
            }

            return new EmulatorSupportResponse(true, chosen.Recommended);
        }

        // § 2.7
        public override IEnumerable<EmulatorImportAction> GetImportActions()
        {
            // Expression de collection : liste en lecture seule à un élément générée par le compilateur.
            return [new BooleanEmulatorImportAction("DownloadCores", Strings.LabelDownloadRetroArchCore, true)];
        }

        // § 2.8
        public string[] GetAllAvailableCores(string emulatorFilePath)
        {
            if (string.IsNullOrWhiteSpace(emulatorFilePath))
            {
                return Array.Empty<string>();
            }

            string? folder = Path.GetDirectoryName(emulatorFilePath);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return Array.Empty<string>();
            }

            string cores = FileHelper.GetFullPath(Path.Combine(folder, "cores"));
            if (!Directory.Exists(cores))
            {
                return Array.Empty<string>();
            }

            // Dossier vide : Array.Empty partagé ; sinon tableau neuf (mesuré sur l'original).
            return Directory.GetFiles(cores, "*.dll").Select(FileHelper.GetFileNameWithoutExtensionSafe).ToArray();
        }

        // § 2.9
        public string[] GetSuggestedCores(string emulatorFilePath, string platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                return Array.Empty<string>();
            }

            DbEmulator? db = LocalDbRetroArch.Get();
            if (db == null || db.Platforms == null)
            {
                return Array.Empty<string>();
            }

            DbEmulatorPlatform? entry = db.Platforms
                .OrderByDescending(p => p.Recommended)
                .FirstOrDefault(p => p.Platform.Equals(platform, StringComparison.InvariantCultureIgnoreCase));
            if (entry == null)
            {
                return Array.Empty<string>();
            }

            string core = Unbroken.LaunchBox.Windows.Search.LocalDbWindows.GetCommandLineOrCore(true, entry.CommandLine, true);
            if (string.IsNullOrWhiteSpace(core))
            {
                return Array.Empty<string>();
            }

            return new[] { core.Trim() };
        }

        // § 2.10.a
        public override IEnumerable<EmulatorBiosFile> GetBiosFilesForPlatform(string platform)
        {
            Bios.UpdateSystemFolder(null);
            return Bios.FromLocalDb(platform);
        }

        // § 2.10.b
        public override IEnumerable<EmulatorBiosFile> GetBiosFilesForPlatform(string emulatorApplicationPath, string platform, string commandLine)
        {
            string? root = string.IsNullOrWhiteSpace(emulatorApplicationPath) ? null : Path.GetDirectoryName(emulatorApplicationPath);
            Bios.UpdateSystemFolder(root);

            if (!string.IsNullOrWhiteSpace(emulatorApplicationPath) && !string.IsNullOrWhiteSpace(commandLine))
            {
                string? coreFile = CommandLines.FindCoreFile(commandLine);
                if (coreFile != null)
                {
                    try
                    {
                        return Bios.ReadTable(coreFile, platform);
                    }
                    catch
                    {
                    }
                }
            }

            return Bios.FromLocalDb(platform);
        }

        // § 2.11
        public override string GetCurrentVersion(string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(applicationPath) || !File.Exists(applicationPath))
            {
                return null!;
            }

            _versionLines = new ConcurrentBag<string>();
            try
            {
                var startInfo = new ProcessStartInfo(applicationPath, "--version")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += OnVersionLine;
                process.ErrorDataReceived += OnVersionLine;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
            catch
            {
                return null!;
            }

            string? line = _versionLines.FirstOrDefault(l => l.StartsWith("Version: "));
            if (line == null)
            {
                return null!;
            }

            int space = line.IndexOf(' ');
            // La fin de ligne (après les k − 2 caractères gardés) est prise d'abord : une ligne trop courte
            // lève donc sur la position de départ (paramètre startIndex), comme l'original.
            string tail = line.Substring(space + 7);
            return line.Substring(9, line.Length - tail.Length - 9);
        }

        private void OnVersionLine(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                _versionLines.Add(e.Data);
            }
        }

        // § 2.12
        public override IEnumerable<EmulatorControllerVersion> GetInstallableVersions()
        {
            string version = RetroArchWeb.GetLatestVersion();
            if (string.IsNullOrWhiteSpace(version))
            {
                return null!;
            }

            return [new EmulatorControllerVersion(version, version)];
        }

        // § 2.13
        public override EmulatorInstallResponse InstallEmulator(InstallEmulatorArgs args) => Installer.InstallEmulator(args);

        // § 2.14
        public PluginResponse UpdateEmulator(InstallEmulatorArgs args) => Installer.UpdateEmulator(args, GetCurrentVersion, InstallEmulator);

        // § 2.15
        public override PluginResponse FireEmulatorImportAction(ImportActionArgs args) => Installer.FireImportAction(args);

        // § 2.16
        public override PrepareForLaunchResponse PrepareEmulatorForLaunch(PrepareForLaunchArgs args) => Cheevos.Prepare(_pluginPaths, args);

        // § 2.17
        public override RetroAchievementSupportResponse SupportsRetroAchievements(string emulatorApplicationPath) => Cheevos.Read(_pluginPaths);

        // § 2.18
        public override PluginResponse InjectRetroAchievementsCredentials(InjectRetroAchievementsCredentialsArgs args) => Cheevos.Inject(_pluginPaths, args);

        // § 3.2
        public override GetSavesResponse GetSaves(GetSavesArgs args) => Saves.GetSaves(args);

        // § 3.3
        public override AddSaveResponse AddSaveFile(AddSaveArgs args) => Saves.AddSaveFile(args);

        // § 3.4
        public override bool IsSecondarySaveFile(string filePath) => Saves.IsSecondarySaveFile(filePath);

        // § 3.5
        public override IReadOnlyList<string> GetCompanionSaveFiles(string primaryFilePath) => Saves.GetCompanionSaveFiles(primaryFilePath);

        // § 3.6
        public override bool TryGetSaveGroupInfo(GameSaveBase save, string primaryFilePath, out string saveGroupId, out string saveGroupName)
        {
            bool result = Saves.TryGetSaveGroupInfo(save, primaryFilePath, out string? id, out string? name);
            saveGroupId = id!;
            saveGroupName = name!;
            return result;
        }

        // § 3.7
        public override bool UseSaveGroupIdForPersistedMatch(GameSaveBase save) => Saves.UseSaveGroupIdForPersistedMatch(save);

        // § 3.8
        public override bool TryComputeSaveSignature(GameSaveBase save, string emulatorApplicationPath, out string signature)
        {
            bool result = Saves.TryComputeSaveSignature(save, out string? computed);
            signature = computed!;
            return result;
        }
    }
}
