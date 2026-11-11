# Casualties Surgery

Trauma Center-inspired surgery for Casualties: Unknown. Working title.

## Current build: 0.8.1 persistent fragment boards

Open the normal health screen, right-click a limb on the dummy, and select
**Extract fragment + suture**. The game's native minigame screen and hand open.
On an attached organic limb with fragments, use forceps to pull a fragment along its marked path, then use
the suture tool to draw one continuous zigzag across the wound. Hold right mouse
for the cursor-centered tool wheel. The window stays full size; wound assets default to 40% scale and the hand to 22%.
Each limb has a seeded board with one site per fragment; interruption and normal
saves preserve completed sites and pending sutures. Click a wound to select it.
Right-click outside to exit. Failed sutures have no cost. Completion removes one fragment,
restores 5 skin points and reduces bleeding by 1 using the latest wound values.
Healing amounts are configurable. Extraction slips cost 1 skin and add up to 0.2 bleeding; failed sutures are harmless.

This playtest uses free tools: crafting, supplies and multiplayer surgery are
not implemented. The world keeps running while the panel is open. The native minigame handles procedure input and exits; the health context menu
blocks background player input while it is open.

- [Hand-scale fix and larger fragments](docs/VISUAL-SCALE-FIX.md)
- [Current board controls, persistence and playtest](docs/PERSISTENT-BOARDS.md)
- [Previous changes, controls and playtest](docs/SURGERY-GRACE.md)
- [Previous controls, scoring and playtest](docs/COMPACT-SURGERY.md)
- [Body-section design note](docs/BODY-SECTIONS-DESIGN.md)
- [Native minigame controls, review and playtest](docs/NATIVE-MINIGAME.md)
- [Surgery kit concept (design only)](docs/SURGERY-KIT-DESIGN.md)
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
plugins. The panel does not pause gameplay. Follow the native minigame playtest before merging.
The standalone F8 window is retired. Old debug config entries have no effect.

Build does not deploy automatically. CUCoreLib is a required dependency.
The previous verified starter remains in Git on `main` and its original net471
build output is retained locally. Do not confuse that older DLL with this build.

No game binaries, downloaded libraries, local paths, or config backups are committed.
