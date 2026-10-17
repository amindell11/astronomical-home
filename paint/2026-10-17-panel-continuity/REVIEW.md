# Front stripe across the panel seam

The front stripe now continues from the main deck onto the ram-nose panel using the same world-space stripe definition. Both sides remain identical. The plum gap and white stripe use the same angles and perpendicular widths on both panels; the physical panel joint remains visible.

The change is confined to the BaseRegion mask on the ram nose and its mirrored UV half. Geometry, UVs, transforms, modifiers, material assignments, and other textures are preserved. Source checkpoint: b62e2387. Saved-file reload and ship-check comparisons accompany this review. Existing source findings are not resolved by this paint-only revision.

Four unused image backups were restored from the checkpoint and given fake users after Blender discarded them on save. Their pixels and packed bytes are preserved; no material uses them.
