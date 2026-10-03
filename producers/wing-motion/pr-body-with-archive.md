Valis now uses the approved reangled, tapered model with its charcoal spars restored and front fins removed. Complete wings rest forward, sweep back under forward thrust, and return to the same rest pose for idle or reverse thrust. Mechanical transitions take half a second.

The Blender source keeps 30 individually editable meshes under nested body and flight-surface parents, with live mirrors, paint UVs and profile controls. Unity retains one skinned hull renderer and all eight existing material sections; four rigid wing bones move each wing's panels, spars, tips and pivots together. Keeping one renderer preserves the existing damage-rendering reference and avoids expanding shared hull rendering. Separate braking poses and per-detail joints are unnecessary for the approved motion.

Collider, mounts, handling, engine settings, AI and other ships remain unchanged. Existing local editor settings and the local HullVisuals toggle stay outside the commit. Authoring and capture helpers remain outside the production branch.

Closes #850

## Validation

- Scoped routed PlayMode: 12/12 passed (wing motion, ship presentation, child-component state and presentation footprint).
- Native Game View capture: 1/1 passed; GIF and MP4 decoded and checked for playback duration/frame count.
- Independent baked-mesh check: all 45,384 surface/contour vertices and UVs match the approved swept Blender source, with maximum position error 1.692447E-07 Unity units. Every vertex has the correct rigid bone assignment.
- Combined quality review: no findings or edits. Verified editable source hierarchy, rigid grouping, positions, UVs, existing material references, collider and mounts.
- ReSharper changed-line ratchet passed with zero blocking or report-only touched-file findings.
- Temporary capture source removed; capture and Unity access leases released.

These scoped routed checks are development evidence; the merge gate still owes full-suite proof for the landing tree.

## Scope conservation

The production diff contains the approved Blender source and geometry fingerprint, source README, WingVisuals and its tests/metadata, and Valis's prefab/hull. Serialized changes replace twenty part joints with four wing joints and update the hull's geometry, bind poses and weights. No changes exceed the approved model-and-motion scope.

Vocab: none

## Visual evidence

### Unity wing motion

Native Game View capture driven by the production pilot command: rest → forward thrust → rest → reverse thrust → rest.

![Updated Unity wing motion](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/Valis-updated-wing-motion-unity.gif)

[MP4](https://github.com/amindell11/astronomical-home/blob/fceaa62bd3600df147fb513418839de80e05dfbd/v02/Valis-updated-wing-motion-unity.mp4)

<details>
<summary>Approved shape and Unity endpoint views</summary>

Approved Blender profile with restored charcoal spars, shown from above, a low angle and the side:

![Approved Blender shape](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/Valis-approved-profile.png)

Unity forward rest pose:

![Unity rest](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/Unity-rest.png)

Unity swept flight pose:

![Unity swept](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/Unity-swept.png)

</details>

The Unity clip isolates presentation and holds the ship at the origin; it does not establish gameplay or handling changes.

<details>
<summary>Blender authoring comparisons and previous motion pass</summary>

These are authoring-stage comparisons; the approved result and current Unity motion are shown above.

Initial tapered profile: low angle.

![Initial tapered profile: low angle](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/after-low.png)

Initial tapered profile: side view.

![Initial tapered profile: side view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/after-side.png)

Initial tapered profile: top view.

![Initial tapered profile: top view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/after-top.png)

Restored parts aligned to the reangled wings: low angle.

![Restored parts aligned to the reangled wings: low angle](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/aligned-low.png)

Restored parts aligned to the reangled wings: side view.

![Restored parts aligned to the reangled wings: side view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/aligned-side.png)

Restored parts aligned to the reangled wings: top view.

![Restored parts aligned to the reangled wings: top view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/aligned-top.png)

Original cross-section: low angle.

![Original cross-section: low angle](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/before-low.png)

Original cross-section: side view.

![Original cross-section: side view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/before-side.png)

Original cross-section: top view.

![Original cross-section: top view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/before-top.png)

Earlier combined assembly preview: low angle.

![Earlier combined assembly preview: low angle](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/grouped-low.png)

Earlier combined assembly preview: view board.

![Earlier combined assembly preview: view board](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/grouped-preview.png)

Earlier combined assembly preview: side view.

![Earlier combined assembly preview: side view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/grouped-side.png)

Earlier combined assembly preview: top view.

![Earlier combined assembly preview: top view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/grouped-top.png)

Grouping diff.

![Grouping diff](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/grouping-diff.png)

Intermediate shape preview: low angle.

![Intermediate shape preview: low angle](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/latest-low.png)

Intermediate shape preview: side view.

![Intermediate shape preview: side view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/latest-side.png)

Intermediate shape preview: top view.

![Intermediate shape preview: top view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/latest-top.png)

Approved individually editable parts: low angle.

![Approved individually editable parts: low angle](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/nested-low.png)

Approved individually editable parts: side view.

![Approved individually editable parts: side view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/nested-side.png)

Approved individually editable parts: top view.

![Approved individually editable parts: top view](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/nested-top.png)

Profile comparison.

![Profile comparison](https://media.githubusercontent.com/media/amindell11/astronomical-home/fceaa62bd3600df147fb513418839de80e05dfbd/v02/history/profile-comparison.png)

Previous Blender animation pass.

![Previous Blender animation pass](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Valis-wing-motion-v01.gif)

Previous Unity animation pass.

![Previous Unity animation pass](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Valis-wing-motion-unity.gif)

Previous Unity forward pose.

![Previous Unity forward pose](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Unity-forward.png)

Previous Unity reverse pose.

![Previous Unity reverse pose](https://media.githubusercontent.com/media/amindell11/astronomical-home/c695e2eaf954dc6d45aec55834ee589ec640354c/Unity-reverse.png)

[Previous Unity MP4](https://github.com/amindell11/astronomical-home/blob/c695e2eaf954dc6d45aec55834ee589ec640354c/Valis-wing-motion-unity.mp4)

</details>

## Authoring archive

All available Valis concepts and editable authoring revisions are retained separately from main on `evidence/valis-geometry`, pinned at `ce7e26719515715f334fad5b6fa60deb41ef47ce`:

- [Original concept images and turnaround](https://github.com/amindell11/astronomical-home/tree/ce7e26719515715f334fad5b6fa60deb41ef47ce/history/concepts).
- [Complete geometry and concept history](https://github.com/amindell11/astronomical-home/tree/ce7e26719515715f334fad5b6fa60deb41ef47ce/history).
- [Wing, profile and paint authoring snapshot](https://github.com/amindell11/astronomical-home/tree/ce7e26719515715f334fad5b6fa60deb41ef47ce/history/2026-10-02-wing-motion-and-profiles), including the approved nested editable source, intermediate Blender revisions, both animation passes, paint/palette concepts, masks, helpers and Unity previews. Its manifest records 131 source files whose committed contents/LFS hashes were checked against their original bytes.
- [Additional paint authoring tools and tests](https://github.com/amindell11/astronomical-home/tree/2d433ca2e6790ab6f767198a59f7d6d3657405ad/art/ships/valis) on `codex/valis-paint-authoring-archive`.

Raw capture frame intermediates, build/test logs, handoffs and unrelated editor backups are excluded; concept images, encoded clips and editable source snapshots are preserved. Earlier archive history remains intact.