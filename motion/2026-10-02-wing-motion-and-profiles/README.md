# Valis wing, profile and paint authoring archive

The approved model from PR #852 is in approved-source/. It retains individually editable nested parts, live mirrors, profile shape keys, painted UVs and the restored charcoal trailing spars. The archive is a historical snapshot; it does not overwrite the artist source.

valis-wing-motion/ preserves both animation passes, original and intermediate Blender revisions, profile/nesting/alignment helpers, geometry exports, checks, previews and final Unity clips. Earlier profile and assembly experiments are historical, not the approved production model.

valis-texture-review/ and valis-texturing-restart/ preserve the available palette/paint concepts, paint masks, base models and authoring helpers. Original concept images remain at history/concepts/ in this branch. archive-manifest.json records source paths, archived paths, sizes and SHA-256 hashes; every copy was checked against its source bytes.

Raw per-frame capture intermediates, build/test logs, handoff text, PR drafts and unrelated local editor backups are excluded. Encoded clips and concept/preview images are retained.

Earlier geometry stages remain in the existing history/ folders of evidence/valis-geometry. Additional paint authoring tools/tests remain at codex/valis-paint-authoring-archive, commit 2d433ca2e6790ab6f767198a59f7d6d3657405ad. Published motion evidence remains on evidence/valis-wings, commit fceaa62bd3600df147fb513418839de80e05dfbd. These archive/evidence branches are retained separately from main.
