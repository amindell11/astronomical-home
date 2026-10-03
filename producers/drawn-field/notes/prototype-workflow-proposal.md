## What has been approved?

The user approved the current asteroid style/shape as a prototype and requested a breakdown plus a proposed workflow for future assets. Record this as prototype art-direction approval, not a production rollout or merge instruction. The generated blue-nebula still is a visual reference; it is an AI-edited composite and does not verify native rendering or exact foreground preservation.

## What should be hardened next?

Proposal, not implementation authorization: first preserve the approved asset/recipe/capture baseline and consolidate the scratch authoring passes into one reproducible source-to-output build. Inputs should be the original mesh, a versioned asteroid-style recipe and editable per-asset placements/exclusions. Keep authoring strokes separate from generated ribbons, rebuild derived outputs instead of stacking edits, and emit asset paths plus validation evidence. Keep AI generation for saved base-color/reference inputs outside the deterministic rebuild.

Second, judge the result in native Unity under fixed poses/light sweeps and game-scale motion, then measure the cost of surface, drawing and contour layers. Compare baked interior ink against ribbon geometry before committing to a runtime representation; preserve editable stroke sources either way. Define distant-detail behavior, import settings, UV/tangent checks and collision alignment deliberately. Existing field membership, source-coordinate and pooled-reuse checks remain useful, but do not establish visual fidelity or performance.

Third, test the tool on an unseen asteroid before generalizing it. Share the build/export/preview machinery across future asset families, while keeping rock-specific ridge/crater/hatching rules separate from ship-panel art direction. Nebula/star shader work remains a separate upcoming task.
