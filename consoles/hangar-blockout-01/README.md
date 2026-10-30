# Hangar dispatch blockout

Open [hangar-blockout.blend](hangar-blockout.blend) in Blender 5.1. It is the editable source. The [review page](index.html) shows the same camera and the captured flight in motion.

This follows the [October 30 hangar ruling](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6517317847) and the owner's direct request for Blender. The player stands at a station dispatch terminal. The real ship remains off screen. The hologram sits above a projector on the shared worktop, and Call is a physical button. The grey hardware establishes placement and scale, not finished art or the final lighting.

## How to edit it

- `TERMINAL` moves the console assembly. `SCREEN` moves or tilts the monitor, including its editable text.
- `PROJECTOR` moves the aperture. `HOLOGRAM` moves or scales the captured flight. Its `Opacity` custom property controls transparency.
- `CALL` moves the button assembly. The cap is a separate mesh.
- `CAMERA` is the only camera. Adjust it directly and press F12 to render.
- The timeline is 180 frames at 30 fps. Frame 46 shows Valis, 106 shows Nightshade, and 166 shows Valis returned. Markers identify the entry and arrow presses.
- Keep `inputs/preview-flight/` beside the blend file. Fonts and still references are packed; the flight sequence uses relative paths.

The source opens at frame 46. The grey meshes have editable bevel modifiers and separate materials. Screen text stays as native text, using the combat session's shared Plex Sans and Plex Mono files. These are provisional menu choices, not a new font selection. The hidden reference collection contains finish B and lighting C for the later paint-over.

After the owner edits the blend file, those edits become the source of record. `build_blockout.py` refuses to overwrite an existing source. Do not replace the edited file by rebuilding it.

## Which parts are real?

The 180 unchanged RGB frames come from `agent-1/results/hangar-carousel/single-frames`, captured from the actual Unity `HangarPreviewStage` render texture for [PR 1076](https://github.com/amindell11/astronomical-home/pull/1076). They show Valis entering, Next selecting Nightshade at two seconds, and Previous returning to Valis at four seconds. The capture fixture and recorded selections are preserved in `inputs/`. This reuses the shipped motion's review capture; it is not a new Unity recording.

The Blender material keys out that capture's uniform background and applies transparency. The ship is a captured image plane for this blockout, not newly generated art or a replacement 3D ship model. The finished hologram shader and Call arrival transition are not implemented here.

`inputs/hangar-data.json` records the current screen and prefab sources at `5f6894e831a63be2ec58b9342119f13ef6342540`. The screen has Ship, Primary and Secondary rows. Valis inherits Lasers and Missiles from ShipBase. Nightshade overrides these to TurboLasers and Missiles. The text changes at the captured selection frames. Engines and shields are ship-bound, so their old selectors are absent. Stats stay blank in this idle state, as `HangarScreen.Refresh` specifies when no row is hovered. `CALL` is the ruling's provisional replacement for Launch.

The sky is an empty 16:9 crop of the actual hangar capture in `inputs/real-hangar-reference.png`, dimmed for background context. It contains no second ship. The dock and control housing are new flat grey Blender geometry.

## What waits for approval?

The owner reviews or adjusts this blockout first. No image generation ran for this stage. After explicit approval, the approved Blender render becomes the edit source for one paint-over round with `art/tools/imagegen/imagegen.py`. Candidate C supplies lighting, B supplies finish, and the brief's other references retain their stated roles. The existing nb2/pro comparison method applies to that single round.

No game code, game assets, shared tuner files or primary-tree files changed. Nothing is proposed for a game-code merge.
