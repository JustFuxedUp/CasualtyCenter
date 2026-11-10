# Casualties Surgery

Trauma Center-inspired surgery for Casualties: Unknown. Working title.

## Current build: 0.4.0 health-dummy surgery

Open the normal health screen, right-click a limb on the dummy, and select
**Extract fragment + suture**. On an attached organic limb with fragments, use forceps to pull a fragment along its marked path, then use
the suture tool to place three stitches. Completion removes one fragment,
restores 5 skin points and reduces bleeding by 1 using the latest wound values.
Healing amounts are configurable. Slips cost 1 skin and add up to 0.2 bleeding.

This playtest uses free tools: crafting, supplies and multiplayer surgery are
not implemented. The world keeps running while the panel is open. Game input
handlers and UI raycasts are blocked behind the panel; verify this in-game.

- [Health menu controls, review and playtest](docs/HEALTH-MENU.md)
- [MVP playtest and controls](docs/MVP-PLAYTEST.md)
- [MVP review notes](docs/MVP-REVIEW.md)
- [Library documentation](docs/LIBRARIES.md)
- [Fragment design and confirmed game fields](docs/FRAGMENT-DESIGN.md)
- [Foundation review notes](docs/REVIEW.md)

## Build

Requires a current .NET SDK, the local full game with BepInEx 5, and the pinned
CUCoreLib files. `GamePath.local.props` contains the local game path and is ignored
by Git; copy the example if setting up another machine.

```powershell
.\scripts\Restore-Libraries.ps1
& 'C:\Program Files\dotnet\dotnet.exe' build .\CasualtiesSurgery.csproj -c Release
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\ProcedureChecks.csproj -c Release
```

The full SDK path avoids this machine's older 32-bit `dotnet` command. The mod
targets .NET Framework 4.7.2, matching CUCoreLib; the SDK restores its reference
assemblies during build. The small standalone rule-check runner targets .NET 10.

## Install for testing

Close the game first. Copy `lib/CUCoreLib/1.1/CUCoreLib.dll` into
`BepInEx/plugins/CUCoreLib/`. Replace the existing surgery plugin with
`bin/Release/net472/CasualtiesSurgery.dll` in `BepInEx/plugins/CasualtiesSurgery/`.
Keep exactly one copy of each plugin DLL in the plugins tree.

Set `HideManagerGameObject = true` in `BepInEx/config/BepInEx.cfg` and restart.
Launch normally, enter a run and right-click a limb on the health dummy. Check `BepInEx/LogOutput.log` for both
plugins. The panel does not pause gameplay. Follow the health menu playtest before merging.
F8 inspector access is opt-in through `Interface.EnableDebugPanel` in the plugin config.

Build does not deploy automatically. CUCoreLib is a required dependency.
The previous verified starter remains in Git on `main` and its original net471
build output is retained locally. Do not confuse that older DLL with this build.

No game binaries, downloaded libraries, local paths, or config backups are committed.
