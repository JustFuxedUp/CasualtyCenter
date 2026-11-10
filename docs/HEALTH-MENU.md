# 0.4.0: surgery from the health dummy

User verified this menu in-game. In 0.5.0, the same menu launches the
[native minigame](NATIVE-MINIGAME.md); its controls supersede the F8/debug notes below.

## Player flow

1. Open the normal health screen.
2. Right-click a limb on the dummy. A native Surgery context menu opens beside it.
3. Choose **Extract fragment + suture**. Unavailable treatment shows its reason.
4. Complete the existing forceps and three-stitch minigame.
5. Completion, Cancel or Back returns to the health screen. Right-click again for another fragment.

Click outside the context menu, choose Back, or press Escape to dismiss it.
F8 is no longer the normal entry point. The standalone inspector can be enabled
with `Interface.EnableDebugPanel = true` in the plugin config for troubleshooting.
F8/Escape still closes an active operation.

This change leaves the successful 0.3.0 minigame and treatment values intact.
Tools remain free and multiplayer treatment remains disabled. No new art or
downloaded libraries are needed. TextMeshPro comes from the installed game.

## Review notes

- `Game/HealthMenu.cs` adds a right-click handler alongside native `WoundViewLimb`
  components under the local camera's health screen. Existing handlers remain.
- The clicked limb index resolves against that health screen's patient. It does
  not rely on `limbLookingAt`, which is a hover selection and can be stale.
- The context menu uses Unity UI buttons and the game's existing font. It is
  positioned relative to the click and constrained to the health screen bounds.
- Eligibility is refreshed while open and rechecked on click and operation start.
- The menu captures the actual limb reference. Moving the pointer over another
  limb cannot silently change the pending patient or treatment target.
- Only the popup and operation block underlying player input. Outside clicks
  dismiss the popup without acting on the health screen behind it.
- Components and UI are removed on scene changes or plugin shutdown. Closing the
  health screen or replacing its patient ends a pending procedure without reward.
- `Plugin.cs` routes health-menu launches directly into the existing operation,
  then closes its window on completion/cancel rather than returning to the F8 list.
- Successful 0.3.0 gameplay was verified by the user before this change.

Review the increment against `codex/fragment-mvp`:

```powershell
git diff codex/fragment-mvp...codex/health-dummy-menu
```

## Playtest before merging

- Right-click several limbs and confirm the title matches the clicked limb.
- Check no-fragment, detached/prosthetic and unconscious patient restrictions.
- Move the pointer to other limbs after opening the menu; target must remain fixed.
- Complete a treatment and confirm only that limb changes and the health screen remains open.
- Cancel midway: no fragment removal or healing; incurred slip damage remains.
- Outside-click, Back and Escape dismiss only the popup, with no click-through.
- Reopen the health screen repeatedly, start a new run, and check for duplicate menus.
- Check ordinary health actions, item dragging and vanilla extraction still work when surgery is closed.
- Test menus near each screen edge and at your normal display scaling/resolution.
- Confirm the menu explains multiplayer treatment is unavailable during a Together session.

Compilation and the 57 existing procedure/session checks cannot verify Unity event
routing, popup placement or interaction with other mods. Those require this live playtest.

## Art handoff

No assets requested yet. Once this menu flow and the procedure canvas layout are
settled, define dimensions, anchors and state variants for tool icons, fragment/
wound sprites and the operation background before commissioning final art.
