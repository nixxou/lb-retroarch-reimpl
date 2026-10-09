# Sonde A/B : original contre clone (greffon RetroArch)

Rapport produit par `tests\AbProbe` ; ne pas l'editer a la main.

- Rejouer depuis la racine du depot : `dotnet run -c Release --project tests\AbProbe -- --network` (construit aussi le clone).
- Mode `--network` (section 13) : garde OK : requetes redirigees vers le faux serveur local.
- Place libre sur C:\ au debut : 23705 Mio.
- Volume annonce par le vrai reseau (Content-Length cumules des cas reels, rejeux compris) : 1210 Mio ; octets recus par les cartes reseau pendant le run (toute la machine) : 5147 Mio.
- Fenetres visibles du processus pendant le run : 0.
- Original : `G:\LB1326\System\Plugins\RetroArch LaunchBox Integration\Unbroken.LaunchBox.Windows.RetroArch.dll` (lecture seule).
- Clone : `src\Nixx.RetroArch\bin\<config>\net10.0-windows\Nixx.RetroArch.dll`.
- Juge : egalite stricte des deux greffons (valeur rendue en profondeur, exception type + message, arbre du
  dossier de cas avec tailles et hash, `BiosFile.SystemFolder` apres l'appel). `<W>` = dossier de cas jetable.

## Resume

- Cas joues : 39 (39 de la matrice, 0 de fuzzing).
- VERTS : 39. ROUGES : 0. JAUNES (informatif, egalite non exigee) : 0. Erreurs de sonde : 0.
- Non joues : 14 (liste en fin de rapport).

## 13. Reseau, telechargement, installation

| Cas | Verdict | Entree |
|---|---|---|
| N01 | VERT | [reseau reel] GetInstallableVersions() sur le vrai buildbot |
| N02 | VERT | [faux serveur] buildbot avec un lien nightly/, GitHub [{name: v1.2.3}] |
| N03 | VERT | [faux serveur] buildbot 500, GitHub OK |
| N04 | VERT | [faux serveur] buildbot 500, GitHub 500 |
| N05 | VERT | [faux serveur] buildbot vide, GitHub [] |
| N06 | VERT | [faux serveur] buildbot 500, GitHub [{"nom":"x"}] |
| N25 | VERT | [faux serveur] buildbot 500, GitHub [{"name":"V1.2.3"}] |
| N26 | VERT | [faux serveur] buildbot 500, GitHub [{"name":null}] |
| N27 | VERT | [faux serveur] buildbot 500, GitHub [{"name":123}] |
| N28 | VERT | [faux serveur] buildbot 500, GitHub [{"name":{}}] |
| N29 | VERT | [faux serveur] buildbot 500, GitHub [5] |
| N30 | VERT | [faux serveur] buildbot 500, GitHub {} |
| N36 | VERT | [faux serveur] buildbot 500, GitHub [[1]] |
| N07 | VERT | [faux serveur] en-tetes envoyes au buildbot et a GitHub (buildbot 500 pour forcer GitHub) |
| N24 | VERT | [faux serveur] buildbot : liens valides puis un dernier lien tronque sans /" |
| N09 | VERT | [reseau reel] IsUpdateAvailable(EMU) herite, version locale = derniere version publiee (lue en N01) |
| N08 | VERT | [faux serveur] en-tetes envoyes pour les .7z (installation T\MonRA 1.19.1, annulation au premier bloc) |
| N11 | VERT | [faux serveur] InstallEmulator, existant T\RetroArch-Win64\retroarch.exe, 1.19.1 |
| N12 | VERT | [reseau reel] InstallEmulator, existant T\MonRA\retroarch.exe, derniere version (archives reelles) |
| N13 | VERT | [reseau reel] annulation pendant le telechargement du programme (au premier bloc), derniere version |
| N14 | VERT | [faux serveur] annulation pendant les coeurs (progression > 0.5) |
| N15 | VERT | [reseau reel] version 0.0.1 (404 reel), L\Metadata absent |
| N16 | VERT | [faux serveur] archive du programme corrompue |
| N17 | VERT | [faux serveur] archive des coeurs corrompue |
| N18 | VERT | [faux serveur] UpdateEmulator, existant sans ApplicationPath |
| N19 | VERT | [reseau reel] UpdateEmulator, version locale = derniere version publiee (lue en N01) |
| N20 | VERT | [faux serveur] FireEmulatorImportAction, DownloadCores a Value=false |
| N23 | VERT | [faux serveur] delai HTTP depasse avant les en-tetes de RetroArch.7z (serveur muet 105 s), annulation fausse, T\MonRA |
| N31 | VERT | [faux serveur] annulation juste apres le dernier bloc de RetroArch.7z (progression >= 0.5) |
| N34 | VERT | [faux serveur] existant ApplicationPath="retroarch.exe" (sans dossier) |
| N35 | VERT | [faux serveur] existant a la racine d'un lecteur jetable (subst X: sur T\drv), X:\retroarch.exe |
| N37 | VERT | [faux serveur] RootFolder = L\ (separateur final), InstallEmulator sans emulateur existant |
| N42 | VERT | [faux serveur] RootFolder=L sans Metadata, existant T\MonRA, version 0.0.1 (404) |
| N45 | VERT | [reseau reel] classe derivee qui surcharge InstallEmulator ("marqueur"), UpdateEmulator, exe existant absent (ecart E7) |
| N48 | VERT | [faux serveur] InstallEmulator, existant T\Emu\RA\ \retroarch.exe (T\Emu\RA existe), 1.19.1 |
| N48b | VERT | [faux serveur] UpdateEmulator, existant T\Emu\RA\...\retroarch.exe, exe absent |
| N48d | VERT | [faux serveur] sonde : InstallEmulator, existant T\Emu\RA\.\retroarch.exe |
| N48e | VERT | [faux serveur] sonde : InstallEmulator, existant T\Emu\RA.\retroarch.exe |
| N49 | VERT | [faux serveur] pendant le telechargement de RetroArch.7z (serveur lent), ouverture du .7z temporaire en lecture avec partage lecture+ecriture |

## Non joues

| Cas | Raison |
|---|---|
| N10 | [LB] : LocalDb reelle ou session LaunchBox |
| N21 | [LB] : LocalDb reelle ou session LaunchBox |
| N22 | [LB] : LocalDb reelle ou session LaunchBox |
| N40 | [LB] : LocalDb reelle ou session LaunchBox |
| N41 | [LB] : LocalDb reelle ou session LaunchBox |
| N46 | [LB] : LocalDb reelle ou session LaunchBox |
| N47 | [LB] : LocalDb reelle ou session LaunchBox |
| N48c | [LB] : LocalDb reelle ou session LaunchBox |
| N32 | FireEmulatorImportAction interroge la LocalDb avant tout telechargement : exige la LocalDb reelle ([LB] de fait) |
| N33 | FireEmulatorImportAction interroge la LocalDb avant tout telechargement : exige la LocalDb reelle ([LB] de fait) |
| N38 | FireEmulatorImportAction interroge la LocalDb avant tout telechargement : exige la LocalDb reelle ([LB] de fait) |
| N39 | FireEmulatorImportAction interroge la LocalDb avant tout telechargement : exige la LocalDb reelle ([LB] de fait) |
| N43 | ouvre une boite modale de LaunchBox chez l'original : joue seulement avec --interactive (run par defaut) |
| N43b | idem N43 (racine null) : --interactive |
