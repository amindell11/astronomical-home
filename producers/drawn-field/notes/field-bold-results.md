The field drawing now addresses the latest feedback across all ten shapes:

- Existing fitted strokes are 2.2 times wider and use a field-only near-black graphite material.
- The outer contour is 7.5px with a stronger lit-side minimum width.
- Each shape now has 38 selected ridge paths (previously 22), adding intentional lines along quieter ridges and creases.
- The field material palette reduces the yellow-orange cast; the light direction/intensity and dynamic shadow behavior are unchanged.
- The local preview now has side-by-side previous/current detail, full-field and all-ten comparisons. The baseline is the prior complete stylized field pass (74844cd2). Detail views share pose/camera; field captures share seed/framing with small simulation-motion differences.

Final native field test: 20260926-134050-summary.json, 1 passed / 0 failed. Artifacts: results/asteroid-field-study/20260926-204112, 1600x900 stills and 240 frames encoded at 25fps; clip decoding verified. Exact-tree ReSharper: 0 blockers, 67 report-only findings. Combined quality reviews found no required changes.

Committed as ce50d300 and e9b86bf3 on the existing exploration PR #691. The base meshes and relief maps are unchanged in this feedback pass; production adoption/performance remain outside this study.
