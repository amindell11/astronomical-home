## What supersedes the first motion pass?

The user approved the revised Blender shape and editable hierarchy on October 2, 2026. The revised source removes the front fins, reangles the wings, thins and tapers the wings and aft prongs, and restores the charcoal trailing spars. Nested parents retain individual editable meshes, live mirrors, paint UVs, and profile controls. The currently authored model is the swept flight pose.

The Unity follow-up updates that source and its painted hull, replaces twenty part joints with four complete wing joints, and uses two poses: forward rest and swept forward-thrust flight. No separate braking pose remains. Idle and reverse thrust use the same forward rest pose; the existing half-second mechanical transition stays. The swing remains approximately twenty degrees from the earlier approved pass.

Keep one skinned hull renderer and the existing palette, contour, damage-rendering reference, and iris bloom. Preserve collider, mounts, engine, movement, AI, and other ships. Keep authoring/capture helpers outside the production branch. Preserve existing local editor changes separately from the PR.

Acceptance: Unity's swept mesh and UVs match the approved evaluated Blender source; each complete wing moves rigidly, fixed body vertices remain fixed, hangar previews rest forward, and reverse thrust reaches the same pose as idle. Verify scoped PlayMode tests and a native Game View clip. Update PR #852 without merging it.
