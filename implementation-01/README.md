# Painted weapon explosion implementation

Captured from implementation commit `d715a19e`, 2026-10-21.
The approved source drawings remain unchanged in `history/refinement-01/`.

## Unity playback

`unity-preview.gif` and `unity-preview.mp4` show the imported missile burst and
mine disc through the game's camera. Both effects play twice. The missile uses
the pooled effect prefab; the mine uses the actual concussion wave. The ships
provide a size reference. The capture does not exercise missile flight or audio.

The clip has 179 frames at 50 fps, lasting 3.58 seconds. The GIF has 20 ms frame
delays. The missile's fire ends around 0.31 seconds and its tail at 0.58 seconds.
The mine retains its 0.8-second sweep, radius 12 and expansion speed 15.

`WeaponExplosionPreview.cs` is the exact scratch scenario used by the scoped
PlayMode run. `capture-manifest.json` contains the recorder's timing.
`unity-test-summary.json` records 19 passing tests with no failures or skips.
The scoped fixtures cover concussion behavior, transient presentation and capture.

## Drawing and export checks

`painted-preview.gif` shows the shared canvas renderer at original speed.
`preview.html` is a self-contained tuner with playback, scrubbing, visual controls
and a source-layer view. Download it to open it in a browser.
The `preview-*.png` files show 40, 120, 260, 480 and 780 ms.
These browser views expose the artwork and oblique mine projection; the Unity
clip establishes the actual game view.

`browser-checks.json` records transparent atlas borders and final frames at
missile spreads 0.7, 1 and 1.3, lifetime checks, settings export and responsive
layout checks. The exporter carries its fitting scales in `settings.json` so
Unity preserves the visible size. The mine's painted leading rim follows the
existing gameplay radius.

The existing generated Burst cache stayed intact after automatic approval review
blocked its deletion. The scoped Unity run passed with that cache.
