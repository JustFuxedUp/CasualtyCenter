# 0.6.0: compact surgery, radial tools and continuous suturing

## Controls

Start through the same health-dummy menu. Hold right mouse to open the tool wheel
at your cursor. Move right for Forceps or left for Suture and release to select.
Releasing near the center keeps the current tool. The native hand stays in place
while choosing; the cursor chooses tools without hand lag. The right mouse gesture
is consumed so it cannot also trigger a bound vanilla action or exit.

Opening the wheel discards an unfinished gesture without granting a reward or a
new slip penalty. Any earlier slip damage remains. Release the grip and begin a
fresh stroke after selecting. Native exit controls still work; Escape/back also
works while the wheel is held.

Forceps: grip SHARD, pull along the marked extraction path and release at END.
Suture: begin on the upper-left START marker, keep gripping, and draw alternating
zigzags across both sides down the entire wound. Release near the bottom on either
side after at least four crossings. The faint six-crossing guide is an example,
not an exact trace or a demand for six individual clicks.

The suture must be one continuous stroke. Early release, leaving the permitted
wound area, substantial reversal up the wound, or scribbling without advancing
fails. A failed stroke uses the existing slip penalty and can be retried.

Quality (0–100) considers spacing, angle consistency and path efficiency. It is
shown in the game's alert on successful treatment and logged. Quality currently
does not scale healing: a successful extraction plus suture still grants the
configured benefits exactly once. Acceptance thresholds are provisional.

## View and randomization

- Native minigame screen and its child hand use 40% of their previous scale.
- A temporary mesh effect fades the lower arm toward the wrist, without modifying
  game textures. Screen scale and the effect are restored/removed on exit.
- Pointer-to-hand conversion accounts for the compact screen; native tremor remains
  in native local units. Native physics still control the hand.
- No tool sidebar. Wound, trace and compact tool wheel occupy the procedure canvas.
- One wound per treatment has randomized position, angle, length and width within
  safe canvas bounds. Retry keeps that wound layout; a new treatment generates another.
- This is still a placeholder body section and one treated fragment per operation,
  not a full anatomy map or a multi-wound body section yet.

Config: `Interface.ToolWheelKey` defaults to `Mouse1`; `Interface.SurgeryScale`
defaults to `0.4`. Other treatment settings retain their existing values.

## Review map

- `Procedures/SutureStroke.cs`: engine-independent continuous stroke validation,
  turn detection and scoring. Turns use their outermost points, not frame-dependent
  threshold samples. Completion is only evaluated on release.
- `Game/OperationSession.cs`: one atomic suture completion against current wound
  values. The legacy stitch-step method delegates to the same transaction.
- `Game/NativeFragmentMinigame.cs`: hold/release tool wheel, random wound geometry,
  native-hand coordinate mapping, visible thread trace and lifecycle restoration.
- `Game/SurgeryGraphics.cs`: ring sectors and a vertex-based wrist fade. No asset
  download, image modification or new runtime library is required.
- `Plugin.cs`: configurable wheel binding/scale; input suppression covers only our
  menu or the radial hold/release, not the regular native procedure interaction.

## Live playtest required

Build/automated rules do not verify visual wrist alignment, Unity screen scaling
or the tactile feel of hand physics. Check these first:

1. Screen and hand are compact, text remains readable, and the fade ends near the wrist.
2. Hand tracks the intended position at the new scale, including edges and different resolutions.
3. Hold right mouse, choose each tool, release in the dead zone, and switch mid-stroke.
4. Draw four to six quick, evenly spaced zigzags; compare a less consistent stroke.
5. Reject a single diagonal, partial wound coverage and stationary scribbles.
6. Confirm successful extraction+suture removes one fragment and heals once.
7. Exit during the wheel/stroke; run a vanilla minigame afterwards and check its
   hand, scale and controls were restored. Also test focus loss and a new scene.
8. Inspect random layouts across several new treatments; no wound should be unreachable.

The exact feel and scoring thresholds need gameplay feedback. A demonstration of
the intended TC stroke would be useful for tuning, but is not required to test this build.
