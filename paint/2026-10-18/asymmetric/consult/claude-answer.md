## Vanguard paint asymmetry: one proposal for the owner

I didn't edit, bake or run anything. I looked at the four images and read `ship_paint.py` (the shared painter), `vanguard-paint.json` and `valis-history.md`. Items marked **[verified]** come from the code or images. Items marked **[inference]** are predictions that only a bake can confirm.

### 1. Verdict on the current design: misfit

The owner asked for roughly symmetric shapes with independently painted sides. The current paint step can only produce exact mirror images. Two separate mechanisms cause this.

- **Brush texture is mirrored.** It is sampled at `(|x|, y, z)` **[verified]**. Where the fold crosses the centreline it leaves an inkblot pattern. In `vanguard-current-top.png` you can see it on the orange tail stripe (around 480–560, 620–700): a mirrored blob pattern. The nose cap has a matching symmetric dark V. This is the "triangular artifact at the ship centres" the owner described on 2026-10-15.
- **Broad shadow/light sweeps are mirrored.** The sweep runs along `|x|` with `weight_x = 0.65`. For a cap that crosses the centre, `sweep_x` becomes `(0, max|x|)`, so the sweep starts at the centreline and runs outward on both sides **[verified, `ship_paint.py:225-227`]**. Paired left/right caps always get identical shading, and centre caps get a chevron. Side pods and wing roots in the image are mirror-identical down to the dark streaks.
- **Valis does it differently [observed in images, my interpretation].** The big light/dark split on Valis's fuselage runs diagonally across the centreline instead of folding around it. The wing panels carry about the same amount of dark on each side, but the strokes sit in different places. So the shapes are balanced in silhouette and value, but the strokes are not mirrored. Vanguard has the balance but none of the independence.
- **Switching `abs(x)` to signed x is necessary but not enough as written.**
  - The brush would be fixed completely: it becomes continuous through x = 0, with no fold and no seam **[inference, high confidence]**.
  - The sweep would stop mirroring, but `weight_x = 0.65` mixes normalised x and y coordinates. The effective stroke direction then depends on each cap's width-to-height ratio. And because the sweep is mostly sideways, the left pod would go dark at its outer silhouette while the right pod goes dark at its inner edge. That reads as a lopsided ship **[inference]**.
- **The facing ramp is fine.** It shades by how steeply a surface faces (`|nz|`), so it stays symmetric on symmetric geometry. That is the "roughly symmetric shapes" part and should be kept.

### 2. The one alternative: signed coordinates plus one ship-wide light direction

**Root cause:** the painter's coordinate system is folded (`abs(x)`). That was correct for Crimson, whose mirrored parts share UVs. It is wrong for any ship whose PaintUV has independent left/right halves.

**Changes, all in `build_material` and `Part._caps`:**

1. **New setting** `paint.json: "paint_space": "folded" | "signed"`. Leaving it out means `folded`, so Crimson stays byte-identical. Vanguard sets `signed`.
2. **Brush:** in signed mode, sample at `(x, y, z)` instead of the folded position. Nothing else changes.
3. **Sweep:** replace `weight_x` with `"angle_deg"` (one direction for the whole ship, measured from the nose axis).
   - `_caps` stores each cap's min/max of the dot product of `(x, y)` with that direction.
   - Per cap, `s = (dot((x, y), d) − lo) / (hi − lo)`, plus the existing brush jitter.
   - `shadow_coverage` 0.33, `light_above` 0.66, `jitter` 0.5 and depth 0.39 stay as they are.
   - **Start at `angle_deg ≈ 30`.** That keeps the stroke mostly along the hull, so neither side reads as "the dark side". Tune between 20 and 40 by eye.
4. **Facing ramp, accents, ink and canopy:** unchanged. Accents keep using `|x|` because they are authored as mirrored marks.

**How this avoids the problems the brief raised:**

- **No centre seam.** Every input is continuous through x = 0. A centre cap gets a single diagonal stroke instead of a chevron.
- **No whole-side imbalance.** Each cap is still normalised on its own, so every cap gets about 33% dark and 34% light whichever side it's on. The global direction only decides *where* inside each cap the dark third falls. Left/right twins therefore get the same amount of shadow in different places, which matches what Valis does.
- **Strokes stay broad and purposeful.** They are still one stroke per plate, jittered by the brush, which now differs between sides.

**Guard against wrong use (fix-ladder rung 2, earliest deterministic failure):**

- Signed mode on a ship with shared mirrored UVs would quietly bake whichever side happened to land last into the shared pixels. So when `signed` is set, the painter should rasterize which side each face is on into the PaintUV, at bake resolution, and refuse to run if any pixel is used by both an x > 0 face and an x < 0 face.
- Rung 1 (making the mistake impossible to express) would mean deriving the mode from the UVs automatically. I'd rather not: a later re-UV of Crimson would then silently change its look.

**Trade-offs and migration cost:**

- Roughly 30 changed lines, one new setting, plus the overlap check.
- No new painter, no randomness, deterministic output.
- Crimson is unaffected.
- Vanguard's Shadow/Light masks regenerate. Source, UV, artwork, Ink and Emission are not touched by any of these changes.
- Cost: every cap uses the same lighting angle. If that ever looks mechanical, the next step would be tuning, not a per-part variation system.

### 3. Simplification

- **Delete the `sweep_x` special case** (the straddle logic at `ship_paint.py:225`). Projected min/max replaces it.
- **Delete `weight_x`.** One angle replaces a direction that varied with cap shape.
- In signed mode the `folded` combine node disappears.
- **Can't remove:** the folded mode itself, while Crimson keeps shared UVs. It would be worth asking the owner whether Crimson should eventually get split UV halves; after that, folded mode could be deleted.
- **Not worth adding:** per-part random seeds, per-cap angle variation, a new painter, or anything from the Valis image-extraction workflow.

### 4. Red flags

1. **Owner edits move with the regenerated masks (observed mechanism).** Edits are carried over as `mask − baseline` and added to the new generation (`ship_paint.py:443, 512`). Any edit made to touch up a mirrored stroke would now sit on a different stroke. Check `edited_texels` in the last report before rebaking. If it's non-zero, the owner should look at those areas.
2. **Side walls stay mirrored (observed mechanism, Blender's BOX projection).** Faces that face mostly sideways sample the brush with Y and Z only, so left and right walls stay identical even in signed mode. It's hardly visible top-down and may show at the quarter view. Accept it unless the owner objects.
3. **Possible restart at the centre (hypothetical).** If a centre plate is split into two separate caps meeting at x = 0, each half is normalised on its own and the stroke restarts at the centreline. Check the report's `parts[*].caps` for caps whose bounds end at x ≈ 0 without crossing it.

### Visual acceptance plan

1. **Crimson unchanged:** a Crimson rebake is byte-identical.
2. **Vanguard files untouched:** Source, UV, artwork, Ink and Emission are byte-identical. Shadow/Light change.
3. **Real asymmetry:** compare the top composite with its horizontal flip. Hull plates should differ visibly and the silhouette should not.
4. **No seam or chevron:** a 64 px band along the centre of the nose and tail stripe shows no V, inkblot or seam.
5. **No dark side:** mean Shadow on left vs right hull pixels within about 3%.
6. **Owner side-by-side:** Vanguard current / Vanguard signed / Valis at 60, 100 and 150 px and the quarter view. The owner judges whether it reads like Valis. If not, the only knob to adjust is `angle_deg`.
