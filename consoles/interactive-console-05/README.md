# More room for details

The [interactive review](index.html) enlarges the lower display while preserving the selected B main display and the accepted controls/effects from [the previous pass](../interactive-console-04/index.html).

The main display remains 370 by 319 at its existing position. The lower content area grows from 324 by 42 to 364 by 70, about 87 percent more area. It now has an 18-pixel title and two 16-pixel detail lines, using the existing real ship, weapon, and Guard Dash data.

The housing extends down toward the viewer. The accepted original scene supplies everything above the lower housing. A clipped native image component displays only the newly generated lower housing, so the main display and the rest of the scene retain their original geometry. Built-in imagegen produced [that component's source image](lower-housing.png); the [prompts](imagegen-prompt.txt) are preserved.

Hover, click, physical buttons, keyboard selection, knob rotation, and the shared canopy effects retain the previous behavior. No game code, game assets, shared effect files, or Blender files changed.

## Recommended next step

The [owner feedback and next-step recommendation](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6704732902) point toward a camera-matched, medium-detail Blender scene. Start with a new copy of [the saved owner-edited scene](../refinement-04/inputs/owner-edited.blend), preserving that source. Reuse the existing [real Vanguard hologram geometry](../reference-scene-03/inputs/hologram-vanguard.blend).

Match this camera composition first. Keep the main monitor, secondary monitor, selector, buttons, Launch control, projector, cables, and canopy rim separately editable. Put the current UI on separate screen planes and use the approved neutral materials and electronic lighting. A camera render and an oblique construction view should establish the proportions before the full detail pass.

This pass prepares that reference; it does not yet modify the Blender scene.

## Verification

The [preservation check](preservation-checks.json) compares this pass with the previous effects-off render. It found zero changed pixels in the main display and everywhere outside the lower housing. The original background image is byte-identical. The [interaction checks](interaction-checks.json) pass, including detail-text fit and browser errors. See the [full scene](preview.png) and [console crop](console-native.png).
