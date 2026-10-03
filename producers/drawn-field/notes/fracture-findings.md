## What changed after the more specific asteroid reference?

PR #691 now has a separate fractured asteroid source and albedo. The earlier quiet mauve stone missed the intended contrast and irregular silhouette. The new version uses an asymmetric taper, nine sculpted depressions and raised lips, slate/olive paint with selected black fractures, and cool-shadow/amber-facing material colors. The texture was generated from the new user-provided style reference; editable packed Blender source and the exact prompt are committed.

Angular Boolean cuts were tried and removed because their shaded caps read as punched polygonal holes. The retained sculpt is more continuous. It remains softer than the reference, and spherical UV compression near the tip plus insufficiently deliberate brush placement are still visible. This is an exploration candidate, not an accepted match or production rollout; visual mesh and collider still differ.

Native Unity evidence: 144 paired poses under an explicitly selected fixed sun; actual scene capture `20260924-232830-DrawnAsteroidPaintScenario`, with 900 trace rows and 899 frames at 50 Hz. The routed scenario passed rotation, both bank directions and damage assertions (`20260924-232810-summary.json`). Gameplay frame 100 contains zero magenta proxy pixels. Latest local asset commit: `3f8b433d`.

The local review includes the new reference, current and previous turntables, the actual-scene clip and an archived quiet study. It is stored outside the recyclable worktree. The existing gameplay light illuminates a wider amber area than the reference rim, so the scene and controlled study are presented separately.
