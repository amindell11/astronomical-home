# Mine motion revision

Captured from implementation commit `689f3138` on the weapon-explosions branch.
The mine keeps its 0.8-second sweep and 0.40-second center, with a pulsing core,
staggered broken ripples and outward-moving wisps. The missile is unchanged.

`unity-preview.gif` and `unity-preview.mp4` contain two detonations through the game
camera, with ships for scale. Both have 179 frames at 50 fps, lasting 3.58 seconds.
The capture verifies appearance and repeated playback, not missile flight or audio.
The two Unity PNGs show the center and later disc motion.

`painted-preview.gif` shows the shared Canvas animation at large and game scale,
including oblique and overhead projections. The two preview PNGs show 260 and 480 ms.
`source-layers.png` shows the extracted drawing layers. `preview.html` is the
self-contained tuner source, with exported defaults in `settings.json`.

`unity-test-summary.json` records 20 passing PlayMode tests with graphics, including
the regression test for disc progress and pooled reuse. `browser-checks.json`
records lifetime, atlas padding, fixed-size interior change and tuner checks.
The capture scenario and manifest accompany the clips.

The missile texture's SHA-256 is unchanged from the preceding revision:
`0429b049210b0158ea677480a256a6b68ed398498f7caf4e180480b33aae64bf`.
