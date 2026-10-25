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

The normal main integration produced 5b03470bffda030750f702a12abcc75ae7c90f15. All 17 reviewed C files are byte-identical to the reviewed correction at 698e5871fa63b60c9cf58486aa731c7d51e0380d. Ship visual assets, ship prefabs and shaders did not change during that integration. The resident editor boot received 5b03470b as its explicit capture commit.

The exact-source warm neutral fixture passed 1/1 in summary 20261025-003722. Both 2048x512 and 1024x256 outputs contain six PNG/metadata pairs and completed manifests with 5b03470b provenance. Every frame has zero pure-cyan Valis pixels and positive authored jade content (12,116 pixels at 2048; 3,098 at 1024). The test checked preparation ownership, restoration of the original async setting and all other presentation settings, and the prior Game View after repaint. The focused recovery suite passed 4/4 in 20261025-003747. Offline checks independently confirmed all filenames, contiguous frame IDs, increasing finite unscaled timestamps and decoded dimensions.

The corrected final 1024-wide output leaves Vanguard, Crimson and Bulwark pixel-identical to the earlier image; Nightshade differs at the same measured 76 pixels, without assigning a cause. The corrected Valis cell differs at 7,510 pixels. No art assets changed.

The independent combined feedback review cleared 698e5871f with no actionable correctness, scope, comment or Unity-convention findings. It inspected the first-use comparison and corrected colours. Source identity carries that review across the main integration. Local ReSharper at 5b03470b passed against origin/main: 45 changed files, zero blocking findings and two inherited HarnessHost report-only findings. The subsequent normal merge of aligned B 97ae686d3c4099bf622adf0fde78950d2b383916 produced final PR head 192dd22967cf79b7e4fc4182d2d5c1f6f0145a23. That merge changes only A's evidence publisher and its test. The entire Unity project tree is identical to the captured 5b source (a11c556bee286345809aa4e3ec64dec9228dc103), so no graphics repeat is claimed or needed. All reviewed C files remain identical; the C-only review diff is 17 files, +621/-18 against B. A and B remain unmerged while the owner reviews their concrete updated heads.

The capture lane restored Enter Play Mode Options, the coordinator closed its editor PID 61292 and released the lease, and a fresh status showed no remaining owner, boot lease, queue or blocker. Verified generated quality-profile/analytics changes were restored only after the editor closed. On 2026-10-25 the owner instructed, "And merge the last one too it's fixed". This authorizes C at final head 192dd22967cf79b7e4fc4182d2d5c1f6f0145a23 after A and B, subject to the normal pool merge checks. C has not merged yet.
