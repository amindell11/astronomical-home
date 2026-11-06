# Editable 3D console

Open [reference-console.blend](reference-console.blend) in Blender 5.1. The [review page](index.html) compares the camera render, an oblique construction view and the owner's attached reference.

This is the medium-detail modeling pass requested on [November 5](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6679675163). The monitor, deck, electronics, projector, control and cables are separate 3D parts. Screen text is editable. The scene uses the real Vanguard geometry as the hologram.

## Adjust the scene

The file opens at the main camera. Press Numpad 0 to leave or return to camera view. Select an assembly's parent in the Outliner to move it together. `Main console` includes its screen and screen light. `Deck` holds the equipment, cables, projector and control. `Projector` includes its beam, lights and hologram; `Hologram` can also move independently. Each assembly has a local pivot.

The hardware keeps its bevel modifiers. Cable paths are editable curves. Select a screen text object and enter Edit Mode to change its text. The camera and lights are in the first collection. A hidden packed reference image is in the last collection.

Save your edits directly in the Blender file. Once you edit it, that file is the source for further work. The builder refuses to overwrite an existing Blender file.

A fresh Blender load verified the saved camera, all packed images and both fonts, with no missing resources. The camera and oblique views were rendered from the scene.

## What this pass contains

The camera follows the reference's large left display and lower equipment deck, with the hologram above a separate projector. Materials and lighting make the volumes readable while preserving the dim control area and warm electronic light. Surface wear and the final ink finish remain for the detail pass.

The simple screen labels and LAUNCH wording reproduce the reference as temporary artwork. They do not revise the settled ship spec sheet, fitted parts or Call behavior. This artifact is for adjusting the modeled hardware and composition.

The attached image is preserved unchanged in [reference/owner-target.png](reference/owner-target.png). It corresponds to the earlier [lighting candidate C](../lighting-01/electronic-light-contrast-nb2.jpg). The background reuses the existing `art/ships/vanguard/drawn-study/NebulaBackground-v2.png` artwork. Fonts come from the combat session's shared Plex files. The scene packs its images and fonts.

The Vanguard geometry was read from `art/ships/vanguard/Vanguard.blend` and copied into this evidence artifact. The game source remains unchanged. The hologram asset and its build notes are under [inputs/](inputs/).

No image generation, paint-over, primary-tree edits or game merges were performed for this pass.
