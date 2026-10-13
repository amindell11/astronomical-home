# Bulwark — protected twin engines, blockout round 4

Stage: blockout. Proportions are still under owner review; one neutral gray material, no surface-detail pass.

## Tail interpretation

The owner clarified that the rear pieces are thick armored shielding around protected engines. The old solid, tapering tips misread that intent. This revision widens the existing aft cross-sections, opens a deep rear-facing cavity in each shell, and nests a simple engine inside. The central fork stays open.

The original tail-tip mesh was edited in place. Its live Mirror and owner-authored transform remain. The new engine pair is a separate mesh with a live Mirror, parented to the tail armor so it follows adjustments. Both armor and engine geometry remain a coarse blockout.

## Editing

The seven existing groups are preserved. Expand Tail tips, then 05 Tail tips, to select 08 Nested engines independently. Editing one side mirrors to the other. The cockpit retains its editable rounded-front teardrop cage.

The owner's saved checkpoint is b4442d98. Six pre-existing mesh parts are unchanged by this round: ram nose, upper hull, lower hull, tail booms, cockpit, and side insets. Their vertex positions, face connectivity and transforms were checked before saving.

## Validation

Source commit: 05af43c41639ccd15f44cf36d85c19b5ead7698a.
Source SHA256: 0f5cad8ef5435d6d958b5a2446e9175239a8778acccf03b0cf46d2b06e4210b3.

- Orthographic and game-scale renders passed and verified an unchanged source.
- Eight live Mirrors; tail armor and engine meshes are closed with outward normals.
- Source check reports three existing unapplied scales from the owner's proportion edits: upper hull, lower hull, and tail booms. These were preserved, not applied or silently exempted. No other source findings.
- ReSharper skipped automatically: no changed C#. No Unity integration or tests.
- Blockout approval and the three source findings remain open before this stage can be completed.

[Rear quarter](rear-quarter.png) · [Rear](rear.png) · [Top](top-studio.png) · [Side](side-studio.png) · [Source check](ship_check.json) · [Edit audit](edit-audit.json)

Blender blockout previews only.
