# Ship texturing retro — Crimson, Valis, Vanguard, Nightshade, Bulwark

Sources: the codex session transcripts for each ship (condensed to text; `~/.codex/sessions` and `archived_sessions`), the assets on `main` today, the PR bodies (#725, #814, #843, #965) and issue #989, the evidence branches, and the paint stage file `.claude/skills/ship-art-pipeline/stages/6-paint.md`. Five subagents read one ship each; I read the shader, materials, textures and the in-game lineup captures.

## 1. What is actually on main

All four shipped ships render through the same shader, `Astronomical/Drawn/Surface`, but feed it two different things:

| Ship | Paint input | Materials | Shader toggles | Notes |
|---|---|---|---|---|
| Crimson | baked RGB atlas `Crimson_BaseColor.png` 4096² | 1 (`Hull paint`) | `_PaintLayers` off, `_PaletteLighting 1`, `_ShadowThreshold 0.55` | atlas is procedural output of one node script; roles keyed to part names |
| Vanguard | baked RGB atlas `VanguardBaseColor.png` 4096² | 1 hull + ink + canopy + glow | `_PaintLayers` off, same lighting knobs as Crimson | base is flat fills rasterized by part/position/normal; painted bands touch ivory only |
| Valis | `Paint/Shadow,Light,Ink.png` 4096² + `Accent emission` | 7 regions × 3 palettes | `_PaintLayers` on; 1.02 / 0.20 / 1.50 | mirrored charts in the other atlas half (asymmetric paint) |
| Nightshade | `Paint/Shadow,Light,Ink,Emission.png` 4096² | 6 regions × 1 palette | `_PaintLayers` on; 1 / 1 / 1; `_PaintSaturation 1.25`; `_PaintEmissionBlend 1`; per-material `_ShadowThreshold` down to −0.06 | charts in the lower two quadrants only (upper half empty) |
| Bulwark | `layers/Shadow,Light,Ink,BaseRegion.png` 2048² in the slot, not in Unity | temp clones of Valis `Graphite` | `_PaintLayers` on; BaseRegion rides the `_EmissionMap` slot | wear still in the committed layers; cleaned layers only in `results/` |

Mask coverage (from the PNGs): Valis Ink covers ~12 % of the atlas at strength 1.5; Nightshade Ink covers ~0.6 % at strength 1.0, and its Shadow is heavier and finer-grained than Valis's (mean 0.34 vs 0.25).

## 2. How each one was made

### Crimson (Sept 28, PR #725; rebaked Oct 6, #947) — favourite
- Concept: 2 imagegen rounds over ortho captures. Round 1 came back metallic; the owner pasted an in-game screenshot of the old Ship 2 ("preserve the hand drawn look") and round 2's "B / hand-painted field finish" was approved: "that looks perfect".
- Atlas: **100 % procedural, no projection, no hand painting.** One Blender script (`texture_pass.py`, on `evidence/crimson` and at `3da0da5:pipeline/inputs/`) builds a node graph per part and EMIT-bakes it:
  1. role → flat colour by part-name keyword (silver / red / charcoal / ink / glass / green);
  2. the imagegen brush tile, box-projected in world space with X folded so mirrors match, remapped to ~0.8–1.4× and multiplied in (quiet, low contrast);
  3. `|normal.z|` ramp: top faces full colour, side walls cool blue-gray;
  4. on every plate's top cap, a jittered diagonal sweep splits the plate into thirds: dark third ×(.58,.61,.69), light third ×(1.22,1.21,1.16);
  5. inset ink line (distance to cap edge < 0.003) and a bright rim highlight just inside it; fin side walls fully inked;
  6. a few hand-placed accents (canopy band, green service lights, nozzle rings).
- Rounds: 3 bakes, 1 owner review: "This looks great. Put up a pr".
- Not on main: the script (hard-coded slot paths), the R1 prompts, any prose of the recipe. The Oct 6 rebake re-ran a slice of the same script against `Crimson.blend`, so the atlas on main is still this recipe's output.

### Valis (Oct 1–2, PR #843) — second favourite
- First attempt (geometry session): imagegen gouache brush tile + procedural Shadow/Light/Ink from UV edges. Owner: "way worse than what you did for crimson… streaky and ugly… try again in a new chat"; "focus on crimson its so good".
- Retry: flat top render of the jade-iris base model → one imagegen **style-transfer** call with Crimson's top and quarter views as the style reference → `extract_layers.py` samples every PaintUV texel from the painting and splits luminance into Shadow / Light / Ink; unseen texels fall back to base × form term. Mirror UV offsets give each side its own paint. Tuner page for strengths; owner set light to 0.20.
- The mask-over-regions structure was the owner's ask ("a painted layer above the base coat so we could still do recolors later", #823). The split into exactly three masks was the agent's choice; the owner never commented on it. `_PaintLayers` was added to the shader in this PR.
- Owner: "i actually like the asymmetrical version better here"; "that is gorgeous" (palette); "ok cool, add it to the game, and make the purple bloom a bit".
- Order breaks: masks built without a re-approved concept (twice); no flight check before paint ("turns like a boat" found after).
- Tools archived on `codex/valis-paint-authoring-archive`, not main.

### Vanguard (Sept 23 study, PR #691; retexture Sept 30–Oct 2, PR #814) — "too flat"
- Base atlas (Sept 23): `build_texture_mvp_v2.py` rasterizes **flat palette fills** chosen by part name / position / normal, ±0.5 % sine variation. Flat is built in. (Donor AI albedo, livery mask and stamp atlas were leftovers the builder never reads.)
- Study (Sept 27): ~11 rounds, every one an overlay *mesh* (gray service patches, tapered ink lines, scuffs → "battle damage"). Owner: "you just placed gray bits randomly", "tacked on", "cross hatches… just feel random", then "gawgeous". Orange fixed by a material gain, not the texture.
- Retexture: owner "vanguard looks a bit desaturated compared to crimson". `paint_vanguard.py` re-rasterizes the fills (dropping lines and wear, baking the gray vents flat), multiplies Crimson's brush tile, adds sweep bands **on ivory pixels only**, and a `|normal.z|` ramp. 4 of 5 rounds went to removing gray seams the agent had carried in; damage layer removed on request. Owner picked "vivid" from flat Workbench previews beside Crimson.
- Why flat: fills by construction; painted shading touches only ivory; `|normal.z|` ramp gives ≈1.0 on faces the top-down camera sees; every approved detail lived in overlays that were later deleted; `_PaletteLighting 1` + no specular can only darken.
- Never migrated to the ship-tool layout: no `ship.json`, no `.blend` at the art-folder root.

### Nightshade (Oct 6–10, PR #965) — "too painted"
- Concept: 5 rounds / 6 generations over a flat 2×2 render sheet; 05 approved ("Approve a texture pass"). Owner steered against "bronze / gold", "big green lights in the middle".
- Passes 1–2: **procedural** Cycles bakes (AO cavity, noise, fixed light). Owner: "That doesn't match the concept color at all.. are you counting on lighting?"; "you neglected the hand drawn effect… lost the plot". Then: "I like in crimson how the multitone swaths on the metal make it look like light", "the reflections are drawn on", "I like the concept version that looks almost like marker".
- Pass 3–4: two imagegen **"marker" paintings** (top, underside; Crimson capture as a reference) **projected** onto every texel, luminance decomposed against each material's base: shadow = 1−L/base, light = (L−base)/(hi−base), ink from a threshold. Hue discarded. Asymmetric halves at the owner's request ("remove symmetry to make the light work better") implemented as half-scale quadrants, leaving the top half of the 4096 atlas empty. Repair 05 fixed outline streaks the projection copied onto thin wings.
- The agent read `6-paint.md` at least six times and the Valis files. It complied with the format (grayscale masks, recolourable regions, concept before masks) and still invented mask generation twice.
- **Palette flip after extraction:** masks were derived against concept 05's dark palette (Midnight #202C42); the owner recoloured to pale blue (Midnight #A7CBFF) and asked for a saturation control (set 1.25). Masks were never re-derived. On `Nightshade Midnight.mat` today `_PaintLightColor` (0.40, 0.48, 0.61) is darker than `_PaperColor` (0.65, 0.80, 1.0), so the light swaths darken. Shadow carries everything (fine, dense strokes), ink is almost absent, saturation and real-time cel lighting stack on top. That is the "too painted" read: many small marker strokes rather than Crimson's few broad plate shapes, with contrast tuned for a palette that no longer exists.
- Scripts live only in `~/.codex/visualizations/…/01a112c7…/`; passes 03/04 have no provenance JSON in the repo.

### Bulwark (Oct 13–14, #989 open on agent-3) — "rabbit hole of edge wear"
- Palette: concept E plum-bone; owner: "Look at the in game colors not random concepts", "I want to see it in the lineup", "Proceed with plum then". Then "Lock geometry and fix UV's" (flight check skipped on the owner's call). UV repair approved after the cockpit seam moved.
- Masks: a **numpy rasterizer** (`paint_masks*.py`) over an exported mesh dump. Shadow = `0.08+0.38·(1−N·L)` + noise (a lighting bake, not painted planes); Light = `(N·L)³` + **edge wear** + rim; Ink = distance-to-sharp-edge + noise "chips"; a 4th `BaseRegion` mask with the livery written as coordinate formulas. No projection of the concept at all. No AO/curvature bakes; the wear is edge distance × hand-rolled noise.
- The agent read `SKILL.md`, `6-paint.md`, `5-uv.md`, the Valis README and materials, and claimed "the same recolourable mask recipe as Valis".
- Edge wear: **agent initiative** from its first plan ("restrained shading and modest edge wear"), stepped up in r03 unasked, then multiplied *into* BaseRegion so livery and wear were tangled. Present in all six published mask rounds. Owner at close-up: "what is the weird weathering around the edges? it looks super low res and terrible… maybe nightshade but not the others, and i didn't like that about nightshade". Cleanup script has produced wear-free layers in `results/` (Shadow untouched); the committed `layers/*.png` still carry it.
- Seven mask rounds on livery shapes ("nowhere near the concept", "cross into the other piece and be more aggressive", "a bit thin and not super readable at distance", "stripes… one coherent stripe that wraps… I might have to do this editing myself"). Owner is now hand rough-painting the base layer for the agent to clean up.
- State: 2 unpushed checkpoint commits; owner's hand mesh edits mean the live geometry no longer matches `lock.json`; nothing in `Assets/`.

## 3. Comparison

| | Crimson ★ | Valis ★ | Vanguard (flat) | Nightshade (painted) | Bulwark (wear) |
|---|---|---|---|---|---|
| Paint source | imagegen concept as style authority only | imagegen style transfer of flat render, Crimson as reference | imagegen sheets; flat fills | imagegen marker paintings | imagegen palette edit; formulas |
| Mask/atlas generation | rule-based node script, baked | projection + luminance split | fills + Crimson brush + bands on ivory | procedural → projection + decomposition | N·L formulas + noise + wear |
| Per-plate light/shadow shapes | few, broad, hard-edged (diagonal thirds) | yes (inherited from Crimson style via transfer) | only on ivory; ≈none top-down | many, fine, dense | lighting gradient, not shapes |
| Ink | inset line + rim highlight on every plate, fin sides inked | 12 % coverage @1.5 | dropped in retexture | 0.6 % @1.0 | edge-distance + chips |
| Shadow colour | cool blue-gray multiply | tunable, 1.02 | one hard cel step | heavy, saturated 1.25 | formula |
| Brush variation | quiet, world-space, mirrored | from the painting | Crimson tile | from the painting | sin/value noise |
| Wear / damage | none (prompt forbade it) | none | added then removed | implicit in strokes | added unasked, rejected |
| Recolourable | no | yes (3 palettes) | no | yes, but masks tuned for another palette | yes (4th mask hack) |
| Reproducible from main | script on evidence branch only | tools on archive branch | scripts on evidence branch | scripts in `~/.codex` only | scripts in ignored `results/` |
| Read the skill? | predates it | predates it | predates it | yes, 6× | yes |
| Rounds to approval | 2 concept + 1 paint | 1 rejected pass + 1 pass + tuner | ~11 + 5 | 5 concept + 4 passes + repair | 1 + 2 + 7, unfinished |

## 4. Findings

1. **The pipeline specifies a storage format, not a painting recipe.** `6-paint.md` says "masks over recolourable regions… Commands: None yet" and points at Valis. Nightshade and Bulwark followed it faithfully and still had to invent a mask generator each. Three ships, three generators (projection + luminance split; projection + per-material decomposition; N·L formulas + noise). None is on main.
2. **The look the owner likes is a set of rules, and they were written down once, as code, on Crimson.** Broad diagonal light/dark thirds per plate, cool side-wall ramp, inset ink + rim highlight, strong flat role colours, quiet mirrored brush. Valis got the same look indirectly by style-transferring *from Crimson's renders*. Vanguard applied a subset (bands on ivory only). Nightshade and Bulwark used none of it.
3. **Projecting an imagegen painting onto the mesh is the fragile step.** Valis: RBF warps, pivot-cap bleed, doubled seams. Nightshade: outline streaks on thin wings, striped bevels, lights behind louvers, missing canopy strokes, a repair pass. Crimson's rule-based route had zero such rounds.
4. **Masks are derived against a palette and silently go stale when the palette changes** (Nightshade). The format promises recolourability; the derivation step does not.
5. **Agent-initiated wear/damage has been rejected on every ship it appeared on** (Vanguard scuffs and battle damage, Nightshade edge treatment, Bulwark edge wear). Crimson's approved prompt explicitly excluded scratches, rust and grain.
6. **Stage order keeps slipping around paint**: Valis painted before any flight check; Nightshade painted before lock and UV approval, then owner geometry edits forced a repair; Bulwark skipped flight check on the owner's call and the lock is now broken by hand edits.
7. **UV layouts differ per ship for the same asymmetric-paint need**: Valis uses atlas halves, Nightshade half-scale quadrants with the top half empty, Bulwark mirrored (symmetric).
8. **Preview surface matters**: Vanguard's Vivid was picked from flat Workbench renders; the first Unity view was in the PR. Nightshade's pass 1 was judged under lighting the agent was "counting on".

## 5. Proposal (for the owner to decide, not accepted)

**Canonical recipe = Valis's storage + Crimson's generation rules, as one tool.**

- Storage stays as designed: palette regions as material slots (`role`-style collections or a region map), grayscale Shadow / Light / Ink masks on `PaintUV`, `_PaintLayers` on, palettes in `palettes.json`.
- Generation becomes deterministic from the locked geometry, not from a projected painting: per-plate diagonal thirds → Shadow and Light; cool side-wall ramp → Shadow; inset edge line → Ink; rim highlight → Light; a quiet mirrored brush tile modulating Shadow/Light; hand-placed accents as a small list. Knobs per ship: sweep direction, split fractions, ink inset width, brush scale/contrast, light colour. This is `texture_pass.py` with its colour step removed and its three outputs written as masks instead of multiplied into an atlas.
- Imagegen is demoted to what it is good at here: concept sheet and palette/region assignment. No projection onto the mesh. (Fork for the owner: keep Valis's projection route as an *optional* second source for Light/Ink when a concept has shapes the rules cannot express. It produced Valis, but also Nightshade.)
- Owner hand edits ride on top of the generated masks as deltas (what Bulwark is moving to now), so a regeneration does not eat them.
- Rules into `6-paint.md`: the look rules above; no wear/damage/grunge unless the owner asks; masks derive after the palette is approved and re-derive on a palette change; asymmetric paint uses one UV layout (Valis's halves); candidates are judged in the Unity lineup, not Workbench.
- Tooling: unfreeze #868 slice 8 (mask generation + palette material generation) on the grounds it now blocks a roadmap ship; seed it from `evidence/crimson:producers/codex-blender/…/texture_pass.py`.

**Per-ship changes to unify, if the proposal stands**

| Ship | Change | Risk |
|---|---|---|
| Crimson | re-run its own recipe with outputs split into regions + masks; gate with `ship_render --set compare` against today's atlas | look drift in the brush step (today it multiplies colour; as a mask it modulates shadow/light) |
| Valis | none needed for look; optionally regenerate through the tool later for reproducibility | lowest |
| Vanguard | migrate to the ship-tool layout (no `ship.json` today), define regions (ivory / orange / slate / dark / canopy / cores), generate masks with the recipe | its UVs were never density-checked by `ship_uv`; its base is overlay-free so regions are clean |
| Nightshade | re-derive or regenerate masks against the *current* palette, restore ink, drop the 1.25 saturation and the per-material lighting overrides once the masks carry the contrast, reclaim the empty atlas half | owner has play-tuned material values that would be superseded |
| Bulwark | strip wear from the committed layers (cleanup already exists in `results/`), replace the N·L shadow with plate shapes, make BaseRegion a real region/material instead of the emission-slot hack, keep the owner's hand paint as a delta layer, re-lock after the owner's edits | slot is live; coordinate with the running session |

## 6. Housekeeping noticed on the way
- The Crimson, Valis, Nightshade and Bulwark generator scripts are each on exactly one non-main location (evidence branch, archive branch, `~/.codex/visualizations`, ignored `results/`). The Nightshade and Bulwark copies are one disk cleanup from gone.
- `Nightshade_Albedo.png` in the visuals folder is legacy, referenced only by `_Shared/Ship3Material.mat`.
- Primary tree has untracked audio files from the FMOD reorganization (owner's; untouched).
