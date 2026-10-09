# lb-retroarch-reimpl

A complete re-implementation of the "RetroArch LaunchBox Integration" plugin that ships with
LaunchBox 14 (version 1.12, by Unbroken Software), as a white box: every behaviour is written
down in a specification and checked by tests, and the code is open.

It does what the original does, and only that:

- download, update and reinstall RetroArch, with the core downloads of the import action;
- BIOS requirements per core and platform, from the same table of 631 lines;
- RetroAchievements login, hardcore and softcore switching;
- save and save-state management: listing, slots, companions (Saturn `.bcr` / `.bkr` / `.smpc`),
  additional applications, add, backup and restore.

**It is a drop-in replacement, quirks included.** Same public surface (`RetroArchPlugin :
EmulatorPlugin, IEmulatorWithCores`, `Models.BiosFile`), same values returned, same files read and
written, same exceptions, same error texts. The defects of the original are reproduced on purpose:
a `retroarch.cfg` line without `=` hides every save, a version line shorter than expected throws,
and so on. Improvements are for later, on a base proven identical. Only the namespace differs
(`Nixx.RetroArch` instead of `Unbroken.LaunchBox.Windows.RetroArch`).

## How it was written

The work was split between two sides that never shared code:

1. one side wrote `docs/spec.md`, a behavioural specification of the original, public member by
   public member: inputs, outputs, files, network, `retroarch.cfg` keys, defaults, error cases,
   quirks. It also wrote `docs/test-matrix.md`, the 318 cases the spec implies;
2. the other side wrote `src/` from the spec alone, against the public plugin SDK and the public
   types of the LaunchBox assemblies the original itself calls. It never saw anything of the
   original.

No code of Unbroken Software is in this repository. The one thing taken as is, as data, is the
BIOS table (`resources/Bios_Requirements.txt`), because the plugin cannot give the same answers
without it.

## How it was checked

- `tests/AbProbe` loads the original plugin and this one in the same process and calls both on
  the same inputs: fake games and emulators, every `retroarch.cfg` of the matrix, throw-away
  folders, a fake `retroarch.exe`. It compares the value returned (type and every public property),
  the exception (type, message, parameter), the files written (tree and hashes), and the static
  state. Last run: 376 offline cases (matrix plus fuzzing of the cfg and of save names) and 39
  download and install cases (real network and a local fake server), 0 difference. The reports are
  in `tests/AbProbe/last-run*.md`.
- `docs/binary-judge.md`: an independent review of the two compiled DLLs, looking for behaviours
  the matrix did not cover. It compared them on 4 026 generated cases without finding a
  difference.

What is not covered by the probe: the cases that need a loaded LaunchBox (its local database),
listed as `[LB]` in the matrix.

## Build

- .NET 10 SDK, Windows.
- A LaunchBox 14.0.1 install, for the assemblies the plugin calls and that are not vendored
  (`Unbroken`, `Unbroken.LaunchBox`, `Unbroken.LaunchBox.LocalDb`, `Unbroken.LaunchBox.Windows`,
  `Newtonsoft.Json`). Point `LbCore` at its `Core` folder:

```
dotnet build -c Release src\Nixx.RetroArch -p:LbCore=D:\LaunchBox\Core
```

The output is `Nixx.RetroArch.dll` plus `manifest.json`; the SDK and the LaunchBox assemblies are
referenced with `Private=false` and must never be copied next to the plugin (see `vendor/README.md`).

## Install

LaunchBox routes everything for an emulator to the plugin of that emulator, so this plugin and the
original must not be installed together on the same `retroarch.exe`.

1. Back up and remove `LaunchBox\System\Plugins\RetroArch LaunchBox Integration\`.
2. Copy `Nixx.RetroArch.dll` and `manifest.json` into `LaunchBox\Local\Plugins\Nixx-RetroArch\`.
3. Start LaunchBox. "Nixx-RetroArch" appears, ticked, in the plugins manager.

To go back, delete the `Nixx-RetroArch` folder and put the original back.

## Layout

| Path | What |
|---|---|
| `src/Nixx.RetroArch` | the plugin |
| `resources/Bios_Requirements.txt` | the BIOS table, embedded as is |
| `docs/surface.md` | the public surface to reproduce, obtained by reflection |
| `docs/spec.md`, `docs/test-matrix.md` | the specification and its cases |
| `docs/binary-judge.md` | the independent review of the two compiled DLLs |
| `tests/AbProbe` | the black-box A/B probe (`dotnet run -c Release --project tests\AbProbe`, add `-- --network` for the download cases, `-- --interactive` for the two cases where LaunchBox opens a dialog) |
| `vendor/` | the public plugin SDK, 14.0.1.2 |

## License

MIT. The BIOS table and the plugin SDK in `vendor/` belong to their owners.
