# Ship art pipeline retrospective (2026-10-03)

Three ships have gone from idea to animated in-game asset: Vanguard (July to October), Crimson (four days in late September), Valis (three days, ending today). This traces each stage across all three, from the PRs, issues, evidence branches, working tree and the Codex session logs (the owner's own corrections are quoted from those logs).

Times are UTC. "Owner" is the human. Nearly all art work was done by Codex sessions; Claude sessions did recovery and review.

---

## 1. Stage by stage

### Concept

| Ship | What happened | Rounds |
|---|---|---|
| Vanguard | Modelled first from top/side references. The concept sheet came ten weeks later as a paintover of the existing mesh. The owner redlined at image coordinates ("make the decals consistent, these are just random marks"). | 7 sheets |
| Crimson | Profile options generated before modelling. Three rejected ("way too thin", "too short and stubby", "the front looks like a straight up different ship"). The owner drew a contour by hand and picked the closest. | 3 + hand sketch |
| Valis | Three image-gen studies, owner picked one with a condition, top view and turnaround generated, approved "no notes". Recorded on #796. | 1, about 30 minutes |

Went well: an approved top view plus turnaround before any geometry (Valis). Coordinate redlines turn taste into precise edits. Prompts and sidecars were kept.
Went badly: concept after model (Vanguard). No side profile agreed before modelling (Crimson). Concepts left untracked.
Deterministic: capturing reference views. Everything else is judgment.

### Modelling

This is the most expensive stage on every ship.

| Ship | What happened |
|---|---|
| Vanguard | About six owner-led Blender sessions with agent coaching. Agent "apply scale" "completely destroyed the mesh" and was reverted. Nacelles had to be corrected: "FOLLOW THE WINGS CONTOUR". |
| Crimson | Scripted builds drifted from the references ("you lost the side contour"). The agent started early ("you started the build too early") and applied all Mirror modifiers ("why?"), breaking symmetric editing. The owner then did an overnight hand pass; the agent cleaned up after. 39 parts, 33 live mirrors. |
| Valis | Build 1 (28 parts) discarded whole: "dont like tihs direction... no traces of your build". Build 2 took seven repair rounds, several for defects the agent introduced (modifier order, n-gons, broken symmetry, "fat and wormy" cockpit). Locked, integrated, then revised by hand again after flying it, which forced the UVs, hull and rig to be rebuilt. A merge pass dropped the charcoal spars. The owner had to ask twice for grouping. |

Patterns:
- The owner hand-edits geometry in Blender on every ship, usually to fix overall shape. The agent's best contribution was cleanup after those edits.
- Scripted geometry repeatedly missed the references. Whole builds were thrown away twice.
- Destructive agent operations (applied mirrors, applied scale, merged parts) caused the worst moments.
- The two finished sources are organized differently: Crimson by numbered collections, Valis by nested parent empties (the form the owner asked for).
- "Lock" happened before the ship was flown. Handling ("too wide... weird moment of inertia") surfaced after integration.

Deterministic: symmetry, modifier-order, n-gon, scale and naming checks; the geometry fingerprint. Shape is judgment.

### UV and paint

| Ship | What happened |
|---|---|
| Vanguard | Five paint systems in sequence: face-assigned materials, SVG livery mask ("this svg editing is way contrived"), AI livery variants ("a bit too random"), a scripted 4K atlas, overlay meshes for panels and wear (ten correction rounds, discarded four days later), then the Vivid repaint modelled on Crimson. Texturing started before the target shader was chosen. |
| Crimson | Texture concept approved from image-gen. Procedural bake with an image-gen brush texture. Approved first pass and became the reference ("focus on crimson its so good"). The owner later spotted "two faces take up most of the map": one part had 7.7 times the texel density because packing ignored object scale. The fix (#731) is still open, so main ships the bad UVs. |
| Valis | Attempt 1 used a repeated brush texture: "streaky and ugly". Attempt 2 painted over a flat render with image-gen, projected that onto three grayscale masks (shadow, light, ink) over recolourable base regions, and tuned strengths with sliders. Approved. First projection bled across neighbouring surfaces. |

Patterns:
- Two approved recipes exist. Crimson's bakes colour into one atlas. Valis's keeps paint in masks and colour in regions, which is what palette selection (#823) needs.
- A paint concept approved as a picture before any bake worked both times it was used.
- No UV density check existed before paint. It was written only after the problem shipped.

Deterministic: UV density check, mask projection, bake from settings, palette material generation. The look is judgment.

### Export and Unity integration

- **Orientation was wrong twice**: "the ship is upside down" (Vanguard), "The red ship is upside down... might be backwards" (Crimson). Fixed per export each time.
- **Parts came in as separate renderers twice** and were consolidated after pushback: Crimson ("why did you bring all of the crimson pieces in as seperate meshes? it should definitely be one mesh", 84 files) and Vanguard (23 renderers to 5).
- **Physics surprises**: Crimson's new collider changed yaw inertia from 331 to 401 and broke a navigation test. Valis's handling question is still open.
- **Wiring misses**: Valis failed two hosted tests because it was missing from the catalog and its weapons controller was not serialized.
- **Three routes, producers scattered** (detail in the audit). The producer of `Crimson.asset` survives only as an untracked, gitignored file at `results/visual-playable/CrimsonConsolidationAuthoring.cs`. The producer of Valis's skinned hull is `RebuildValis.cs` on an evidence branch, and it cannot run from scratch: it reads bones from the previous prefab. Valis's paint exporter asserts a fingerprint that is already stale.

Deterministic: all of it. This stage has no judgment in it and no shared tool.

### Animation

- **Breakup.** Crimson took three art rounds (unconnected fins moved "as one unit"; cockpit flew off intact; "you dont get to see the ship pieces come apart") and exhausted a session's context ("have you been going in circles?"). Vanguard reused Crimson's spec and needed only "contniue" and "merge". Valis is in flight with two corrections so far ("thats the wrong explosion", from a leftover legacy reference; "a couple too many pieces").
- **Wings (Valis).** The owner posed the forward pose by hand in Blender. Version 1 baked twenty joints and went to PR. Twelve hours later, after the geometry revision, version 2 replaced it with four bones.

Patterns: a breakup spec transfers between ships almost for free; what varies is how pieces are grouped. Rigs built before geometry is final get rebuilt.

### Verification and evidence

Went well: geometry fingerprints, vertex-match proofs, pixel-diff comparisons, native captures.
Went badly: evidence for one ship is spread over as many as four branches. No lineup sheet shows the ships together at one scale and lighting. Approvals live in chat messages. Crimson's in-engine stills were never published.

---

## 2. What the three runs have in common

1. **Order of operations cost the most.** Concept after model (Vanguard). Texture before shader (Vanguard). Model before agreed profile (Crimson). Integrate before texture, lock before flying (Valis). Rig before final geometry (Valis). Each produced a rebuild.
2. **Geometry is the owner's, in practice.** Every ship's final shape came from hand edits. The pipeline never said so, so agents regenerated, merged and applied modifiers over that work.
3. **Late checks.** Orientation, renderer count, UV density, inertia and catalog wiring were all found by the owner or by CI after the work was done. Each is a mechanical check.
4. **Every deterministic step has been scripted at least once, and none can be reused.** About seventy helpers exist across branches, an untracked folder and Codex's own archive. They hardcode ship names, part counts and slot paths. None has tests.
5. **What transferred between ships was a spec, not a tool.** The breakup behaviour and the "parts plus live mirrors" construction carried over well because they were written down as decisions.
6. **Waiting.** Pool slot waits of 25 minutes to 4 hours, a 6-hour approval gap, one context exhaustion. Blender-only stages need no Unity slot but queued for one anyway.

## 3. Prior research not yet adopted

`reports/AI game art pipeline.md` (2026-09-22) recommended: four director gates (brief, pick from a sheet, in-engine blockout, final approval in a lineup); one committed FBX export script plus a Unity import postprocessor; a capped silhouette sheet with a written critique rubric; provenance status that fails a build if exploration art ships. Adopted: the image tool with sidecars, hand-modelled hulls, turnarounds, contact sheets. Not adopted: the in-engine blockout gate, the exporter and postprocessor, the lineup sheet, the rubric, the status check.

## 4. Deterministic steps and their best existing implementation

| Step | Best existing version | Where |
|---|---|---|
| Evaluated mesh export, frozen triangles and normals | `export_study.py` | main |
| Geometry fingerprint and lock | `validate_source.py`; `export_approved.py` | evidence branches |
| UV density check | `check_uv_density.py` | open PR #731 |
| Mask projection and paint bake | `generate_base.py`, `paint_vanguard.py`; Valis layered prototype | evidence branches |
| Contour mesh with joined normals | `BuildValis.cs`, `RebuildValis.cs` | evidence branch |
| Mesh consolidation | `VanguardConsolidation.cs`; `CrimsonConsolidationAuthoring.cs` | evidence branch; untracked |
| Breakup build | `CrimsonBreakupBuilder.cs`, `VanguardBreakupBuilder.cs` | archive branches |
| Wing skinning | `BuildValisWings.cs`, `RebuildValis.cs` | evidence branch |
| Orthographic, turntable, contact sheet, game-scale strip | `capture_views.py`; `package_review.py` | untracked; evidence branch |
| In-engine comparison capture | `VanguardPaintComparison.cs` and Valis equivalents | evidence branches |

Never scripted: collider creation, hardpoint and bumper wiring, catalog registration, material assignment on the rig, copying an atlas into `Assets/` while keeping its GUID, pushing evidence, a lineup across ships.

The one existing model for a tested Blender-to-Unity tool is `art/tools/flat_background/`: versioned presets with a strict parser, a headless render CLI with machine-readable trailers, a publish step that owns the Unity folder layout, a sidecar the Unity import postprocessor parses and rejects on mismatch, and tests on both sides.

## 5. At risk now

- `results/visual-playable/CrimsonConsolidationAuthoring.cs` and `results/valis-wing-motion/v02/` are gitignored and exist only in the primary tree.
- `art/ships/vanguard/vanguard.blend` is untracked and on no branch.
- Crimson's original paint recipe survives only in Codex's local `artifact-archives`.
