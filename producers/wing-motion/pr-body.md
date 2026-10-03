Valis now responds to forward and reverse thrust with the approved mechanical wing poses. Main wings rest near reverse at idle (8% of forward travel), while fins rest halfway between the endpoints; transitions take 0.5 seconds. Hangar previews hold the idle pose until a flight view binds the rig.

Rigid weights animate the existing combined hull through one SkinnedMeshRenderer, preserving the painted surface and contour material sections and the existing HullVisuals renderer reference. The source frame retains Blender's authored width scale so the Unity forward pose matches the user's reference. Collider, weapon mounts, movement, engine settings, AI, other ship prefabs, and the native paint source are unchanged.

Splitting the hull into separate renderers would require expanding damage rendering, so this retains one renderer. Flexible wings and an Animator state machine were unnecessary for the approved rigid poses. Authoring and capture helpers remain outside the production branch.

Closes #850

## Validation

- Routed PlayMode: 12/12 passed (two new wing tests plus ship presentation, child state, and presentation footprint checks).
- Corrected native Unity capture: 1/1 passed; GIF and MP4 decoded and checked for playback duration/frame count.
- Independent baked-mesh comparison: all 14,880 surface and contour vertices match the user's Blender forward pose; maximum error 6.343066E-07 Unity units.
- Combined quality review confirmed byte-identical positions, normals, tangents, UVs, index buffer, submesh records, and all eight material references. No findings or edits.
- ReSharper changed-line ratchet passed, with zero blocking or report-only touched-file findings.
- Editor and capture leases released; temporary capture source and editor profile changes removed.

These scoped routed checks are development evidence; the merge gate still owes full-suite proof for the landing tree.

## Scope conservation

The production diff contains only WingVisuals, its PlayMode tests, their Unity metadata, and Valis's prefab/hull skin data. Most serialized lines are the twenty bone transforms, joint settings, bindposes, and vertex weight stream. No changes exceed the approved visual-motion scope.

Vocab: none

## Visual evidence

### Unity wing motion

Controlled Game View capture: idle → forward thrust → idle → reverse thrust → idle, driven by the production thrust command.

![Unity Valis wing motion](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Valis-wing-motion-unity.gif)

[MP4](https://github.com/amindell11/astronomical-home/blob/c695e2eaf954dc6d45aec55834ee589ec640354c/Valis-wing-motion-unity.mp4)

<details>
<summary>Approved Blender reference and Unity endpoint stills</summary>

The approved Blender first pass, before Unity implementation:

![Approved Blender motion](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Valis-wing-motion-v01.gif)

Unity forward thrust:

![Unity forward](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Unity-forward.png)

Unity reverse thrust:

![Unity reverse](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Unity-reverse.png)

</details>

The Unity clip isolates presentation and holds the ship at the origin; it does not establish gameplay or handling changes.

