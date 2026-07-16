# Starfield Shader Cleanup

*2026-07-13 · implementation scope for `update stars parallax shader`*

## Scope

- Keep the existing single procedural quad and `StarFieldMaterial` production path.
- Correct random ranges, density semantics, size variation, and parallax-cell consistency.
- Improve appearance with cell jitter, HDR two-color stars, controllable twinkle, and antialiased falloff.
- Remove unused shader inputs and fragment work while remaining a single draw call.
- Expose direct material controls for pattern, depth, appearance, and motion.
- Retire confirmed-unreferenced legacy texture materials/textures and the unused instanced-star prototype.

## Out of scope

- `StarfieldProfile` ScriptableObject.
- `SectorSettings` or environment-service wiring.
- Runtime sector transitions or profile crossfades.
- Changing the skybox/far-star locale design.

## Verification

- Unity import/compile and targeted visual inspection in the agent worktree.
- Repository reference scan for every deleted asset GUID.
- Relevant automated tests selected by the worktree submit flow.
