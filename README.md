# Casualties Surgery — starter project

A starting point for a Trauma Center-inspired surgery mod for Casualties: Unknown.
The working title and plugin identifier are provisional.

## What this build does

Loads through BepInEx 5 and adds an F8 development panel. It writes its startup
message and the game's reported version to BepInEx/LogOutput.log. It does not
change injuries, inventory, saves, crafting, or multiplayer behavior.

## Build

This machine has a modern .NET SDK at `C:\Program Files\dotnet\dotnet.exe` and
.NET Framework 4.7.1 reference assemblies. Its default `dotnet` command instead
points at an older 32-bit installation, so use this command from this directory:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build .\CasualtiesSurgery.csproj -c Release
```

`GamePath.local.props` points at your installed game. For another machine, copy
`GamePath.local.props.example` to that filename and update the path. That local
file is excluded from Git. Dependencies are referenced from the installation;
game files are not included in this project or the build output.

The built plugin is `bin/Release/net471/CasualtiesSurgery.dll`.

## Try the plugin

1. Close the game.
2. Create `BepInEx/plugins/CasualtiesSurgery` inside the game folder.
3. Copy only `CasualtiesSurgery.dll` into that folder.
4. In `BepInEx/config/BepInEx.cfg`, set `HideManagerGameObject = true`. This protects plugin callbacks from game object cleanup. Restart the game after changing this setting, then press F8. Confirm the panel opens and closes.
5. Check `BepInEx/LogOutput.log` for the plugin startup message and game version.

The panel does not pause the game or capture all gameplay input; test it at the
main menu first. The hotkey can be changed in the generated BepInEx config.
To uninstall, close the game and remove this plugin DLL.

## Development milestones

1. Verify this plugin loads in the full release.
2. Inspect the installed game's injury model and select one real, treatable condition.
3. Implement a complete self-treatment procedure with a simple interaction panel.
4. Add tools, medicine, and recipes using a verified compatible CUCoreLib release.
5. Integrate patient selection and procedure synchronization with the updated
   Casualties Together release, then test host and client treatment in both directions.
6. Add interrupted procedures, death/disconnection handling, saving, and balancing.

Keep procedure rules separate from the game adapter, interface, and multiplayer
adapter as these are added. A proposed multiplayer model is to submit treatment
actions to the host for validation and distribute the resulting patient state;
the actual implementation must fit Together's supported networking mechanisms.

CUCoreLib and Casualties Together are not dependencies of this first load-test
plugin. Their full-release compatibility has not been verified here.

## References

- https://github.com/jimmyking9999999/CUCoreLib
- https://github.com/creaturefeaturelarry/casualties-together

The existing installation's log confirms BepInEx 5.4.23.5 and Unity 2022.3.62.
The game version is believed to be 7.1.0 based on the user's report; confirm it
using this plugin's startup log or the game UI before adding version-specific hooks.
