# Sonde A/B : original contre clone (greffon RetroArch)

Rapport produit par `tests\AbProbe` ; ne pas l'editer a la main.

- Rejouer depuis la racine du depot : `dotnet run -c Release --project tests\AbProbe` (construit aussi le clone).
- Original : `G:\LB1326\System\Plugins\RetroArch LaunchBox Integration\Unbroken.LaunchBox.Windows.RetroArch.dll` (lecture seule).
- Clone : `src\Nixx.RetroArch\bin\<config>\net10.0-windows\Nixx.RetroArch.dll`.
- Juge : egalite stricte des deux greffons (valeur rendue en profondeur, exception type + message, arbre du
  dossier de cas avec tailles et hash, `BiosFile.SystemFolder` apres l'appel). `<W>` = dossier de cas jetable.
- Garde reseau : OK : requete bloquee avant connexion. Toute requete HTTP est interceptee avant connexion (proxy de secours : port ferme local).

## Resume

- Cas joues : 376 (256 de la matrice, 120 de fuzzing).
- VERTS : 376. ROUGES : 0. JAUNES (informatif, egalite non exigee) : 0. Erreurs de sonde : 0.
- Non joues : 59 (liste en fin de rapport).

## 1. Membres simples

| Cas | Verdict | Entree |
|---|---|---|
| S01 | VERT | new RetroArchPlugin(null) |
| S02 | VERT | EmulatorName |
| S03 | VERT | SupportsSaveManagement() |
| S04 | VERT | GetPotentialSaveSlots() |
| S05 | VERT | GetImportActions() puis Add, Clear, Insert, Remove, RemoveAt, ecriture et lecture par indice (IList et IList<T>), IsReadOnly, IsFixedSize, IsSynchronized, seconde instance |
| S06 | VERT | GetApplicableEmulators(null) |
| S07 | VERT | GetApplicableEmulators([EMU, null, e(""), e(T\x\RetroArch.EXE), e(T\x\retroarch_debug.exe), e(retroarch.exe)]) |
| S08 | VERT | IsSecondarySaveFile sur null, "", a.bcr, a.BKR, a.smpc, a.srm, a.bkr.bak, a |
| S09 | VERT | UseSaveGroupIdForPersistedMatch sur null, null, "  ", saturn, SATURN-x, xsaturn |
| S10 | VERT | GetCompanionSaveFiles sur null, "", T\s\a.srm, a.bcr |
| S11 | VERT | GetAllAvailableCores sur null, "", retroarch.exe |
| S12 | VERT | GetSuggestedCores("ignored", null) et ("ignored", "  ") |
| S13 | VERT | hors LaunchBox, LocalDb non initialisee : GetBiosFilesForPlatform("Sega CD") |
| S14 | VERT | types : GetApplicableEmulators(null), GetPotentialSaveSlots(), GetCompanionSaveFiles("x.srm"), GetAllAvailableCores(""), GetSuggestedCores("","") |
| S15 | VERT | types : GetCompanionSaveFiles avec dossier valide sans compagnon ; GetAllAvailableCores sur cores vide ; BIOS 3 param., coeur absent |

## 2. Lecture du cfg pour les saves

| Cas | Verdict | Entree |
|---|---|---|
| C01 | VERT | cfg standard |
| C02 | VERT | pas de retroarch.cfg |
| C03 | VERT | cfg + ligne vide a la fin |
| C04 | VERT | cfg avec # commentaire en tete |
| C05 | VERT | cfg avec #include "x.cfg" |
| C06 | VERT | cfg avec video_driver = "" |
| C07 | VERT | cfg avec audio_latency = 64 |
| C08 | VERT | cfg avec audio_latency = 6 |
| C09 | VERT | cfg avec menu_x = |
| C10 | VERT | savefile_directory=":\saves" sans espaces |
| C11 | VERT | savefile_directory en double, la 2e vaut ":\saves2", fichier dans saves2 |
| C12 | VERT | Savefile_Directory = ":\saves2" en plus, fichier dans saves |
| C13 | VERT | savefile_directory retiree |
| C14 | VERT | savefile_directory absolu T\RA\saves |
| C15 | VERT | savefile_directory = "default", cwd T, T\default\Game.srm |
| C16 | VERT | savefile_directory = "default", sans T\default |
| C17 | VERT | EMU relatif RA\retroarch.exe, cwd T, RootFolder=T |
| C18 | VERT | dossier saves absent |
| C19 | VERT | savefiles_in_content_dir = "true", T\roms\snes\Game.srm |
| C20 | VERT | savefiles_in_content_dir = "true", seul Game.sfc |
| C21 | VERT | savefiles_in_content_dir = "TRUE" |
| C22 | VERT | savefiles_in_content_dir = true (sans guillemets) |
| C23 | VERT | cfg UTF-8 avec BOM |
| C24 | VERT | savefile_directory = "x=y" |
| C25 | VERT | savefile_directory = ":\..\savesUp", T\savesUp\Game.srm |
| C26 | VERT | savefile_directory = ":\", T\RA\Game.srm |
| C27 | VERT | savefile_directory = U+00AD puis :\saves |
| C28 | VERT | EMU relatif, cwd T, RootFolder=L |
| C29 | VERT | EMU relatif, cwd = autre dossier, RootFolder=T |
| C30 | VERT | EMU relatif, cwd T, RootFolder=L, savefile/savestate absolus |
| C31 | VERT | EMU retroarch.exe sans dossier, cwd T\RA, RootFolder=T\RA, sort_savefiles_enable, saves\Snes9x\Game.srm |
| C32 | VERT | C31 avec RootFolder=T |
| C33 | VERT | savefile_directory = ":\./a/b", EMU RA\retroarch.exe, cwd T, RootFolder=T\LBR, T\LBR\RA\a\b\Game.srm |
| C34 | VERT | savefile_directory = ":\a/b/", EMU absolu, T\RA\a\b\Game.srm |
| C35 | VERT | savefile_directory = ":\a\..\a\b\.", EMU absolu, T\RA\a\b\Game.srm |

## 3. Sous-dossiers de tri

| Cas | Verdict | Entree |
|---|---|---|
| D01 | VERT | sort_savefiles_by_content_enable, saves\snes\Game.srm |
| D02 | VERT | sort par contenu, fichier dans saves seulement |
| D03 | VERT | sort_savefiles_enable, saves\Snes9x\Game.srm |
| D04 | VERT | les deux tris, saves\snes\Snes9x\Game.srm |
| D05 | VERT | les deux tris, saves\Snes9x\snes\Game.srm seulement |
| D06 | VERT | sort_savefiles_enable, G.CommandLine = mgba, saves\mGBA\Game.srm |
| D07 | VERT | sort_savefiles_enable, G.Platform="" et G.CommandLine="" |
| D08 | VERT | sort_savefiles_enable, plateforme de G absente de EMU, EMU.CommandLine = snes9x |
| D09 | VERT | sort_savefiles_enable, plateforme ecrite en minuscules dans EMU, EMU.CommandLine="" |
| D10 | VERT | sort_savefiles_enable, .info absent |
| D11 | VERT | sort_savefiles_enable, corename="Snes9x" sans espaces, saves\es9x |
| D12 | VERT | sort_savefiles_enable, corename = "" |
| D13 | VERT | sort_savefiles_enable, ligne corename seule |
| D14 | VERT | sort_savestates_enable, G.CommandLine = mgba, effective snes9x, states\Snes9x\Game.state |
| D15 | VERT | deux tris des states, states\snes\Snes9x\Game.state1 |
| D16 | VERT | ROM a la racine d'un lecteur absent (Q:\Game.sfc), sort par contenu |
| D17 | VERT | sort_savefiles_enable, .info : U+00AD puis corename = "Snes9x" |

## 4. Enumeration des saves

| Cas | Verdict | Entree |
|---|---|---|
| E01 | VERT | aucun fichier |
| E02 | VERT | Game.srm |
| E03 | VERT | Game.sav |
| E04 | VERT | Game.srm, Game.rtc |
| E05 | VERT | Game.srm, Game.mcr |
| E06 | VERT | Game.mcr, Game.rtc |
| E07 | VERT | Game.sav, Game.rtc |
| E08 | VERT | Game.SRM, Game.rtc |
| E09 | VERT | Game.srm, Game (Rev 1).srm |
| E10 | VERT | Game (Rev 1).srm seul |
| E11 | VERT | Game sans extension |
| E12 | VERT | Gam.srm |
| E13 | VERT | game.srm |
| E14 | VERT | savestate_directory = :\saves, Game.srm + Game.state |
| E15 | VERT | savestate_directory = :\saves, seul Game.state |
| E16 | VERT | jeu deux fois dans Games |
| E17 | VERT | Games=[null, jeu a ApplicationPath vide, G] |
| E18 | VERT | Emulator=null |
| E19 | VERT | app de meme ApplicationPath que G |
| E20 | VERT | app de chemin en casse differente |
| E21 | VERT | args=null |
| E22 | VERT | G.GetEffectiveCommandLine() rend null |
| E23 | VERT | G.GetAllAdditionalApplications() rend [null] |

## 5. Enumeration des savestates

| Cas | Verdict | Entree |
|---|---|---|
| T01 | VERT | Game.state |
| T02 | VERT | Game.state1 |
| T03 | VERT | Game.state.auto |
| T04 | VERT | Game.state.AUTO |
| T05 | VERT | Game.STATE1 |
| T06 | VERT | Game.state1.png |
| T07 | VERT | Game.state03 |
| T08 | VERT | Game.state-2 |
| T09 | VERT | Game.state.bak |
| T10 | VERT | Game.state99 |
| T11 | VERT | Game.stateX |
| T12 | VERT | Game.state, Game.state1, Game.state.auto, Game.state1.png |
| T13 | VERT | Game.state, savestate_directory absente |
| T14 | VERT | Game.state1 dans T\roms\snes, savestates_in_content_dir |
| T15 | VERT | Game.state.state3 |

## 6. Applications additionnelles

| Cas | Verdict | Entree |
|---|---|---|
| A01 | VERT | [A], Games=null, saves\Game (Disc 2).srm |
| A02 | VERT | [A] et [G], Game.srm et Game (Disc 2).srm |
| A03 | VERT | [null, A] |
| A04 | VERT | A.GameId inconnu |
| A05 | VERT | A.CommandLine = mgba, sort_savefiles_enable, saves\mGBA\Game (Disc 2).srm |
| A06 | VERT | A effective "", sort_savestates_enable, states\Snes9x\Game (Disc 2).state |
| A07 | VERT | A.ApplicationPath="" |
| A08 | VERT | A effective sans -L |

## 7. Saturn dans GetSaves

| Cas | Verdict | Entree |
|---|---|---|
| R01 | VERT | Panzer.bcr, .bkr, .smpc |
| R02 | VERT | Panzer.bkr, .smpc |
| R03 | VERT | Panzer.smpc |
| R04 | VERT | Panzer.bcr, Panzer.srm |
| R05 | VERT | Panzer.bcr, Panzer (J).bcr |
| R06 | VERT | PANZER.BCR, panzer.bkr |
| R07 | VERT | G (SNES, snes9x) avec Game.bcr, Game.bkr |
| R08 | VERT | G effective yabause, Game.bcr seul |
| R09 | VERT | Platform="My Saturn Games", coeur snes9x, x.bcr + x.bkr |
| R11 | VERT | Panzer.bcr + states\Panzer.state1 |
| R12 | VERT | "Panzer .bcr" (espace avant l'extension) |
| R13 | VERT | jeu Saturn T\roms\sat\Stra.cue ; Strax.bcr, Stra U+00AD x.bkr, Stra U+0130.smpc |
| R14 | VERT | jeu Saturn Stra.cue ; Strai.bcr, StraI.bkr, Stra U+0131.smpc |

## 8. AddSaveFile

| Cas | Verdict | Entree |
|---|---|---|
| W01 | VERT | SaveToAdd=null |
| W02 | VERT | FileLocation absent |
| W03 | VERT | GameId inconnu |
| W04 | VERT | AdditionalApplicationId="zz" absent |
| W05 | VERT | G.EmulatorId="" |
| W06 | VERT | G.EmulatorId="nope" |
| W07 | VERT | G.ApplicationPath="" |
| W08 | VERT | GameSaveGame T\src\x.srm |
| W09 | VERT | W08 avec SaveGroupName, Title, SaveGroupId, MatchLineageId et autres champs |
| W10 | VERT | GameSaveGame T\src\x sans extension |
| W11 | VERT | GameSaveState slot null |
| W12 | VERT | GameSaveState slot 0 |
| W13 | VERT | GameSaveState slot 4 |
| W14 | VERT | GameSaveState slot -1 |
| W15 | VERT | GameSaveState slot -7 |
| W16 | VERT | destination existe, ShouldOverwriteFunc=null |
| W17 | VERT | destination existe, func -> false |
| W18 | VERT | destination existe, func -> true |
| W19 | VERT | dossier saves absent |
| W20 | VERT | savefile_directory absente |
| W21 | VERT | sort_savefiles_enable, .info absent |
| W22 | VERT | source = destination, func -> true |
| W23 | VERT | destination en lecture seule, func -> true |
| W24 | VERT | application A (emu1), AdditionalApplicationId=a1 |
| W25 | VERT | sort par contenu, saves\snes absent |
| W26 | VERT | args=null |
| W27 | VERT | app A (Game2.sfc, effective null), GameSaveState slot 1 |
| W27b | VERT | W27 avec sort_savestates_enable, states\Snes9x absent |
| W28 | VERT | W27 + ligne effective du jeu null, states absent |
| W29 | VERT | AdditionalApplicationId=a1, GetAllAdditionalApplications() rend null |
| W30 | VERT | GetAllAdditionalApplications() rend [null, A] |
| W31 | VERT | app A a ligne effective "", sort_savestates_enable, GameSaveState |

## 9. AddSaveFile en contexte Saturn

| Cas | Verdict | Entree |
|---|---|---|
| WS1 | VERT | GS, source Old.bcr avec Old.bkr et Old.smpc |
| WS2 | VERT | GS, source Old.bkr (+ Old.bcr) |
| WS3 | VERT | GS, source Old.bcr seul |
| WS4 | VERT | GS, saves\Panzer.bkr present, source .bcr + .bkr |
| WS5 | VERT | GS, GameSaveState slot 2 s.state2 avec s.bcr voisin |
| WS6 | VERT | GS, GameSaveState slot -1, pas de voisins |
| WS7 | VERT | GS, source Old.BCR (+ Old.bkr) |
| WS8 | VERT | G (SNES), source x.bcr, SaveGroupId=sg |
| WS9 | VERT | GS, cwd T\src, source Old.bcr sans dossier, Old.bkr voisin |

## 10. Compagnons, groupes, signatures

| Cas | Verdict | Entree |
|---|---|---|
| X01 | VERT | GetCompanionSaveFiles(T\s\a.bcr), a.bcr a.bkr a.smpc |
| X02 | VERT | T\s\a.bkr, a.bcr a.bkr |
| X03 | VERT | T\s\a.BKR, a.bcr a.bkr |
| X04 | VERT | T\s\a.bcr seul |
| X05 | VERT | T\s\a.bcr absent, a.bkr present |
| X06 | VERT | TryGetSaveGroupInfo(null, ...) |
| X07 | VERT | save Panzer.bkr, primaire "" |
| X08 | VERT | save quelconque, primaire T\s\ Zz .bcr |
| X09 | VERT | Game.srm, coeur mednafen_saturn_libretro |
| X10 | VERT | Game.state.auto, coeur Saturn |
| X11 | VERT | Game.srm, coeur snes9x |
| X12 | VERT | TryComputeSaveSignature, a.bcr (01), a.bkr (02 03) |
| X13 | VERT | idem avec A.BCR sur disque |
| X14 | VERT | FileLocation absent |
| X15 | VERT | Game.srm + coeur Saturn, aucun .bcr/.bkr/.smpc |
| X16 | VERT | Game.srm + coeur Saturn + Game.bkr |
| X17 | VERT | a.bcr ouvert en ecriture partagee |
| X18 | VERT | a.bkr ouvert en exclusif |
| X19 | VERT | GetCompanionSaveFiles("T/s/a.bcr") avec des / |
| X20 | VERT | GetCompanionSaveFiles("T\s\.\a.bcr") |
| X21 | VERT | GetCompanionSaveFiles("X:\a.bcr") sur un lecteur jetable (subst), X:\a.bkr present |

## 11. BIOS

| Cas | Verdict | Entree |
|---|---|---|
| B03 | VERT | T\RA\retroarch.exe, Panasonic 3DO, 4do |
| B04 | VERT | idem, panasonic 3do |
| B05 | VERT | idem, Sega Saturn |
| B06 | VERT | mednafen_saturn, Sega Saturn |
| B07 | VERT | ep128emu_core_libretro, plateforme x |
| B08 | VERT | puae, Commodore Amiga CD32 |
| B09 | VERT | puae, Commodore CDTV |
| B10 | VERT | CoreName.dll, plateforme Platform (en-tete) |
| B11 | VERT | inexistant_libretro.dll |
| B15 | VERT | system_directory = "D:\Bios" |
| B16 | VERT | system_directory = "default" |
| B17 | VERT | cfg sans system_directory |
| B18 | VERT | system_directory_extra = "Z" avant system_directory |
| B19 | VERT | cfg ouvert en exclusif |
| B20 | VERT | -L "cores\a.b.dll" x.dll" |
| B21 | VERT | U+00AD puis system_directory = "D:\Bios", 4do, x |
| B22 | VERT | system_directory = U+00AD:\sys |
| B23 | VERT | exe relatif, cwd T, system_directory = "REL", coeur inexistant |
| B24 | VERT | B23 avec un autre repertoire courant |
| B26 | VERT | exe a la racine d'un lecteur jetable (subst X: sur T\drv), X:\retroarch.cfg avec system_directory = "Z" |
| B90 | VERT | sonde : 4do, plateforme vide (colonne vide seulement) |
| B91 | VERT | sonde : 4do, plateforme null |
| BF1 | VERT | SystemFolder=X1, new BiosFile(f.bin,true,d,m,null), SystemFolder=X2 |
| BF2 | VERT | SystemFolder=abc puis null |
| BF3 | VERT | new BiosFile(null, false) |

## 12. Version, coeurs, RetroAchievements

| Cas | Verdict | Entree |
|---|---|---|
| V01 | VERT | GetCurrentVersion(null), (""), chemin absent |
| V02 | VERT | sortie Version: 1.19.1 (Git abc) |
| V03 | VERT | sortie Version: 1.9.0 (Git abc) |
| V04 | VERT | sortie Version: 1.9.0 |
| V05 | VERT | sortie Version: 1.10.10 |
| V06 | VERT | stderr Version: 1.20.0 |
| V07 | VERT | sortie version: 1.19.1 |
| V08 | VERT | fichier texte renomme .exe |
| V09 | VERT | sortie Version: 1.2.34 |
| V10 | VERT | sortie U+0000 puis Version: 1.2.3 (x) |
| V11 | VERT | sortie RetroArch / Version: 1.20.0 (Git 1) / foo |
| V12 | VERT | deux lignes Version (informatif) |
| V13 | VERT | sonde : sortie vide |
| V14 | VERT | sonde : U+00AD puis Version: 1.2.3 (x), sans saut final |
| K01 | VERT | GetAllAvailableCores(T\RA\retroarch.exe), cores absent |
| K02 | VERT | cores : a_libretro.dll, b_libretro.dll, c.txt, d.dll_old, e.dllx |
| K08 | VERT | GetAllAvailableCores("RA\retroarch.exe"), cwd T, RootFolder=L |
| K09 | VERT | K08 avec RootFolder=T |
| Q01 | VERT | SupportsRetroAchievements("x"), fichier absent |
| Q02 | VERT | Inject(u, t, hardcore false) puis Supports |
| Q03 | VERT | Inject(hardcore null, enable false) puis Supports |
| Q04 | VERT | fichier : cheevos_enable = "untrue" puis cheevos_token = "a=b\"c" |
| Q05 | VERT | fichier verrouille en exclusif |
| Q06 | VERT | PrepareEmulatorForLaunch sans identifiants |
| Q07 | VERT | Prepare u/t, EnableHardcoreAchievements=true, ligne -L "cores\x.dll" "rom" |
| Q08 | VERT | Prepare, DataDirectory sous un fichier (creation du dossier impossible) |
| Q09 | VERT | fichier : U+00AD puis cheevos_enable = "true" |
| Q10 | VERT | InjectRetroAchievementsCredentials(null) |
| Q11 | VERT | Inject username null, token null |
| Q12 | VERT | PrepareEmulatorForLaunch(null) |
| Q13 | VERT | DataDirectory=null, SupportsRetroAchievements("x") |
| Q14 | VERT | fichier : U+00AD puis cheevos_token = "t" |
| Q15 | VERT | sonde : Prepare avec app et hardcore false, identifiants vides |
| Q16 | VERT | sonde : Prepare, EmulatorBeingLaunched null |

## J. Ecarts du juge de code (docs\code-judge.md)

| Cas | Verdict | Entree |
|---|---|---|
| N42 | VERT | E1 : RootFolder=L sans Metadata, InstallEmulator(existant T\MonRA\retroarch.exe, version 0.0.1), requete bloquee |
| WS10 | VERT | E3 : GS, source T\src\.bcr (01), T\src\.bkr (02), T\src.bkr (03) |
| WS11 | VERT | E3 : jeu Saturn de contenu T\roms\saturn\.cue, sources T\src\Old.bcr et Old.bkr |
| C36 | VERT | E4 : savefile_directory = ":\<lettre de T>:saves", cwd T, T\RA\saves\Game.srm, T\saves absent |
| W32 | VERT | E4 : C36 en AddSaveFile (GameSaveGame T\src\x.srm) |
| C37 | VERT | E4 : exe X:\retroarch.exe (subst X: sur T\x), cwd T, savefile_directory = ":\\saves", T\x\saves\Game.srm |
| D18 | VERT | E5 : sort_savefiles_enable, G.CommandLine="", GetAllEmulatorPlatforms()=null, EMU.CommandLine=snes9x, saves\Snes9x\Game.srm |
| W33 | VERT | E5 : D18 en AddSaveFile (GameSaveGame T\src\x.srm) |
| N44 | VERT | E6 : DataManager null, LocalDb non initialisee, DownloadCores vrai, GamesImported=[G] |

## F1. Fuzzing du retroarch.cfg

| Cas | Verdict | Entree |
|---|---|---|
| FZC00 | VERT | cfg = "\uFEFFsavestate_directory= \"{T}\\RA\\saves\"" / "system_directory = \":\\system\"" / "sort_savestates_enable = \"false\"" / "savefile_directory = \":\\saves\"" / "savestates_in_content_dir = \"false\"" / "system_directory= \":\\\u00E9\u00E8 \uD83C\uDFAE\"" / "savefiles_in_content_dir = \"true\"" / "system_directory_extra == \"\" " / "sort_savefiles_by_content_enable = \"true\"" / "savestate_directory = \":\\states\"" / "sort_savefiles_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC01 | VERT | [BOM] [LF] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "cheevos_enable= '1'" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"true\"" / "\uFEFFsystem_directory_extra ='  :\\saves'" / "\u200Bsavestates_in_content_dir = \" true" / "\"quoted key\" = \"v\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC02 | VERT | cfg = "sort_savestates_enable = \"false\"" / "savestate_directory = \":\\states\"" / "savestates_in_content_dir = \"false\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "sort_savestates_by_content_enable = \"true\"" / "savefile_directory = \":\\saves\"" / "sort_savefiles_enable = \"false\"" / "cheevos_enable = \"true \"" / "sort_savefiles_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC03 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"true\"" / "sort_savestates_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC04 | VERT | cfg = "sort_savestates_enable = \"false\"" / "system_directory_extra = \":\\saves2\"" / "sort_savefiles_by_content_enable = \"true\"" / "savefile_directory = \":\\saves\"" / "savefiles_in_content_dir = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "savestate_directory = \":\\states\"" / "sort_savefiles_enable = \"false\"" / "system_directory = \":\\system\"" / "garbage" / "savestates_in_content_dir = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 3), BIOS 4do |
| FZC05 | VERT | [LF] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" / "savestate_directory =\"x=y\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC06 | VERT | [BOM] cfg = "sort_savestates_enable = \"true\"" / "sort_savestates_by_content_enable = \"false\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savefiles_enable = \"false\"" / "savefile_directory= \"\" " / "savestates_in_content_dir = \"true\"" / "savestate_directory = \":\\states\"" / "savefile_directory = \":\\saves\"" / "video_driver == \"TRUE\"" / "sort_savestates_enable =\"true " ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC07 | VERT | cfg = "savefile_directory = \":\\saves\"" / "\u0009cheevos_enable == 'false'" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "Savefile_Directory\u0009=\u0009\"\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC08 | VERT | cfg = "sort_savestates_enable = \"false\"" / "savefiles_in_content_dir = \"false\"" / "savestate_directory = \":\\states\"" / "system_directory_extra  =  \":\\saves\\snes\" " / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "savefile_directory = \":\\saves\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable  =  \" true\"" / "system_directory = \":\\system\"" / "sort_savestates_by_content_enable = \"false\"" / "\uFEFFsavefiles_in_content_dir  =  \"\"" / "system_directory_extra=\":\\states\" " ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC09 | VERT | [BOM] [LF] cfg = "\uFEFFcheevos_enable  =  \"true\"" / "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "system_directory=':\\'" / "savefiles_in_content_dir = \"true\"" / "\uFEFFsort_savestates_enable=\" true\"" / "savestates_in_content_dir = \"false\"" / "\u200Bsavefile_directory =\":\\\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC10 | VERT | cfg = "\u00ADSavefile_Directory = \"" / "savefile_directory = \":\\saves\"" / "k = \"\uD83C\uDFAE\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "\u200Bcheevos_enable\u0009=\u0009\"\" " / "savefiles_in_content_dir = \"true\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"true\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC11 | VERT | [BOM] [LF] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 3), BIOS 4do |
| FZC12 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"true\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC13 | VERT | cfg = "a = \"x\" # c" / "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "video_driver= \u201Cfalse\u201D" / "\u200Bsystem_directory = \"  :\\saves\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "savefile_directory= \"x=y\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC14 | VERT | [LF] cfg = "savefile_directory = \":\\saves\"" / " savestate_directory= \u201C  :\\saves\u201D" / "savestate_directory\u0009=\u0009\":/saves" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"true\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC15 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / " sort_savestates_enable  =  ''" / "savestates_in_content_dir = \"true\"" / "k = \"\"\"\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC16 | VERT | [LF] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"true\"" / "Savefile_Directory=\"true\"" / "\uFEFFsystem_directory = \":/saves\"" / "savefile_directory=\u201C\u201D" ; GetSaves([G]), AddSaveFile(x.srm et state slot 3), BIOS 4do |
| FZC17 | VERT | [LF] cfg = "savefile_directory = \":\\saves\"" / "sort_savestates_by_content_enable = \"false\"" / "k = '\u00E7'" / "\u0009savestates_in_content_dir  =  \"TRUE\"" / "savestates_in_content_dir = \"false\"" / "savefiles_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "system_directory = \":\\system\"" / "savestate_directory = \":\\states\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC18 | VERT | [LF] cfg = "sort_savestates_enable =\"TRUE\"" / "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "audio_latency = 64" / "sort_savefiles_enable = \"false\"" / "savefiles_in_content_dir =\" true\"" / "sort_savestates_enable = \"true\"" / "garbage" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC19 | VERT | [BOM] [LF] cfg = "savestate_directory = \":\\states\"" / "sort_savefiles_by_content_enable = \"true\"" / "system_directory = \":\\system\"" / "savestates_in_content_dir = \"false\"" / "savefiles_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "savefile_directory_x =\"default\"" / "savefile_directory = \":\\saves\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -2), BIOS 4do |
| FZC20 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "\u200Bsavefile_directory_x = \":\\states\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC21 | VERT | cfg = "savefiles_in_content_dir = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savefiles_enable = \"false\"" / "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "sort_savestates_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" / "savestates_in_content_dir = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC22 | VERT | [BOM] cfg = "savefile_directory = \":\\saves\"" / "sort_savefiles_enable  =  \"false" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / "\u0009savestates_in_content_dir= \"true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC23 | VERT | cfg = "savefiles_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "\uFEFFsystem_directory_extra  =  \":\\saves\\snes\"" / "sort_savefiles_by_content_enable = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savestates_enable = \"true\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefile_directory = \":\\saves\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC24 | VERT | [LF] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "\uFEFFsort_savefiles_enable = 1" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "#include \"x.cfg\"" / "video_driver\u0009=\u0009\"true \"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 0), BIOS 4do |
| FZC25 | VERT | cfg = "sort_savefiles_enable = \"false\"" / "savestate_directory = \":\\states\"" / "sort_savestates_by_content_enable = \"false\"" / "savestates_in_content_dir = \"false\"" / "savefile_directory = ':\\..\\savesUp'" / "system_directory = \":\\system\"" / "savefile_directory = \":\\saves\"" / "sort_savestates_enable = \"false\"" / "\u0009savefile_directory_x= \"\u00AD:\\saves\" " / "sort_savefiles_by_content_enable = \"true\"" / "savefiles_in_content_dir = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC26 | VERT | cfg = "savefile_directory = \":\\saves\"" / "a = 6" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savefiles_in_content_dir  =  \" true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "a = \"x\" # c" / "sort_savestates_enable=\"\"" / "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC27 | VERT | [BOM] cfg = "savefile_directory  =  \"default\"" / "sort_savefiles_by_content_enable = \"false\"" / "savestates_in_content_dir = \"false\"" / "\u00ADsavefiles_in_content_dir=\"false" / "sort_savefiles_enable = \"false\"" / "savefile_directory = \":\\saves\"" / "\u0009savefiles_in_content_dir\u0009=\u0009\u201C1\u201D" / "savestate_directory = \":\\states\"" / "sort_savestates_enable = \"false\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -2), BIOS 4do |
| FZC28 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -2), BIOS 4do |
| FZC29 | VERT | cfg = "cheevos_enable= \"TRUE\"" / "savestate_directory = \":\\states\"" / "\u00ADsort_savestates_by_content_enable =\"\"" / "system_directory = \":\\system\"" / "sort_savefiles_by_content_enable = \"false\"" / "\uFEFFsavefiles_in_content_dir = \"1\" " / "sort_savestates_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "\u200Bsort_savefiles_by_content_enable =\"TRUE\"" / "savefile_directory = \":\\saves\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC30 | VERT | [BOM] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC31 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "\uFEFFsort_savestates_by_content_enable\u0009=\u0009\" true" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "\uFEFFsort_savefiles_enable= \"TRUE\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC32 | VERT | [BOM] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"true\"" / "savestates_in_content_dir= \"false" / "sort_savestates_enable = \"false\"" / "\u200Bsort_savestates_by_content_enable = \"\" " / "sort_savefiles_by_content_enable = \"false\"" / "system_directory  =  \":\\saves2\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 0), BIOS 4do |
| FZC33 | VERT | cfg = "savefile_directory = \":\\saves\"" / "sort_savestates_by_content_enable=\"TRUE\"" / "\u200Bcheevos_enable = '1'" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "\u00ADcheevos_enable == true" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC34 | VERT | [BOM] cfg = " Savefile_Directory\u0009=\u0009\"false" / "\u00ADsort_savestates_enable\u0009=\u0009\"TRUE\"" / "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "sort_savefiles_enable = \"true\"" / "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC35 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC36 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 0), BIOS 4do |
| FZC37 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "menu_x =" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "savefiles_in_content_dir =\"TRUE\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "\u200BSavefile_Directory\u0009=\u0009\"1\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC38 | VERT | cfg = " = " / "sort_savestates_by_content_enable = \"false\"" / "system_directory = \":\\system\"" / "\u0009sort_savefiles_by_content_enable ='TRUE'" / "savefiles_in_content_dir == \"true\"" / "sort_savestates_enable = \"false\"" / "savestate_directory = \":\\states\"" / "savefile_directory = \":\\saves\"" / "sort_savefiles_by_content_enable = \"true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -2), BIOS 4do |
| FZC39 | VERT | [BOM] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / "sort_savestates_by_content_enable=\"\"" / "savestates_in_content_dir = \"false\"" / "\uFEFFsavefile_directory_x\u0009=\u0009\":\\saves \"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 3), BIOS 4do |
| FZC40 | VERT | cfg = "sort_savestates_enable = \"false\"" / "savestates_in_content_dir = \"false\"" / "system_directory = \":\\system\"" / "sort_savefiles_by_content_enable = \"false\"" / "savestate_directory = \":\\states\"" / "savefiles_in_content_dir = \"false\"" / "sort_savestates_by_content_enable = \"true\"" / "sort_savefiles_enable = \"false\"" / "" / " savestate_directory = \"{T}\\RA\\saves\"" / "savefile_directory = \":\\saves\"" / "savestates_in_content_dir = true " ; GetSaves([G]), AddSaveFile(x.srm et state slot -2), BIOS 4do |
| FZC41 | VERT | cfg = "savefile_directory = \":\\saves\"" / "system_directory= ':\\'" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "video_driver=\"TRUE\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"true\"" / "\u00ADsavestate_directory = \":\\\u00E9\u00E8 \uD83C\uDFAE\" " / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC42 | VERT | cfg = "savestate_directory = \":\\states\"" / "savestates_in_content_dir = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "savefile_directory = \":\\saves\"" / "savefiles_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "system_directory = \":\\system\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC43 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"true\"" / "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 3), BIOS 4do |
| FZC44 | VERT | [BOM] cfg = "sort_savefiles_by_content_enable = \"true\"" / "savefile_directory = \":\\saves\"" / "sort_savestates_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "system_directory = \":\\system\"" / "savestate_directory = \":\\states\"" / "savefiles_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_by_content_enable\u0009=\u0009" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC45 | VERT | [BOM] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"true\"" / "savestates_in_content_dir =\"true\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC46 | VERT | cfg = "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"false\"" / "savefiles_in_content_dir = \"true\"" / " savefile_directory_x  =  \"\"" / "sort_savefiles_enable = \"false\"" / "savestates_in_content_dir = \"false\"" / "savefile_directory = \":\\saves\"" / "system_directory = \":\\system\"" / "savestate_directory = \":\\states\"" / "sort_savestates_by_content_enable = \"false\"" / "\u00ADsavefile_directory_x =\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC47 | VERT | [BOM] [LF] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / "savestates_in_content_dir = \"false\"" / "\uFEFFsort_savefiles_enable == \"false\"" / "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 3), BIOS 4do |
| FZC48 | VERT | cfg = "" / "sort_savestates_enable = \"false\"" / "system_directory = \":\\system\"" / "savestate_directory = \":\\states\"" / "cheevos_enable = true" / "savefile_directory = \":\\saves\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "#include \"x.cfg\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |
| FZC49 | VERT | cfg = "a = 6" / "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"true\"" / " system_directory =\":\\" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "\uFEFFsavefiles_in_content_dir  =  false" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 0), BIOS 4do |
| FZC50 | VERT | cfg = "savestates_in_content_dir = \"false\"" / "sort_savestates_enable =\"\" " / " savestates_in_content_dir\u0009=\u0009\" true\"" / "sort_savestates_enable = \"false\"" / "savefiles_in_content_dir = \"true\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" / "\uFEFFsavefiles_in_content_dir == 'false'" / "system_directory = \":\\system\"" / "savestate_directory = \":\\states\"" / "sort_savefiles_by_content_enable = \"false\"" / "\u00ADsavefiles_in_content_dir\u0009=\u0009 true" / "savefile_directory = \":\\saves\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 3), BIOS 4do |
| FZC51 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"true\"" / "\u00ADsort_savestates_by_content_enable =\"false\"" / "\u0009system_directory  =  \":\\\"" / "\u00E9 = \"\u00E8\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savefiles_by_content_enable == \u201Cfalse\u201D" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC52 | VERT | [BOM] cfg = "system_directory = \":\\system\"" / "\uFEFFsort_savestates_by_content_enable =\"\"" / "savestate_directory = \":\\states\"" / "sort_savefiles_by_content_enable = \"true\"" / "savefiles_in_content_dir = \"false\"" / "sort_savestates_by_content_enable = \"true\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_enable = \"false\"" / "savefile_directory = \":\\saves\"" / "savestates_in_content_dir = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC53 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "a = \"x\" # c" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -2), BIOS 4do |
| FZC54 | VERT | cfg = "savefile_directory = \":\\saves\"" / "a = 6" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "sort_savefiles_enable = \"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"true\"" / "sort_savefiles_by_content_enable = \"true\"" / "\u00ADsystem_directory=\"{T}\\RA\\states\"" / "sort_savestates_by_content_enable = \"false\"" / "\u00ADsavestate_directory == \u201C:/saves\u201D" ; GetSaves([G]), AddSaveFile(x.srm et state slot -2), BIOS 4do |
| FZC55 | VERT | cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "garbage" / "savestate_directory =\"  :\\saves\"" / "savefiles_in_content_dir = \"true\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC56 | VERT | [BOM] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "\uFEFFvideo_driver=\"true \"" / "sort_savestates_enable = \"false\"" / "savefiles_in_content_dir == \"false\" " / "sort_savefiles_by_content_enable = \"true\"" / "sort_savestates_by_content_enable = \"true\"" / "\u200Bsystem_directory_extra=\":\\saves\\snes\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 4), BIOS 4do |
| FZC57 | VERT | cfg = "=\"v\"" / "savefile_directory = \":\\saves\"" / "savefile_directory_x  =  \":\\states" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"true\"" / "system_directory  =  \u201C:\\saves \u201D" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"false\"" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 1), BIOS 4do |
| FZC58 | VERT | [LF] cfg = "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "\u200Bsort_savestates_enable =\"\"" / "savestates_in_content_dir = \"false\"" / "savestates_in_content_dir  =  \"\"" / "savefile_directory\u0009=\u0009':\\s\u00E4ves'" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"true\"" / " = " / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"true\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot 2), BIOS 4do |
| FZC59 | VERT | [BOM] cfg = " savestates_in_content_dir= \"TRUE\"" / "system_directory= \":\\s\u00E4ves" / "savefile_directory = \":\\saves\"" / "savestate_directory = \":\\states\"" / "system_directory = \":\\system\"" / "savefiles_in_content_dir = \"false\"" / "savestates_in_content_dir = \"false\"" / "sort_savefiles_enable = \"false\"" / "sort_savestates_enable = \"true\"" / "savefiles_in_content_dir=\u201Ctrue \u201D" / "sort_savefiles_by_content_enable = \"false\"" / "sort_savestates_by_content_enable = \"false\"" / "\uFEFFSavefile_Directory = \"TRUE\"" ; GetSaves([G]), AddSaveFile(x.srm et state slot -1), BIOS 4do |

## F2. Fuzzing des noms de saves

| Cas | Verdict | Entree |
|---|---|---|
| FZN00 | VERT | jeu SNES G ; fichiers "states\\Game.bcr.STATE", "saves\\GAME.state.AUTO", "states\\Game.state -1", "saves\\Game\u00AD.state\u0663", "saves\\Game_\u00E4.state 4" ; AddSaveFile(state slot -2 "Game.smpc" avec voisins .bcr/.bkr) |
| FZN01 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "saves\\Gamex.mcr", "saves\\Game_\u00E4.state", "saves\\Gam\u00E9.state+5" ; AddSaveFile(game "Game_\u00E4.BKR" avec voisins .bcr/.bkr) |
| FZN02 | VERT | jeu SNES G ; fichiers "saves\\GAME.state.state3" ; AddSaveFile(game "game.state.AUTO" avec voisins .bcr/.bkr) |
| FZN03 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Gam\u00E9.Smpc", "saves\\Game  x.state", "saves\\game.rtc", "saves\\Gamex.bkr", "saves\\Game  x.state2147483648" ; AddSaveFile(game "game.state03" avec voisins .bcr/.bkr) |
| FZN04 | VERT | jeu SNES G, states = saves ; fichiers "saves\\Game_\u00E4.state.auto", "states\\Gamex.state.AUTO", "states\\Game  x.state.AUTO", "saves\\Game_\u00E4.bcr.bak" ; AddSaveFile(game "game.smpc" avec voisins .bcr/.bkr) |
| FZN05 | VERT | jeu Saturn Game.cue ; fichiers "saves\\ Game.state.AUTO", "states\\Gam.state" ; AddSaveFile(game "Game\u00AD.STATE" avec voisins .bcr/.bkr) |
| FZN06 | VERT | jeu SNES G ; fichiers "states\\Game.smpc", "states\\Game\u00AD.state.AUTO", "saves\\Game (Rev 1).state", "states\\Game.state.AUTO", "saves\\Gam\u00E9", "saves\\GAME.state 4" ; AddSaveFile(game "Game.srm" avec voisins .bcr/.bkr) |
| FZN07 | VERT | jeu SNES G ; fichiers "states\\Game (Rev 1).state\u00AD1", "saves\\Game .state", "saves\\Game_\u00E4.mcr", "saves\\Game.state.state" ; AddSaveFile(state slot null "Game.state1.png" avec voisins .bcr/.bkr) |
| FZN08 | VERT | jeu Saturn Game.cue ; fichiers "states\\Gam.bcr", "saves\\Game.bcr.state.AUTO" ; AddSaveFile(state slot 10 "Gamex.bcr.bak" avec voisins .bcr/.bkr) |
| FZN09 | VERT | jeu SNES G ; fichiers "saves\\Game.state.state+5", "saves\\Game (Rev 1).state", "states\\Game .state1.png", "states\\game.state1", "saves\\Game (Rev 1).state.auto", "saves\\Game (Rev 1).bcr.bak" ; AddSaveFile(game "Game.bcr.state.state3" avec voisins .bcr/.bkr) |
| FZN10 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game\u00AD.rtc", "saves\\Game.state.state.AUTO", "saves\\Game.bcr.bkr", "saves\\Game.bcr.state -1", "states\\Game\u00AD.state.AUTO" ; AddSaveFile(game "Game" avec voisins .bcr/.bkr) |
| FZN11 | VERT | jeu SNES G, states = saves ; fichiers "saves\\game.state1.png", "saves\\game.state", "saves\\game.SRM" ; AddSaveFile(game "Gam.state-2" avec voisins .bcr/.bkr) |
| FZN12 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game .SRM" ; AddSaveFile(state slot 8 "Game  x.state.AUTO" avec voisins .bcr/.bkr) |
| FZN13 | VERT | jeu SNES G ; fichiers "saves\\Game  x.state03", "states\\Game.state", "states\\ Game.state03", "saves\\ Game.state03", "states\\Game.bcr.sav" ; AddSaveFile(game "Gam\u00E9.state" avec voisins .bcr/.bkr) |
| FZN14 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "states\\Game.bcr.state -1", "states\\Gam.state.AUTO", "saves\\GAME.BKR" ; AddSaveFile(game "Game  x.srm" avec voisins .bcr/.bkr) |
| FZN15 | VERT | jeu SNES G ; fichiers "states\\Gamex.state1", "saves\\Game\u00AD.state" ; AddSaveFile(game "Game  x.Smpc" avec voisins .bcr/.bkr) |
| FZN16 | VERT | jeu Saturn Game.cue ; fichiers "states\\GAME.state.AUTO", "saves\\Gam\u00E9.state.AUTO", "saves\\Gamex.state\u0663", "states\\Game\u00AD.SRM", "saves\\Game_\u00E4.state", "states\\Gam.state03" ; AddSaveFile(game "Gam.state\u0663" avec voisins .bcr/.bkr) |
| FZN17 | VERT | jeu SNES G ; fichiers "saves\\Gam\u00E9.bkr", "saves\\ Game.state", "states\\Game .state-2", "saves\\Game_\u00E4.state1.png" ; AddSaveFile(game "Game (Rev 1).state\u00AD1" avec voisins .bcr/.bkr) |
| FZN18 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "saves\\Game_\u00E4.stat\u00E9", "saves\\Game (Rev 1).state2147483648" ; AddSaveFile(state slot -1 "Game.state" avec voisins .bcr/.bkr) |
| FZN19 | VERT | jeu SNES G ; fichiers "saves\\ Game", "saves\\Game.state.state1", "saves\\Game.state -1", "states\\Gamex.STATE", "saves\\Game  x.state 4", "saves\\Game  x.state+5" ; AddSaveFile(game "GAME.bcr" avec voisins .bcr/.bkr) |
| FZN20 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game.state 4", "states\\GAME.state2147483648", "saves\\Gam\u00E9.state+5", "saves\\Game_\u00E4.rtc", "saves\\game.SRM" ; AddSaveFile(game "Game  x.mcr" avec voisins .bcr/.bkr) |
| FZN21 | VERT | jeu SNES G, states = saves ; fichiers "saves\\Game (Rev 1).sav", "states\\Game_\u00E4.state1", "saves\\Game.bcr.state1" ; AddSaveFile(game "Game.state 4" avec voisins .bcr/.bkr) |
| FZN22 | VERT | jeu Saturn Game.cue ; fichiers "states\\Gam.state.state3" ; AddSaveFile(state slot null "Game.state2147483648" avec voisins .bcr/.bkr) |
| FZN23 | VERT | jeu SNES G ; fichiers "states\\Game.state.srm", "saves\\Gam.state1", "saves\\Game  x.state.AUTO", "saves\\Game.state.state+5", "states\\Gamex.smpc" ; AddSaveFile(state slot null "Game.state.bcr" avec voisins .bcr/.bkr) |
| FZN24 | VERT | jeu Saturn Game.cue ; fichiers "states\\Game\u00AD.state-2", "saves\\Gamex.state2147483648", "saves\\Game\u00AD.STATE" ; AddSaveFile(game "Gam\u00E9.bcr" avec voisins .bcr/.bkr) |
| FZN25 | VERT | jeu SNES G, states = saves ; fichiers "saves\\game.state2147483648", "saves\\Gam\u00E9.state1" ; AddSaveFile(game "Game (Rev 1).state" avec voisins .bcr/.bkr) |
| FZN26 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game .state", "saves\\ Game.state2147483648", "saves\\Game_\u00E4.mcr", "states\\Game.bcr.BKR", "states\\Game\u00AD.state1.png", "saves\\Game.bcr.state1" ; AddSaveFile(game "Game\u00AD.state1" avec voisins .bcr/.bkr) |
| FZN27 | VERT | jeu SNES G ; fichiers "saves\\Game  x.bcr.bak", "saves\\Game .state1", "states\\Gamex.bcr.bak", "saves\\Gam.state\u00AD1" ; AddSaveFile(game "Gam.srm" avec voisins .bcr/.bkr) |
| FZN28 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "saves\\Game.bcr.BKR", "states\\Game.bcr.state2147483648" ; AddSaveFile(game "Game.state1.png" avec voisins .bcr/.bkr) |
| FZN29 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Gamex.state\u0663", "states\\GAME.state1", "saves\\Game.bcr.bkr", "states\\ Game.bcr.bak", "saves\\Game.bcr.bcr.bak", "states\\ Game.state.state3" ; AddSaveFile(game "Game.state.state2147483648" avec voisins .bcr/.bkr) |
| FZN30 | VERT | jeu SNES G ; fichiers "states\\GAME", "states\\Game_\u00E4.state2147483648", "saves\\game.SRM", "saves\\Game  x.smpc", "saves\\Gam\u00E9.BKR" ; AddSaveFile(state slot -1 "Game.state -1" avec voisins .bcr/.bkr) |
| FZN31 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "states\\Gam\u00E9.STATE", "saves\\game.state1", "states\\Gam.stat\u00E9" ; AddSaveFile(game "GAME.state.AUTO" avec voisins .bcr/.bkr) |
| FZN32 | VERT | jeu SNES G ; fichiers "states\\Game_\u00E4.mcr" ; AddSaveFile(game "Game  x.state2147483648" avec voisins .bcr/.bkr) |
| FZN33 | VERT | jeu Saturn Game.cue ; fichiers "saves\\ Game.state03", "saves\\Gamex.state1", "saves\\GAME.BKR", "saves\\Game_\u00E4.state2147483648", "saves\\Game (Rev 1).state03" ; AddSaveFile(game "Game\u00AD.sav" avec voisins .bcr/.bkr) |
| FZN34 | VERT | jeu SNES G ; fichiers "saves\\Game.Smpc", "saves\\Gam\u00E9.state2147483648", "saves\\Game  x.Smpc" ; AddSaveFile(game "Game (Rev 1).state.auto" avec voisins .bcr/.bkr) |
| FZN35 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "saves\\Game (Rev 1).state.auto", "saves\\Game\u00AD.state1" ; AddSaveFile(game "Gamex.state\u0663" avec voisins .bcr/.bkr) |
| FZN36 | VERT | jeu SNES G ; fichiers "saves\\Gam.state.AUTO", "states\\Game .state2147483648", "states\\Game .state-2", "states\\Game_\u00E4.state-2", "states\\game.state\u00AD1", "saves\\Game.BKR" ; AddSaveFile(game "Gam\u00E9.mcr" avec voisins .bcr/.bkr) |
| FZN37 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game.state.smpc", "states\\Game.bcr.state1", "saves\\Game.state", "saves\\Game .Smpc" ; AddSaveFile(game "Game.bcr" avec voisins .bcr/.bkr) |
| FZN38 | VERT | jeu SNES G, states = saves ; fichiers "states\\Game\u00AD.state\u00AD1", "states\\Game (Rev 1).state2147483648" ; AddSaveFile(game "Game.state -1" avec voisins .bcr/.bkr) |
| FZN39 | VERT | jeu Saturn Game.cue ; fichiers "states\\game.bcr", "saves\\Game.state.state.auto", "saves\\Gam\u00E9.state 4", "states\\Game.state.SRM", "states\\Gamex.SRM", "saves\\game.state.auto" ; AddSaveFile(game "game.STATE" avec voisins .bcr/.bkr) |
| FZN40 | VERT | jeu SNES G ; fichiers "states\\Game .state+5", "saves\\game.state -1", "states\\Game.bcr.state1", "saves\\Gam\u00E9.state.state3", "saves\\Game_\u00E4.state-2" ; AddSaveFile(game "game.bcr.bak" avec voisins .bcr/.bkr) |
| FZN41 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game  x.rtc", "saves\\Game  x.state.auto", "states\\GAME.rtc" ; AddSaveFile(game "game.mcr" avec voisins .bcr/.bkr) |
| FZN42 | VERT | jeu SNES G, states = saves ; fichiers "saves\\Game.bcr.state1.png" ; AddSaveFile(game "Game.state -1" avec voisins .bcr/.bkr) |
| FZN43 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Gamex.SRM", "saves\\Gam.state.auto", "saves\\Game\u00AD.STATE", "saves\\Gam.sav", "saves\\Gam.state\u0663" ; AddSaveFile(game "GAME.Smpc" avec voisins .bcr/.bkr) |
| FZN44 | VERT | jeu SNES G ; fichiers "saves\\GAME.state03", "states\\Game\u00AD.state -1", "saves\\Game (Rev 1).state" ; AddSaveFile(game "game.STATE" avec voisins .bcr/.bkr) |
| FZN45 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "saves\\Gam\u00E9.state -1" ; AddSaveFile(state slot -1 "Game.state.auto" avec voisins .bcr/.bkr) |
| FZN46 | VERT | jeu SNES G ; fichiers "states\\Game_\u00E4.state1", "states\\ Game.state -1", "saves\\Gamex.bcr.bak", "states\\Game  x.state 4", "saves\\Gam\u00E9.Smpc", "states\\Game (Rev 1).rtc" ; AddSaveFile(game "Game.state\u00AD1" avec voisins .bcr/.bkr) |
| FZN47 | VERT | jeu Saturn Game.cue ; fichiers "states\\ Game.bcr.bak", "saves\\Game (Rev 1).state.auto", "saves\\Gam\u00E9.state.state3", "saves\\GAME" ; AddSaveFile(state slot 5 "Gamex.state1" avec voisins .bcr/.bkr) |
| FZN48 | VERT | jeu SNES G ; fichiers "saves\\Game.bkr", "saves\\Game.state.state -1" ; AddSaveFile(game "Game.bcr.bkr" avec voisins .bcr/.bkr) |
| FZN49 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game (Rev 1).stat\u00E9", "saves\\GAME.state.auto", "states\\game.SRM", "states\\Game_\u00E4.bcr", "saves\\Game (Rev 1).BKR", "saves\\Game .bkr" ; AddSaveFile(game "Game  x.bkr" avec voisins .bcr/.bkr) |
| FZN50 | VERT | jeu SNES G ; fichiers "saves\\Gam", "saves\\Game_\u00E4.state -1", "states\\Gam.stat\u00E9", "saves\\Game (Rev 1).stat\u00E9" ; AddSaveFile(game "Game\u00AD.state 4" avec voisins .bcr/.bkr) |
| FZN51 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game.state.state 4", "saves\\Game.state.auto", "saves\\ Game.state03" ; AddSaveFile(game "Gamex.BKR" avec voisins .bcr/.bkr) |
| FZN52 | VERT | jeu Saturn Game.cue, states = saves ; fichiers "saves\\Game\u00AD.sav" ; AddSaveFile(game "Gam.state -1" avec voisins .bcr/.bkr) |
| FZN53 | VERT | jeu SNES G ; fichiers "saves\\game.state.state3", "states\\Gamex.state.auto", "saves\\Game_\u00E4.Smpc", "saves\\Game .bkr", "saves\\Game.bcr.state -1" ; AddSaveFile(state slot 11 "Gam.state.AUTO" avec voisins .bcr/.bkr) |
| FZN54 | VERT | jeu Saturn Game.cue ; fichiers "states\\Game .srm", "states\\Gam\u00E9.state -1", "states\\Gamex.state\u0663" ; AddSaveFile(game "Game.bcr.bak" avec voisins .bcr/.bkr) |
| FZN55 | VERT | jeu SNES G, states = saves ; fichiers "states\\Game  x.state-2" ; AddSaveFile(game "Game.state.auto" avec voisins .bcr/.bkr) |
| FZN56 | VERT | jeu Saturn Game.cue ; fichiers "saves\\Game.bcr.state2147483648", "saves\\Game (Rev 1).state -1", "saves\\Game.state", "states\\Gam\u00E9.state.AUTO", "saves\\Game_\u00E4", "saves\\Game  x.srm" ; AddSaveFile(state slot null "Game (Rev 1).state-2" avec voisins .bcr/.bkr) |
| FZN57 | VERT | jeu SNES G ; fichiers "saves\\Gamex.state", "saves\\Game.bcr.state.auto", "saves\\Game.state -1", "saves\\game.state1" ; AddSaveFile(state slot 3 "Game (Rev 1).STATE" avec voisins .bcr/.bkr) |
| FZN58 | VERT | jeu Saturn Game.cue ; fichiers "saves\\GAME.state\u00AD1", "saves\\GAME.state -1" ; AddSaveFile(state slot 10 "Gam\u00E9.state 4" avec voisins .bcr/.bkr) |
| FZN59 | VERT | jeu SNES G, states = saves ; fichiers "saves\\Gam\u00E9.BKR", "saves\\Game_\u00E4.state.auto", "states\\Game.bcr.state1", "states\\Gam.state.auto", "saves\\Gam.state.auto", "states\\Game_\u00E4" ; AddSaveFile(game "Game.Smpc" avec voisins .bcr/.bkr) |

## Non joues

| Cas | Raison |
|---|---|
| S16 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| R10 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| R15 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| B01 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| B02 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| B12 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| B13 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| B14 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| B25 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| B27 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| K03 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| K04 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| K05 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| K06 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| K07 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| K10 | [LB] : LocalDb ou session LaunchBox chargee (hors perimetre de la sonde) |
| N01 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N02 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N03 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N04 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N05 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N06 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N07 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N08 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N09 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N10 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N11 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N12 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N13 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N14 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N15 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N16 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N17 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N18 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N19 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N20 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N21 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N22 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N23 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N24 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N25 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N26 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N27 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N28 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N29 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N30 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N31 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N32 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N33 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N34 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N35 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N36 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N37 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N38 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N39 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N40 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N41 | section 13 : jouee seulement par l'option --network (rapport last-run-network.md) |
| N43 | ouvre une boite modale de LaunchBox chez l'original (observe le 09/10) : joue seulement avec --interactive |
| N43b | ouvre une boite modale de LaunchBox chez l'original (observe le 09/10) : joue seulement avec --interactive |
