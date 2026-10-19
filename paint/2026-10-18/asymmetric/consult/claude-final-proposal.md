# Vanguard paint asymmetry: corrected proposal (read-only)

I read `cap-inspection.json`, `followup.md` and `ship_regions.py`, and checked the rest against `ship_paint.py` again. I implemented and ran nothing.

## Answers to your five checks

**1. Compatibility.** Yes, that's the smallest honest design, and I withdraw the deletions.
- With `paint_space` absent, the painter runs today's folded code exactly: `ax`, `sweep_x` and `weight_x` stay as they are.
- `"paint_space": "signed"` is a separate branch that applies only to the brush coordinate and the sweep.
- Each mode's fields are exclusive and checked when `paint.json` is read: signed requires `sweep.angle_deg` and rejects `weight_x`, and folded does the reverse. Without that, a leftover field would be silently ignored.
- New per-cap data (centroid, vertex xy) is stored but never reaches folded material graphs, so Crimson's bake can't change.

**2. Coverage guarantees.** Withdrawn. A third of the projected interval is not a third of the area. Per-cap normalisation removes the panel's position from the sweep, so a ship-wide gradient can't form. It doesn't remove bias from tapered panels.
- **Fix: centre on the area-weighted centroid instead of the min/max interval.** Same knobs, no quantile system:
  - `c` = area-weighted xy centroid of the cap
  - `r` = max |(v − c)·d| over the cap's vertices
  - `s = 0.5 + (p − c)·d / (2r) + (brush − 0.65)·jitter`
  - The existing `shadow_coverage` / `light_above` thresholds then apply unchanged.
- **Worked example (arithmetic on an ideal triangle, not measured on Vanguard):**
  - Min/max normalisation with the wide end dark gives about 56% dark area. With the narrow end dark it gives about 11%.
  - Centroid centring gives about 20% and 19% dark, and about 21% and 23% light.
  - So it largely cancels the taper bias. It also makes real coverage lower than the nominal 0.33, which makes `shadow_coverage` a tuning value in signed mode.
- Equal coverage per side becomes a diagnostic, not a guarantee.

**3. Your probe.** Accepted: the split-centre case isn't present, and the report-based check I suggested wouldn't have worked. Two more facts from your data:
- **Mirrored caps are already split differently by side.**
  - `MVP Wing`: the right side is one 4-face cap (area 0.1996). The left side is a 3-face cap (0.1739) plus a 1-face cap (0.0172).
  - `MVP wing_armor`: the right side has 5+1 faces, the left side 4.
  - In signed mode the left wing would get two strokes and the right wing one. This is already true in folded mode, since normalisation is per cap.
  - **Unknown:** whether these caps are HullPrimary, the only region with sweep enabled.
- The near-centre split in `Vanguard structural ink` doesn't matter, because canopy frames have all paint set to zero.

**4. Facing ramp.** Agreed. I no longer claim the nose and root facets disappear. `side_wall_depth` stays a visual tuning value and needs no architecture change; the ramp's symmetry is the intended "roughly symmetric shapes" part.
- The contributions can be separated with existing settings, no code needed:
  - **Brush only:** `shadow_depth 0`, `sweep.light 0`, `side_wall_depth 0`
  - **Sweep only:** `brush.contrast 0`, `side_wall_depth 0`. Brush jitter inside the sweep remains, which is correct.
  - **Ramp only:** `contrast 0`, `shadow_depth 0`, `sweep.light 0`
- **These must run on a copy of the source directory.** The painter writes `layers/` and `layers/baseline/` beside the source (`ship_paint.py:502-517`), so running in place would overwrite Vanguard's masks.

**5. UV overlap check.** Reuse the existing machinery and don't write a second rasterizer.
- Move the per-triangle pixel loop out of `ship_regions.rasterize` (lines 141–154) into one shared helper. `rasterize` calls it with its current inclusive tolerance (−1e-6), so its output must stay byte-identical.
- The new check, `side_conflicts`, calls the same helper with a strict-interior tolerance (+1e-6), so pixel centres lying exactly on a shared edge are not claimed.
- It labels faces by the sign of their centroid x, skipping faces within ε of 0. It refuses if any pixel is claimed by both a +x and a −x face. It runs before the margin is padded out.
- Shared mirrored UVs overlap in their interiors, so they can't slip through.
- **I rejected the cheaper "left faces in one atlas half" bounding check.** The painter would have to reconstruct the UV producer's layout, which is exactly the outputs-become-inputs smell.
- **Cost:** another full pass over all triangles at 4096, roughly equal to the authored-region pass that already runs on every Vanguard bake.

## Final proposal

**Root cause:** the painter's coordinate system is folded by design (correct for Crimson's shared UVs). There is no way to declare that a ship's PaintUV halves are independent.

**Rungs:**
- The mode is parsed once when `paint.json` is read (operating input; parse, don't validate).
- The signed mode's precondition is enforced at rung 2: it refuses before baking if any pixel is shared.
- Rung 1 (deriving the mode from the UVs automatically) would let a later re-UV silently change Crimson's look.

**Essential for the first preview:**
1. `paint_space: "signed"` in Vanguard's `paint.json`, with exclusive fields per mode.
2. Brush sampled at signed `(x, y, z)`.
3. Sweep centred on the centroid along a single `angle_deg`, starting at 30° from the nose axis.
4. Everything else stays at current values.
5. The shared-helper refactor plus `side_conflicts`.

Honest size: about 60–90 lines, mostly the refactor and the overlap check.

**Tuning afterwards, by eye:** the sign and size of `angle_deg` (about 20–40°), signed-mode `shadow_coverage` / `light_above`, `side_wall_depth`, and brush contrast. No noise controls and no per-part randomness.

**Owner edits:** the baseline record doesn't include paint settings, so the existing baseline is reused and owner deltas carry onto the new strokes. Check `edited_texels` before rebaking.

## Checks

**Pass/fail gates:**
- Crimson rebake is byte-identical.
- Vanguard source, UV, artwork, Ink, Emission and Region masks are byte-identical.
- Signed mode passes `side_conflicts` on Vanguard.
- A scratch copy of Crimson set to signed is refused.

**Diagnostics only, no thresholds:**
- The three contribution-isolation bakes (on a scratch copy).
- A table of Shadow/Light fractions for each mirrored cap pair, used to point the owner at outliers.

**Visual acceptance (owner):** current vs signed vs Valis, top and quarter views, at 60/100/150 px and full size. The owner judges:
- whether the centre inkblot is gone
- whether the broad strokes read as one deliberate light direction
- whether either side reads darker
- whether the remaining nose facets (from the ramp) are acceptable

## Measured vs hypothesis

**Verified from code:**
- The fold applies to both brush and sweep.
- `sweep_x` special-cases caps that cross the centre.
- The ramp uses `|nz|`, which is symmetric.
- Layers are written beside the source.
- The baseline record doesn't include paint settings.
- The rasterizer's edges are inclusive.
- Exactly which existing settings zero out each contribution.

**Measured by your probe:**
- The centre caps are connected across x = 0.
- No cap ends on the centre.
- Wing and wing-armor caps are partitioned differently by side.

**Computed:** the triangle coverage example only.

**Visual hypotheses still to confirm:**
- Signed sampling removes the tail and nose inkblot. I saw the inkblot in the image but haven't isolated its cause.
- Centroid centring keeps the sides balanced on Vanguard's real panels.
- 30° reads like Valis.
- How much of the nose faceting comes from the ramp.
- Whether the differently split wing caps are HullPrimary and visibly matter.
