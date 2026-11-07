# Editable console, B composition

Open [console.blend](console.blend) in Blender 5.1. The [review page](index.html) presents the main-camera render and an angled construction view beside the [approved interactive composition](../interactive-console-05/preview.png).

This is the [medium-detail Blender pass requested on November 7](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6704791427). The source is a copy of [the saved owner-edited scene](../refinement-04/inputs/owner-edited.blend). That original remains unchanged. The copy under [inputs/owner-source.blend](inputs/owner-source.blend) preserves the starting point.

## What remains editable

The monitor housing, two display surfaces, selector, physical buttons, Launch control, projector, cable runs, and viewport rim are separate parts. Assembly parents allow groups to move together. Bevel modifiers remain available, and cable paths remain curves.

The screens use packed images exported from the accepted HTML prototype. You can move, resize, or replace their screen surfaces and materials in Blender. Individual text and glyph layout remains in the [HTML source](../interactive-console-05/index.html); it is not recreated as separate Blender text objects. The [exported UI files and manifest](inputs/ui/manifest.json) include the main display and the four detail states.

The Vanguard hologram uses the existing real ship geometry. The scene includes a hidden packed reference of the approved composition for alignment. Surface wear and the final ink finish remain for the later detail pass.

This file becomes the modeling source once you edit it. Preserve your edits; do not rerun a scene builder over that saved file.

## Tweaking the scene

The file opens with `Hero camera` active. `Angled construction camera` is also saved. The construction render hides the nebula backdrop and beam volume so the depth is visible; those remain enabled in the saved scene.

Move assembly parents in the Outliner: `Main console`, `Detail display`, `Selector knob`, `Previous button`, `Next button`, `Deck`, `Projector`, `Three broad cooling fins`, `Projection controls`, `Launch control`, `Cable looms`, `Hologram`, and `Viewport`. Rotate `Selector knob` around local Z. Button caps are separate from their sockets. Cable objects retain editable Bezier paths.

The detail display material's Image Texture node can use `detail-hud.png`, `detail-lasers.png`, `detail-missiles.png`, or `detail-dash.png`; all are packed. The camera's disabled background image holds the approved composition for alignment. `99 Source archive (hidden)` retains the original objects. Leave that collection hidden during normal rendering.

The Blender text block `START HERE - editable 3D console` carries the same essential editing notes. The [fresh-load verification](verification.json) checks packed images, source hashes, saved camera settings, assembly movement, selector rotation, and cable curves.

To reproduce this pass in a new filename, load `inputs/owner-source.blend` and run `adapt_console.py -- --output draft-new.blend`, then load that draft and run `finalize_scene.py -- --output console-new.blend`. Both scripts refuse an existing blend output. The finalizer writes `hero.png`, `angled.png`, and `scene-manifest.json` beside itself. Do this in a separate copy if you want to preserve the current review renders.

No game code, game assets, primary-tree files, or shared HUD tools changed. No merge or image-generated paint-over was performed for this pass.
