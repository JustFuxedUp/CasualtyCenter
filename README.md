# Casualties Surgery

Trauma Center-inspired surgery for Casualties: Unknown. Working title.

## Current build: 0.2.0 foundation

F8 opens a read-only inspector showing the local player's fragment count, skin
health, and bleeding for each limb. No treatment, crafting, inventory changes,
or multiplayer surgery are enabled yet. The procedure rules are tested separately.

- [Library documentation](docs/LIBRARIES.md)
- [Fragment design and confirmed game fields](docs/FRAGMENT-DESIGN.md)
- [Code-review guide](docs/REVIEW.md)

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
Launch normally, enter a run and press F8. Check `BepInEx/LogOutput.log` for both
plugins. The panel does not pause gameplay or capture all underlying game input.

Build does not deploy automatically. CUCoreLib is a required dependency of 0.2.0.
The previous verified starter remains in Git on `main` and its original net471
build output is retained locally. Do not confuse that older DLL with this build.

No game binaries, downloaded libraries, local paths, or config backups are committed.
