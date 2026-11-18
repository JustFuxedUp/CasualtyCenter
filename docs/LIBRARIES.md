# Library documentation and reading guide

Recovery continuation (2026-11-18): CasualtiesTogetherUtils.Util exposes static bool AccurateRaycastInteractionCheckObstruction(Body who, Body target, bool do_effect). Native medical actions use it alongside distance checks; remote surgery now calls it with false for prompt, approval and ongoing permission. MedicalSync.Server_QueueSendCharacterHealth(NetBody,bool) queues absolute current body state; normal Server_SendCharacterHealth targets AllClientIdsExceptHost, and CharacterHealthStateSyncPacket.Apply(NetBody,bool) uses tick ordering. This supports using the patient's NetBody without force=true; it does not establish live delivery latency or loss recovery.

Unreleased multiplayer pass inspection (2026-11-17): Together.AnyObjectNetId is a value type whose zero value means no native target. MinigameMPManager.GetMinigameTargetObjectNetId(Minigame) reflects the `limb` field even on custom types. The startup prefix rejects limb-targeted unknown minigames until PlayerCamera_ApplyWoundItem_MultiplayerPatch.applying_on_limb exists. A scoped generic Harmony prefix returns default(AnyObjectNetId) for ISurgery; native targets and startup reset/veto logic remain intact. The real-Harmony harness now includes this cold-start prerequisite.

ScavPlayer.ClientIdToPlayerDict uses knetid keys; knetid wraps a ushort and ToString returns its numeric identity. Remote targets resolve against live dictionary entries and exact Body references. Multiplayer.rules is a MultiplayerGameRules struct implementing INetSerializeByMemcpy; adding a custom rule field is not a supported extension. The authorized fallback uses its bool ConsentForFeeding. ServerMain.PromptPlayer(ScavPlayer,string,string,string,Action<byte>) stores a per-recipient callback and sends CLIENT_CustomPrompt; answer zero is acceptance and pending callbacks expire after 30 seconds. The native feeding handler already exempts unconscious patients. Surgery additionally revalidates identity/range on callback, ignores duplicate/stale callbacks and binds consent to one operation. Live prompt behavior for host-as-patient still requires testing.

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

## Multiplayer assembly findings (2026-11-16)

Verified by targeted local decompilation, not inferred from online examples:

- Pinned CUCoreLib DLL SHA-256: `C0F064260B2671D02BF802C460AA346C6B858EC9269F2FF93439EBABAA940AA3`.
- Installed Together declares BepInPlugin version `5.5.0`; CasualtiesMP DLL SHA-256: `C5CE87BE6B574D78130D001CD5787F3B0982BECCCFD9B711F4BBF98D82C0B0A6`.
- `MultiplayerApi.RegisterServerHandler(string, Func<JToken,JToken>)` exposes payload only. MultiplayerBridge's `Func<uint,JToken,JToken>` registration overload is internal. Do not put a sender ID in payload and treat it as authenticated.
- Together's public `Multiplayer.RegisterCustomServerReceiver(string, Action<ScavPlayer,NetDataReader>)` resolves ScavPlayer from the transport client ID before invoking the callback. The paired client registration takes `Action<NetDataReader>`. Registration rejects duplicate channel hashes. The optional development adapter now uses these APIs; its static callbacks remain inert after disposal and are reused on reinstall. Actual game reload/teardown ordering still requires validation.
- `ScavPlayer.playerId` is a `knetid`, not a durable save identifier. `TryGetPlayerAndBodyFromClientId(knetid,out ScavPlayer,out Body)` and `MedicalSync.TryGetNetBodyAndLimb(knetid,byte,out NetBody,out Limb)` exist. Connection IDs need a fresh session epoch and body-generation mapping to prevent stale targets after reconnect/respawn.
- `MedicalSync.Server_QueueSendCharacterHealth(NetBody,bool force=false)`, `Server_SendCharacterHealth`, and `ForceHealth` exist. Queueing depends on `CanSyncHealth()` unless forced. Verify native ownership/confirmation behavior before choosing forced updates; do not routinely bypass the game's health-sync policy.
- `CharacterHealthStateSyncPacket` captures/applies body.internalBleeding and uses server tick ordering. `CharacterLimbHealthState` captures/applies shrapnel, skinHealth, muscleHealth, bleedAmount, pain, infection fields, boneHealTimer and disinfectionTime. Shrapnel is byte-encoded and disinfectionTime ushort-encoded; skin/muscle/bleeding use compressed storage. Multiplayer acceptance needs representable bounds and quantization tolerance, not exact float equality across peers.
- `ScavPlayer.ClientIdToPlayerDict`, `body` and `LocalPlayer` are public fields. The runtime uses dictionary membership and exact player-object identity before resolving a self-patient.
- `Multiplayer.CreateNamedWriter(string)` writes the native custom-packet header/channel hash. The adapter appends an int32 byte length and raw UTF-8 bytes through `NetDataWriter.Put(int)` / `Put(byte[])`; the reader uses `AvailableBytes`, `GetInt()` and `GetBytesSegment(int)` with bounds checked before reading.
- `Net.Client_Send(in DeliveryMethod,in NetDataWriter)` and `Server_SendTo(in DeliveryMethod,in NetDataWriter,in ScavPlayer)` are the reflected send overloads. Client_Send loops back through the native server-message dispatcher when invoked by the host. ReliableOrdered is the selected delivery mode. Signature/queue tests use doubles; native loopback and sockets have not been exercised in game.

Still unverified: initialization/receiver lifetime during reload, custom MinigameMPManager interoperability, actual two-peer health confirmation and update cadence, save identity across reconnect, safe host shutdown/migration. Finding a public method is not evidence these scenarios work.

Additional local inspection for 0.14.0 (2026-11-17): Together's `CanSyncHealth()` reads `ServerMain._DEV_ENABLE_HP_SYNC`, whose inspected default is true. MinigameMPManager builds its type-ID map from loaded Minigame subclasses; both peers must use matching plugin assemblies. Its server start receiver recognizes the type, records it, and only creates specialized native sessions for supported native operations. Our custom authority view supplies its own clinical protocol. These findings support preparing the test build; they do not replace live startup/minigame validation.

0.14.0 live-test correction: the host's Together registry lacked NativeAuthorityMinigame, and `MinigameBase_StartMinigame_MultiplayerPatch.Postfix` threw from `GetMinigameTypeId` after native Start ran. The inspected postfix has `(MinigameBase,Minigame,Item,ref bool)` parameters; Together's end prefix is `bool Prefix(MinigameBase)`. 0.14.1 installs guarded prefixes on these two patch methods before reporting transport availability. For ISurgery only, it skips the start notification and returns true from Together's end prefix without running its notification body, preserving original game cleanup. Together's original start prefix still resets native minigame input/session flags and preserves its occupied-host veto. No type IDs are assigned, aliased or rebuilt. Installation validates signatures and is idempotent; the plugin's Harmony cleanup removes these guards. Thirteen checks use the actual installed HarmonyX on .NET Framework with minimal lifecycle doubles, including the pre-fix exception, already-patched wrapper, unrelated native games and unload. Unity/Mono and two-peer acceptance are still required.
