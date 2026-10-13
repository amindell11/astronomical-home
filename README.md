## What failed and why

After #998, the hangar ship preview is too large and all ten option arrows stop receiving mouse clicks at a 2560x1408 Game View. The owner also reports GPU saturation. This diagnosis isolates the click failure and resource regression; no gameplay code or prefab changes have been made.

## Did the two size changes compound?

Yes. #967 (`78ba4d38`) changed `ShipPreview` sizeDelta from 880x880 to 1200x1200. #998 (`dc31a11d`) kept 1200 and changed localScale from 1 to 1.44. The resulting reference width is 1728, or 1.9636 times the pre-#967 width. At the measured canvas scale of 1.3333, this is 2304 screen pixels. The RawImage's measured screen rectangle extends from x=568 to x=2872.

## Why do the option buttons appear frozen?

`ShipPreview` is a decorative RawImage with raycastTarget=true, drawn above the option controls. Its enlarged rectangle covers all ten arrow centers (x approximately 699 and 771). Actual EventSystem.RaycastAll calls hit ShipPreview first instead of the intended Button. The buttons themselves remain interactable.

Controlled checks on the same live prefab:

- Shipped scale and hit settings: **RED**, 10/10 arrows blocked by ShipPreview; Launch remains reachable.
- Change only preview raycastTarget to false: **GREEN**, 10/10 arrows reachable; scale and texture remain unchanged.
- Restore raycastTarget=true and change only localScale to 1: **GREEN**, 10/10 arrows reachable.
- Restore shipped settings: **RED**, 10/10 arrows blocked again.
- Disable only the preview camera with the shipped rectangle and hit settings: still **RED**, 10/10 arrows blocked. Rendering stalls are not required to reproduce the input defect.

## What changed the rendering cost?

#998 replaces the 768-square, 1-sample RenderTexture with a screen-sized square target and 8x MSAA. At 2560 wide, the compounded UI scale drives a 2304-square target: 9x the pixels and 72x the nominal sample count of the old target. This is a workload comparison, not a 72x GPU-time claim.

Measured with UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong on created targets, using the live preview camera and the same ship pose:

| Target | MSAA | Reported bytes | MiB |
|---|---:|---:|---:|
| 768x768, old settings | 1 | 7,078,440 | 6.75 |
| 768x768 | 8 | 58,982,952 | 56.25 |
| 1601x1601, scale=1 at this view | 8 | 256,320,652 | 244.45 |
| 2304x2304, shipped at this view | 8 | 530,842,152 | 506.25 |
| 2560x2560, cap | 8 | 655,360,552 | 625.00 |

The slightly different object overhead in the baseline frame capture does not change these rounded values. The measured color format is R8G8B8A8_SRGB and depth/stencil is D32_SFloat_S8_UInt. The cap is not a 350 MB allocation budget on this device.

The live target had one instance across 90 sampled frames, so a repeated resize/allocation loop was not observed. Source inspection of `HangarPreviewStage.Resize` agrees: unchanged width returns immediately. The enlarged continuously rendered target, high sample count, and VRAM pressure are the confirmed resource regression.

Limits: machine-wide GPU utilization was already near 100% around startup, and other GPU applications were active. This does not prove #998 alone explains the entire 100% reading. Camera-render/readback timings are recorded in raw evidence, but are not a clean GPU-only benchmark: they include CPU and synchronization, active-app contention, and allocation/cache effects while cycling targets. Do not treat them as a frame-rate promise or a precise speedup. The profile was Performant via the coordinator's LowMemory editor profile; the defect reproduces even there.

## Why did the existing tests stay green?

`HangarOptionRowsPlayModeTests.Show` removes the RawImage before showing the screen, and its Next/Previous helpers directly invoke Button.onClick. `HangarInputGatePlayModeTests` invokes Launch.onClick directly. These cover row behavior and player input parking, not pointer hit routing through the visible preview. The framing tests assert 3D framing, not UI rectangle coverage or GPU allocation. None of these establishes a rendering budget.

## What fix should be scoped next?

Restore the pre-#998 preview scale (keep #967's 1200-unit size) and disable pointer hits on the decorative preview. This removes the unintended enlargement and stops the image from blocking controls independently of layout. Removing the extra scale also roughly halves this view's preview-target allocation, but leaves the expensive 8x setting in place.

A graphics test should raycast the actual visible prefab at the arrow centers. A resource budget or lower MSAA/resolution policy needs an owner decision because #998 explicitly records an 8x visual-quality choice; silently lowering it would change that choice. No structural UI rewrite is indicated by this diagnosis.

## Reproduction and preserved evidence

Diagnosis tree: main `eddfa101722dd151deb01e9035b352a313745f56`, pooled worktree agent-2, Unity 6000.1.8f1 / Direct3D 11, RTX 2000 Ada Generation Laptop GPU. Open Assets/Scenes/InitScene.unity, enter Play Mode, leave the hangar open at 2560x1408, and run:

```powershell
unity command run_script --file <absolute-path>/hangar_probe.cs --entry HangarProbe.Main --project-path <worktree>/src/Asteroids3D --format json
```

The returned `data.result.result.verdict` is RED and blocked=10 on the shipped tree. `hangar_set_raycast.cs` and `hangar_set_scale.cs` provide the independent green controls. `hangar_resource_snapshot.cs` and `hangar_render_cost.cs` record the actual created-target allocations. The probes and raw reports accompany this README on the evidence/hangar-regression branch. The diagnosis issue pins its evidence commit.

## Exit criterion

The real hangar's option arrows receive pointer hits with the preview visible; the preview returns to the intended size; an explicitly chosen preview rendering budget is verified with graphics enabled. Diagnosis is complete; implementation scope is awaiting owner confirmation.
