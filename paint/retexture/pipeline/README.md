# Vanguard paint pipeline

This branch never merges. The production PR carries the selected paint, editable
source and rig binding. Blender 5.1.2 generated the selected Vivid finish with the
original MVP_Atlas layout; all scene topology, coordinates, corner normals,
transforms, UV coordinates and modifiers were verified unchanged.

## Reproduce the selected finish

Run Blender with --background --factory-startup --python-exit-code 1 and the
following scripts. Each script accepts arguments after -- and exits nonzero on
failure. Arguments use absolute paths. Standard output includes Blender logs;
the JSON manifests are the machine-readable output and own the output filenames.

1. generate_base.py --repo-root <checkout> --output-dir <out>
2. paint_vanguard.py --repo-root <checkout> --output-dir <out> --base <clean_base from step 1> --variant vivid --resolution 4096 --source-output <checkout>/art/ships/vanguard/drawn-study/VanguardPainted.blend
3. Copy the atlas path reported by preview-manifest.json to the existing
   Assets/Visuals/Ships/Vanguard/DrawnStudy/VanguardBaseColor.png. Its GUID stays unchanged.
4. validate_source.py --repo-root <checkout> --output-dir <out>

The checkout must contain the tracked VanguardStudy.blend, VanguardStructure.blend,
service-panels.json and Crimson painted-brush-source.png. The source-output is a
new editable handoff; neither generator saves either original study. The production
rig uses a dedicated Drawn Surface material with neutral OrangeGain. Its obsolete
surface-wear and service-panel objects are removed from the live rig. The vent/panel artwork now lives in the painted hull UV.

The two named color settings are Rich (0.98, 0.48, 0.10) and Vivid (1, 0.36, 0.065),
expressed in sRGB. Vivid also increases the brush range and painted shadow contrast.
The user chose Vivid after matched Crimson comparisons, rejected the authored
battle-damage layer, and rejected the fine gray seams and triangle grids. The
original baseline and original overlay geometry remain recoverable in the study
sources. The live separate canopy and blue core materials remain separate.

## Helper provenance

inputs/legacy-paint-layout.json preserves the September 23 Vanguard surface-coordinate
paint layout. base_palette.py derives only the pure paint functions from that
layout's build_texture_mvp_v2.py, omitting the rejected hardcoded tail seam. The
regeneration filters out line and wear shapes, then draws the existing service
patches into the atlas without changing the source triangles or UVs.

inputs/crimson-original-texture-pass.py is the recovered original Crimson #725
recipe. inputs/crimson-uv-rebake-reference.py is the locally recovered helper for
pending #731. They are read-only provenance references with old hardcoded outputs:
do not execute either. Their folded world-X brush coordinates, geometry-normal
painted light/shadow and emission-bake approach informed paint_vanguard.py.
No UV repack or layout change was selected for Vanguard: the inspected major hull
parts already had comparable world-space texel density.

VanguardPaintComparison.cs is a scratch game-capture scenario. Run it through
agent_worktree_pool.sh run-tests <slot> -Mode PlayMode -TestFilter
Tests.PlayMode.CaptureScenarioPlayModeTests -WithGraphics -Windowed
-CaptureScenario VanguardPaintComparison. It films real Ship_1 and Ship_2 units
with one lighting rig, first close and then at gameplay scale. It checks the
absent rejected overlays, neutral orange gain and imported 4K texture.
## Consolidated live mesh handoff

The user added intact-mesh consolidation after choosing Vivid. `VanguardConsolidation.cs`
loads the split Vivid rig from production commit `cd264037b6068b25db1af226a60e39b9ce414f97`
(the input state), combines nine painted meshes and eleven silhouette meshes through
Unity Mesh.CombineMeshes, saves two `.asset` meshes and updates the existing illustrated
prefab through PrefabUtility. Run the helper as a scratch game-capture scenario in a
leased checkout at that input commit, using the same runner syntax with
`-CaptureScenario VanguardConsolidation`. It overwrites only its own two named mesh assets,
retaining their GUIDs on a repeated run; it requires the original split rig as input.
The resulting active art has five renderers: paint, contour, ink, canopy and cores.
The two rejected overlays are removed from the live prefab and remain in the packed
Blender sources. No source part merging, UV repack, vertex welding or normal recalculation
occurs. Missing UV channels on core contour vertices become zero UVs; the contour shader
does not sample them. Authored UV values and triangle indices compare exactly, and
transformed positions/normals compare within 0.00001. The helper records these assertions
in `results/vanguard-retexture/consolidation/mesh-validation.json`, and films the split
and combined units beside each other. `history/consolidation` preserves verified captures
and the existing illustrated-prefab fixture's result. The original native paint capture
in `history/native` predates consolidation and checked inactive overlays; the updated
VanguardPaintComparison helper checks their absence in the final live prefab.

For breakup #795, use the separate parts and selected packed atlas in VanguardPainted.blend.
The combined intact mesh assets are a runtime optimization, not the editable breakup source.
The helper removes only four spurious inherited-Light bounding-sphere modifications
introduced by Unity's prefab serializer; the original input has none. Their count
is checked and all unrelated Light serialization remains inherited. Generated mesh
byte hashes and committed hashes after trailing-whitespace normalization are both recorded.
