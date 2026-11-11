# Direct interaction and feedback — 0.10.0

Grabbing an exposed shard tip now selects that wound and starts extraction in the same press. Forceps can also acquire a tip while grip is held. Suturing binds to the nearest eligible wound on press. Only the radial menu changes the selected tool; it persists between wounds and reopened operations during the game session.

Successful extraction and suturing show a small rising, fading label: Bad at 0–40, Good at 41–74, Great at 75–100. Numeric scores and wound-selection markers are removed. Mistakes have sound only. The surgery panel, health menu and radial menu use dark blue accents.

## Local audio

Place `Bad-Ok.wav`, `Great.wav`, `Mistake.wav`, and `SutureStitch.wav` under `BepInEx/plugins/CasualtiesSurgery/Audio/`. These temporary supplied assets are excluded from Git. The decoder supports 16-bit mono/stereo PCM WAV. Bad and Good share Bad-Ok; Great uses Great; failed gestures use Mistake; committed thread turns use SutureStitch. `[Audio] FeedbackVolume` defaults to 0.7. Missing/invalid files log a warning and leave gameplay functional.

## Review

`BindGestureTarget` resolves eligible wounds before beginning the gesture, using the same tip tolerance as FragmentPull.Begin. SelectSite no longer consumes a grip press or changes tools. SurgeryFeedback keeps sound and result labels alive independently of the operation screen. WaveData and ResultRating have standalone checks; audio data is loaded once when the plugin starts.

## Playtest

1. Open a limb with several fragments. Grab successive exposed tips directly without first clicking wound centers. Try moving a held grip into a tip.
2. Extract several shards before suturing. Forceps must remain selected. Switch to suture and finish several wounds; suture must remain selected, including on reopening.
3. Confirm feedback on extraction, stitch turns, failed gestures and completed sutures. No numeric scores or white guide dots/boxes should remain. Final-wound feedback should fade after the panel closes.
4. Check dark blue panel and wheel readability. Adjust FeedbackVolume after closing the game if needed.
5. Interrupt, save and reload: pending sutures and the seeded layout should remain intact.

Build and standalone checks do not verify live Unity input, placement or perceived sound volume; those need this in-game pass.
