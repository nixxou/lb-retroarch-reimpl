using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace Nixx.RetroArch.Internal
{
    /// <summary>La famille des saves (spec § 3.2 à 3.8).</summary>
    internal static class Saves
    {
        private const string SaveFileGroupName = "My Save File";
        private const string SaveStateGroupName = "My Save State";

        // ---- § 3.2 GetSaves ----

        internal static GetSavesResponse GetSaves(GetSavesArgs args)
        {
            var found = new List<GameSaveBase>();
            IEmulator? emulator = args.Emulator;
            string? emulatorFileName = Path.GetFileName(emulator?.ApplicationPath);

            if (args.AdditionalApplications != null)
            {
                foreach (IAdditionalApplication? app in args.AdditionalApplications)
                {
                    if (app == null || string.IsNullOrWhiteSpace(app.ApplicationPath))
                    {
                        continue;
                    }

                    string core = CommandLines.FindCoreName(app.GetEffectiveCommandLine()) ?? "";
                    string baseName = Path.GetFileNameWithoutExtension(app.ApplicationPath);
                    IGame? game = PluginHelper.DataManager.GetGameById(app.GameId);
                    Collect(found, emulator!, game, app, core, baseName, game?.Id, app.Id, emulatorFileName);
                }
            }

            if (args.Games != null)
            {
                foreach (IGame? game in args.Games)
                {
                    if (game == null || string.IsNullOrWhiteSpace(game.ApplicationPath))
                    {
                        continue;
                    }

                    IAdditionalApplication[]? apps = game.GetAllAdditionalApplications();
                    if (apps != null && apps.Any(a => a.ApplicationPath == game.ApplicationPath))
                    {
                        continue;
                    }

                    string core = CommandLines.FindCoreName(game.GetEffectiveCommandLine()) ?? "";
                    string baseName = Path.GetFileNameWithoutExtension(game.ApplicationPath);
                    Collect(found, emulator!, game, null, core, baseName, game.Id, null, emulatorFileName);
                }
            }

            return new GetSavesResponse(found);
        }

        private static void Collect(List<GameSaveBase> found, IEmulator emulator, IGame? game, IAdditionalApplication? app,
            string core, string baseName, string? gameId, string? appId, string? emulatorFileName)
        {
            string? saveFolder = SaveFolders.GetSaveFolder(emulator, game, app, create: false);
            if (saveFolder != null)
            {
                List<string> remaining = Directory.GetFiles(saveFolder, baseName + "*.*").ToList();

                if (Saturn.IsContext(core, game))
                {
                    var groups = new List<KeyValuePair<string, List<string>>>();
                    foreach (string file in remaining)
                    {
                        if (!Saturn.HasSaturnExtension(file))
                        {
                            continue;
                        }

                        string name = Path.GetFileNameWithoutExtension(file);
                        int index = groups.FindIndex(g => string.Equals(g.Key, name, StringComparison.OrdinalIgnoreCase));
                        if (index < 0)
                        {
                            groups.Add(new KeyValuePair<string, List<string>>(name, new List<string> { file }));
                        }
                        else
                        {
                            groups[index].Value.Add(file);
                        }
                    }

                    foreach (KeyValuePair<string, List<string>> group in groups)
                    {
                        string primary = group.Value.OrderBy(Saturn.Rank).First();
                        found.Add(new GameSaveGame
                        {
                            GameId = gameId,
                            AdditionalApplicationId = appId,
                            FileLocation = primary,
                            EmulatorFileName = emulatorFileName,
                            EmulatorCore = core,
                            SaveGroupId = Saturn.GroupId(group.Key),
                            SaveGroupName = SaveFileGroupName,
                        });
                    }

                    remaining = remaining.Where(f => !Saturn.HasSaturnExtension(f)).ToList();
                }

                var selected = new List<string>();
                if (remaining.Count == 1)
                {
                    selected.Add(remaining[0]);
                }
                else
                {
                    string? srm = remaining.FirstOrDefault(f => Path.GetExtension(f) == ".srm");
                    string? mcr = remaining.FirstOrDefault(f => Path.GetExtension(f) == ".mcr");
                    if (srm != null)
                    {
                        selected.Add(srm);
                    }

                    if (mcr != null)
                    {
                        selected.Add(mcr);
                    }

                    if (srm == null && mcr == null)
                    {
                        selected.AddRange(remaining);
                    }
                }

                foreach (string file in selected)
                {
                    found.Add(new GameSaveGame
                    {
                        GameId = gameId,
                        AdditionalApplicationId = appId,
                        FileLocation = file,
                        EmulatorFileName = emulatorFileName,
                        EmulatorCore = core,
                        SaveGroupName = SaveFileGroupName,
                    });
                }
            }

            string? stateFolder = SaveFolders.GetStateFolder(emulator, game, app, create: false);
            if (stateFolder != null)
            {
                foreach (string file in Directory.GetFiles(stateFolder, baseName + ".state*"))
                {
                    string suffix = Path.GetExtension(file).Replace(".state", "").Trim();
                    int slot;
                    if (string.IsNullOrWhiteSpace(suffix))
                    {
                        slot = 0;
                    }
                    else if (suffix.Equals(".auto", StringComparison.OrdinalIgnoreCase) || suffix.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    {
                        slot = -1;
                    }
                    else if (!int.TryParse(suffix, out slot))
                    {
                        continue;
                    }

                    found.Add(new GameSaveState
                    {
                        GameId = gameId,
                        AdditionalApplicationId = appId,
                        FileLocation = file,
                        Slot = slot,
                        EmulatorFileName = emulatorFileName,
                        EmulatorCore = core,
                        SaveGroupName = SaveStateGroupName,
                    });
                }
            }
        }

        // ---- § 3.3 AddSaveFile ----

        internal static AddSaveResponse AddSaveFile(AddSaveArgs args)
        {
            GameSaveBase? source = args.SaveToAdd;
            if (source == null || string.IsNullOrWhiteSpace(source.FileLocation) || !File.Exists(source.FileLocation))
            {
                return new AddSaveResponse("The save file doesn't exist!");
            }

            IGame? game = PluginHelper.DataManager.GetGameById(source.GameId);
            if (game == null)
            {
                return new AddSaveResponse("Game doesn't exist!");
            }

            IAdditionalApplication? app = null;
            string emulatorId;
            if (!string.IsNullOrWhiteSpace(source.AdditionalApplicationId))
            {
                IAdditionalApplication[]? apps = game.GetAllAdditionalApplications();
                app = apps?.FirstOrDefault(a => a.Id == source.AdditionalApplicationId);
                if (app == null)
                {
                    return new AddSaveResponse("App doesn't exist!");
                }

                emulatorId = app.EmulatorId;
            }
            else
            {
                emulatorId = game.EmulatorId;
            }

            if (string.IsNullOrWhiteSpace(emulatorId))
            {
                return new AddSaveResponse("Can't get emulator record for game/app in question!");
            }

            IEmulator? emulator = PluginHelper.DataManager.GetEmulatorById(emulatorId);
            if (emulator == null)
            {
                return new AddSaveResponse("Can't get emulator record game/app in question!");
            }

            string content = !string.IsNullOrWhiteSpace(app?.ApplicationPath) ? app!.ApplicationPath : game.ApplicationPath;
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AddSaveResponse("No Game/App application path found!");
            }

            bool isState = source is GameSaveState;
            string? folder;
            string destination;
            if (source is GameSaveState sourceState)
            {
                string suffix;
                if (sourceState.Slot == null || sourceState.Slot == 0)
                {
                    suffix = ".state";
                }
                else if (sourceState.Slot > 0)
                {
                    suffix = ".state" + sourceState.Slot.Value;
                }
                else
                {
                    suffix = ".state.auto";
                }

                folder = SaveFolders.GetStateFolder(emulator, game, app, create: true);
                if (folder == null)
                {
                    return new AddSaveResponse("Couldn't determine where to place this save!");
                }

                destination = Path.Combine(folder, Path.GetFileNameWithoutExtension(content) + suffix);
            }
            else
            {
                folder = SaveFolders.GetSaveFolder(emulator, game, app, create: true);
                if (folder == null)
                {
                    return new AddSaveResponse("Couldn't determine where to place this save!");
                }

                destination = Path.Combine(folder, Path.GetFileNameWithoutExtension(content) + Path.GetExtension(source.FileLocation));
            }

            if (File.Exists(destination) && (args.ShouldOverwriteFunc == null || !args.ShouldOverwriteFunc()))
            {
                return new AddSaveResponse("Operation cancelled by user!");
            }

            string? effective = null;
            if (app != null)
            {
                effective = app.GetEffectiveCommandLine();
            }

            if (effective == null)
            {
                effective = game.GetEffectiveCommandLine();
            }

            string core = CommandLines.FindCoreName(effective) ?? "";
            bool saturn = Saturn.IsContext(core, game);

            try
            {
                File.Copy(source.FileLocation, destination, true);
                if (saturn)
                {
                    string? sourceDir = Path.GetDirectoryName(source.FileLocation);
                    string? destinationDir = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(sourceDir) && !string.IsNullOrWhiteSpace(destinationDir))
                    {
                        string sourceBase = Path.GetFileNameWithoutExtension(source.FileLocation);
                        string destinationBase = Path.GetFileNameWithoutExtension(destination);
                        foreach (string extension in Saturn.Extensions)
                        {
                            string companion = Path.Combine(sourceDir, sourceBase) + extension;
                            if (File.Exists(companion) && !string.Equals(companion, source.FileLocation, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(companion, Path.Combine(destinationDir, destinationBase) + extension, true);
                            }
                        }
                    }
                }
            }
            catch
            {
                return new AddSaveResponse("Could not copy file to destination!");
            }

            string? emulatorFileName = Path.GetFileName(emulator.ApplicationPath);
            string? saveGroupId = saturn ? Saturn.GroupId(Path.GetFileNameWithoutExtension(destination)) : source.SaveGroupId;
            GameSaveBase added;
            if (isState)
            {
                added = new GameSaveState
                {
                    GameId = source.GameId,
                    AdditionalApplicationId = source.AdditionalApplicationId,
                    Slot = ((GameSaveState)source).Slot,
                    Title = source.Title,
                    FileLocation = destination,
                    EmulatorFileName = emulatorFileName,
                    EmulatorCore = core,
                    SaveGroupId = saveGroupId,
                    SaveGroupName = !string.IsNullOrWhiteSpace(source.SaveGroupName) ? source.SaveGroupName : SaveStateGroupName,
                };
            }
            else
            {
                added = new GameSaveGame
                {
                    GameId = source.GameId,
                    AdditionalApplicationId = source.AdditionalApplicationId,
                    Title = source.Title,
                    FileLocation = destination,
                    EmulatorFileName = emulatorFileName,
                    EmulatorCore = core,
                    SaveGroupId = saveGroupId,
                    SaveGroupName = !string.IsNullOrWhiteSpace(source.SaveGroupName) ? source.SaveGroupName : SaveFileGroupName,
                };
            }

            return new AddSaveResponse(added);
        }

        // ---- § 3.4 à 3.8 ----

        internal static bool IsSecondarySaveFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string extension = Path.GetExtension(filePath);
            return extension.Equals(".bkr", StringComparison.OrdinalIgnoreCase) || extension.Equals(".smpc", StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyList<string> GetCompanionSaveFiles(string primaryFilePath)
        {
            if (string.IsNullOrWhiteSpace(primaryFilePath) || !Saturn.HasSaturnExtension(primaryFilePath))
            {
                return Array.Empty<string>();
            }

            string? folder = Path.GetDirectoryName(primaryFilePath);
            string baseName = Path.GetFileNameWithoutExtension(primaryFilePath);
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(baseName))
            {
                return Array.Empty<string>();
            }

            var companions = new List<string>();
            foreach (string extension in Saturn.Extensions)
            {
                string candidate = Path.Combine(folder, baseName + extension);
                if (File.Exists(candidate) && !string.Equals(candidate, primaryFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    companions.Add(candidate);
                }
            }

            return companions;
        }

        private static bool CoreIsSaturn(string? core)
            => !string.IsNullOrWhiteSpace(core) && core.Contains("saturn", StringComparison.OrdinalIgnoreCase);

        internal static bool TryGetSaveGroupInfo(GameSaveBase save, string primaryFilePath, out string? saveGroupId, out string? saveGroupName)
        {
            saveGroupId = null;
            saveGroupName = null;
            if (save == null)
            {
                return false;
            }

            string? path = !string.IsNullOrWhiteSpace(primaryFilePath) ? primaryFilePath : save.FileLocation;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (!Saturn.HasSaturnExtension(path) && !CoreIsSaturn(save.EmulatorCore))
            {
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return false;
            }

            saveGroupId = Saturn.GroupId(baseName);
            saveGroupName = baseName;
            return true;
        }

        internal static bool UseSaveGroupIdForPersistedMatch(GameSaveBase save)
            => save != null
               && !string.IsNullOrWhiteSpace(save.SaveGroupId)
               && save.SaveGroupId.StartsWith("saturn", StringComparison.OrdinalIgnoreCase);

        internal static bool TryComputeSaveSignature(GameSaveBase save, out string? signature)
        {
            signature = null;
            if (save == null || string.IsNullOrWhiteSpace(save.FileLocation) || !File.Exists(save.FileLocation))
            {
                return false;
            }

            string path = save.FileLocation;
            if (!Saturn.HasSaturnExtension(path) && !CoreIsSaturn(save.EmulatorCore))
            {
                return false;
            }

            string? folder = Path.GetDirectoryName(path);
            string baseName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(baseName))
            {
                return false;
            }

            List<string> files = Saturn.Extensions
                .Select(extension => Path.Combine(folder, baseName + extension))
                .Where(File.Exists)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0)
            {
                return false;
            }

            try
            {
                var text = new StringBuilder();
                foreach (string file in files)
                {
                    string hash;
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        hash = Convert.ToHexString(MD5.HashData(stream));
                    }

                    text.Append(Path.GetFileName(file).ToLowerInvariant()).Append('|').Append(hash).Append("\r\n");
                }

                signature = Convert.ToHexString(MD5.HashData(new UTF8Encoding(false).GetBytes(text.ToString())));
                return true;
            }
            catch
            {
                signature = null;
                return false;
            }
        }
    }
}
