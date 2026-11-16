# Outpost kit, gate 4: drift, and the two defects the owner saw, 2026-11-15

The owner asked for "a tiny random drift so they dont look like they are rooted in space", with the drift going on while a station is dark ("yes"). From a capture they also reported "weird shadow aliasing on the modeled hash marks and this light which doesnt sit quite on the metal" (#1384).

The drift is on `task/outpost-kit-engine` at `68d8db45cb0f3a8352063a66a09fff73b29d1ade`, with no PR. Nothing in the game references the stations yet.

| File | What it is |
|---|---|
| `gate4c-drift-repair-copper.mp4` | The repair station at size 15 for 30 s of game time: 8 s lit, then dark while the parts coast to a stop and the drift goes on |
| `gate4c-drift-repair-copper-4x.mp4` | The same 30 s at four times speed, where the drift is easier to see |
| `gate4c-drift-overlay-dark.png` | Frames at 18.75 s and 30 s blended together, both dark with the spin already stopped. The doubled edges are the drift |
| `gate4c-sheet-copper-z15-msaa.png`, `gate4c-sheet-copper-z25-msaa.png` | The route B sheets for Copper Turquoise, re-shot with the game's 4x MSAA |
| `gate4c-aa-innerarm.png` | A 4x crop from the same view: left as the earlier captures were shot (no anti-aliasing), right with 4x MSAA |
| `gate4c-shadows-highfidelity.png`, `gate4c-shadows-performant.png` | The berth light and a hash mark close up, with the station frozen. Columns: shadows as built, shadows off on marks and lights, shadows off on the whole station |
| `gate4c-berth-light-onoff.png` | The berth light with its lamp mesh shown and hidden |
| `gate4c-innerarm-z3.png` | The inner arm's end light at size 3 |
| `gate4c-berth-housing-options.png` | The berth light with its housing in slate (as built), bone and canvas. Top row at size 2.5, bottom row cropped from size 15 |
| `gate4c-focus.cs`, `gate4c-freeze.cs`, `gate4c-shadows.cs`, `gate4c-svc_toggle.cs`, `gate4c-shot_aa.cs`, `gate4c-drift_clip.cs` | Capture steps added since `gate4b-index.md` |

## Drift

- Each station wanders up to 0.4 u from where it was placed and turns up to 2 degrees about each axis, changing course over about 20 s. The three values are inspector fields on `StationMotion`.
- The drift is seeded from the spawn position, not `UnityEngine.Random`, so gameplay's random stream is untouched and two stations in one sector move differently.
- Measured on the clip: once the spin has stopped (from 18 s), the difference between frames keeps growing. At 18.75 s against 30 s, 4.4% of pixels differ; neighbouring frames differ in 0.01%.

## Hash marks

- Shadows play no part. Close up, with the station frozen, the three shadow settings give the same picture at High Fidelity (4096 shadow map) and at Performant (1024).
- The jagged edges were in the captures. The capture script rendered to a texture without anti-aliasing. The game renders High Fidelity, the standalone default, with 4x MSAA, Performant with 2x, and Balanced with none.
- At size 15 a stroke is one or two pixels wide, so a mark reads as a small scribble even with MSAA.

## Berth light

- Shadows play no part here either.
- The lamp does sit on the metal: hidden, it leaves a dark slate hex block, the housing's end. From the game camera that block falls in the dark shading band and reads as a gap between the green pad and the pale ring.
- With the housing in bone or canvas, the pad reads as set into the metal. That is a material change on the housing renderers only: the hull materials share one mask atlas per station, so no re-UV or rebake.
