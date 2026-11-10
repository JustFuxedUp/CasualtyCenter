# Library documentation and reading guide

## CUCoreLib 1.1

Downloaded the official full-game release DLL and companion XML documentation.
Both SHA-256 hashes match GitHub's release asset metadata and are pinned in
`dependencies.json`. Run `scripts/Restore-Libraries.ps1` to restore them.
The release author notes the binary can be replaced as fixes arrive; the checksum
pin intentionally requires review when that happens.

- [Release and downloads](https://github.com/jimmyking9999999/CUCoreLib/releases/tag/v1.1)
- [Documentation home](https://cucorelib.jimmyking.dev/docs/welcome/)
- [Project setup](https://cucorelib.jimmyking.dev/docs/setup/)
- [Full-game migration](https://cucorelib.jimmyking.dev/docs/full-game-transition/)
- [Player and limb data](https://cucorelib.jimmyking.dev/docs/player/)
- [Items](https://cucorelib.jimmyking.dev/docs/item/)
- [Recipes](https://cucorelib.jimmyking.dev/docs/recipe/)
- [Minigames](https://cucorelib.jimmyking.dev/docs/minigames/)
- [Saving](https://cucorelib.jimmyking.dev/docs/saving/)
- [Multiplayer compatibility](https://cucorelib.jimmyking.dev/docs/multi-mod-compatibility/)
- [Debugging and testing](https://cucorelib.jimmyking.dev/docs/debug-testing/)
- [Documentation source, including machine-readable topic files](https://github.com/jimmyking9999999/CUCoreLib/tree/main/CUCoreLibWebapp/public/api/topics)
- [Library license](https://github.com/jimmyking9999999/CUCoreLib/blob/main/LICENSE)

The adjacent `lib/CUCoreLib/1.1/CUCoreLib.xml` enables library documentation
tooltips in C# editors. Online docs follow the evolving project: verify APIs
against our pinned DLL before relying on examples.

Read setup and player first, then minigames and multiplayer. The minigame page
explicitly labels its wrapper API as in development and recommends custom code
or waiting for a dedicated tool. We have not committed the surgery UI to that API.

`MultiplayerApi.Apply` supports float fields, but the fragment counter is an int.
Its documented client routing also refers to the sender's body. These helpers
alone are insufficient for treating another player. We need an explicit patient
identity and a host-validated, single-completion operation message.

## Existing tools

- [BepInEx plugin development](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html): plugin startup, metadata, logging and configuration.
- [BepInEx troubleshooting](https://docs.bepinex.dev/articles/user_guide/troubleshooting.html): our F8 fix is `HideManagerGameObject = true`.
- [Harmony documentation](https://harmony.pardeike.net/articles/intro.html): prefixes/postfixes for extending existing methods.
- [HarmonyX source](https://github.com/BepInEx/HarmonyX): the Harmony variant supplied with BepInEx. We do not install a second patching DLL.
- [Casualties Together source and installation](https://github.com/creaturefeaturelarry/casualties-together): deferred runtime dependency; compatibility is untested.

The project references game DLLs locally and does not commit them. Library
binaries and configuration backups are also excluded from Git.
