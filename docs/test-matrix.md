# Matrice de tests A/B (original contre clone)

Chaque cas est joué deux fois, sur l'original puis sur le clone, avec des entrées identiques et un dossier de travail
recréé à neuf. On compare la valeur rendue (type d'exécution, champs, ordre), l'exception éventuelle (type et message),
l'état du disque après l'appel (arborescence, contenus, octets) et, pour `BiosFile.SystemFolder`, la valeur statique
après l'appel. Les renvois `§` pointent vers `spec.md`.

## 0. Montage commun

- `T` = dossier de travail neuf (chemin absolu, sans espace sauf cas dédiés).
- Émulateur factice `EMU` (implémente `IEmulator`) : `Id="emu1"`, `Title="RetroArch"`,
  `ApplicationPath=T\RA\retroarch.exe`, `CommandLine=""`, une ligne de plateforme `Super Nintendo Entertainment System`
  de `CommandLine` `-L "cores\snes9x_libretro.dll"`. Le fichier `T\RA\retroarch.exe` existe (vide) sauf mention.
- `T\RA\info\snes9x_libretro.info` contient la ligne `corename = "Snes9x"` (UTF-8, CRLF), précédée d'une ligne
  `display_name = "Nintendo - SNES / SFC (Snes9x - Current)"`.
- Jeu factice `G` (implémente `IGame`) : `Id="g1"`, `ApplicationPath=T\roms\snes\Game.sfc` (fichier présent),
  `CommandLine=""`, `Platform="Super Nintendo Entertainment System"`, `EmulatorId="emu1"`,
  `GetEffectiveCommandLine()` = `-L "cores\snes9x_libretro.dll"`, `GetAllAdditionalApplications()` = tableau vide.
- `PluginHelper.DataManager` = un `IDataManager` factice qui rend `G` pour `"g1"` et `EMU` pour `"emu1"`, null sinon.
- cfg « standard » `T\RA\retroarch.cfg` (UTF-8 sans BOM, CRLF) :
  ```text
  savefile_directory = ":\saves"
  savestate_directory = ":\states"
  system_directory = ":\system"
  savefiles_in_content_dir = "false"
  savestates_in_content_dir = "false"
  sort_savefiles_enable = "false"
  sort_savestates_enable = "false"
  sort_savefiles_by_content_enable = "false"
  sort_savestates_by_content_enable = "false"
  ```
  Dossiers `T\RA\saves` et `T\RA\states` créés vides sauf mention.
- `GetSaves` est appelé avec `Emulator=EMU`, `Games=[G]`, `AdditionalApplications=null` sauf mention.
- **Répertoire courant et racine** : avant CHAQUE cas, le répertoire courant du processus est fixé à `T` et
  `NamingHelper.RootFolder` à `L`, un dossier distinct de `T` (ex. la vraie racine de LaunchBox, ou `T\..\lbroot` dans
  une sonde hors LaunchBox : la propriété a un setter public, mesuré). Les cas qui changent l'un ou l'autre le disent
  (colonne Entrée). Plusieurs résultats en dépendent (spec, Conventions).
- Cas V avec des caractères non ASCII dans la sortie de `--version` : ils n'ont de sens que si l'original et le clone
  sont joués dans le MÊME hôte (le décodage des flux redirigés dépend du processus hôte).
- Ordre du système de fichiers : les cas supposent NTFS (ordre des noms ; l'espace trie avant le point, le point avant
  les chiffres).
- Les cas `Root.DataManager` (contexte Saturn par `ScrapeAs`, installation) exigent une session LaunchBox chargée ; les
  cas LocalDb (`GamesDb`) exigent la base `Metadata` de LaunchBox lisible. Ils sont marqués `[LB]`.
- Un « résultat attendu » décrit l'original selon la spec ; la sonde vérifie surtout l'ÉGALITÉ des deux.

Total : 318 cas, dont 51 en section 13 (réseau, téléchargement, installation ou mutation des données LaunchBox), à jouer seulement avec l'accord de Mehdi.

## 1. Membres simples (17)

| Id | Entrée | Attendu |
|---|---|---|
| S01 | `new RetroArchPlugin(null)` | `ArgumentNullException`, ParamName `pluginPaths` |
| S02 | `EmulatorName` | `RetroArch` |
| S03 | `SupportsSaveManagement()` | `true` |
| S04 | `GetPotentialSaveSlots()` | 11 entrées, ordre -1..9, -1→`Auto`, n→`n` |
| S05 | `GetImportActions()` | 1 `BooleanEmulatorImportAction`, Id `DownloadCores`, Label = `Strings.LabelDownloadRetroArchCore`, `DefaultValue=true`, `Value=true` ; type `<>z__ReadOnlySingleElementList`1` ; `IList.Add`, `Clear`, `Insert`, `Remove`, `RemoveAt`, écriture par indice (générique ou non) → `NotSupportedException` « Specified method is not supported. » ; lecture `[1]` ou `[-1]` → `IndexOutOfRangeException` ; `IsReadOnly`/`IsFixedSize` vrais, `IsSynchronized` faux (mesuré) |
| S06 | `GetApplicableEmulators(null)` | liste vide |
| S07 | `GetApplicableEmulators([EMU, null, e(path=""), e("T\x\RetroArch.EXE"), e("T\x\retroarch_debug.exe"), e("retroarch.exe")])` | `[EMU, e("T\x\RetroArch.EXE"), e("retroarch.exe")]` dans cet ordre |
| S08 | `IsSecondarySaveFile` sur `null`, `""`, `a.bcr`, `a.BKR`, `a.smpc`, `a.srm`, `a.bkr.bak`, `a` | `false,false,false,true,true,false,false,false` |
| S09 | `UseSaveGroupIdForPersistedMatch` sur null, SaveGroupId null, `"  "`, `saturn`, `SATURN-x`, `xsaturn` | `false,false,false,true,true,false` |
| S10 | `GetCompanionSaveFiles` sur `null`, `""`, `T\s\a.srm`, `a.bcr` (sans dossier) | 4 × `Array.Empty` (type tableau) |
| S11 | `GetAllAvailableCores` sur `null`, `""`, `retroarch.exe` | 3 × tableau vide |
| S12 | `GetSuggestedCores("ignored", null)` et `("ignored", "  ")` | tableaux vides |
| S13 | hors LaunchBox, LocalDb non initialisée : `GetBiosFilesForPlatform("Sega CD")` | `InvalidOperationException` directe (pas d'`AggregateException`) (mesuré) |
| S14 | types : `GetApplicableEmulators(null)`, `GetPotentialSaveSlots()`, `GetCompanionSaveFiles("x.srm")`, `GetAllAvailableCores("")`, `GetSuggestedCores("","")` | `List<IEmulator>`, `Dictionary<int,string>`, `string[]` vide, `Array.Empty<string>()` (même instance), `string[]` vide (mesuré) |
| S15 | types : `GetCompanionSaveFiles` avec dossier valide sans compagnon ; `GetAllAvailableCores` sur un dossier `cores` vide ; `GetBiosFilesForPlatform` à 3 paramètres, cœur absent | `List<string>` vide ; `Array.Empty<string>()` (instance partagée, mesuré) ; `List<EmulatorBiosFile>` vide |
| S16 | `[LB]` type du repli LocalDb trouvé de `GetBiosFilesForPlatform("Sega CD")` et repli vide (`"Inconnue"`) | liste en lecture seule à un élément ; `Array.Empty<EmulatorBiosFile>()` |
| S17 | `typeof(BiosFile).Name`, `typeof(RetroArchPlugin).Name`, base, interfaces, liste des membres publics | `BiosFile` / `RetroArchPlugin`, mêmes membres publics ; l'espace de noms n'est PAS comparé (original `Unbroken.LaunchBox.Windows.RetroArch[.Models]`, clone `Nixx.RetroArch[.Models]`) |

## 2. Lecture du cfg pour les saves (via `GetSaves`) (37)

Fichier `T\RA\saves\Game.srm` présent (contenu `S`) sauf mention. Attendu par défaut : une ligne `GameSaveGame`
`T\RA\saves\Game.srm`.

| Id | Variation du cfg / du disque | Attendu |
|---|---|---|
| C01 | cfg standard | 1 save `T\RA\saves\Game.srm`, `GameId=g1`, `AdditionalApplicationId=null`, `EmulatorFileName=retroarch.exe`, `EmulatorCore=snes9x_libretro`, `SaveGroupName=My Save File`, `SaveGroupId=null`, `Title=null` |
| C02 | pas de `retroarch.cfg` | 0 ligne |
| C03 | cfg avec une ligne vide ajoutée à la fin | 0 ligne (cfg rejeté en entier) |
| C04 | cfg avec `# commentaire` en tête | 0 ligne |
| C05 | cfg avec `#include "x.cfg"` | 0 ligne |
| C06 | cfg avec `video_driver = ""` (valeur de 2 caractères) | 1 save (valeur vide acceptée) |
| C07 | cfg avec `audio_latency = 64` | 1 save (`64` → `""`) |
| C08 | cfg avec `audio_latency = 6` | 0 ligne (valeur d'1 caractère) |
| C09 | cfg avec `menu_x =` | 0 ligne |
| C10 | `savefile_directory=":\saves"` sans espaces | 1 save |
| C11 | `savefile_directory = ":\saves"` en double, la 2ᵉ vaut `":\saves2"` ; fichier mis dans `T\RA\saves2` | 1 save dans `saves2` (dernière occurrence) |
| C12 | `Savefile_Directory = ":\saves2"` en plus de la clé standard ; fichier dans `saves` | 1 save dans `saves` (casse des clés) |
| C13 | `savefile_directory` retirée | 0 ligne |
| C14 | `savefile_directory = "T\RA\saves"` en absolu | 1 save |
| C15 | `savefile_directory = "default"`, répertoire courant = `T`, `T\default\Game.srm` présent | 1 save `default\Game.srm` (chemin relatif tel quel) |
| C16 | idem C15 sans `T\default` | 0 ligne |
| C17 | EMU `ApplicationPath=RA\retroarch.exe` (relatif), répertoire courant `T`, `RootFolder=T`, cfg standard | 1 save `T\RA\saves\Game.srm` + states (cfg trouvé via le répertoire courant, `:\` résolu contre la racine) (mesuré) |
| C18 | `savefile_directory = ":\saves"` et dossier `saves` absent | 0 ligne, dossier NON créé |
| C19 | `savefiles_in_content_dir = "true"`, `T\roms\snes\Game.srm` présent | 2 fichiers (`Game.sfc`, `Game.srm`) : 1 ligne `Game.srm` |
| C20 | `savefiles_in_content_dir = "true"`, seul `Game.sfc` dans `T\roms\snes` | 1 ligne `GameSaveGame` pointant `Game.sfc` (la ROM) |
| C21 | `savefiles_in_content_dir = "TRUE"` | traité comme faux : 1 save dans `saves` |
| C22 | `savefiles_in_content_dir = true` (sans guillemets) | valeur `ru` : faux, 1 save dans `saves` |
| C23 | cfg en UTF-8 avec BOM | 1 save |
| C24 | `savefile_directory = "x=y"` | valeur `""` → 0 ligne |
| C25 | `savefile_directory = ":\..\savesUp"`, `T\savesUp\Game.srm` | 1 save `T\savesUp\Game.srm` (chemin normalisé) (mesuré) |
| C26 | `savefile_directory = ":\"`, `T\RA\Game.srm` | 1 save `T\RA\Game.srm` (mesuré) |
| C27 | `savefile_directory` = U+00AD suivi de `:\saves` | 0 save (valeur devenue `\saves`, racine du lecteur) ; states listés (mesuré) |
| C28 | EMU relatif `RA\retroarch.exe`, répertoire courant `T`, `RootFolder=L` (≠ `T`) | 0 ligne : cfg trouvé, mais `:\saves` résolu sous `L\RA` (mesuré) |
| C29 | EMU relatif, répertoire courant = un autre dossier, `RootFolder=T` | 0 ligne (cfg introuvable) (mesuré) |
| C30 | EMU relatif, répertoire courant `T`, `RootFolder=L`, cfg avec `savefile_directory` et `savestate_directory` ABSOLUS | saves et states trouvés (mesuré) |
| C31 | EMU `ApplicationPath="retroarch.exe"` (sans dossier), répertoire courant `T\RA`, `RootFolder=T\RA`, `sort_savefiles_enable="true"`, `T\RA\saves\Snes9x\Game.srm` | 1 save (cfg et `info` lus dans le répertoire courant) (mesuré) |
| C32 | C31 avec `RootFolder=T` | 0 save (`:\saves` résolu sous `T`) (mesuré) |
| C33 | `savefile_directory = ":\./a/b"`, EMU relatif `RA\retroarch.exe`, répertoire courant `T`, `RootFolder=T\LBR`, `T\LBR\RA\a\b\Game.srm` | 1 save `T\LBR\RA\a\b\Game.srm` (mesuré) |
| C34 | `savefile_directory = ":\a/b/"`, EMU absolu, `T\RA\a\b\Game.srm` | 1 save `T\RA\a\b\Game.srm` (mesuré) |
| C35 | `savefile_directory = ":\a\..\a\b\."`, EMU absolu | 1 save `T\RA\a\b\Game.srm` (mesuré) |
| C36 | `savefile_directory = ":\<L>:saves"` (`<L>` = lettre du lecteur de `T`, dans la casse du chemin de `T`), répertoire courant `T`, `T\RA\saves\Game.srm` présent, `T\saves` absent | 1 save `T\RA\saves\Game.srm` (reste `X:nom` résolu contre le dossier de l'exe) |
| C37 | exe sur un lecteur jetable (`subst X:` sur `T\x`), répertoire courant `T` (autre lecteur), `savefile_directory = ":\\saves"` (deux antislashs dans le fichier), `X:\saves\Game.srm` présent | 1 save `X:\saves\Game.srm` (racine du lecteur de l'EXE) |

## 3. Sous-dossiers de tri (18)

| Id | Variation | Attendu |
|---|---|---|
| D01 | `sort_savefiles_by_content_enable="true"`, `T\RA\saves\snes\Game.srm` | 1 save dans `saves\snes` |
| D02 | idem, fichier dans `saves` seulement | 0 ligne |
| D03 | `sort_savefiles_enable="true"`, `G.CommandLine=""`, ligne plateforme de EMU, `saves\Snes9x\Game.srm` | 1 save dans `saves\Snes9x` |
| D04 | les deux tris, `saves\snes\Snes9x\Game.srm` | 1 save (ordre contenu puis cœur) |
| D05 | les deux tris, `saves\Snes9x\snes\Game.srm` seulement | 0 ligne |
| D06 | `sort_savefiles_enable`, `G.CommandLine=-L "cores\mgba_libretro.dll"`, `info\mgba_libretro.info` (`corename = "mGBA"`), `saves\mGBA\Game.srm` | 1 save dans `saves\mGBA`, `EmulatorCore=snes9x_libretro` (le cœur affiché vient de la ligne effective) |
| D07 | `sort_savefiles_enable`, `G.Platform=""` et `G.CommandLine=""` | 0 ligne |
| D08 | `sort_savefiles_enable`, plateforme de G absente de EMU, `EMU.CommandLine=-L "cores\snes9x_libretro.dll"` | 1 save dans `saves\Snes9x` (repli sur l'émulateur) |
| D09 | `sort_savefiles_enable`, plateforme de G écrite `super nintendo entertainment system` dans EMU, `EMU.CommandLine=""` | 0 ligne (égalité ordinale) |
| D10 | `sort_savefiles_enable`, `.info` absent | 0 ligne |
| D11 | `sort_savefiles_enable`, `.info` avec `corename="Snes9x"` (sans espaces), dossier `saves\es9x` | 1 save dans `saves\es9x` |
| D12 | `sort_savefiles_enable`, `.info` avec `corename = ""` | 0 ligne |
| D13 | `sort_savefiles_enable`, `.info` avec `corename` seul | 0 ligne (ligne trop courte) |
| D14 | `sort_savestates_enable="true"`, `G.CommandLine=-L "cores\mgba_libretro.dll"`, effective = snes9x, `states\Snes9x\Game.state` | 1 state (les states suivent la ligne EFFECTIVE) |
| D15 | `sort_savestates_by_content_enable` + `sort_savestates_enable`, `states\snes\Snes9x\Game.state1` | 1 state slot 1 |
| D16 | ROM à la racine d'un lecteur virtuel (`subst` ou chemin `X:\Game.sfc`), `sort_savefiles_by_content_enable` | base inchangée (nom de dossier vide) |
| D17 | `sort_savefiles_enable`, `.info` dont la ligne est U+00AD puis `corename = "Snes9x"`, `saves\Snes9x\Game.srm` présent | 0 save (nom calculé `"Snes9x`, invalide) (mesuré) |
| D18 | `sort_savefiles_enable = "true"`, `G.CommandLine = ""`, EMU avec `GetAllEmulatorPlatforms()` = null et `CommandLine = -L "cores\snes9x_libretro.dll"`, `saves\Snes9x\Game.srm` | 1 save (repli sur la ligne de l'émulateur) |

## 4. Énumération des saves (`GetSaves`) (23)

cfg standard. Fichiers dans `T\RA\saves` sauf mention.

| Id | Fichiers | Attendu (ordre des lignes) |
|---|---|---|
| E01 | aucun | 0 ligne |
| E02 | `Game.srm` | 1 |
| E03 | `Game.sav` | 1 (`Game.sav`, extension libre quand il est seul) |
| E04 | `Game.srm`, `Game.rtc` | 1 (`Game.srm`) |
| E05 | `Game.srm`, `Game.mcr` | 2 (`.srm` puis `.mcr`) |
| E06 | `Game.mcr`, `Game.rtc` | 1 (`Game.mcr`) |
| E07 | `Game.sav`, `Game.rtc` | 2 lignes : `Game.rtc` puis `Game.sav` (ordre NTFS) |
| E08 | `Game.SRM`, `Game.rtc` | 2 (`.SRM` non reconnu) |
| E09 | `Game.srm`, `Game (Rev 1).srm` | 1 ligne : `Game (Rev 1).srm` (premier `.srm` en ordre NTFS) |
| E10 | `Game (Rev 1).srm` seul | 1 (préfixe : attrapé) |
| E11 | `Game` (sans extension) | 1 |
| E12 | `Gam.srm` | 0 |
| E13 | `game.srm` (casse différente) | 1 |
| E14 | `Game.srm` + `Game.state` dans `saves` (savestate_directory = `:\saves`) | saves : 1 (`.srm`) ; states : 1 slot 0 |
| E15 | savestate_directory = `:\saves`, seul `Game.state` | 1 save `Game.state` (fichier unique) + 1 state slot 0 |
| E16 | jeu présent deux fois dans `Games` | lignes doublées |
| E17 | `Games=[null, jeu à ApplicationPath vide, G]` | lignes de G seulement |
| E18 | `Emulator=null` | 0 ligne |
| E19 | `G.GetAllAdditionalApplications()` contient une app de même `ApplicationPath` (exact) | 0 ligne pour G |
| E20 | idem avec chemin de l'app en casse différente | lignes de G présentes |
| E21 | `args=null` | `NullReferenceException` |
| E22 | `G.GetEffectiveCommandLine()` rend null | `ArgumentNullException` |
| E23 | `G.GetAllAdditionalApplications()` rend `[null]` | `NullReferenceException` |

## 5. Énumération des savestates (15)

| Id | Fichiers dans `T\RA\states` | Attendu |
|---|---|---|
| T01 | `Game.state` | slot 0, `SaveGroupName=My Save State`, `EmulatorCore=snes9x_libretro` |
| T02 | `Game.state1` | slot 1 |
| T03 | `Game.state.auto` | slot -1 |
| T04 | `Game.state.AUTO` | slot -1 |
| T05 | `Game.STATE1` | ignoré |
| T06 | `Game.state1.png` | ignoré |
| T07 | `Game.state03` | slot 3 |
| T08 | `Game.state-2` | slot -2 |
| T09 | `Game.state.bak` | ignoré |
| T10 | `Game.state99` | slot 99 |
| T11 | `Game.stateX` | ignoré |
| T12 | `Game.state`, `Game.state1`, `Game.state.auto`, `Game.state1.png` | 3 lignes dans l'ordre NTFS : `Game.state` (0), `Game.state.auto` (-1), `Game.state1` (1) |
| T13 | `Game.state` mais `savestate_directory` absente du cfg | 0 state |
| T14 | `Game.state1` avec `savestates_in_content_dir="true"` (fichier dans `T\roms\snes`) | slot 1 |
| T15 | `Game.state.state3` | slot 3 (mesuré) |

## 6. Applications additionnelles (`GetSaves`) (8)

App factice `A` : `Id="a1"`, `GameId="g1"`, `ApplicationPath=T\roms\snes\Game (Disc 2).sfc`, `CommandLine=""`,
`GetEffectiveCommandLine()` = `-L "cores\snes9x_libretro.dll"`.

| Id | Entrée | Attendu |
|---|---|---|
| A01 | `AdditionalApplications=[A]`, `Games=null`, `saves\Game (Disc 2).srm` | 1 save, `GameId=g1`, `AdditionalApplicationId=a1` |
| A02 | `[A]` et `Games=[G]`, `Game.srm` et `Game (Disc 2).srm` | app : 1 (`Game (Disc 2).srm`) ; jeu : motif `Game*.*` → 2 fichiers → 1 ligne, le premier `.srm` dans l'ordre du système de fichiers (`Game (Disc 2).srm` sur NTFS) ; ordre : app puis jeu |
| A03 | `[null, A]` | lignes de A |
| A04 | `A.GameId` inconnu | lignes avec `GameId=null` |
| A05 | `A.CommandLine=-L "cores\mgba_libretro.dll"`, `sort_savefiles_enable`, `saves\mGBA\Game (Disc 2).srm` | 1 save (la `CommandLine` brute de l'app décide) |
| A06 | `A.GetEffectiveCommandLine()=""`, `sort_savestates_enable`, `states\Snes9x\Game (Disc 2).state` | 0 state (pas de repli vers le jeu) |
| A07 | `A.ApplicationPath=""` | ignorée |
| A08 | `[A]` avec `A.GetEffectiveCommandLine()` sans `-L` | `EmulatorCore=""` |

## 7. Saturn dans `GetSaves` (15)

Jeu `GS` : comme G mais `ApplicationPath=T\roms\saturn\Panzer.cue`, `Platform="Sega Saturn"`, effective
`-L "cores\mednafen_saturn_libretro.dll"`.

| Id | Fichiers dans `saves` | Attendu |
|---|---|---|
| R01 | `Panzer.bcr`, `Panzer.bkr`, `Panzer.smpc` | 1 ligne `Panzer.bcr`, `SaveGroupId=saturn-panzer`, `SaveGroupName=My Save File` |
| R02 | `Panzer.bkr`, `Panzer.smpc` | 1 ligne `Panzer.bkr` |
| R03 | `Panzer.smpc` | 1 ligne `Panzer.smpc` |
| R04 | `Panzer.bcr`, `Panzer.srm` | 2 lignes : groupe Saturn puis `Panzer.srm` (fichier restant unique) |
| R05 | `Panzer.bcr`, `Panzer (J).bcr` | 2 groupes (`saturn-panzer`, `saturn-panzer (j)`) |
| R06 | `PANZER.BCR`, `panzer.bkr` | 1 groupe, `FileLocation` = `PANZER.BCR`, id selon la casse du premier vu, en minuscules |
| R07 | G (plateforme SNES, cœur snes9x) avec `Game.bcr`, `Game.bkr` | hors contexte : 2 fichiers sans `.srm/.mcr` → 2 lignes ordinaires sans `SaveGroupId` |
| R08 | jeu G (SNES) avec effective `-L "cores\yabause_libretro.dll"` (nom de cœur sans `saturn`), `Game.bcr` seul | hors contexte : 1 ligne ordinaire sans `SaveGroupId` |
| R09 | `Platform="My Saturn Games"`, cœur quelconque, `x.bcr`+`x.bkr` | contexte Saturn (plateforme contient `saturn`) |
| R10 | `[LB]` plateforme locale `Consoles 32 bits` avec `ScrapeAs="Sega Saturn"` | contexte Saturn |
| R11 | `Panzer.bcr` + `Panzer.state1` dans `states` | 1 save Saturn + 1 state slot 1 sans `SaveGroupId` |
| R12 | `Panzer .bcr` (espace avant l'extension) | `SaveGroupId=saturn-panzer` (trim) |
| R13 | jeu Saturn de chemin `T\roms\sat\Stra.cue` ; `saves` : `Strax.bcr`, `Stra` U+00AD `x.bkr`, `Straİ.smpc` | 3 groupes : `saturn-strax`, `saturn-stra` U+00AD `x`, `saturn-stra` U+0130 (mesuré) |
| R14 | `saves` : `Strai.bcr`, `StraI.bkr`, `Straı.smpc` | 2 groupes : `Strai.bcr` (avec `StraI.bkr`), `Straı.smpc` à part (mesuré) |
| R15 | `[LB]` jeu de plateforme locale `Consoles` dont `ScrapeAs` est null, cœur sans `saturn`, `x.bcr`+`x.bkr` | pas de contexte Saturn, aucune exception |

## 8. `AddSaveFile` (34)

Source dans `T\src`. cfg standard, `G.EmulatorId="emu1"`.

| Id | Entrée | Attendu |
|---|---|---|
| W01 | `SaveToAdd=null` | `WasSuccess=false`, `The save file doesn't exist!` |
| W02 | `FileLocation` absent | idem |
| W03 | `GameId` inconnu | `Game doesn't exist!` |
| W04 | `AdditionalApplicationId="zz"` absent | `App doesn't exist!` |
| W05 | `G.EmulatorId=""` | `Can't get emulator record for game/app in question!` |
| W06 | `G.EmulatorId="nope"` | `Can't get emulator record game/app in question!` |
| W07 | `G.ApplicationPath=""` | `No Game/App application path found!` |
| W08 | `GameSaveGame` `T\src\x.srm` | succès, copie vers `T\RA\saves\Game.srm`, `SaveAdded` : `GameSaveGame`, `GameId=g1`, `FileLocation` = destination, `EmulatorFileName=retroarch.exe`, `EmulatorCore=snes9x_libretro`, `SaveGroupName=My Save File`, `SaveGroupId` et `Title` repris |
| W09 | W08 avec `SaveGroupName="Perso"`, `Title="T"`, `SaveGroupId="gid"`, `MatchLineageId="m"` | repris sauf `MatchLineageId` (null) |
| W10 | `GameSaveGame` `T\src\x` sans extension | destination `T\RA\saves\Game` |
| W11 | `GameSaveState` slot null | destination `T\RA\states\Game.state`, `Slot=null`, `SaveGroupName=My Save State` |
| W12 | slot 0 | `Game.state`, `Slot=0` |
| W13 | slot 4 | `Game.state4` |
| W14 | slot -1 | `Game.state.auto` |
| W15 | slot -7 | `Game.state.auto`, `Slot=-7` |
| W16 | destination existe, `ShouldOverwriteFunc=null` | `Operation cancelled by user!`, destination inchangée |
| W17 | destination existe, func → false | idem |
| W18 | destination existe, func → true | succès, écrasée |
| W19 | dossier `saves` absent | créé, succès |
| W20 | `savefile_directory` absente | `Couldn't determine where to place this save!` |
| W21 | `sort_savefiles_enable`, `.info` absent | même message ; aucun dossier créé |
| W22 | source = destination (même fichier), func → true | `Could not copy file to destination!` |
| W23 | destination en lecture seule, func → true | `Could not copy file to destination!` |
| W24 | application `A` (EmulatorId `emu1`), `AdditionalApplicationId=a1` | destination `T\RA\saves\Game (Disc 2).srm`, `AdditionalApplicationId=a1` |
| W25 | sort par contenu actif, dossier `saves\snes` absent | `saves\snes` créé |
| W26 | `args=null` | `NullReferenceException` |
| W27 | app `A` (`EmulatorId=emu1`, chemin `T\roms\snes\Game2.sfc`, ligne effective null) ; `GameSaveState` slot 1, `AdditionalApplicationId=a1` | succès, `T\RA\states\Game2.state1`, `EmulatorCore=snes9x_libretro` (repli sur la ligne du jeu) (mesuré) |
| W27b | W27 avec `sort_savestates_enable = "true"` (dossier `T\RA\states\Snes9x` absent) | succès, destination `T\RA\states\Snes9x\Game2.state1`, dossier `Snes9x` créé (repli sur la ligne effective du jeu) |
| W28 | W27 avec en plus la ligne effective du jeu null | `ArgumentNullException` ; `T\RA\states` créé s'il manquait |
| W29 | `AdditionalApplicationId="a1"`, `G.GetAllAdditionalApplications()` rend null | `App doesn't exist!` |
| W30 | `G.GetAllAdditionalApplications()` rend `[null, A]` | `NullReferenceException` |
| W31 | app `A` à ligne effective `""`, `sort_savestates_enable="true"`, `GameSaveState` | `Couldn't determine where to place this save!` (pas de repli) |
| W32 | cfg de C36, `AddSaveFile` d'un `GameSaveGame` `T\src\x.srm` | écrit `T\RA\saves\Game.srm` ; `T\saves` n'est PAS créé |
| W33 | montage de D18, `AddSaveFile` d'un `GameSaveGame` `T\src\x.srm` | écrit `T\RA\saves\Snes9x\Game.srm` |

## 9. `AddSaveFile` en contexte Saturn (11)

Jeu `GS` (§ 7) enregistré dans le DataManager factice sous `"gs"`.

| Id | Entrée | Attendu |
|---|---|---|
| WS1 | `GameSaveGame` `T\src\Old.bcr`, `T\src\Old.bkr`, `T\src\Old.smpc` présents | `saves\Panzer.bcr/.bkr/.smpc` écrits, `SaveGroupId=saturn-panzer` |
| WS2 | source `T\src\Old.bkr` (+ `.bcr`) | `saves\Panzer.bkr` + `saves\Panzer.bcr` |
| WS3 | source `T\src\Old.bcr` seul | `saves\Panzer.bcr` seul |
| WS4 | `saves\Panzer.bkr` déjà présent, source `.bcr`+`.bkr`, destination `.bcr` absente | `.bkr` écrasé sans demande |
| WS5 | `GameSaveState` slot 2 `T\src\s.state2` avec `T\src\s.bcr` voisin | `states\Panzer.state2` ET `states\Panzer.bcr` ; `SaveGroupId=saturn-panzer` |
| WS6 | `GameSaveState` slot -1, pas de voisins | `states\Panzer.state.auto`, `SaveGroupId=saturn-panzer.state` |
| WS7 | source `T\src\Old.BCR` | pas de recopie du primaire comme compagnon |
| WS8 | jeu SNES G, source `x.bcr` | hors contexte : simple copie `saves\Game.bcr`, `SaveGroupId` repris de la source |
| WS9 | jeu `GS`, répertoire courant `T\src`, source donnée comme `Old.bcr` (sans dossier) avec `Old.bkr` voisin | seul `saves\Panzer.bcr` écrit (pas de recopie des compagnons) |
| WS10 | jeu `GS` ; `T\src\.bcr` (octet `01`), `T\src\.bkr` (octet `02`), `T\src.bkr` (octet `03`, dans `T`) ; `AddSaveFile(GameSaveGame FileLocation=T\src\.bcr, GameId=gs)` | `saves\Panzer.bcr` = `01`, `saves\Panzer.bkr` = `03` (compagnons cherchés à côté du dossier source) |
| WS11 | jeu Saturn de contenu `T\roms\saturn\.cue` ; sources `T\src\Old.bcr`, `T\src\Old.bkr` | `T\RA\saves\.bcr` et `T\RA\saves.bkr` (à côté du dossier) ; `SaveGroupId = saturn` |

## 10. Compagnons, groupes, signatures (21)

| Id | Entrée | Attendu |
|---|---|---|
| X01 | `GetCompanionSaveFiles(T\s\a.bcr)`, présents `a.bcr`, `a.bkr`, `a.smpc` | `[T\s\a.bkr, T\s\a.smpc]` (`List`) |
| X02 | `T\s\a.bkr`, présents `a.bcr`, `a.bkr` | `[T\s\a.bcr]` |
| X03 | `T\s\a.BKR`, présents `a.bcr`, `a.bkr` | `[T\s\a.bcr]` |
| X04 | `T\s\a.bcr`, aucun autre | `List` vide |
| X05 | `T\s\a.bcr` absent, `a.bkr` présent | `[T\s\a.bkr]` |
| X06 | `TryGetSaveGroupInfo(null, …)` | `false`, sorties null |
| X07 | save `FileLocation=T\s\Panzer.bkr`, primaire `""` | `true`, `saturn-panzer`, `Panzer` |
| X08 | save quelconque, primaire `T\s\ Zz .bcr` | `true`, `saturn-zz`, ` Zz ` |
| X09 | `Game.srm`, `EmulatorCore=mednafen_saturn_libretro` | `true`, `saturn-game`, `Game` |
| X10 | `Game.state.auto`, cœur Saturn | `true`, `saturn-game.state`, `Game.state` |
| X11 | `Game.srm`, cœur snes9x | `false` |
| X12 | `TryComputeSaveSignature`, `a.bcr` (octets `01`), `a.bkr` (octets `02 03`) | `true`, signature = MD5 hexa majuscule du texte `a.bcr|<md5>\r\na.bkr|<md5>\r\n` |
| X13 | idem avec `A.BCR` (nom en majuscules sur disque) | noms mis en minuscules dans le texte |
| X14 | `FileLocation` absent | `false`, null |
| X15 | `Game.srm` + cœur Saturn, aucun `.bcr/.bkr/.smpc` | `false` |
| X16 | `Game.srm` + cœur Saturn + `Game.bkr` présent | `true`, signature du seul `.bkr` |
| X17 | `a.bcr` ouvert par un autre processus en écriture partagée | `true` |
| X18 | `a.bkr` ouvert en exclusif par un autre processus | `false`, null |
| X19 | `GetCompanionSaveFiles("T/s/a.bcr")` (barres obliques), présents `a.bcr`, `a.bkr` | `[T\s\a.bcr, T\s\a.bkr]` : le primaire figure parmi ses compagnons (mesuré) |
| X20 | `GetCompanionSaveFiles("T\s\.\a.bcr")`, présents `a.bcr`, `a.bkr` | `[T\s\.\a.bkr]` (mesuré) |
| X21 | `GetCompanionSaveFiles("X:\a.bcr")` sur le lecteur jetable de B26, `X:\a.bkr` présent | `[X:\a.bkr]` |

## 11. BIOS (30)

`BiosFile.SystemFolder` est remis à `"system"` avant chaque cas, puis lu après.

| Id | Entrée | Attendu |
|---|---|---|
| B01 | `GetBiosFilesForPlatform("Sega CD")` `[LB]` | `SystemFolder="system"` ; 0 ou 1 `BiosFile` selon `RequiredBiosFile` de la LocalDb, `Required=true`, `Location=system` |
| B02 | `GetBiosFilesForPlatform("Plateforme inconnue")` `[LB]` | `Array.Empty` |
| B03 | 3 param., `T\RA\retroarch.exe`, `Panasonic 3DO`, `-L "cores\4do_libretro.dll" "%romfile%"` | 11 lignes de la table dans l'ordre, groupes `0` (`System BIOS`, requis, Any) et `1` (polices, non requis, None), `Location` = `system` (cfg standard `:\system`) |
| B04 | idem, plateforme `panasonic 3do` | même résultat (sans casse) |
| B05 | idem, plateforme `Sega Saturn` | liste vide (pas de repli) |
| B06 | `-L "cores\mednafen_saturn_libretro.dll"`, `Sega Saturn` | 4 lignes, md5 tous renseignés ; `sega_101.bin` et `mpr-17933.bin` requis, groupe `0` « Regional BIOS » (`IsGroupRequired=true`, `AllItemsRequired=false`, même objet) ; les 2 cartouches non requises, sans groupe (mesuré) |
| B07 | `-L "cores\ep128emu_core_libretro.dll"`, plateforme `x` | 22 lignes ; `exos20.rom` porte le groupe `2` de description `Enterprise 128 Expandible OS` |
| B08 | `-L "cores\puae_libretro.dll"`, `Commodore Amiga CD32` | 2 lignes, groupe `0` `Commodore Amiga CD32 BIOS`, `AllItemsRequired=true` |
| B09 | `-L "cores\puae_libretro.dll"`, `Commodore CDTV` | 1 ligne `kick34005.CDTV`, `Required=true`, sans groupe |
| B10 | `-L "cores\CoreName.dll"`, plateforme `Platform` | 1 BIOS `BiosPath`, desc `BiosDesc`, md5 `MD5`, `Required=false`, groupe `GroupId`/`GroupDesc`, `IsGroupRequired=false`, `AllItemsRequired=false` |
| B11 | `-L "cores\inexistant_libretro.dll"` | liste vide |
| B12 | ligne `-L "Cores\4do_libretro.dll"` `[LB]` | repli LocalDb (format non reconnu) |
| B13 | `commandLine=""` `[LB]` | repli LocalDb |
| B14 | `emulatorApplicationPath=""`, ligne valide `[LB]` | `SystemFolder="system"`, repli LocalDb |
| B15 | cfg `system_directory = "D:\Bios"` | `SystemFolder="D:\Bios"`, `Location` des lignes = `D:\Bios` |
| B16 | cfg `system_directory = "default"` | `SystemFolder="default"` |
| B17 | cfg sans `system_directory` | `"system"` |
| B18 | cfg avec `system_directory_extra = "Z"` AVANT `system_directory` | `SystemFolder="Z"` |
| B19 | cfg ouvert en exclusif par un autre processus | exception d'E/S qui remonte |
| B20 | `-L "cores\a.b.dll" x.dll"` (deux `.dll"`) | nom capturé jusqu'au dernier `.dll"` ; liste vide |
| B21 | cfg : U+00AD puis `system_directory = "D:\Bios"`, ligne `4do`, plateforme `x` | liste vide, `SystemFolder="D:\Bios"` (mesuré) |
| B22 | cfg `system_directory` = U+00AD suivi de `:\sys` | `SystemFolder="\sys"` (mesuré) |
| B23 | exe RELATIF `RA\retroarch.exe`, répertoire courant `T`, cfg `system_directory = "REL"`, ligne `-L "cores\inexistant.dll"` | liste vide, `SystemFolder="REL"` (mesuré) |
| B24 | B23 avec un autre répertoire courant | `SystemFolder="system"` (mesuré) |
| B25 | `[LB]` enchaînement sans remise à zéro : B15 (`D:\Bios`) puis `GetBiosFilesForPlatform("Sega CD")` | `SystemFolder` vaut `D:\Bios` après le 1er appel, `system` après le 2nd |
| B26 | exe à la racine d'un lecteur jetable (`subst X:` sur un dossier de `T`), `X:\retroarch.cfg` avec `system_directory = "Z"` | `SystemFolder="Z"` (fichier `X:\retroarch.cfg`) |
| B27 | `[LB]` base LocalDb de test où l'entrée RetroArch a une plateforme à `Platform` null avant `Sega CD` : `GetBiosFilesForPlatform("Sega CD")` | aucune exception, entrée nulle ignorée |
| BF1 | `SystemFolder="X1"`, `new BiosFile("f.bin", true, "d", "m", null)`, puis `SystemFolder="X2"` | objet : `Location=X1`, `FileName=f.bin`, `Required=true`, `Description=d`, `Md5=m`, groupe null (mesuré) |
| BF2 | `SystemFolder="abc"` puis lecture ; `SystemFolder=null` puis lecture | `abc` ; null |
| BF3 | `new BiosFile(null, false)` | aucune exception, `FileName=null`, `Location` = `SystemFolder` courant |

## 12. Version, cœurs, RetroAchievements, divers hors réseau (38)

Pour les cas V, `T\fake\retroarch.exe` est un petit exécutable de test qui écrit sur stdout les lignes données.

| Id | Entrée | Attendu |
|---|---|---|
| V01 | `GetCurrentVersion(null)`, `("")`, chemin absent | `null` × 3 |
| V02 | sortie `Version: 1.19.1 (Git abc)` | `1.19.1` |
| V03 | sortie `Version: 1.9.0 (Git abc)` | `1.9.0 ` (espace final) |
| V04 | sortie `Version: 1.9.0` | `ArgumentOutOfRangeException`, `ParamName=startIndex`, message « startIndex cannot be larger than length of string. (Parameter 'startIndex') » (mesuré) |
| V05 | sortie `Version: 1.10.10` | `1.10.1` |
| V06 | sortie sur stderr `Version: 1.20.0` | `1.20.0` |
| V07 | sortie `version: 1.19.1` | `null` |
| V08 | fichier non exécutable (texte renommé `.exe`) | `null` |
| V09 | sortie `Version: 1.2.34` (15 caractères) | `1.2.34` (mesuré) |
| V10 | sortie U+0000 puis `Version: 1.2.3 (x)` | ` 1.2.3 ` (mesuré) |
| V11 | sortie `RetroArch`, `Version: 1.20.0 (Git 1)`, `foo` | `1.20.0` (mesuré) |
| V12 | sortie `Version: 1.11.1 a` puis `Version: 1.22.2 b` | informatif : original mesuré `1.22.2` ; non garanti, égalité non exigée |
| K01 | `GetAllAvailableCores(T\RA\retroarch.exe)`, `T\RA\cores` absent | vide |
| K02 | `cores\a_libretro.dll`, `cores\b_libretro.dll`, `cores\c.txt`, `cores\d.dll_old`, `cores\e.dllx` | `a_libretro`, `b_libretro` seulement (mesuré) |
| K03 | `GetSuggestedCores(x, "Super Nintendo Entertainment System")` `[LB]` | 1 nom, celui de l'entrée recommandée de la LocalDb |
| K04 | `GetSuggestedCores(x, "Plateforme inconnue")` `[LB]` | vide |
| K05 | `IsPlatformSupported("Sony Playstation")` `[LB]` | `(true, Recommended)` selon LocalDb |
| K06 | `IsPlatformSupported("sony playstation")` `[LB]` | même résultat |
| K07 | `IsPlatformSupported("Inconnue")`, `(null)` `[LB]` | `(false,false)` × 2 |
| K08 | `GetAllAvailableCores("RA\retroarch.exe")` relatif, répertoire courant `T`, cœurs dans `T\RA\cores`, `RootFolder=L` | tableau vide (mesuré : résolu contre la racine) |
| K09 | K08 avec `RootFolder=T` | `a_libretro`, `b_libretro` |
| K10 | `[LB]` base LocalDb de test avec une plateforme RetroArch à `Platform` null : `IsPlatformSupported(null)` | `NullReferenceException` |
| Q01 | `SupportsRetroAchievements("x")`, `DataDirectory\RetroAchievements.cfg` absent | `(true,false,false,null,null)` |
| Q02 | fichier = sortie de `InjectRetroAchievementsCredentials(user "u", token "t", hardcore false)` | `(true,true,false,"u","t")` ; octets du fichier identiques entre original et clone (UTF-8 sans BOM, CRLF, 4 lignes) |
| Q03 | Inject avec `hardcore=null`, `enable=false` | fichier `cheevos_enable = "true"`, hardcore `"true"` |
| Q04 | fichier écrit à la main, octets exacts (antislash littéral dans le fichier) : `cheevos_enable = "untrue"` puis `cheevos_token = "a=b\"c"` | activé `true`, jeton `ab\c` (mesuré) |
| Q05 | fichier verrouillé en exclusif | `(true,false,false,null,null)` |
| Q06 | `PrepareEmulatorForLaunch` sans identifiants | succès, `NewCommandLine=null`, aucun fichier |
| Q07 | Prepare avec identifiants `u`/`t`, `EMU.EnableHardcoreAchievements=true`, ligne `-L "cores\x.dll" "rom"` | fichier 4 lignes (hardcore `true`) ; `NewCommandLine` = ligne + ` --appendconfig "<DataDirectory>\RetroAchievements.cfg"` |
| Q08 | Prepare, `DataDirectory` = dossier inexistant en lecture seule (parent verrouillé) | exception de création de dossier qui remonte |
| Q09 | fichier : U+00AD puis `cheevos_enable = "true"` | activé `true` (mesuré) |
| Q10 | `InjectRetroAchievementsCredentials(null)` | `(false, "Object reference not set to an instance of an object.")` ; dossier `data` créé, aucun fichier (mesuré) |
| Q11 | Inject avec `username=null`, `token=null` | fichier : `cheevos_token = ""`, `cheevos_username = ""`, hardcore `"true"` (mesuré) |
| Q12 | `PrepareEmulatorForLaunch(null)` | `NullReferenceException` |
| Q13 | `IPluginPaths.DataDirectory=null`, `SupportsRetroAchievements("x")` | `ArgumentNullException` |
| Q14 | fichier : U+00AD puis `cheevos_token = "t"` | jeton = U+00AD + deux espaces + `t` |
| N44 | hors LaunchBox : `PluginHelper.DataManager = null`, LocalDb non initialisée, action `DownloadCores` vraie, `GamesImported = [G]` | `NullReferenceException` (avant la LocalDb) |
| N44b | `[LB]` LocalDb présente, `PluginHelper.DataManager = null`, `GamesImported = []` | `NullReferenceException` |

## 13. Réseau, téléchargement, installation (51) — SEULEMENT AVEC L'ACCORD DE MEHDI

Pour ces cas, on préfère un faux serveur HTTP local derrière un proxy système ou une redirection DNS de test plutôt que
le vrai buildbot ; si l'on touche le vrai serveur, c'est avec l'accord explicite de Mehdi, et jamais dans une vraie
installation LaunchBox (copie jetable seulement).

| Id | Entrée | Attendu |
|---|---|---|
| N01 | `GetInstallableVersions()`, buildbot répond une liste valide | 1 version = le plus grand numéro ; `Identifier=Label` |
| N02 | buildbot répond une liste contenant `nightly/` | GitHub interrogé ; version = premier tag sans `v` |
| N03 | buildbot 500, GitHub OK | version GitHub |
| N04 | buildbot 500, GitHub 500 | `HttpRequestException` |
| N05 | buildbot vide, GitHub `[]` | `InvalidOperationException(ErrorParseRetroArchWebsite)` |
| N06 | GitHub `[{"nom":"x"}]` après échec buildbot | exception du lieur dynamique |
| N07 | en-têtes envoyés au buildbot et à GitHub | `User-Agent: request`, rien d'autre ajouté |
| N08 | en-têtes envoyés pour les `.7z` | aucun `User-Agent` |
| N09 | `IsUpdateAvailable(EMU)` (hérité), version locale égale | `false` |
| N10 | `InstallEmulator` neuf (ExistingEmulator null, Platform `Super Nintendo Entertainment System`) `[LB]` | dossier `<racine>\Emulators\RetroArch` peuplé, nouvel `Emulator` avec les champs de § 2.13, lignes LocalDb |
| N11 | `InstallEmulator` avec émulateur existant dans `T\RetroArch-Win64` | pas de renommage d'archive ; `ApplicationPath` relatif réécrit ; même objet rendu |
| N12 | émulateur existant dans `T\MonRA` | archive renommée, fichiers dans `T\MonRA` |
| N13 | annulation pendant le téléchargement du programme | `WasSuccess=false`, `Message=""`, fichier temporaire supprimé |
| N14 | annulation pendant les cœurs | idem, programme déjà installé laissé en place |
| N15 | version inexistante (`Version="0.0.1"`, 404), `L\Metadata` absent avant l'appel | `InvalidOperationException(ErrorUnableToDownloadRetroArch)` ; aucun fichier temporaire, mais `L\Metadata\Temp` CRÉÉ (vide) |
| N16 | archive corrompue | `AccessViolationException(ErrorRetroArchArchive)` |
| N17 | archive des cœurs corrompue | `AccessViolationException(ErrorCouldNotExtractRetroArchCores)` |
| N18 | `UpdateEmulator` sans `ApplicationPath` | `InvalidOperationException(ErrorNoRetroArchAppPath)` (avant tout réseau) |
| N19 | `UpdateEmulator`, version locale = dernière | `(false, "You are already on the latest version of RetroArch!")` |
| N20 | `FireEmulatorImportAction`, action `DownloadCores` à `Value=false` | `(true)`, aucun réseau (cas sans réseau, rangé ici pour la LocalDb) |
| N21 | `FireEmulatorImportAction` `DownloadCores` vrai, plateforme absente de l'émulateur, dossier RA existant `[LB]` | plateforme ajoutée puis `ArgumentNullException` |
| N22 | `FireEmulatorImportAction` `DownloadCores` vrai, cœur `cores\snes9x_libretro.dll` absent `[LB]` | archive des cœurs téléchargée et extraite dans le dossier RA, progression `LabelDownloadingMissingCores` |
| N23 | délai HTTP dépassé avant les en-têtes de `RetroArch.7z`, `ShouldCancelFunc` rend faux, dossier cible `T\MonRA` | `AccessViolationException(ErrorRetroArchArchive)`, aucun fichier temporaire, `L\Metadata\Temp` présent |
| N24 | buildbot : liens valides puis un dernier lien tronqué sans `/"` | le plus grand des numéros valides, GitHub non interrogé |
| N25 | buildbot en échec, GitHub `[{"name":"V1.2.3"}]` | `V1.2.3` |
| N26 | GitHub `[{"name":null}]` | `NullReferenceException` |
| N27 | GitHub `[{"name":123}]` | `RuntimeBinderException` |
| N28 | GitHub `[{"name":{}}]` | `NullReferenceException` |
| N29 | GitHub `[5]` | `RuntimeBinderException` |
| N30 | GitHub `{}` (pas un tableau) | `JsonReaderException` |
| N36 | buildbot en échec, GitHub `[[1]]` | `RuntimeBinderException` (mesuré avec Newtonsoft) |
| N37 | `RootFolder` terminé par `\`, `InstallEmulator` sans émulateur existant | dossier `<racine>Emulators\RetroArch` (un seul séparateur) |
| N38 | `FireEmulatorImportAction` : émulateur 1 au cœur manquant, émulateur 2 dont une plateforme vient d'être ajoutée | téléchargement et extraction pour l'émulateur 1, puis `ArgumentNullException` pour le 2 |
| N39 | `FireEmulatorImportAction` : un émulateur, plateforme A au cœur manquant puis plateforme B à ajouter | `ArgumentNullException`, AUCUN téléchargement, ligne B ajoutée |
| N40 | `[LB]` LocalDb sans entrée RetroArch, `InstallEmulator` neuf avec `Platform="Sega CD"` | aucune exception ; une seule ligne `Sega CD`, ligne de commande `""` |
| N41 | `[LB]` LocalDb de test : entrée RetroArch avec `Platforms` null ; action `DownloadCores` vraie, jeu importé sur une plateforme absente de l'émulateur, ligne existante `Super Nintendo Entertainment System` dont le cœur existe | `(true)`, aucune exception, aucune ligne ajoutée, aucun téléchargement |
| N42 | `RootFolder = L` avec `L\Metadata` ABSENT ; `InstallEmulator` (existant `T\MonRA\retroarch.exe`, `Version="0.0.1"`), faux serveur 404 | `InvalidOperationException(ErrorUnableToDownloadRetroArch)` ET `L\Metadata\Temp` présent, vide |
| N43 | `L\Metadata` est un FICHIER ; `InstallEmulator` (existant `T\MonRA\retroarch.exe`, `Version="1.19.1"`), faux serveur qui journalise | l'`IOException` de création du dossier, non enveloppée (type exact à relever) ; AUCUNE requête reçue |
| N43b | `RootFolder = null`, même appel | `ArgumentNullException`, ParamName `path1` ; aucune requête |
| N45 | classe dérivée qui surcharge `InstallEmulator` pour rendre `new EmulatorInstallResponse("marqueur")` ; `UpdateEmulator` avec un exe existant absent ; faux serveur pour la dernière version | `(false, "marqueur")`, aucun téléchargement (appel virtuel) |
| N46 | `[LB]` LocalDb de test avec une entrée RetroArch `Platform = null` ; plateforme locale `Consoles` (`ScrapeAs` null) portant un jeu à `EmulatorId` = GUID nul ; `InstallEmulator` neuf | jeu inchangé (ou exception commune si `GetDefaultEmulatorExistsForPlatform("RetroArch", null)` lève : à relever) |
| N47 | `[LB]` répertoire courant `T` sur le lecteur `X`, `T\cores\snes9x_libretro.dll` présent, `X:\cores\snes9x_libretro.dll` absent, émulateur `X:retroarch.exe` avec la ligne SNES existante | `(true)`, aucun téléchargement |
| N48 | `InstallEmulator`, émulateur existant `T\Emu\RA\ \retroarch.exe` (dernier segment = une espace), faux serveur | contenu des archives extrait dans `T\Emu` (parent normalisé), dossier racine renommé en ` ` |
| N48b | `UpdateEmulator`, émulateur existant `T\Emu\RA\...\retroarch.exe`, exe absent, faux serveur | même chose : extraction dans `T\Emu` |
| N48c | `FireEmulatorImportAction` `DownloadCores`, émulateur `T\Emu\RA\...\retroarch.exe` à cœur manquant, faux serveur | archive des cœurs extraite dans `T\Emu` |
| N49 | pendant le téléchargement de `RetroArch.7z` (faux serveur lent), un autre processus ouvre le fichier `<racine>\Metadata\Temp\<GUID>.7z` en lecture avec partage lecture+écriture | échec de l'ouverture (violation de partage) tant que le téléchargement dure |
| N31 | `ShouldCancelFunc` devient vrai juste après le dernier bloc de `RetroArch.7z` | `(false, "")`, archive complète laissée dans `<racine>\Metadata\Temp` |
| N32 | action d'import `DownloadCores` vraie, cœur manquant, `ImportActionArgs` construit avec une fonction d'annulation null | `InvalidOperationException(ErrorUnableToDownloadRetroArch)` au premier bloc ; fichier temporaire VIDE laissé dans `<racine>\Metadata\Temp` |
| N33 | idem avec un rappel de progression null | même exception ; fichier temporaire laissé, contenant le premier bloc |
| N34 | émulateur existant `ApplicationPath="retroarch.exe"` (sans dossier) | téléchargement puis `ArgumentException`, archive laissée |
| N35 | émulateur existant à la racine d'un lecteur (`X:\retroarch.exe`, lecteur jetable) | à mesurer (destination d'extraction null) |
