# Casualties Surgery

Trauma Center-inspired surgery for Casualties: Unknown. Working title.

## Current build: 0.10.2 full-circle wounds and grab feedback

Open the normal health screen, right-click a limb on the dummy, and select
**Extract fragment + suture**. The game's native minigame screen and hand open.
On an attached organic limb with fragments, use forceps to pull a fragment away from the wound, then use
the suture tool to cross the wound at least three times in one held stroke. Hold right mouse
for the cursor-centered tool wheel. The window stays full size; wound assets default to 40% scale and the hand to 33%.
Each limb has a seeded board with one site per fragment; interruption and normal
saves preserve completed sites and pending sutures. Grab any exposed shard tip directly; the selected tool stays unchanged between wounds.
Right-click outside to exit. Failed sutures have no cost. Completion removes one fragment,
restores 5 skin points and reduces bleeding by 1 using the latest wound values.
Healing amounts are configurable. Extraction slips cost 1 skin and add up to 0.2 bleeding; failed sutures are harmless.

This playtest uses free tools: crafting, supplies and multiplayer surgery are
not implemented. The world keeps running while the panel is open. The native minigame handles procedure input and exits; the health context menu
blocks background player input while it is open.

- [Art dimensions, rotation and visibility report](docs/ART-HANDOFF.md)
- [Interaction and audio feedback](docs/DIRECT-FEEDBACK.md)
- [Previous controls, scoring and save migration](docs/GAMEPLAY-FLOW.md)
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
For local sound feedback, copy the five placeholder WAVs into an `Audio/` folder beside the surgery DLL. Audio assets are not included in Git; missing sounds do not prevent play.
Keep exactly one copy of each plugin DLL in the plugins tree.

Set `HideManagerGameObject = true` in `BepInEx/config/BepInEx.cfg` and restart.
Launch normally, enter a run and right-click a limb on the health dummy. Check `BepInEx/LogOutput.log` for both
plugins. The panel does not pause gameplay. Follow the native minigame playtest before merging.
The standalone F8 window is retired. Old debug config entries have no effect.

Build does not deploy automatically. CUCoreLib is a required dependency.
The previous verified starter remains in Git on `main` and its original net471
build output is retained locally. Do not confuse that older DLL with this build.

No game binaries, downloaded libraries, local paths, or config backups are committed.
