# Third slipstream checkpoint

The owner authorized another pass against Vanguard Slipstream C and Bulwark Bow shock C, followed by a game-size still and clip and another stop for review. Claude's density and brightness reading was advisory, not an owner ruling.

Source checkpoint: `92b11d97504c52989bba1be6ab69e262e2f94447` on `task/keystone-trails-dash`. The scope is Vanguard and Bulwark's slipstream look. Hull flashes, ability windows, audio, other ships, and ship geometry are unchanged.

## Why density alone did not solve the gap

Ten body sheets gave more overlap, but Vanguard still collapsed into narrow rippled lines through turns. The strip width followed the hull's right vector even when that vector ran almost along the travelled path. Adjacent strip triangles could face opposite ways.

The wider diagnostic candidate exposed 41 folded quads out of 106 on Vanguard, versus zero out of 133 on Bulwark. These are diagnostic-candidate measurements, not counts attributed to the earlier review2 capture. A fold means the two triangles of one quad have opposite signed areas.

The rung 1 fix gives each ribbon a shared width direction perpendicular to its head-to-tail path. The stored hull attachments still define the curved centerline. Its equal-width translated sections form parallelograms, so each quad's two triangles cannot oppose one another. The change is local to KeystoneSlipstream and adds no dependencies or authoring fields.

## Alternatives checked

- Turning hatch off did not remove the fine ripple. Hatch returned to its existing value.
- The loaded atlas was already RGBA32. Texture compression was not the cause tested here.
- Offline reconstruction with a different normal at each path sample still produced one folded Vanguard quad and two folded Bulwark quads. A shared direction produced none and needed less code.
- Raising fill before correcting the geometry made the folded edges brighter. After the geometry correction, that same high fill merged the layers into opaque white shapes. The final fill sits between that version and a lower-fill candidate that lost too much brightness at game size.

## Layering and timing

The fan combines longer faint sheets and shorter bright sheets. Applying lengthScale after the available-history cap makes those lengths distinct during the first activation; previously the cap made the scales identical there. Quadratic age fade retains more fill through the middle of a sheet's lifetime, then reaches zero at expiry.

Bulwark uses the same body layout scaled to its hull, with two overlapping sheet pairs at its front corners. Its front sheets retain their stronger opacity and longer reach while the outer body layers stay faint.

## Evidence contract

The final footage is native Game View output from KeystoneTrailScenario in TuningSector, at 1920 by 1080 and Performant quality. It retains review2's framing, with Vanguard about 70 pixels long. The first activations coincide; the later activations occur separately through turns. The stills are unmodified PNG frames.

The captured simulation advances by 20 ms per frame. Playback uses 50 fps to show normal simulation speed, excluding PNG capture overhead. The half-resolution GIF samples every second frame and preserves that speed. Native MP4, frame timing, provenance, the capture test summary, editor setup, and mesh diagnostics accompany the stills.

This is a look checkpoint. Full combat validation and owner acceptance remain open.

## Verification

The final live mesh sample contains zero folded quads out of 106 for Vanguard and zero out of 145 for Bulwark. The lower width/path alignment values confirm that the surface now spans across the path. The diagnostic observer only reads mesh state and removes itself after sampling both ships.

Compilation passed all eight assemblies after the code change. The later changes only tune the two materials and prefabs through the Unity Editor. The pre-existing QualitySettings edit is excluded from every source checkpoint.

The final CaptureScenarioPlayModeTests run passed one of one tests. The capture contains 427 frames. The native MP4 encoder verified its decoded frame count. The GIF encoder verified the 214-frame playback duration and subject colour. Capture settings were restored, the leased editor was closed, and the coordinator reported no owners, boot, queue entries, or blockers.

My visual assessment is that the sheets now hold their width through turns and read as a fuller, brighter fan than review2. The repeated edge ripple is absent from the inspected first and later activation frames. The arrangement still looks more orderly and pane-like than the selected C references. That remains a visual review question, not a claimed match or an owner ruling.
