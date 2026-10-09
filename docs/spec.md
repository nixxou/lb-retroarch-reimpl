# Spécification comportementale : greffon RetroArch de LaunchBox (original 1.12)

Document « salle blanche ». Il décrit ce que l'original fait, vu de l'extérieur, membre public par membre public.
Il ne décrit pas comment l'original est construit. Le clone doit reproduire chaque comportement, bizarreries comprises
(section « Bizarreries »), sauf décision contraire de Mehdi. Les points marqués « mesuré » ont été obtenus en appelant
l'original (ou LaunchBox) depuis une sonde, le 09/10.

Conventions :

- « RA » désigne le dossier de l'émulateur, c'est-à-dire `Path.GetDirectoryName(emulateur.ApplicationPath)`, pris TEL
  QUEL. S'il est relatif, tout accès disque fait avec lui (lecture de `retroarch.cfg`, du dossier `info`, test
  d'existence) se résout contre le **répertoire courant du processus**, PAS contre la racine de LaunchBox. Seules
  exceptions : le préfixe `:\` des valeurs du cfg (§ 3.1.2) et le dossier `cores` de § 2.8, qui se résolvent contre la
  racine de LaunchBox. Le résultat de ces membres dépend donc du répertoire courant (mesuré).
- « vide » veut dire null, chaîne vide ou seulement des blancs (`string.IsNullOrWhiteSpace`).
- Comparaisons : « ordinal » = `StringComparison.Ordinal` ; « sans casse ordinal » = `OrdinalIgnoreCase` ;
  « sans casse invariant » = `InvariantCultureIgnoreCase`. Sauf mention, l'égalité est ordinale.
- **« commence par (culturel) »** = la comparaison par défaut de .NET pour un test de préfixe sur une chaîne
  (`StartsWith(string)` sans argument de comparaison) : linguistique, dans la culture courante. Elle ignore les
  caractères ignorables (mesuré : U+0000 et U+00AD devant le préfixe sont ignorés). Le clone ne doit PAS passer
  `Ordinal` à ces tests. Les recherches « contient », « remplace » et la recherche d'un caractère restent ordinales.
- « LocalDb RetroArch » = le premier enregistrement rendu par `GamesDb.GetEmulatorsByNameWithPlatformsAsync("RetroArch")`
  (attendu de façon synchrone), avec sa liste `Platforms` (des `DbEmulatorPlatform`, dans l'ordre rendu).
- Les textes `Strings.Xxx` viennent de `Unbroken.LaunchBox.Properties.Strings` (localisés par LaunchBox) ; la valeur
  anglaise est donnée entre parenthèses. Le clone appelle la même propriété, il ne recopie pas le texte.
- Un « nom de base » est `Path.GetFileNameWithoutExtension(chemin)` (seule la dernière extension tombe).
- Appels asynchrones de LaunchBox ou du réseau attendus de façon synchrone (LocalDb, `GetValidPlatformNameAsync`,
  téléchargements) : en cas d'échec, c'est l'exception d'ORIGINE qui sort, jamais une `AggregateException` (mesuré :
  LocalDb non initialisée → `InvalidOperationException` directe).
- Assemblage des chemins : règles de `Path.Combine`. Un dossier vide donne le nom seul (chemin relatif, donc résolu
  contre le répertoire courant) ; un dossier qui se termine par `\` (racine `X:\` ou autre) ne double pas le
  séparateur (`X:\` + `nom.ext` → `X:\nom.ext`) ; un dossier null fait lever `ArgumentNullException`.

---

## 1. Type `BiosFile` (sous-espace `Models`)

- Public, dérive de `Unbroken.LaunchBox.Plugins.EmulatorBiosFile`, nom de type `BiosFile`. Mêmes membres publics que
  l'original ; l'espace de noms est laissé au clone (l'original : `Unbroken.LaunchBox.Windows.RetroArch.Models` ; le
  clone : `Nixx.RetroArch.Models`, décision de la session principale).
- Propriété **statique** publique `string SystemFolder { get; set; }`, valeur initiale `"system"`, lecture et écriture
  libres. Elle est globale au processus et réécrite par les deux surcharges de `GetBiosFilesForPlatform` (§ 2.10).
- Constructeur public `BiosFile(string fileName, bool required, string description = null, string md5 = null, EmulatorBiosGroup group = null)` :
  l'objet a `Location` = valeur de `SystemFolder` AU MOMENT de la construction, et `FileName`, `Required`,
  `Description`, `Md5`, `ApplicableGroup` = les paramètres. Changer `SystemFolder` ensuite ne change pas l'objet
  (mesuré). Aucun autre effet, aucune validation (null accepté partout).
- Aucun autre membre public propre.

## 2. Type `RetroArchPlugin`

Nom de type `RetroArchPlugin`, mêmes membres publics que l'original, espace de noms laissé au clone (original
`Unbroken.LaunchBox.Windows.RetroArch`, clone `Nixx.RetroArch`). Public, non scellé, dérive de `EmulatorPlugin`, implémente `IEmulatorWithCores` (méthodes publiques implicites).
Les membres virtuels de la base que l'original ne surcharge PAS ne doivent pas être surchargés :
`IsUpdateAvailable` (les deux surcharges), `RemoveSave`, `IsSaveActive`, `TryBackupSave`, `IsSaveContainer`,
`NormalizeCommandLineForExecutable`. Ils gardent le comportement du SDK (rappel : `IsUpdateAvailable` appelle
`GetInstallableVersions`, donc le réseau, puis `GetCurrentVersion`, et compare au `Label` sans casse invariant).

### 2.1 Constructeur `RetroArchPlugin(IPluginPaths pluginPaths)`

- `pluginPaths` null : `ArgumentNullException`, nom de paramètre `"pluginPaths"`.
- Sinon aucun effet visible : ni disque, ni réseau. `DataDirectory` est relu à chaque usage (§ 2.16 à 2.18).
- Aucun cache : chaque appel relit le disque, la LocalDb et le réseau.

### 2.2 `string EmulatorName { get; }`

Rend toujours `"RetroArch"`.

### 2.3 `bool SupportsSaveManagement()`

Rend toujours `true`.

### 2.4 `IReadOnlyDictionary<int, string> GetPotentialSaveSlots()`

Rend un `Dictionary<int,string>` neuf de 11 entrées, énumérées dans l'ordre : `-1 → "Auto"`, puis `0 → "0"`, …,
`9 → "9"`.

### 2.5 `IEnumerable<IEmulator> GetApplicableEmulators(IEnumerable<IEmulator> emulators)`

- `emulators` null : `List<IEmulator>` vide.
- Sinon `List<IEmulator>` neuve, dans l'ordre d'entrée, des émulateurs non null dont `ApplicationPath` n'est pas vide et
  dont le nom de fichier de `ApplicationPath` vaut `retroarch.exe` sans casse ordinal. Aucun accès disque.

### 2.6 `EmulatorSupportResponse IsPlatformSupported(string platform)`

- LocalDb RetroArch absente : `(Supported=false, Recommended=false)`.
- Sinon, parmi TOUTES ses plateformes dont `Platform` est égal à `platform` sans casse invariant (toutes sont examinées :
  une entrée à `Platform` null n'importe où fait lever `NullReferenceException`, y compris quand `platform` est null), on
  retient la première marquée
  `Recommended`, à défaut la première. Aucune, ou `Platforms` null, ou `platform` null : `(false,false)`.
- Trouvée : `(true, Recommended de l'entrée retenue)`.

### 2.7 `IEnumerable<EmulatorImportAction> GetImportActions()`

Liste en lecture seule à un élément : `BooleanEmulatorImportAction(id: "DownloadCores",
label: Strings.LabelDownloadRetroArchCore ("Automatically download RetroArch core if missing"), defaultVal: true)`,
neuve à chaque appel.

### 2.8 `string[] GetAllAvailableCores(string emulatorFilePath)` (IEmulatorWithCores)

- `emulatorFilePath` vide, ou sans dossier parent (ex. `"retroarch.exe"`) : tableau vide.
- Sinon le dossier `cores` voisin de l'exe, rendu absolu par `FileHelper.GetFullPath` (mesuré : un chemin relatif est
  résolu contre la RACINE de LaunchBox, pas contre le répertoire courant).
- Dossier absent : tableau vide. Sinon le nom de base (via `FileHelper.GetFileNameWithoutExtensionSafe`) de chaque
  fichier du dossier (non récursif) dont l'extension est `.dll` (motif `*.dll` de .NET : mesuré, `x.dll_old` et `x.dllx`
  ne sont PAS retenus ; aucun traitement spécial à prévoir). Ordre du système de fichiers, sans tri ni dédoublonnage.
- Erreur d'accès au dossier : l'exception remonte.

### 2.9 `string[] GetSuggestedCores(string emulatorFilePath, string platform)` (IEmulatorWithCores)

- `emulatorFilePath` ignoré. `platform` vide : tableau vide. LocalDb RetroArch absente ou sans `Platforms` : vide.
- Les plateformes LocalDb sont rangées `Recommended` d'abord (tri stable) ; on retient la première dont `Platform` est
  égal à `platform` sans casse invariant. La recherche s'arrête à la première trouvaille : une entrée à `Platform` null
  ne fait lever `NullReferenceException` que si elle est rencontrée AVANT. Aucune : tableau vide.
- Cœur = `LocalDbWindows.GetCommandLineOrCore(isRetroArch: true, coreOrCommand: CommandLine de l'entrée,
  returnCore: true)`. Vide : tableau vide. Sinon tableau d'un élément, ce texte sans blancs autour.
  (Mesuré : `-L "cores\snes9x_libretro.dll"` → `snes9x_libretro` ; ligne sans `-L` → `""` ; null →
  `ArgumentNullException`, qui remonte.)

### 2.10 `GetBiosFilesForPlatform`

#### Lecture du dossier « system » (commune aux deux surcharges)

Chaque appel donne à `BiosFile.SystemFolder` une valeur, avant de construire le moindre `BiosFile`, d'après un
« dossier racine » :

- `"system"` si la racine est vide (null, vide ou blanche), si `<racine>\retroarch.cfg` n'existe pas (racine relative :
  répertoire courant, mesuré ; racine `X:\` → `X:\retroarch.cfg`), si aucune ligne du cfg ne « commence par
  (culturel) » `system_directory`, ou si la première ligne qui le fait ne contient pas de `=`.
- Sinon, la valeur portée par la PREMIÈRE ligne qui « commence par (culturel) » `system_directory` (une clé plus longue
  au même préfixe compte si elle vient avant ; un caractère ignorable en tête est accepté, mesuré). Cette valeur est le
  texte qui suit le premier `=` de la ligne, sans aucun `"` (où qu'ils soient) et sans blancs autour ; si elle
  « commence par (culturel) » `:\`, on n'en garde que la partie qui suit ses deux premiers caractères.
- Une erreur de lecture du cfg (verrou exclusif, droits) remonte à l'appelant.

| Ligne du cfg (première `system_directory…`) | `SystemFolder` |
|---|---|
| `system_directory = ":\system"` | `system` |
| `system_directory = "D:\Bios"` | `D:\Bios` |
| `system_directory = "default"` | `default` |
| `system_directory = ""` | `""` |
| `system_directory = "a"b"` | `ab` |
| `system_directory = x = y` | `x = y` |
| U+00AD puis `system_directory = "D:\Bios"` | `D:\Bios` (mesuré) |
| `system_directory = "` U+00AD `:\sys"` | `\sys` (mesuré) |
| `system_directory_x = "Z"` avant `system_directory = "Y"` | `Z` |
| `system_directory` (sans `=`) | `system` |

#### 2.10.a `GetBiosFilesForPlatform(string platform)`

- Racine = null, donc `SystemFolder = "system"` à chaque appel.
- Première plateforme de la LocalDb RetroArch (ordre LocalDb) dont `Platform` est égal à `platform` sans casse ordinal.
  Une entrée à `Platform` null est simplement ignorée (pas d'exception) ; `Platforms` null équivaut à « absente ».
  Absente, LocalDb RetroArch absente, ou `RequiredBiosFile` vide : `Array.Empty<EmulatorBiosFile>()`. La règle vaut aussi
  pour le repli de 2.10.b.
- Sinon liste en lecture seule à un élément : `BiosFile(RequiredBiosFile, required: true)` (Location `"system"`).

#### 2.10.b `GetBiosFilesForPlatform(string emulatorApplicationPath, string platform, string commandLine)`

1. Racine = dossier de `emulatorApplicationPath` s'il n'est pas vide, sinon null ; mise à jour de `SystemFolder`.
2. Si `emulatorApplicationPath` ET `commandLine` ne sont pas vides et que `commandLine` contient le format « fichier de
   cœur » (§ 4.2) : lecture de la table BIOS (§ 5) avec le nom trouvé et `platform` ; on rend la
   `List<EmulatorBiosFile>` obtenue, MÊME VIDE (cœur absent de la table = liste vide, sans repli). Une exception pendant
   la lecture de la table serait avalée et mènerait au repli (impossible avec la table livrée).
3. Sinon : repli identique à 2.10.a, mais avec le `SystemFolder` calculé en 1 comme `Location`.

### 2.11 `string GetCurrentVersion(string applicationPath)`

- `applicationPath` vide ou fichier absent : `null`.
- Sinon lance le programme : `applicationPath`, argument `--version`, sans shell, fenêtre cachée, sortie standard ET
  erreur redirigées. Le clone ne fixe AUCUN encodage pour ces flux et garde le défaut de .NET, qui dépend du processus
  hôte (dans une sonde console, un caractère non ASCII arrive transformé ; dans LaunchBox, sans console, le résultat peut
  différer). Répertoire de travail non fixé. Attente de la fin SANS délai.
- Échec au lancement ou pendant l'attente : `null`.
- Parmi les lignes reçues sur les deux flux, on retient une ligne qui « commence par (culturel) » `Version: ` (espace
  compris, casse comprise). Avec plusieurs candidates, l'élue n'est pas garantie (mesuré : la dernière reçue). Deux appels
  simultanés sur la même instance peuvent voir les lignes l'un de l'autre.
- Aucune : `null`.
- Sinon le résultat est la tranche de la ligne qui commence à son 10e caractère et dont la longueur vaut la position
  (comptée depuis 0) de la première espace de la ligne, moins 2. Si la ligne n'a pas assez de caractères pour cette
  tranche : `ArgumentOutOfRangeException`, qui REMONTE. Pour une ligne qui commence littéralement par `Version: `, cela revient à
  « les 6 caractères qui suivent ». Exemples mesurés : `Version: 1.19.1 (Git abc)` → `1.19.1` ;
  `Version: 1.9.0 (Git abc)` → `1.9.0 ` (espace final) ; `Version: 1.2.34` → `1.2.34` ; `Version: 1.9.0` → exception ;
  U+0000 puis `Version: 1.2.3 (x)` → ` 1.2.3 ` (7 caractères, espace en tête) ; `version: …` → `null`.
  L'exception de la ligne trop courte est exactement `ArgumentOutOfRangeException`, `ParamName = "startIndex"`, message
  `startIndex cannot be larger than length of string. (Parameter 'startIndex')` (texte .NET en anglais, mesuré). Aucune
  autre découpe de chaîne de l'original ne laisse sortir d'exception : celles du cfg des saves et du `.info` sont
  interceptées (« aucun dossier »), celles de `system_directory` et de `:\` ne peuvent pas échouer.

### 2.12 `IEnumerable<EmulatorControllerVersion> GetInstallableVersions()`

- Dernière version (§ 6.1) ; si elle lève, l'exception REMONTE. Vide : `null`.
- Sinon liste en lecture seule à un élément : `EmulatorControllerVersion(identifier: v, label: v)`, sans description.

### 2.13 `EmulatorInstallResponse InstallEmulator(InstallEmulatorArgs args)`

`args` peut être null ; `args.ReportProgressAction` et `args.ShouldCancelFunc` null sont tolérés partout (aucun
rappel, aucune annulation). `args.ShouldCreateEmulator` est ignoré.

**Dossier cible `D`** : le dossier de `args.ExistingEmulator.ApplicationPath` tel quel si cet émulateur existe et que
son chemin n'est pas vide ; sinon `<NamingHelper.RootFolder>\Emulators\RetroArch` (racine terminée par `\` : pas de
double séparateur ; racine vide : `Emulators\RetroArch`, relatif au répertoire courant ; racine null :
`ArgumentNullException`). Un chemin existant sans dossier (`retroarch.exe` seul) donne un `D` vide.

**Version `V`** : `args.Version` si non null (même vide), sinon la dernière version (§ 6.1, peut lever).

**Effets attendus, dans l'ordre où ils sont observables** :

1. Téléchargement de l'archive du programme (§ 6.2, URL § 6.3), progression rapportée par
   `ReportProgressAction(Strings.LabelDownloadingRetroArch ("Downloading and installing RetroArch..."), "", f × 0.5)`.
2. Si `ShouldCancelFunc` rend alors vrai : rend `new EmulatorInstallResponse("")` (`WasSuccess=false`, `Message=""`),
   rien n'est installé ; une archive complète (annulation arrivée après le dernier bloc) reste dans le dossier
   temporaire.
3. `D` (et ses parents) existe ensuite, même si la suite échoue. `D` vide : `ArgumentException`, archive laissée.
4. Le contenu de l'archive, dont le dossier racine s'appelle `RetroArch-Win64`, se retrouve DANS `D` (dossier racine de
   l'archive renommé au nom de `D` quand ce nom diffère, en ordinal, de `RetroArch-Win64`, puis extraction dans le
   parent de `D`, ce parent étant rendu absolu contre le répertoire courant). Le « parent » est celui du chemin de `D`
   APRÈS normalisation Windows (mesuré, règles de `DirectoryInfo`) : un dernier segment fait seulement d'espaces ou de
   points disparaît, `.` est résolu, les points et espaces finaux d'un segment tombent. Exemples pour l'exe
   `T\Emu\RA\ \retroarch.exe` ou `T\Emu\RA\...\retroarch.exe` : `D` = `T\Emu\RA\ ` (ou `…\...`), parent normalisé
   `T\Emu`, extraction dans `T\Emu` (constat du juge sur l'original) ; `T\Emu\RA\.` et `T\Emu\RA.` → parent `T\Emu`.
   Le nom donné au dossier racine de l'archive reste, lui, le dernier segment BRUT de `D` (` `, `...`, `.`, `RA.`). Le clone passe par
   `SevenZip.RenameArchiveFolder(archive, "RetroArch-Win64", nom)` (seulement si le nom diffère) et
   `SevenZip.Extract(archive, parent)`. Si l'un des deux rend faux (archive corrompue, verrouillée, ou absente après un
   téléchargement interrompu sans annulation, mesuré : 7-Zip rend faux pour une archive null) :
   `AccessViolationException(Strings.ErrorRetroArchArchive)` ("Could not install Retroarch because the archive could
   not be extracted; perhaps it is corrupt or files are in use?"). `D` racine d'un lecteur : non mesuré.
5. L'archive temporaire est supprimée ; une erreur de suppression est passée à `Root.Logging.PublishIgnoredException`.
6. Même chose pour l'archive des cœurs : progression `0.5 + f × 0.5`, même libellé ; annulation : `("")` avec le
   programme déjà installé ; échec d'extraction : `AccessViolationException(Strings.ErrorCouldNotExtractRetroArchCores)`
   ("Could not extract the downloaded RetroArch cores; perhaps the download was corrupt or files were in use?").
7. Émulateur existant fourni : son `ApplicationPath` devient `FileHelper.GetRelativePath(D\retroarch.exe)` et l'on rend
   `new EmulatorInstallResponse(cetEmulateur)` (succès, message null) ; ses plateformes ne sont pas touchées.
8. Sinon un nouvel `Unbroken.LaunchBox.Windows.Data.Emulator` (Id donné par son constructeur) est rendu, avec ces
   valeurs (ordre alphabétique, l'ordre d'affectation n'est pas observable) :

   | Propriété | Valeur |
   |---|---|
   | `AggressiveWindowHiding` | `false` |
   | `ApplicationPath` | `FileHelper.GetRelativePath(D\retroarch.exe)` |
   | `AutoExtract` | `true` |
   | `AutoHotkeyScript`, `ExitAutoHotkeyScript`, `LoadStateAutoHotkeyScript`, `PauseAutoHotkeyScript`, `ResetAutoHotkeyScript`, `ResumeAutoHotkeyScript`, `SaveStateAutoHotkeyScript`, `SwapDiscsAutoHotkeyScript` | la méthode `LocalDbEmulator.Get<Propriété>("RetroArch")` de même nom |
   | `CommandLine` | `""` |
   | `DisableShutdownScreen` | `false` |
   | `FileNameWithoutExtensionAndPath` | `false` |
   | `ForcefulPauseScreenActivation` | `true` |
   | `HideAllNonExclusiveFullscreenWindows` | `false` |
   | `HideConsole` | `true` |
   | `HideMouseCursorInGame` | `true` |
   | `NoQuotes`, `NoSpace` | `false` |
   | `StartupLoadDelay` | `5000` |
   | `SuspendProcessOnPause` | `true` |
   | `Title` | `RetroArch` |
   | `UsePauseScreen`, `UseStartupScreen` | `true` |

   Ses lignes de plateforme (`Platforms`) :
   - une ligne par plateforme de la LocalDb RetroArch, dans l'ordre LocalDb, doublons compris :
     `EmulatorPlatform(emulateur.Id, plateformeDb, CommandLine de l'entrée,
     isDefault: !Root.DataManager.GetDefaultEmulatorExistsForPlatform("RetroArch", plateformeDb), m3uDiscLoadEnabled: false)`
     (`IsDefault` est évalué sans tenir compte des lignes que l'installation ajoute) ;
   - si aucune plateforme LocalDb n'est égale (ordinal) à `args.Platform` et que `args.Platform` n'est pas vide, une
     ligne de plus pour `args.Platform`, dont la ligne de commande est celle de la première plateforme LocalDb égale
     (ordinal) au `ScrapeAs` de `Root.DataManager.GetPlatform(args.Platform)` (plateforme null ou `ScrapeAs` vide :
     `""` ; une entrée LocalDb à `Platform` null rencontrée avant la trouvaille : `NullReferenceException`), avec
     `isDefault: !GetDefaultEmulatorExistsForPlatform("RetroArch", args.Platform)` et M3U désactivé.
   LocalDb RetroArch absente ou sans plateformes : aucune exception, seule la ligne de `args.Platform` (s'il y a lieu).
   Effet sur les données de LaunchBox (en mémoire seulement) : pour chaque plateforme LocalDb, chaque plateforme locale
   (`Root.DataManager.Platforms.Values`) dont `ScrapeAs` ou `Name` est égal (ordinal) à elle, chaque jeu
   (`Root.DataManager.Games.Values`) de cette plateforme locale (égalité ordinale de `Platform` avec son `Name` ; un jeu
   à `Platform` null : `NullReferenceException`) dont `EmulatorId` vaut `00000000-0000-0000-0000-000000000000` reçoit
   l'Id du nouvel émulateur, de même que ses applications additionnelles (liste non null) à ce GUID nul. Une entrée
   LocalDb à `Platform` null ne correspond à AUCUNE plateforme locale, même à une plateforme dont `ScrapeAs` ou `Name`
   est null (lu ; à confirmer en `[LB]`, l'appel `GetDefaultEmulatorExistsForPlatform("RetroArch", null)` pouvant lever
   avant).

Hors annulation, aucun échec n'est rendu en `Success=false` : tout lève.

### 2.14 `PluginResponse UpdateEmulator(InstallEmulatorArgs args)` (public, non virtuel)

1. `args.ExistingEmulator` null ou `ApplicationPath` vide : `InvalidOperationException(Strings.ErrorNoRetroArchAppPath)`
   ("The RetroArch emulator does not have an application path assigned to it."), avant tout réseau. `args` null :
   `NullReferenceException`.
2. Chemin sans dossier : même `InvalidOperationException`.
3. Dernière version (§ 6.1), peut lever.
4. Si l'exe existe : version courante (`GetCurrentVersion`) ; égale (ordinal) à la dernière : rend
   `new PluginResponse(false, "You are already on the latest version of RetroArch!")`. Toute exception à cette étape est
   passée à `Root.Logging.PublishIgnoredException` et ignorée.
5. Sinon rend le résultat de `InstallEmulator(args)` appelé VIRTUELLEMENT : une classe dérivée qui surcharge
   `InstallEmulator` (ou `GetCurrentVersion`, à l'étape 4) voit sa surcharge appelée. L'installation redemande la
   dernière version si `args.Version` est null.

### 2.15 `PluginResponse FireEmulatorImportAction(ImportActionArgs args)`

1. Action dont l'`Id` n'est pas exactement `DownloadCores`, ou qui n'est pas une `BooleanEmulatorImportAction`, ou dont
   `Value` est faux : rend `new PluginResponse(true)` sans rien faire. `args.Action` null : `NullReferenceException`.
2. `args.GamesImported` null : `NullReferenceException`. Les jeux null, ou sans `EmulatorId`, ou sans `Platform`, sont
   ignorés. Les `EmulatorId` d'un côté et les `Platform` de l'autre sont dédoublonnés SÉPARÉMENT (ordinal), puis chaque
   émulateur est confronté à CHAQUE plateforme, même si aucun jeu ne réunit les deux. Les émulateurs sont parcourus dans
   l'ordre de leur première apparition parmi les jeux importés, et pour chacun les plateformes dans l'ordre de leur
   première apparition : cet ordre fixe l'ordre des lignes ajoutées, des téléchargements et la première exception levée.
3. `PluginHelper.DataManager` null : `NullReferenceException`, levée APRÈS le parcours des jeux et AVANT toute lecture
   de la LocalDb, même si aucun jeu n'est retenu.
   LocalDb RetroArch absente : `NullReferenceException` (même sans jeu retenu). LocalDb RetroArch présente mais `Platforms` null : traitée
   comme une liste vide, sans exception ; aucune plateforme n'est alors ajoutée (étape 4, « aucune plateforme LocalDb ne
   correspond »), seules les lignes déjà présentes sur l'émulateur passent le contrôle du cœur.
4. Pour chaque émulateur (obtenu par `PluginHelper.DataManager.GetEmulatorById`, éventuellement null) et chaque
   plateforme :
   - si l'émulateur n'a pas de ligne pour cette plateforme (égalité sans casse ordinal ; une ligne à `Platform` null est
     ignorée ; `GetAllEmulatorPlatforms()` rendant null fait lever `ArgumentNullException`) : on cherche le nom officiel
     (`GamesDb.GetValidPlatformNameAsync`) puis une plateforme LocalDb égale (ordinal) à ce nom. Si le nom officiel est
     null, vide ou blanc, ou si aucune plateforme LocalDb ne lui correspond, on passe à la plateforme suivante sans autre contrôle. Sinon, si l'émulateur existe, on lui ajoute une ligne
     (`AddNewEmulatorPlatform()`) avec `Platform` = la plateforme importée, `CommandLine` = celle de la LocalDb,
     `IsDefault = !Root.DataManager.GetDefaultEmulatorExistsForPlatform(emulateur.Title, plateforme)`,
     `M3uDiscLoadEnabled = false`.
   - Contrôle du cœur, seulement si l'`ApplicationPath` de l'émulateur n'est pas vide et que son dossier existe (relatif :
     répertoire courant) : le fichier obtenu en assemblant (règles de `Path.Combine`) le dossier de l'exe et
     `LocalDbWindows.GetRetroArchCoreFileName(ligne de commande de la ligne EXISTANTE de l'émulateur)` doit exister.
     Exe `X:retroarch.exe` (lecteur sans antislash) : le dossier vaut `X:` et le fichier testé est `X:cores\…`, donc
     relatif au répertoire courant du lecteur `X`. Conséquences (comportement mesuré de cet appel) :
     - ligne existante `-L "cores\x.dll"` : cœur manquant si `dossier\cores\x.dll` n'existe pas ;
     - ligne existante sans `-L` (ex. vide) : le chemin testé est le dossier lui-même, jamais un fichier : cœur
       « manquant » ;
     - ligne existante à `CommandLine` null, OU plateforme que l'action vient d'ajouter : `ArgumentNullException`, qui
       remonte (après l'ajout de la ligne).
   - Si au moins une plateforme a un cœur manquant pour cet émulateur, l'archive complète des cœurs est téléchargée UNE
     fois et extraite (§ 6.4), ce qui réécrit tous les cœurs. Ce téléchargement a lieu APRÈS le parcours de toutes les
     plateformes de cet émulateur et AVANT de passer à l'émulateur suivant. Une exception sur une plateforme (même
     après une plateforme au cœur manquant) interrompt tout : aucun téléchargement pour cet émulateur, ceux des
     émulateurs précédents sont déjà faits. Progression : `args.ReportActionProgress(
     Strings.LabelDownloadingMissingCores ("Downloading any missing RetroArch cores..."), fraction)` ; annulation :
     `args.ShouldCancelAction`.
5. Rend `new PluginResponse(true)`. Toute exception remonte : jamais de `Success=false`.

### 2.16 `PrepareForLaunchResponse PrepareEmulatorForLaunch(PrepareForLaunchArgs args)`

- `args` null : `NullReferenceException`.
- Sans identifiants (`RetroAchievementCredentials` sans valeur) : `PrepareForLaunchResponse(true)`, `NewCommandLine`
  null, rien d'écrit.
- Sinon `F = <pluginPaths.DataDirectory>\RetroAchievements.cfg`. `DataDirectory` null : `ArgumentNullException` qui
  remonte. Création du dossier de `F` : une erreur REMONTE.
- Puis (erreurs interceptées) écriture complète de `F`, UTF-8 sans BOM, quatre lignes terminées par `\r\n` :
  ```text
  cheevos_enable = "true"
  cheevos_token = "<Token>"
  cheevos_username = "<Username>"
  cheevos_hardcore_mode_enable = "<true|false>"
  ```
  `Token`, `Username` insérés bruts (null → rien entre les guillemets, aucun échappement). Hardcore `"true"` si
  `EmulatorBeingLaunched` existe et que son `EnableHardcoreAchievements` est vrai, sinon `"false"`.
- Rend `PrepareForLaunchResponse(true)` avec `NewCommandLine = CurrentCommandLine + " --appendconfig \"" + F + "\""`.
- Exception à l'écriture : `PrepareForLaunchResponse(false, message de l'exception)`.

### 2.17 `RetroAchievementSupportResponse SupportsRetroAchievements(string emulatorApplicationPath)`

- Argument ignoré. `F` comme en 2.16 ; `DataDirectory` null : `ArgumentNullException` qui remonte.
- `F` absent : `RetroAchievementSupportResponse(isSupported: true)`.
- `F` présent : pour chaque ligne, premier test qui réussit parmi (« commence par (culturel) ») :
  1. `cheevos_enable` → activé = la ligne contient (ordinal) `true` ;
  2. `cheevos_hardcore_mode_enable` → hardcore = la ligne contient `true` ;
  3. `cheevos_token` → jeton = la ligne privée de toutes les occurrences (ordinales) de `cheevos_token`, de tous les `=`
     et de tous les `"`, sans blancs autour ;
  4. `cheevos_username` → nom, même traitement.
  Autres lignes ignorées ; la dernière occurrence gagne. Rend `(true, activé, hardcore, nom, jeton)`.
- Erreur de lecture : avalée, rend `(true)`.

### 2.18 `PluginResponse InjectRetroAchievementsCredentials(InjectRetroAchievementsCredentialsArgs args)`

- `F` comme en 2.16 ; `DataDirectory` null ou erreur de création du dossier : l'exception remonte.
- Écrit les mêmes quatre lignes : `cheevos_enable = "true"` TOUJOURS (`IsEnabled` ignoré), jeton `args.Token`, nom
  `args.Username` (null → vide), hardcore `"false"` seulement si `args.HardcoreEnabled` vaut exactement `false`.
  `EmulatorApplicationPath` ignoré.
- Rend `PluginResponse(true)`. Exception à l'écriture : `PluginResponse(false, message)`. `args` null : le dossier est
  créé, aucun fichier n'est écrit et l'on rend `PluginResponse(false, <message de NullReferenceException>)` (mesuré).

---

### 2.19 Types exacts des collections rendues (mesuré)

| Membre | Cas | Type d'exécution |
|---|---|---|
| `GetImportActions`, `GetInstallableVersions` (non null), `GetBiosFilesForPlatform` (repli LocalDb trouvé, les deux surcharges) | un élément | liste en lecture seule à un élément, du type que génère le compilateur C# pour une expression de collection `[x]` visant `IEnumerable<T>` (nom `<>z__ReadOnlySingleElementList`1`) |
| `GetBiosFilesForPlatform` | repli vide | `Array.Empty<EmulatorBiosFile>()` (instance partagée) |
| `GetBiosFilesForPlatform` à 3 paramètres | table lue | `List<EmulatorBiosFile>` (même vide) |
| `GetApplicableEmulators` | toujours | `List<IEmulator>` |
| `GetPotentialSaveSlots` | toujours | `Dictionary<int,string>` |
| `GetAllAvailableCores`, `GetSuggestedCores` | vide (chemin null, vide ou sans dossier ; dossier `cores` absent ; plateforme vide ou introuvable) | `Array.Empty<string>()` (instance partagée, mesuré) |
| `GetAllAvailableCores` | dossier trouvé | `string[]` neuf s'il contient au moins un cœur ; dossier sans `.dll` : `Array.Empty<string>()` (instance partagée, mesuré par l'A/B) |
| `GetSuggestedCores` | trouvé | `string[]` d'un élément |
| `GetCompanionSaveFiles` | sortie anticipée | `Array.Empty<string>()` ; sinon `List<string>` (même vide) |
| `GetSaves` | toujours | `FoundSaves` = la `List<GameSaveBase>` que construit le SDK |

Comportement de la liste en lecture seule à un élément (mesuré) : `Count = 1` ; `IsReadOnly = true` ; `IsFixedSize = true` ;
`IsSynchronized = false` ; `SyncRoot` = la liste elle-même ; `Contains`, `IndexOf` (-1 si absent), `CopyTo` normaux ;
toute modification (`Add`, `Clear`, `Insert`, `Remove`, `RemoveAt`, écriture par indice, génériques ou non) lève
`NotSupportedException` avec le message par défaut `Specified method is not supported.` ; une lecture hors de l'indice 0
lève `IndexOutOfRangeException` (message `Index was outside the bounds of the array.`), pas
`ArgumentOutOfRangeException`. Elle implémente `IEnumerable`, `ICollection`, `IList`, `IEnumerable<T>`,
`IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `ICollection<T>`, `IList<T>`. Chaque appel rend une liste et des objets neufs.

---

## 3. La famille des saves

### 3.1 Règles communes

#### 3.1.1 Format du `retroarch.cfg` pour les saves

Fichier : `RA\retroarch.cfg` (relatif : répertoire courant ; exe sans dossier, RA vide : `retroarch.cfg` dans le
répertoire courant, mesuré ; de même `info\<cœur>.info`). Absent : pas de dossier. Relu à chaque résolution. Rien
n'y est jamais écrit. Règle de format, ligne par ligne (encodage détecté par .NET, UTF-8 par défaut, BOM accepté) :

- clé = texte avant le premier `=`, sans blancs autour ;
- valeur = texte entre le premier `=` et le deuxième `=` (ou la fin de ligne), sans blancs autour, puis privé de son
  premier ET de son dernier caractère (on suppose des guillemets sans le vérifier) ;
- une ligne sans `=` (vide, commentaire `#…`, `#include "…"`), ou dont la valeur fait moins de 2 caractères, INVALIDE
  le fichier entier : le dossier demandé est alors « aucun », comme si le cfg manquait ;
- exemples : `a = "x"` → `x` ; `a="x"` → `x` ; `a = "x=y"` → `""` ; `a = true` → `ru` ; `a = 64` → `""` ; `a = 6` et
  `a =` → fichier invalide ;
- clé répétée : la dernière gagne ; clés sensibles à la casse ; pas de commentaires ni de sections ;
- une option n'est « vraie » que si sa valeur décodée vaut exactement `true` (ordinal).

#### 3.1.2 Dossier des saves de jeu

Entrées : émulateur, jeu (peut être null), application additionnelle (peut être null), et « créer s'il manque » (vrai
seulement pour `AddSaveFile`). Toute exception pendant la résolution donne « aucun dossier ».

1. `ApplicationPath` de l'émulateur vide : aucun.
2. Contenu `C` = `ApplicationPath` de l'application s'il n'est pas vide, sinon celui du jeu ; vide : aucun.
3. Cfg absent ou invalide : aucun.
4. Base :
   - `savefiles_in_content_dir` vrai : le dossier de `C`, tel quel ;
   - sinon `savefile_directory` absente : aucun. Si sa valeur « commence par (culturel) » `:\`, on lui retire ses deux
     premiers caractères et l'on obtient un chemin ABSOLU et NORMALISÉ (`.` et `..` résolus) en résolvant le reste contre
     le dossier de l'exe, l'exe étant d'abord rendu absolu contre `NamingHelper.RootFolder` (pas contre le répertoire
     courant). La résolution suit les règles de `Path.GetFullPath(reste, dossierDeLExe)` de .NET : reste absolu → lui-même
     normalisé ; reste `\nom` (enraciné sans lecteur) → `<racine du lecteur de l'EXE>\nom` ; reste `X:nom` (lecteur sans
     antislash) du même lecteur que l'exe → `<dossier de l'exe>\nom` ; jamais le répertoire courant. Mesuré : `:\saves` → `<dossier de l'exe>\saves` ; `:\..\x` → le dossier `x` voisin du dossier de l'exe ;
     `:\` seul → le dossier de l'exe ; U+00AD suivi de `:\saves` → `\saves`, résolu à la racine du lecteur de l'exe. La
     normalisation traite aussi `/` et le séparateur final : `:\./a/b`, `:\a\..\a\b\.` et `:\a/b/` donnent tous
     `<dossier de l'exe>\a\b` (mesuré). Exe relatif `RA\retroarch.exe` avec la racine `L` : `<L>\RA\a\b`, quel que soit
     le répertoire courant (mesuré) ; exe `retroarch.exe` sans dossier : `<L>\a\b`. Toute autre
     valeur est prise telle quelle, sans normalisation (un chemin relatif comme `default` se résout contre le répertoire
     courant).
5. `sort_savefiles_by_content_enable` vrai : on ajoute le nom du dossier qui contient DIRECTEMENT le contenu
   (`T\roms\snes\Game.sfc` → `snes` ; contenu à la racine d'un lecteur → rien n'est ajouté).
6. `sort_savefiles_enable` vrai : on ajoute ENSUITE le dossier du cœur :
   - ligne de commande = `CommandLine` BRUTE de l'application si non vide ; sinon celle du jeu si non vide ; sinon, jeu
     absent ou à `Platform` vide : aucun ; sinon `CommandLine` de la première ligne de plateforme de l'émulateur dont
     `Platform` est égal (ordinal) à celle du jeu, et à défaut (pas de telle ligne, `GetAllEmulatorPlatforms()` rendant
     null, ou ligne à `CommandLine` vide) `CommandLine` de l'émulateur ; vide au
     bout du compte : aucun ;
   - nom du cœur par le format « nom de cœur » (§ 4.1) ; absent : aucun ;
   - fichier `RA\info\<cœur>.info` (relatif : répertoire courant) ; absent : aucun ;
   - première ligne de ce fichier qui « commence par (culturel) » `corename` ; aucune : aucun ; le nom est la
     partie de cette ligne comprise entre son 13e caractère et son avant-dernier caractère inclus (forme attendue
     `corename = "Nom"` → `Nom` ; `corename="Snes9x"` → `es9x`) ; ligne trop courte ou nom vide : aucun. Mesuré : U+00AD devant `corename = "Snes9x"` donne `"Snes9x` (guillemet en tête), nom de
     dossier invalide, donc aucun dossier ;
   - on ajoute ce nom.
7. Résultat vide : aucun.
8. Dossier absent : si « créer » est faux, aucun ; sinon il est créé (avec ses parents).
9. Rend le chemin (normalisé seulement dans le cas `:\`, relatif s'il l'était).

#### 3.1.3 Dossier des savestates

Comme 3.1.2 avec `savestates_in_content_dir`, `savestate_directory`, `sort_savestates_by_content_enable`,
`sort_savestates_enable`, SAUF la ligne de commande de l'étape 6 : celle que rend `app.GetEffectiveCommandLine()` si
l'application existe ET que ce résultat n'est pas null ; sinon `jeu.GetEffectiveCommandLine()` (jeu absent : null).
Résultat null, vide ou blanc : aucun. Une application dont la ligne effective est une chaîne vide bloque donc le repli
vers le jeu ; une ligne null ne le bloque pas (mesuré).

#### 3.1.4 Contexte Saturn

Pour un nom de cœur et un jeu :

- vrai si le nom de cœur contient `saturn` sans casse ordinal ;
- sinon, si le jeu est null ou que sa `Platform` est vide : faux ;
- sinon vrai si `Platform` contient `saturn` sans casse ordinal ;
- sinon vrai si `Root.DataManager` et ses `Platforms` existent, contiennent la clé `Platform` du jeu (recherche du
  dictionnaire LaunchBox), et que le `ScrapeAs` de cette plateforme contient `saturn` sans casse ordinal (plateforme
  null ou `ScrapeAs` null, vide ou blanc : « pas Saturn », sans exception) ;
- sinon faux.

Extensions Saturn, par rang : `.bcr` (primaire), `.bkr`, `.smpc` (sans casse ordinal).
Identifiant de groupe Saturn d'un nom de base `b` : `"saturn"` si `b` est vide, sinon `"saturn-"` + `b` sans blancs
autour, le tout en minuscules invariantes.

#### 3.1.5 Objets rendus

`GameSaveGame` ou `GameSaveState` du SDK. Seuls les champs cités sont renseignés ; `Title` (sauf `AddSaveFile`),
`MatchLineageId`, `MigrationFamilyId`, `DisplayChipText`, `OriginalFileName`, `ReportedFileSizeBytes`,
`ReportedLastModifiedUtc` restent null et `IsDirectory` faux. Les horodatages sont ceux que le SDK calcule sur
`FileLocation`.

### 3.2 `GetSavesResponse GetSaves(GetSavesArgs args)`

`args` null : `NullReferenceException`. Rend `new GetSavesResponse(liste)` (`WasSuccess=true`, liste éventuellement
vide), sans tri ni dédoublonnage. Une erreur d'énumération d'un dossier existant (droits) REMONTE.
`EmulatorFileName` = nom de fichier de `args.Emulator?.ApplicationPath` partout. L'émulateur utilisé est toujours
`args.Emulator`.

**Première passe : `args.AdditionalApplications`** (null = aucune), dans l'ordre. Pour chaque application `a` :

1. `a` null ou `a.ApplicationPath` vide : ignorée.
2. Cœur `K` = nom trouvé (§ 4.1) dans `a.GetEffectiveCommandLine()`, sinon `""` (ligne null : `ArgumentNullException`,
   qui remonte).
3. `B` = nom de base de `a.ApplicationPath` ; jeu `J` = `PluginHelper.DataManager.GetGameById(a.GameId)` (peut être
   null).
4. Dossier des saves (§ 3.1.2) pour `(args.Emulator, J, a)`, sans création. S'il existe :
   - `fichiers` = les fichiers (non récursif) du dossier dont le nom COMMENCE par `B` (motif Windows `B*.*`, sans casse ;
     avec ou sans extension) : les jeux au nom plus long (`Mario` attrape `Mario Kart.srm`), le contenu et les
     savestates s'y trouvent aussi quand les dossiers coïncident. Ordre du système de fichiers (NTFS : ordre des noms,
     l'espace avant le point).
   - Contexte Saturn (`K`, `J`) : les fichiers à extension Saturn sont regroupés par nom de base, égalité SANS CASSE
     ORDINALE (mesuré : `Strax.bcr` et `Stra` U+00AD `x.bkr` font deux groupes, alors qu'une égalité invariante les
     confondrait ; `Strai` et `StraI` en font un ; `Straİ` et `Straı` restent à part ; l'identifiant de `Straİ` garde
     U+0130 après la mise en minuscules invariantes). Groupes dans
     l'ordre de première apparition, clé = nom du premier vu). Un `GameSaveGame` par groupe : `GameId = J?.Id`,
     `AdditionalApplicationId = a.Id`, `FileLocation` = le membre de plus petit rang (à rang égal le premier vu),
     `EmulatorFileName`, `EmulatorCore = K`, `SaveGroupId` = identifiant de groupe Saturn de la clé,
     `SaveGroupName = "My Save File"`. Ces fichiers sont retirés des restants. Hors contexte, ils restent ordinaires.
   - Restants (ordre conservé) :
     - exactement 1 : un `GameSaveGame` pour lui, quelle que soit son extension (`GameId = J?.Id`,
       `AdditionalApplicationId = a.Id`, `FileLocation`, `EmulatorFileName`, `EmulatorCore = K`,
       `SaveGroupName = "My Save File"`) ;
     - sinon : le premier dont l'extension vaut EXACTEMENT `.srm` (ordinal), puis le premier dont elle vaut exactement
       `.mcr`, donnent chacun une ligne ; si ni l'un ni l'autre n'existe, une ligne pour CHAQUE restant.
5. Dossier des savestates (§ 3.1.3) pour `(args.Emulator, J, a)`, sans création. S'il existe : fichiers dont le nom
   commence par `B.state` (motif Windows `B.state*`, sans casse). Pour chacun, `x` = son extension (la dernière, point
   compris) privée de toutes les occurrences exactes (ordinales) de `.state`, sans blancs autour :
   - `x` vide → slot 0 ; `x` égal à `.auto` ou `auto` sans casse ordinal → slot -1 ;
   - sinon entier lu par `int.TryParse` (culture courante, signe et blancs permis) → ce slot ; échec → fichier ignoré.
   Mesuré ou déduit : `B.state1` → 1, `B.state03` → 3, `B.state-2` → -2, `B.state.state3` → 3, `B.state.AUTO` → -1 ;
   ignorés : `B.STATE1`, `B.state1.png`, `B.state.bak`.
   `GameSaveState` : `GameId = J?.Id`, `AdditionalApplicationId = a.Id`, `FileLocation`, `Slot`, `EmulatorFileName`,
   `EmulatorCore = K`, `SaveGroupName = "My Save State"`.

**Seconde passe : `args.Games`** (null = aucun), dans l'ordre. Pour chaque jeu `g` :

1. `g` null ou `ApplicationPath` vide : ignoré.
2. Si `g.GetAllAdditionalApplications()` n'est pas null et contient une application dont `ApplicationPath` est
   EXACTEMENT (ordinal) celui du jeu : ignoré. Une application null dans ce tableau : `NullReferenceException`.
3. `K` depuis `g.GetEffectiveCommandLine()` ; `B` = nom de base de `g.ApplicationPath`.
4. Saves et states comme ci-dessus avec `(args.Emulator, g, null)`, `GameId = g.Id`, `AdditionalApplicationId` null,
   contexte Saturn `(K, g)`.

### 3.3 `AddSaveResponse AddSaveFile(AddSaveArgs args)`

`args` null : `NullReferenceException`. Contrôles dans l'ordre (`WasSuccess=false`, message exact) :

1. `SaveToAdd` null, `FileLocation` vide ou fichier absent : `"The save file doesn't exist!"`.
2. `J = PluginHelper.DataManager.GetGameById(SaveToAdd.GameId)` null : `"Game doesn't exist!"`.
3. `SaveToAdd.AdditionalApplicationId` non vide : application `A` = première de `J.GetAllAdditionalApplications()` dont
   `Id` est égal (ordinal) ; tableau null ou application introuvable : `"App doesn't exist!"` ; application null dans le
   tableau : `NullReferenceException`. Id d'émulateur = `A.EmulatorId`. Sinon id = `J.EmulatorId`.
4. Id vide : `"Can't get emulator record for game/app in question!"`.
5. `E = GetEmulatorById(id)` null : `"Can't get emulator record game/app in question!"` (sans « for »).
6. Contenu `C` = `A.ApplicationPath` s'il n'est pas vide, sinon `J.ApplicationPath` ; vide :
   `"No Game/App application path found!"`.
7. Destination (le dossier est CRÉÉ s'il manque, même si l'on s'arrête ensuite) :
   - source `GameSaveState` : suffixe `.state` (Slot null ou 0), `.state<N>` (N > 0), `.state.auto` (Slot < 0) ;
     dossier des savestates (§ 3.1.3) pour `(E, J, A)` ; destination = dossier + nom de base de `C` + suffixe ;
   - sinon : dossier des saves (§ 3.1.2) pour `(E, J, A)` ; destination = dossier + nom de base de `C` + extension de la
     source (casse d'origine ; rien si la source n'en a pas).
   Aucun dossier : `"Couldn't determine where to place this save!"`.
8. Destination existante et `ShouldOverwriteFunc` null ou rendant faux : `"Operation cancelled by user!"`.
9. Cœur `K` = nom trouvé (§ 4.1) dans la ligne effective de l'application si elle existe et n'est pas null, sinon celle
   du jeu ; sinon `""`. Si les deux sont null : `ArgumentNullException`, qui REMONTE (après la création du dossier et la
   question d'écrasement). Contexte Saturn évalué avec `K` et le JEU `J`.
10. Copie source → destination avec écrasement. En contexte Saturn, et si les dossiers de la source et de la
    destination ne sont pas vides (une source donnée par un nom de fichier seul n'a pas de dossier : pas de recopie),
    pour `.bcr`, `.bkr`, `.smpc` dans cet ordre : le fichier « préfixe source + ext », s'il existe et que sa chaîne
    diffère (sans casse ordinal) de celle du chemin source, est copié vers « préfixe destination + ext » avec écrasement,
    sans demander. Vaut aussi pour un savestate. Le préfixe d'un chemin est son dossier assemblé (règles de
    `Path.Combine`) avec son nom de base ; quand le nom de base est VIDE, le préfixe est le dossier lui-même, sans
    séparateur final : source `T\src\.bcr` → candidats `T\src.bcr`, `T\src.bkr`, `T\src.smpc` (à côté du dossier ; un
    `T\src.bcr` existant écrase alors le primaire qui vient d'être écrit) ; destination `T\RA\saves\.bcr` →
    compagnons écrits en `T\RA\saves.bkr`, `T\RA\saves.smpc`.
    Toute exception de copie : `"Could not copy file to destination!"` (des fichiers peuvent déjà être copiés).
11. Succès : `AddSaveResponse(nouvelleSave)` :
    - `GameSaveState` si la source en est un : `GameId`, `AdditionalApplicationId`, `Slot`, `Title` de la source ;
      `FileLocation` = destination ; `EmulatorFileName` = nom de fichier de `E.ApplicationPath` ; `EmulatorCore = K` ;
      `SaveGroupId` = identifiant de groupe Saturn du nom de base de la destination en contexte Saturn, sinon celui de
      la source ; `SaveGroupName` = celui de la source s'il n'est pas vide, sinon `"My Save State"` ;
    - sinon `GameSaveGame`, mêmes règles avec `"My Save File"` et sans `Slot`.
    Aucun autre champ n'est reporté.

### 3.4 `bool IsSecondarySaveFile(string filePath)`

`true` si et seulement si `filePath` n'est pas vide et que son extension est `.bkr` ou `.smpc` sans casse ordinal.

### 3.5 `IReadOnlyList<string> GetCompanionSaveFiles(string primaryFilePath)`

- Vide, extension non Saturn, ou dossier ou nom de base vide : `Array.Empty<string>()`.
- Sinon `List<string>` des chemins `<dossier>\<nom de base><ext>` (dossier racine `X:\` : `X:\<nom de base><ext>`) (ext = `.bcr`, `.bkr`, `.smpc`, en minuscules) qui
  existent et dont la CHAÎNE diffère, sans casse ordinal, de `primaryFilePath`. Le dossier est celui que rend
  `Path.GetDirectoryName`, qui normalise les `/` en `\` : un primaire écrit avec des `/` n'est donc pas reconnu et figure
  parmi ses propres compagnons (mesuré) ; un `.\` dans le chemin est conservé et ne pose pas ce problème (mesuré). Le
  primaire n'a pas besoin d'exister. Liste possiblement vide.

### 3.6 `bool TryGetSaveGroupInfo(GameSaveBase save, string primaryFilePath, out string saveGroupId, out string saveGroupName)`

- Sorties null au départ. `save` null : `false`.
- Chemin = `primaryFilePath` s'il n'est pas vide, sinon `save.FileLocation` ; vide : `false`.
- Extension non Saturn ET `save.EmulatorCore` vide ou sans `saturn` (sans casse ordinal) : `false`.
- Nom de base vide : `false`.
- Sinon `saveGroupId` = identifiant de groupe Saturn du nom de base, `saveGroupName` = le nom de base tel quel, `true`.

### 3.7 `bool UseSaveGroupIdForPersistedMatch(GameSaveBase save)`

`true` si `save` existe, que `SaveGroupId` n'est pas vide et qu'il commence par `saturn` sans casse ordinal.

### 3.8 `bool TryComputeSaveSignature(GameSaveBase save, string emulatorApplicationPath, out string signature)`

- `signature` null au départ ; second argument ignoré.
- `save` null, `FileLocation` vide ou absent : `false`. Ni extension Saturn ni cœur contenant `saturn` : `false`.
  Dossier ou nom de base vide : `false`.
- Fichiers signés : ceux qui existent parmi `<dossier>\<nom de base>.bcr`, `.bkr`, `.smpc`, dans cet ordre. Aucun :
  `false`. Le fichier pointé n'en fait partie que s'il a lui-même une extension Saturn.
- `signature` = MD5, en hexadécimal majuscule (32 caractères), des octets UTF-8 sans BOM d'un texte qui compte une
  ligne par fichier signé, chaque ligne terminée par `\r\n` : le nom du fichier en minuscules invariantes, `|`, puis
  le MD5 de son contenu en hexadécimal majuscule. Exemple : `a.bcr` (octet `01`) et `a.bkr` (octets `02 03`) →
  MD5 de `a.bcr|55A54008AD1BA589AA210D2629C1DF41\r\na.bkr|<MD5 de 02 03>\r\n`. Rend `true`.
- Les fichiers doivent rester lisibles pendant qu'un autre processus les a ouverts en écriture partagée (X17).
- Exception pendant le hachage : `false`, `signature` null.

---

## 4. Formats reconnus dans les lignes de commande

Les deux formats sont sensibles à la casse et cherchés n'importe où dans la ligne ; c'est la première occurrence qui
convient qui compte. Les tableaux d'exemples font foi.

### 4.1 Format « nom de cœur » (saves, savestates, AddSave)

Le nom de cœur suit le texte littéral `-L "cores\` (tiret, `L` majuscule, une seule espace, guillemet droit, `cores`
en minuscules, antislash). Règles :

- le nom compte au moins un caractère et ne contient aucun point (antislash, espaces, guillemets et sauts de ligne sont
  permis) ;
- il doit être suivi d'exactement un caractère quelconque, sauf un saut de ligne, puis de `dll"` ;
- parmi les noms possibles après une même occurrence, c'est le plus long qui est retenu ;
- si aucune occurrence ne convient : pas de nom (`""` là où la spec parle de cœur vide).

| Ligne | Nom |
|---|---|
| `-L "cores\snes9x_libretro.dll"` | `snes9x_libretro` |
| `-L "cores\snes9x_libretro.dll" "%romfile%"` | `snes9x_libretro` |
| `-L "cores\sub\x_libretro.dll"` | `sub\x_libretro` |
| `-L "cores\abc_dll"` | `abc` |
| `-f -L "cores\x.dll"` | `x` |
| `-L "cores\a.b.dll"` | aucun |
| `-L "Cores\x.dll"`, `-L cores\x.dll`, `-L "C:\RA\cores\x.dll"`, `-L  "cores\x.dll"` | aucun |

### 4.2 Format « fichier de cœur » (table BIOS)

Même texte de tête `-L "cores\`. Le nom est tout ce qui sépare cette tête de la DERNIÈRE occurrence de `.dll"` (point
compris) sur la même ligne ; il compte au moins un caractère et peut contenir des points, pas de saut de ligne.

| Ligne | Nom |
|---|---|
| `-L "cores\4do_libretro.dll" "%romfile%"` | `4do_libretro` |
| `-L "cores\a.b.dll"` | `a.b` |
| `-L "cores\a.b.dll" x.dll"` | `a.b.dll" x` |
| `-L "cores\.dll"` | aucun |
| `-L "Cores\x.dll"` | aucun |

---

## 5. Table BIOS

Ressource unique `Bios_Requirements`, reprise octet pour octet dans `resources\Bios_Requirements.txt` : 631 lignes
séparées par `\r\n` (pas de saut final, pas de BOM), 10 colonnes séparées par une tabulation, sans guillemets. La
première ligne est un en-tête (`CoreName`, `Platform`, `BiosDesc`, `BiosPath`, `MD5`, `Required`, `GroupId`,
`GroupDesc`, `IsGroupRequired`, `GroupRequirements(None|Any|All)`) qui n'est PAS sautée : c'est une ligne comme une autre.

Pour un nom de fichier de cœur `c` (§ 4.2) et une plateforme `p` :

1. Découpage sur `\r\n` exactement, puis sur `\t`.
2. Ligne retenue si la colonne 0 est égale à `c` sans casse invariant ET si la colonne 1 est vide ou égale à `p` sans
   casse invariant. Ordre du fichier.
3. Pour chaque ligne retenue, un `BiosFile` (Location = `BiosFile.SystemFolder` du moment) : `fileName` = colonne 3
   (séparateur `/` gardé), `required` = colonne 5 lue comme booléen .NET (`TRUE`/`FALSE` sans casse ; autre texte →
   faux), `description` = colonne 2, `md5` = colonne 4 ou null si vide. Groupe : aucun si la colonne 6 est vide ; sinon
   un `EmulatorBiosGroup` partagé par identifiant, créé à la PREMIÈRE ligne retenue de l'appel qui porte cet
   identifiant : `id` = colonne 6, `description` = colonne 7, `isRequired` = colonne 8 en booléen .NET, exigence =
   colonne 9 passée au constructeur du SDK qui prend une chaîne (`AllItemsRequired` vrai si et seulement si `all` sans
   casse). Les lignes suivantes du même identifiant réutilisent CE groupe même si leurs colonnes 7 à 9 diffèrent.
4. Rend la `List<EmulatorBiosFile>` (vide si aucune ligne).

Données utiles (mesuré) : 103 cœurs ; 303 lignes à plateforme vide ; 331 sans groupe. `mednafen_saturn_libretro` :
4 lignes, md5 tous renseignés, les deux premières (`sega_101.bin`, `mpr-17933.bin`, requises) dans le groupe `0`
« Regional BIOS » (requis, exigence `Any`), les deux autres sans groupe. `ep128emu_core_libretro` : 22 lignes, `exos20.rom`
porte le groupe `2` décrit « Enterprise 128 Expandible OS » (première définition). `puae_libretro` et
`puae2021_libretro` réutilisent l'identifiant `0` avec des descriptions différentes selon la plateforme.

`BiosFile.SystemFolder` ne sert que de `Location` aux `BiosFile` construits ensuite.

---

## 6. Réseau et installation

### 6.1 Dernière version de RetroArch

1. Source principale : GET `https://buildbot.libretro.com/stable/`, en-tête `User-Agent: request` (seul ajout ; pas de
   `Referer`). Non 2xx ou corps vide : échec de la source.
   Lecture : on cherche, sans casse ordinal, chaque occurrence du texte `<a href="/stable/` ; le texte qui la suit
   jusqu'au premier `/"` est un numéro de version, jugé par `System.Version` (son analyse fait foi). Règles :
   - un numéro refusé par `System.Version` fait échouer TOUTE la source ;
   - si `/"` manque après une occurrence, ou suit immédiatement le texte cherché, la lecture S'ARRÊTE sans échec et
     garde les numéros déjà lus ;
   - une occurrence en position 0 du corps arrête la lecture (aucun numéro) ;
   - liste vide : échec de la source ; sinon le plus grand numéro (ordre de `System.Version`), sous la forme que rend
     `System.Version` (`1.09.0` → `1.9.0`).
2. Sur n'importe quel échec de la source principale : GET `https://api.github.com/repos/libretro/RetroArch/tags`, même
   en-tête. Non 2xx : `HttpRequestException` qui remonte. Corps vide, ou tableau JSON vide :
   `InvalidOperationException(Strings.ErrorParseRetroArchWebsite)` ("Unable to parse the RetroArch website to retrieve
   the latest version."). Corps qui n'est pas un tableau JSON : `JsonReaderException` (Newtonsoft) qui remonte. Sinon,
   selon le PREMIER élément du tableau (ordre de l'API, sans tri) ; mesuré avec la même bibliothèque :

   | Premier élément | Résultat |
   |---|---|
   | objet avec `name` chaîne | la chaîne privée de tous ses `v` MINUSCULES de tête (`v1.2.3` → `1.2.3`, `vv1.2` → `1.2`, `V1.2.3` reste `V1.2.3`) |
   | objet sans `name` | `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException` |
   | objet avec `name` à `null` | `NullReferenceException` |
   | objet avec `name` nombre ou booléen | `RuntimeBinderException` (pas de conversion en texte) |
   | objet avec `name` objet | `NullReferenceException` |
   | objet avec `name` tableau | `RuntimeBinderException` |
   | nombre, chaîne ou `null` (pas un objet) | `RuntimeBinderException` |
   | tableau (`[[1]]`, `[[]]`) | `RuntimeBinderException` (mesuré) |

3. Client HTTP unique pour le greffon, réglages par défaut de .NET (redirections suivies, délai de 100 s).

### 6.2 Téléchargement d'une archive

- Avant toute requête, le chemin du fichier temporaire est déterminé par `Paths.GetTempFilePath() + ".7z"` (mesuré :
  `<racine LaunchBox>\Metadata\Temp\<GUID>`). Cet appel CRÉE le dossier `<racine>\Metadata\Temp` s'il manque (sans
  fichier), donc ce dossier existe après l'appel même si la requête échoue ou est annulée (mesuré par le juge). Si ce
  chemin ne peut pas être obtenu (`NamingHelper.RootFolder` null : `ArgumentNullException`, ParamName `path1` ;
  `<racine>\Metadata` est un fichier : l'`IOException` de création du dossier), l'exception d'origine sort telle
  quelle, NON enveloppée, et AUCUNE requête n'est envoyée.
- GET SANS en-tête ajouté (pas de User-Agent), lecture dès réception des en-têtes. Non 2xx : échec, et aucun FICHIER
  n'est créé (le dossier `Metadata\Temp`, lui, existe).
- Après un statut de succès, création (ou écrasement) du fichier temporaire, ouvert SANS AUCUN PARTAGE pendant tout le
  téléchargement : un autre processus qui tente de l'ouvrir, même en lecture seule, échoue (violation de partage). Lecture par blocs (au plus 80 Kio). Après
  chaque bloc reçu et avant de l'écrire : si la fonction d'annulation rend vrai, annulation. Sinon écriture puis rappel
  de progression `(octets cumulés, Content-Length ou -1 s'il manque)` ; les fractions calculées par les appelants sont
  `cumulés / total`, donc négatives sans `Content-Length`.
- Annulation (fonction d'annulation, ou délai HTTP dépassé, que .NET signale comme une annulation) : le fichier
  temporaire éventuel est supprimé (erreur avalée) et le téléchargement ne rend « aucune archive », sans exception.
  L'appelant consulte ensuite lui-même la fonction d'annulation.
- Toute autre erreur (HTTP non 2xx, réseau, disque, exception levée par le rappel de progression ou par la fonction
  d'annulation) : `InvalidOperationException(Strings.ErrorUnableToDownloadRetroArch)` ("Unable to download Retroarch.")
  avec l'erreur d'origine en `InnerException` ; un fichier partiel déjà créé n'est PAS supprimé.

### 6.3 URL

- Programme : `https://buildbot.libretro.com/stable/{V}/windows/x86_64/RetroArch.7z`
- Cœurs : `https://buildbot.libretro.com/stable/{V}/windows/x86_64/RetroArch_cores.7z`
  (`{V}` inséré tel quel.) Les deux archives ont un dossier racine `RetroArch-Win64`.

### 6.4 Mise à jour des cœurs d'un émulateur (action d'import)

1. Dernière version (§ 6.1, peut lever).
2. Téléchargement des cœurs (§ 6.2) ; progression `cumulés / total` vers `ReportActionProgress`. Si le délégué de
   progression ou d'annulation fourni à la construction de l'`ImportActionArgs` est null, l'appel lève au premier bloc
   et l'erreur devient l'`InvalidOperationException(ErrorUnableToDownloadRetroArch)` de § 6.2.
3. Puis `ShouldCancelAction()` ; vrai : arrêt silencieux (une archive complète reste dans le dossier temporaire).
4. `ApplicationPath` de l'émulateur vide : `InvalidOperationException(Strings.ErrorNoRetroArchAppPath)`.
5. Dossier vide : `AccessViolationException(Strings.ErrorCouldNotExtractRetroArchCores)`. Puis le contenu de l'archive
   arrive dans le dossier de l'émulateur selon la même règle qu'en § 2.13 (effet 4), parent normalisé compris (un
   dossier d'exe `…\RA\ ` ou `…\RA\...` extrait dans le parent de `RA`) ; échec (dont
   archive absente) : même `AccessViolationException`.
6. Suppression de l'archive, erreur journalisée (`Root.Logging.PublishIgnoredException`) et ignorée.

---

## 7. API internes de LaunchBox utilisées

Le clone peut les appeler ; il les appelle aux mêmes moments et attend les mêmes résultats.

SDK `Unbroken.LaunchBox.Plugins` :

- `EmulatorPlugin` et ses membres virtuels ; `IEmulatorWithCores` ; `IPluginPaths.DataDirectory`.
- `PluginHelper.DataManager` (`IDataManager`) : `GetGameById(string)`, `GetEmulatorById(string)`.
- `IGame` : `Id`, `ApplicationPath`, `CommandLine`, `EmulatorId`, `Platform`, `GetEffectiveCommandLine()`,
  `GetAllAdditionalApplications()`.
- `IAdditionalApplication` : `Id`, `GameId`, `ApplicationPath`, `CommandLine`, `EmulatorId`, `GetEffectiveCommandLine()`.
- `IEmulator` : `ApplicationPath` (lu et écrit), `CommandLine`, `Title`, `EnableHardcoreAchievements`,
  `GetAllEmulatorPlatforms()`, `AddNewEmulatorPlatform()`.
- `IEmulatorPlatform` : `Platform`, `CommandLine`, `IsDefault`, `M3uDiscLoadEnabled`.
- `GameSaveBase`, `GameSaveGame`, `GameSaveState` ; `AddSaveArgs`, `AddSaveResponse` ; `GetSavesArgs`,
  `GetSavesResponse(IEnumerable)` ; `PluginResponse(bool, string)` ; `PrepareForLaunchArgs`, `PrepareForLaunchResponse` ;
  `RetroAchievementCredentials` ; `RetroAchievementSupportResponse` ; `InjectRetroAchievementsCredentialsArgs` ;
  `ImportActionArgs` (`Action`, `GamesImported`, `ReportActionProgress`, `ShouldCancelAction`) ;
  `EmulatorImportAction.Id`, `BooleanEmulatorImportAction` ; `InstallEmulatorArgs` ; `EmulatorInstallResponse` ;
  `EmulatorSupportResponse` ; `EmulatorControllerVersion` ; `EmulatorBiosFile` (constructeur à 6 paramètres) ;
  `EmulatorBiosGroup(string, string, bool, string)`.

`Unbroken.LaunchBox.LocalDb` : `GamesDb.GetEmulatorsByNameWithPlatformsAsync(string, CancellationToken?)`,
`GamesDb.GetValidPlatformNameAsync(string, CancellationToken?)` ; `DbModels.DbEmulator.Platforms` ;
`DbModels.DbEmulatorPlatform` (`Platform`, `CommandLine`, `Recommended`, `RequiredBiosFile`).

`Unbroken.LaunchBox` : `NamingHelper.RootFolder` ; `Properties.Strings` (`LabelDownloadRetroArchCore`,
`LabelDownloadingMissingCores`, `LabelDownloadingRetroArch`, `ErrorUnableToDownloadRetroArch`,
`ErrorParseRetroArchWebsite`, `ErrorRetroArchArchive`, `ErrorCouldNotExtractRetroArchCores`, `ErrorNoRetroArchAppPath`) ;
`Data.PlatformBase` (`Name`, `ScrapeAs`) ; `Data.GameBase` (`Platform`, `EmulatorId`, `AdditionalApplications`) ;
`Data.AdditionalApplicationBase.EmulatorId`.

`Unbroken.LaunchBox.Windows` :

- `Root.DataManager` : `Platforms` (`TryGetValue`, `Values`), `Games.Values`, `GetPlatform(string)` (dont `ScrapeAs`),
  `GetDefaultEmulatorExistsForPlatform(string emulatorName, string platform)`.
- `Root.Logging.PublishIgnoredException(Exception)`.
- `Data.Emulator` (constructeur sans paramètre, `Id`, propriétés de § 2.13, `Platforms.Add`) ;
  `Data.EmulatorPlatform(string emulatorId, string platform, string commandLine, bool isDefault, bool m3uDiscLoadEnabled)`.
- `Search.LocalDbEmulator.Get…AutoHotkeyScript(string)` (les 8 de § 2.13).
- `Search.LocalDbWindows.GetRetroArchCoreFileName(string)` et `GetCommandLineOrCore(bool, string, bool)` (comportements
  mesurés cités en § 2.9 et § 2.15).
- `SevenZip.RenameArchiveFolder(string, string, string)` et `SevenZip.Extract(string, string)` (paramètres optionnels
  par défaut). Mesuré : ils lancent `<racine>\ThirdParty\7-Zip\7z.exe` et rendent `false` pour une archive null ou
  invalide.
- `FileHelper.GetFullPath(string)` (relatif résolu contre la racine de LaunchBox, mesuré),
  `FileHelper.GetFileNameWithoutExtensionSafe(string)`, `FileHelper.GetRelativePath(string)` ; `Paths.GetTempFilePath()`.

Les corps de ces assemblies sont obfusqués ; les appeler exécute le vrai code.

Newtonsoft.Json sert uniquement à lire la réponse de l'API GitHub (§ 6.1) ; le clone doit reproduire exactement le
tableau des résultats de § 6.1, exceptions comprises.

---

## 8. Ce que LaunchBox observe de l'original

L'identité de l'assembly du clone (nom, version, PluginId, manifest) est fixée par la session principale, hors de cette
spec. Côté comportement : LaunchBox appelle l'original par `EmulatorPlugin` et `IEmulatorWithCores`. Aucune référence en
clair au nom `UpdateEmulator` d'un greffon n'a été trouvée dans les binaires de LaunchBox (les occurrences relevées sont
des libellés d'interface) ; savoir si LaunchBox l'appelle par son nom reste ouvert (§ 10).

## 9. Bizarreries à reproduire telles quelles

1. Une seule ligne de cfg sans `=` ou à valeur trop courte annule toutes les saves et tous les states ; valeurs sans
   guillemets amputées d'un caractère de chaque côté.
2. `savefile_directory = "default"` (ou tout chemin relatif sans `:\`) est relatif au répertoire courant ; `AddSaveFile`
   peut y créer un dossier.
3. Asymétrie des chemins relatifs : le cfg et `info` suivent le répertoire courant, la valeur `:\` suit la racine de
   LaunchBox, le dossier `cores` aussi. Après `:\`, un reste `\nom` vise la racine du lecteur de l'EXE et un reste
   `X:nom` du même lecteur vise le dossier de l'exe ; le dossier temporaire `Metadata\Temp` est créé avant toute
   requête, même si elle échoue.
4. Le préfixe `:\` et les clés (`system_directory`, `corename`, `Version: `, `cheevos_*`) sont testés de façon
   culturelle ; un caractère ignorable en tête est accepté. Ensuite :
   - `Version: `, `:\` et `corename` : la découpe par position compte ce caractère (` 1.2.3 `, `\sys`, `"Snes9x`) ;
   - `system_directory` : le caractère disparaît, la valeur étant prise après le `=` ;
   - `cheevos_token` et `cheevos_username` : il RESTE dans la valeur (obtenue par suppressions ; le retrait des blancs
     ne l'enlève pas). U+00AD puis `cheevos_token = "t"` donne un jeton fait de U+00AD, de deux espaces puis de `t`.
5. Ordre des sous-dossiers : contenu puis cœur.
6. Dossier de cœur des saves pris de la `CommandLine` brute ; celui des states de la ligne effective (une chaîne vide
   d'application bloque le repli, null non).
7. Nom de cœur du `.info` : 12 caractères de tête et dernier retirés sans contrôle.
8. Format « nom de cœur » : caractère libre avant `dll"`, nom sans point, `cores` en minuscules.
9. `GetSaves` : préfixe `B*`, fichier unique rendu quel qu'il soit (la ROM), `.srm`/`.mcr` sensibles à la casse, tous les
   fichiers rendus faute de `.srm`/`.mcr`.
10. States : `.state-2` → -2, `.state03` → 3, `.state.state3` → 3, `.STATE1` ignoré.
11. Pas de dédoublonnage entre passes ni entre doublons d'entrée.
12. `AddSaveFile` : message sans « for » ; Saturn jugé sur le jeu ; compagnons Saturn copiés à côté d'un savestate, groupe
    `saturn-<nom de base du state>` (`Jeu.state.auto` → `saturn-jeu.state`) ; dossier créé même en cas d'abandon ; avec un nom de base vide (`.bcr`), les compagnons sont cherchés et écrits À CÔTÉ
    du dossier (`T\src.bkr`, `T\RA\saves.bkr`).
13. `TryGetSaveGroupInfo` groupe tout fichier d'un cœur Saturn, states compris ; `TryComputeSaveSignature` ignore un
    fichier pointé non Saturn ; `GetCompanionSaveFiles` se cite lui-même pour un chemin à `/`.
14. `GetCurrentVersion` : découpe par positions, espace final, exception si trop court, attente sans fin.
15. BIOS : cœur absent de la table = liste vide ; en-tête lu comme données (cœur `CoreName`, plateforme `Platform`) ;
    première description de groupe imposée ; `SystemFolder` global réécrit à chaque appel, `"system"` pour la surcharge
    à un paramètre.
16. `FireEmulatorImportAction` : émulateurs × plateformes ; plateforme ajoutée puis `ArgumentNullException` ; ligne
    vide = cœurs « manquants » ; tout lève.
17. Installation : archive laissée après échec ou annulation tardive ; délai HTTP = annulation silencieuse puis échec
    7-Zip ; jeux à `Platform` null font planter ; doublons LocalDb recopiés ; `IsDefault` calculé sans les lignes
    ajoutées.
18. RetroAchievements : `IsEnabled` ignoré ; hardcore null = `true` ; « contient `true` » ; jeton et nom dépouillés de
    `=` et `"`.
19. `UpdateEmulator` interroge deux fois le réseau et compare la version tronquée en ordinal.

## 10. Points non tranchés

- Corps internes de LaunchBox non lus (obfusqués) ; seuls les comportements cités comme mesurés ont été vérifiés.
- Extraction avec destination null (`D` à la racine d'un lecteur) : non mesurée.
- Le répertoire courant de LaunchBox au moment des appels (décide des chemins relatifs).
- Format actuel de `https://buildbot.libretro.com/stable/` non vérifié en direct (aucun accès réseau).
- Appel éventuel de `UpdateEmulator` par son nom depuis LaunchBox.
