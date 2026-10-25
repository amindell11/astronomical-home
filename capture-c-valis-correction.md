# Valis lineup colour correction

## What failed

The owner rejected the Valis colours in the neutral lineup published from c2b5c2c9234d5be8b93624ae6260618e14c9c520. The other four ships were accepted as controls. The original 1024-wide image contains 6,598 exact RGB (0,255,255) pixels in Valis. That image remains negative evidence at 4885361f36bb9bcbd222a2936d3cb382dd35a5dc; it is not the corrected result.

## Cause and correction

The neutral presentation scope changed ShaderUtil.allowAsyncCompilation, which controls compilation at the current call site. Native Game View rendering uses EditorSettings.asyncShaderCompilation; it was still true. Unity documents plain cyan as the loading shader shown while a required variant compiles.

The existing presentation scope and journal now snapshot, disable and restore the actual Game View setting. This prevents rendering an unfinished shader while the named view is prepared (fix-ladder rung 1). The recorder, shaders, materials, palettes, lights and camera settings were not changed by this correction. No wait, fallback colour or compile polling was added.

Sources: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorSettings-asyncShaderCompilation.html and https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ShaderUtil-allowAsyncCompilation.html .

## Comparisons that separated the cause

The isolated Valis and five-ship lineup both rendered the authored jade/iris palette through native Game View and direct sRGB Camera.Render after their variants were warm. Authored jade-iris material references and colours matched, and the renderer property block was empty. RenderSettings.sun already named the neutral key; explicitly assigning that key made byte-identical direct images. Source comparison against the approved #1046 capture found no material or DrawnSurface shader changes. No normal-spawn palette initialization was missing.

The fresh five-ship native image differs from the old wrong image at zero pixels for Vanguard, Crimson and Bulwark, 76 pixels for Nightshade, and 7,510 pixels for Valis. No cause is assigned to the 76 Nightshade pixel differences. The native/direct rasterization differences are recorded separately in capture-c-valis-pixel-comparison.json; neither path has loading-cyan pixels in Valis.

ClearCachedData did not evict already compiled variants, so its green comparison was not counted as cold proof. A temporary in-memory copy of the same DrawnSurface source, with only a unique shader name and equivalent local variable, made first use observable without touching an asset. Both variants reported uncompiled before recording. With Game View async=true and call-site async=false, all eight frames contained 8,982 pure-cyan Valis pixels and shader_compiling=1. With Game View async=false, all eight contained zero cyan pixels and shader_compiling=0, with authored jade/iris visible. The PNGs, matched frame rows, source and state reports are preserved in capture-c-valis-diagnosis.tar.gz.

## Regression and its boundary

The durable graphics fixture supplies an inactive Valis clone with copied materials and an uncompiled same-source shader before preparing the view. It checks the actual Game View setting, positive jade pixels and zero loading-cyan pixels in every output PNG at both sizes, exact frame metadata and restoration of the prior presentation and Game View settings. Original asset objects remain unchanged; the other four hulls use their original materials.

A first attempt changed an already rendered material's shader after recording readiness. That artificial mid-render replacement produced a black first frame and correct later frames, which the positive colour assertion rejected. The final fixture supplies its view before preparation, matching the supported caller contract; no wait was added to mask that result. The separate first-use comparison preserves the visual old-setting red and corrected-setting green.

Diagnostic summary 20261024-235735 failed on the original setting with 37,332 cyan pixels in its first 2048-wide frame. The final pre-integration fixture passed 1/1 in 20261025-000352 and checked both 6-frame outputs and restoration. These local diagnostic runs used the c2 boot provenance while the source was being edited; they are not claimed as final exact-source acceptance.

## Final integrated proof

Pending main integration, exact-source warm capture and final quality checks. Local correction commit: 698e5871fa63b60c9cf58486aa731c7d51e0380d. C remains a draft for review; the owner authorized A and B to merge separately, not C.
