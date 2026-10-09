# Surface publique de l'original (inventaire par réflexion)

Source : `G:\LB1326\System\Plugins\RetroArch LaunchBox Integration\Unbroken.LaunchBox.Windows.RetroArch.dll`,
manifest 1.12, PluginId `ab7ff3c3-aae4-4342-beb6-1a226160b2b1`, cible .NETCoreApp v10.0, compilé contre le SDK 13.28.0.5.
Obtenu par réflexion sur les métadonnées seulement : aucun corps de méthode lu.

Ressource embarquée unique : `Unbroken.LaunchBox.Windows.RetroArch.Resources.resources`, entrée `Bios_Requirements`
(chaîne, 77 212 caractères), reprise telle quelle dans `resources/Bios_Requirements.txt` (décision de Mehdi, 09/10).

```text
# Unbroken.LaunchBox.Windows.RetroArch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
  [asm] CompilationRelaxationsAttribute 8
  [asm] RuntimeCompatibilityAttribute 
  [asm] DebuggableAttribute 263
  [asm] TargetFrameworkAttribute .NETCoreApp,Version=v10.0
  [asm] AssemblyMetadataAttribute Sentry.ProjectDirectory,D:\Repos\Unbroken\LaunchBox.worktrees\system-plugins\Source\Unbroken.LaunchBox.Windows.RetroArch\
## References
  System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  System.Text.RegularExpressions, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  Unbroken.LaunchBox.Plugins, Version=13.28.0.5, Culture=neutral, PublicKeyToken=null
  System.Net.Http, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  System.Collections.Concurrent, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  System.Collections, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  Unbroken.LaunchBox.LocalDb, Version=13.28.0.5, Culture=neutral, PublicKeyToken=null
  System.Diagnostics.Process, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed
  System.Linq, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  Unbroken.LaunchBox, Version=13.28.0.5, Culture=neutral, PublicKeyToken=null
  Unbroken.LaunchBox.Windows, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
  System.Linq.Expressions, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  System.Memory, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
  System.Security.Cryptography, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  Microsoft.CSharp, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  Unbroken, Version=13.28.0.5, Culture=neutral, PublicKeyToken=null
## Resources
  Unbroken.LaunchBox.Windows.RetroArch.Resources.resources (77443 bytes)
## Types: 37 total, 2 public

### class Unbroken.LaunchBox.Windows.RetroArch.Models.BiosFile
  base: Unbroken.LaunchBox.Plugins.EmulatorBiosFile
  [NullableContextAttribute]
  [NullableAttribute]
  ctor(String fileName, Boolean required, String description = null, String md5 = null, Unbroken.LaunchBox.Plugins.EmulatorBiosGroup group = null)
  prop String SystemFolder { get; set; }

### class Unbroken.LaunchBox.Windows.RetroArch.RetroArchPlugin
  base: Unbroken.LaunchBox.Plugins.EmulatorPlugin
  implements: Unbroken.LaunchBox.Plugins.IEmulatorWithCores
  [NullableContextAttribute]
  [NullableAttribute]
  ctor(Unbroken.LaunchBox.Plugins.IPluginPaths pluginPaths)
  prop override String EmulatorName { get; }
  Boolean IsSecondarySaveFile(String filePath) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  IReadOnlyList<String> GetCompanionSaveFiles(String primaryFilePath) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Boolean TryGetSaveGroupInfo(Unbroken.LaunchBox.Plugins.Data.GameSaveBase save, String primaryFilePath, String& saveGroupId, String& saveGroupName) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Boolean UseSaveGroupIdForPersistedMatch(Unbroken.LaunchBox.Plugins.Data.GameSaveBase save) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Boolean TryComputeSaveSignature(Unbroken.LaunchBox.Plugins.Data.GameSaveBase save, String emulatorApplicationPath, String& signature) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.AddSaveResponse AddSaveFile(Unbroken.LaunchBox.Plugins.AddSaveArgs args) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.PluginResponse FireEmulatorImportAction(Unbroken.LaunchBox.Plugins.ImportActionArgs args) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  IEnumerable<Unbroken.LaunchBox.Plugins.Data.IEmulator> GetApplicableEmulators(IEnumerable<Unbroken.LaunchBox.Plugins.Data.IEmulator> emulators) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  IEnumerable<Unbroken.LaunchBox.Plugins.EmulatorBiosFile> GetBiosFilesForPlatform(String platform) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  IEnumerable<Unbroken.LaunchBox.Plugins.EmulatorBiosFile> GetBiosFilesForPlatform(String emulatorApplicationPath, String platform, String commandLine) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  String GetCurrentVersion(String applicationPath) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  IEnumerable<Unbroken.LaunchBox.Plugins.EmulatorImportAction> GetImportActions() override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  IEnumerable<Unbroken.LaunchBox.Plugins.EmulatorControllerVersion> GetInstallableVersions() override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  IReadOnlyDictionary<Int32,String> GetPotentialSaveSlots() override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.GetSavesResponse GetSaves(Unbroken.LaunchBox.Plugins.GetSavesArgs args) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.EmulatorInstallResponse InstallEmulator(Unbroken.LaunchBox.Plugins.InstallEmulatorArgs args) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.EmulatorSupportResponse IsPlatformSupported(String platform) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.PluginResponse UpdateEmulator(Unbroken.LaunchBox.Plugins.InstallEmulatorArgs args)
  Boolean SupportsSaveManagement() override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  String[] GetAllAvailableCores(String emulatorFilePath)
  String[] GetSuggestedCores(String emulatorFilePath, String platform)
  Unbroken.LaunchBox.Plugins.PrepareForLaunchResponse PrepareEmulatorForLaunch(Unbroken.LaunchBox.Plugins.PrepareForLaunchArgs args) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.RetroAchievementSupportResponse SupportsRetroAchievements(String emulatorApplicationPath) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]
  Unbroken.LaunchBox.Plugins.PluginResponse InjectRetroAchievementsCredentials(Unbroken.LaunchBox.Plugins.InjectRetroAchievementsCredentialsArgs args) override[Unbroken.LaunchBox.Plugins.EmulatorPlugin]

## Non-public type count by namespace (names omitted)
  : 2
  System.Text.RegularExpressions.Generated: 7
  Unbroken.LaunchBox.Windows.RetroArch: 26
```
