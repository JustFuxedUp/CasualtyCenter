# Casualty Center

A Trauma Center-inspired surgery mod for **Casualties: Unknown**, built on BepInEx 5 and CUCoreLib 1.1. The current prototype supports self-surgery through the game's health dummy and native minigame system.

**Installed baseline at handoff: 0.12.1. Local pending patch: 0.12.2 knife visual anchoring.** The patch built successfully but was not installed, committed or pushed when approval review hit a usage limit. See [current status](docs/STATUS.md) before resuming.

## Playable features

- Seeded fragment wounds: extract with forceps, then close with a continuous suture or gel.
- Fractures: scalpel incision, two end-pivot bone halves to align, then suture closure. Treatment reduces the timer without instantly mending the bone.
- Four-tool cursor-centered radial menu; native hand motion and sway; retained tool choice.
- Persistent incisions, skin debt, extracted fragments, gel coverage and operation progress.
- Gel disinfection and surgery-related infection risk based on preparation, immunity and accuracy.
- Short feedback sounds, fading ratings and tool trails, debug supply counters, and open-surgery dummy markers.

This is a **singleplayer prototype** with free test tools. Actual crafting, inventory consumption, multiplayer surgery, internal-bleeding procedures and final art are not implemented.

## Start here

| Need | Read |
| --- | --- |
| Resume development | [AGENTS.md](AGENTS.md), then [STATUS](docs/STATUS.md) |
| Intended gameplay and defaults | [PROJECT](docs/PROJECT.md) |
| Code ownership and save rules | [ARCHITECTURE](docs/ARCHITECTURE.md) |
| Build, test, install, review and model choice | [WORKFLOW](docs/WORKFLOW.md) |
| Next bounded tasks | [ROADMAP](docs/ROADMAP.md) |
| Art sizes, library references, historical notes | [Documentation index](docs/INDEX.md) |

## Controls

Right-click a limb in the normal health dummy and choose a procedure. Hold right mouse inside the minigame to select **Forceps right / Suture up / Scalpel left / Gel down**; release to select. Right-click outside to exit. Fragment boards use PageUp/PageDown when necessary. The world stays live.

## Build

Requires the installed game/BepInEx, pinned CUCoreLib and a current .NET SDK. Copy `GamePath.local.props.example` to the ignored local props file and set the actual game path. Restore libraries through `scripts/Restore-Libraries.ps1` only if missing or intentionally updating dependencies.

```powershell
dotnet build .CasualtiesSurgery.csproj -c Release
dotnet run --project .	estsProcedureChecks.csproj -c Release
```

The plugin targets **net472**; the rule runner targets **net10.0**. Use the current 64-bit SDK if the default `dotnet` resolves to an older installation. The build does not deploy automatically. Install instructions and machine-specific setup routing are in WORKFLOW.
