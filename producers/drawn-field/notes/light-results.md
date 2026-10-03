The corrected asteroid now uses unlit stone color and real-time shadow darkness. The previous image baked the reference shadow shapes into the material; that interpretation is rejected. This follows the scoped correction in the preceding brief.

Native graphics red/green: fully lit black pixels 4,066 → 0; opposing lights change 15,757 of 29,755 surface pixels between dark and lit. The final run passed 3/3, including both existing ink regressions. The shader property is opt-in and defaults to prior behavior for other materials. ReSharper: zero blockers, 60 report-only/touched-file findings. Combined quality review required only updating obsolete provenance; corrected.

The fixed-mesh moving-light comparison and uniform-illumination diagnostics are exported. A separate image applies the committed scene-lighting rig: it remains darker and less graphic than the reference. The mesh still needs sharper deliberate planes. High Fidelity is verified; the lowest performance pipeline disables main-light shadows. No production rollout, performance claim or art acceptance is implied. The user's agent-7 editor was not touched; captures ran through coordinated graphics batch tests.

Editable Blender source packs the clean texture; exact ImageGen prompts are in art/asteroid-study/README.md. PR #691 remains the exploration branch; no merge performed.
