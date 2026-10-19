# Vanguard: clean canopy rim

Follow-up to #1016 and the accepted [independent paint pass](../asymmetric/README.md). The owner requested removal of the bright paint flecks at both ends of the orange canopy rim, suggesting the paint texture should not run through that band.

Work checkpoint: `8d188a183d89b796b1ce10194698b9bd7d28f6b0`.

The rim is an authored HullAccent marking on the cockpit surface, so it inherited that surface's generated Shadow and Light layers. An owner edit clears both layers within the existing rim marking. This uses the stage-6 mask-edit workflow and retains the generated baseline, so ordinary rebakes preserve the cleanup without changing the shared painter or stripping texture from the surrounding cockpit hull.

The selection contains 33,728 texels. This pass changes 32,749 Shadow texels and 11,902 Light texels. Every pixel outside the selection is identical to the preceding accepted version, including the nose correction. Geometry, UVs, palette, authored regions, Ink, Emission and all baselines are unchanged.

## Verification

- A normal full 4096-pixel canonical painter run passes and reproduces both edited masks byte for byte.
- `checks/verification.json` verifies zero Shadow and Light throughout the selected rim, exact preservation outside it, unchanged baselines, and matching edited-texel counts.
- Source SHA256 remains `e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031`.
- `preview/top.png` and `preview/hero.png` are the corrected Blender views, both visually inspected; `preview/before-top.png` is the preceding accepted pass.
- This is a two-mask art edit. No code changes or new tests; the Size-S quality-agent exception applies. No Unity launch: the owner deferred the Unity lineup and waived the flight check.
- ReSharper skipped because no C# changed. `git diff --check` passes.

## Files

`implementation/` contains only the changed source masks. Apply these after the earlier asymmetric implementation; retain that version's unchanged baselines. `runtime-bundle/Vanguard/` is the generated copy-ready bundle, not an installed Unity change. `producers/` contains the exact scratch scripts and `checks/` the selection and reports.

The first scratch audit stopped after writing the masks because its allowed-path comparison mixed Windows backslashes with forward slashes. The producer now uses POSIX-relative paths. `recover_rim_audit.py` independently checked the saved pre-edit layers against Git's hydrated objects and verified the result before the canonical rebake; it made no image edits.
