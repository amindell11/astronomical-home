# Bulwark paint cleanup — 2026-10-17

The owner's final live paint was preserved in commit 6f6ea672 before cleanup. The earlier saved blend contained an older packed texture; the live pixel buffer was explicitly saved and packed before replacement.

The final 2048-pixel BaseRegion mask uses solid color regions, straight polygon boundaries, and consistent-width main bands. Existing asymmetric coverage is retained. No geometry, UVs, modifiers, transforms, material assignments, or other image layers changed. No wear was added.

Two image-generation previews were rejected. The owner explicitly authorized scripted cleanup; clean_precise.py traces the original mask, removes brush scallops and small pinholes, and regularizes selected stripe boundaries. Supersampling preserves antialiased edges. The original owner-mask.png and final precise-BaseRegion.png accompany the scripts.

Validation: every scene object matches the pre-edit snapshot; all other image pixels and packed bytes match. A fresh Blender reload matches every post-edit object, image pixel hash, and packed image. ship_check fingerprints and its existing 51 findings are unchanged (source validation remains failing on those pre-existing geometry findings). Six saved-source views and a small-scale preview were inspected. In-game integration and renewed owner geometry/UV validation remain outside this paint-only change.
