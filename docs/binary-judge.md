# Jugement binaire : Unbroken RetroArch contre Nixx.RetroArch

Date : 10/10/2026. Juge indépendant. Je n'ai travaillé qu'à partir des deux DLL, chargées et exécutées côte à côte. Je n'ai ouvert ni le code source du clone, ni ses documents, ni les rapports des juges précédents.

## 1. Binaires jugés

| Rôle | Fichier | SHA-256 |
|---|---|---|
| Original | `G:\LB1326\Backups\nixx-retroarch-essai-2026-10-09\RetroArch LaunchBox Integration\Unbroken.LaunchBox.Windows.RetroArch.dll` | `3AB1386A055CEFB935C2E44D76215002730716B438DA4F889B2AFD36B769E2CA` |
| Clone | `G:\LB1326\Local\Plugins\Nixx-RetroArch\Nixx.RetroArch.dll` | `406A153D4B297EBE50B51F5224B6514A408EAD49C3DADEE2C4A95B5E3039DBFC` |

J'ai travaillé sur des copies, hors du dépôt. Je n'ai rien écrit dans `G:\LB1326`.

## 2. Méthode

1. J'ai lu tous les chemins de chaque membre public des deux DLL, y compris les helpers internes du clone.
2. Un banc jetable en .NET 10 (WindowsDesktop) charge les deux DLL et résout les dépendances depuis `G:\LB1326\Core`. Il fait pointer `NamingHelper.RootFolder` vers un dossier temporaire factice et pose un `PluginHelper.DataManager` factice. Les `IGame`, `IEmulator`, `IAdditionalApplication`, `IEmulatorPlatform` et `IPluginPaths` sont des proxys `DispatchProxy`.
3. Chaque cas tourne deux fois, sur deux arborescences identiques et neuves, une par DLL. Pour chaque cas, je compare la valeur rendue (sérialisée en profondeur, type concret compris), l'exception (type, message, paramètre), les paramètres `out`, la valeur statique `BiosFile.SystemFolder` et l'état final du disque (arborescence et MD5 de chaque fichier).
4. Résultat : **4026 cas, 4026 identiques, 0 différent**. Aucune boîte modale ne s'est ouverte, et les dossiers temporaires ont été effacés.
5. Je n'ai pas exécuté les membres qui touchent la base locale de LaunchBox (`GamesDb`), le réseau ou 7-Zip, pour ne lancer ni téléchargement ni écriture dans la base. Je les ai comparés à la lecture, branche par branche (classement « certain »).

## 3. Surface publique

Comparaison par réflexion, avec les espaces de noms ramenés à un même nom :

- **Types exportés** : il y en a deux de chaque côté, `RetroArchPlugin` (classe publique qui dérive de `EmulatorPlugin` et implémente `IEmulatorWithCores`) et `Models.BiosFile` (qui dérive de `EmulatorBiosFile`). Leurs attributs de type sont les mêmes.
- **Membres** : les mêmes 40 lignes de surface. Mêmes noms, signatures, types de retour, noms de paramètres, paramètres `out`, valeurs par défaut (le constructeur de `BiosFile` a les mêmes 3 paramètres optionnels à `null`) et propriété statique `SystemFolder` (initialisée à `system`).
- **Surcharges** : les mêmes de chaque côté. Sont surchargés `EmulatorName`, `AddSaveFile`, `FireEmulatorImportAction`, `GetApplicableEmulators`, les deux `GetBiosFilesForPlatform`, `GetCurrentVersion`, `GetImportActions`, `GetInstallableVersions`, `GetPotentialSaveSlots`, `GetSaves`, `InjectRetroAchievementsCredentials`, `InstallEmulator`, `IsPlatformSupported`, `PrepareEmulatorForLaunch`, `SupportsRetroAchievements`, `SupportsSaveManagement`, `IsSecondarySaveFile`, `GetCompanionSaveFiles`, `TryGetSaveGroupInfo`, `UseSaveGroupIdForPersistedMatch` et `TryComputeSaveSignature`. Ne sont surchargés ni d'un côté ni de l'autre : `IsUpdateAvailable` (×2), `RemoveSave`, `IsSaveActive`, `TryBackupSave`, `IsSaveContainer` et `NormalizeCommandLineForExecutable`.
- **Membres non virtuels** : `UpdateEmulator` est public et non virtuel des deux côtés. `GetAllAvailableCores` et `GetSuggestedCores` sont des implémentations scellées de `IEmulatorWithCores`, avec la même table d'interface.
- **Table BIOS** : l'original la lit dans une ressource `.resources`, le clone dans un `.txt` embarqué. Le texte rendu est **identique au caractère près** (77 212 caractères, comparaison ordinale). Le fichier du clone n'a pas de BOM. La table compte 631 lignes séparées par CRLF, toutes à 10 colonnes, sans CR ni LF parasite.
- **Différences de surface sans effet de comportement**, notées pour mémoire et non comptées comme écarts :
  - **Nullabilité** : l'original porte des annotations `NullableAttribute`/`NullableContextAttribute` sur `GetCurrentVersion`, `GetInstallableVersions`, `GetApplicableEmulators`, `TryGetSaveGroupInfo`, `TryComputeSaveSignature` et `UseSaveGroupIdForPersistedMatch`, que le clone n'a pas. Ce ne sont que des métadonnées de compilation, et aucun appel n'en dépend.
  - **Attributs d'assembly** : l'original a été compilé en Debug (`DebuggableAttribute` 263, optimisations coupées), en version 0.0.0.0, avec une métadonnée Sentry `ProjectDirectory` et `UnverifiableCode`. Le clone est en Release, en version 1.0.0.0, avec Company, Product, Title, InformationalVersion et `SupportedOSPlatform(Windows7.0)`.
  - **Assemblies référencés** : l'original a été compilé contre les assemblies LaunchBox 13.28.0.5, le clone contre la 14.0.1.2. À l'exécution, les deux se lient aux assemblies présents dans `Core`.
  - **Ressource** : le nom de la ressource diffère, mais LaunchBox ne la lit pas. Seul le texte compte, et il est identique.

## 4. Comportement, membre par membre

| Membre | Comparaison | Résultat |
|---|---|---|
| Constructeur | mesuré : `null` donne `ArgumentNullException` (paramètre `pluginPaths`) des deux côtés | conforme |
| `EmulatorName` | mesuré : « RetroArch » | conforme |
| `SupportsSaveManagement` | mesuré : vrai | conforme |
| `GetPotentialSaveSlots` | mesuré : même type (`Dictionary<int,string>`), même ordre (-1 « Auto », puis 0 à 9) | conforme |
| `GetImportActions` | mesuré : même conteneur à élément unique, même action booléenne « DownloadCores », même libellé, vraie par défaut | conforme |
| `GetApplicableEmulators` | mesuré : `null`, éléments nuls, chemins vides, casse de `retroarch.exe`, extension `.bak`, espace final, dossier seul. Même type de liste, même filtre ordinal sans casse, même ordre | conforme |
| `GetAllAvailableCores` | mesuré : 8 chemins (nul, vide, sans dossier, dossier absent, sous-dossier nommé `.dll`, extensions mêlées). Même tableau, même ordre | conforme |
| `GetSuggestedCores` | mesuré pour une plateforme vide ou nulle. Lu pour la recherche en base : préférence au cœur recommandé avec un tri stable, comparaison invariante sans casse, cœur extrait par le même helper LaunchBox puis rogné, tableau vide si rien | conforme |
| `GetBiosFilesForPlatform(platform)` | lu : `SystemFolder` remis à « system », même requête en base, même comparaison ordinale sans casse, même conteneur vide ou à élément unique | conforme |
| `GetBiosFilesForPlatform(app, platform, cmd)` | mesuré : 8 variantes de `retroarch.cfg` pour `system_directory` (absent, `:\`, absolu, préfixe homonyme, sans `=`, indenté, guillemets internes, vide), plus un balayage complet de 107 cœurs × 36 plateformes (3 852 cas). Même liste, mêmes fichiers, mêmes groupes, partagés par référence de la même façon, même `SystemFolder`. Le repli sur la base est lu identique | conforme |
| `GetCurrentVersion` | mesuré avec des scripts factices : sortie normale, sur stderr, courte, longueurs limites 14 et 15, double espace, casse différente, sans ligne de version, code de sortie 1, chemin nul, absent, dossier ou non exécutable. Valeurs identiques, et même exception (`ArgumentOutOfRangeException`, même message, même paramètre) pour une version trop courte | conforme |
| `GetInstallableVersions` | lu : buildbot d'abord, avec le même en-tête User-Agent. En cas d'échec de toute nature, y compris une liste vide, une version illisible ou un statut HTTP en erreur, repli sur les tags GitHub, avec les mêmes exceptions de parsing et le même `v` retiré. Même conteneur à élément unique, `null` si la version est vide | conforme |
| `InstallEmulator` | lu : même dossier cible, même version, mêmes URL, même progression (0 à 0,5 puis 0,5 à 1, même libellé), mêmes points d'annulation et réponse vide, même renommage et extraction 7-Zip, mêmes messages `AccessViolationException`, même journalisation des suppressions ratées. Même émulateur créé (champs et scripts AHK), mêmes plateformes ajoutées, même rattachement des jeux et applications à GUID vide, même plateforme supplémentaire par `ScrapeAs`. Le clone rappelle `Directory.CreateDirectory` sur le dossier déjà créé avant les cœurs : sans effet | conforme |
| `UpdateEmulator` | lu : mêmes `InvalidOperationException`, même version distante récupérée avant le test d'existence, même message « already on the latest version », même exception journalisée, puis appel virtuel de `InstallEmulator` | conforme |
| `FireEmulatorImportAction` | lu : même garde (identifiant et booléen vrai), même dédoublonnage ordinal dans l'ordre d'insertion, même `NullReferenceException` si le DataManager est nul, même requête en base, même ajout de plateforme, même test du fichier de cœur, même mise à jour des cœurs avec la même progression et la même annulation | conforme |
| `PrepareEmulatorForLaunch` | mesuré : sans identifiants, mode hardcore oui ou non, émulateur et ligne nuls, fichier verrouillé, dossier de données qui est un fichier. Mêmes octets (UTF-8 sans BOM, CRLF), même nouvelle ligne de commande, même `IOException` qui remonte, même échec rendu avec le message | conforme |
| `SupportsRetroAchievements` | mesuré : fichier absent, produit, écrit à la main avec des lignes piégées, verrouillé, ou dossier à la place du fichier. Mêmes réponses | conforme |
| `InjectRetroAchievementsCredentials` | mesuré : mode hardcore nul, vrai ou faux, valeurs nulles, caractères Unicode et guillemets, verrou, dossier de données qui est un fichier. Mêmes octets, mêmes réponses, même exception | conforme |
| `GetSaves` | mesuré : 30 scénarios. Dossiers `:\` absolus et relatifs au répertoire courant, contenu à côté du jeu, tri par contenu et par cœur (ligne du jeu, de la plateforme ou de l'émulateur, `.info` absent, court ou vide), cfg mal formé ou à double `=`, saves Saturn par cœur, par plateforme ou hors contexte, applications additionnelles et jeux masqués par une application, entrées nulles, ligne effective nulle (même `ArgumentNullException`), motif invalide. Mêmes saves, dans le même ordre, avec les mêmes champs | conforme |
| `AddSaveFile` | mesuré : 28 scénarios. Tous les messages d'erreur, états aux emplacements nul, 0, 3, 12, -1 et -5, écrasement refusé, accepté ou sans fonction, destination qui est un dossier, Saturn avec compagnons (et compagnon verrouillé), passage par une application, création du dossier trié, ligne effective nulle. Mêmes réponses et mêmes fichiers copiés | conforme |
| `IsSecondarySaveFile` | mesuré : 13 chemins | conforme |
| `GetCompanionSaveFiles` | mesuré : 12 chemins. Même type (tableau vide ou `List<string>`), même ordre, même casse des chemins rendus | conforme |
| `TryGetSaveGroupInfo` | mesuré : 12 combinaisons. Mêmes valeurs rendues et mêmes paramètres `out` | conforme |
| `UseSaveGroupIdForPersistedMatch` | mesuré : 8 valeurs | conforme |
| `TryComputeSaveSignature` | mesuré : 10 cas, dont un fichier verrouillé. Mêmes signatures MD5 | conforme |

Comparaisons de chaînes : partout les mêmes, ordinales ou sans casse selon le cas (invariantes ou ordinales), et `StartsWith` sensible à la culture aux mêmes endroits. Côté réseau : même `HttpClient` statique sans configuration, et même User-Agent « request » sur les deux pages de version, absent des téléchargements. Les seuls accès dépendant du répertoire courant (émulateur ou dossiers de saves en chemin relatif) sont les mêmes, et je les ai mesurés.

## 5. Écarts

**Aucun écart de comportement.**

- mesuré : 0
- certain : 0
- à mesurer : 0

## 6. Membres publics jugés conformes

- `RetroArchPlugin(IPluginPaths)` (constructeur)
- `EmulatorName`
- `SupportsSaveManagement()`
- `GetPotentialSaveSlots()`
- `GetImportActions()`
- `GetApplicableEmulators(IEnumerable<IEmulator>)`
- `GetAllAvailableCores(string)`
- `GetSuggestedCores(string, string)`
- `GetBiosFilesForPlatform(string)`
- `GetBiosFilesForPlatform(string, string, string)`
- `GetCurrentVersion(string)`
- `GetInstallableVersions()`
- `InstallEmulator(InstallEmulatorArgs)`
- `UpdateEmulator(InstallEmulatorArgs)`
- `FireEmulatorImportAction(ImportActionArgs)`
- `PrepareEmulatorForLaunch(PrepareForLaunchArgs)`
- `SupportsRetroAchievements(string)`
- `InjectRetroAchievementsCredentials(InjectRetroAchievementsCredentialsArgs)`
- `GetSaves(GetSavesArgs)`
- `AddSaveFile(AddSaveArgs)`
- `IsSecondarySaveFile(string)`
- `GetCompanionSaveFiles(string)`
- `TryGetSaveGroupInfo(GameSaveBase, string, out string, out string)`
- `UseSaveGroupIdForPersistedMatch(GameSaveBase)`
- `TryComputeSaveSignature(GameSaveBase, string, out string)`
- `Models.BiosFile(string, bool, string, string, EmulatorBiosGroup)` (constructeur) et `Models.BiosFile.SystemFolder`
