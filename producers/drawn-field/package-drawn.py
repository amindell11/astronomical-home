from pathlib import Path
import shutil,json
out=Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
source=Path('results/asteroid-light-study/20260926-072617')
page=out/'comparison.html'
s=page.read_text(encoding='utf-8')
if not (out/'outline-study.html').exists(): (out/'outline-study.html').write_text(s,encoding='utf-8')
for src,dst in [(source.with_suffix('.mp4'),'drawn-light.mp4'),(source/'turntable.mp4','drawn-turntable.mp4'),(source/'f_00003.png','drawn-light.png'),(source/'turntable/f_00000.png','drawn-turntable.png'),(source/'scene-lighting.png','drawn-scene.png'),(source/'drawing-fully-lit.png','drawn-unlit.png'),(source/'drawing-left.png','drawn-left.png'),(source/'drawing-right.png','drawn-right.png'),(source/'measurement.json','drawn-measurement.json'),(Path('results/unity-tests-agent/20260926-002556-summary.json'),'drawn-test-summary.json')]: shutil.copy2(src,out/dst)
s=s.replace('Asteroid · moving light study','Asteroid · authored drawing study')
s=s.replace('Thick contour. Moving shadows.','Drawn creases. Chipped stone.')
s=s.replace('The thicker outline is back around the silhouette. Both views use the corrected stone texture and light-responsive shadows; only the outer contour changes.','Directional stone brushwork, tapered crease strokes, small broken crater rims and paired chisel nicks. Each mark follows a chosen part of the mesh. The thick contour and moving shadows remain.')
s=s.replace('Outline on · 4.5-pixel contour','Current · authored surface drawing').replace('Outline off · same lighting','Previous · clean stone + outline')
s=s.replace('id="after" src="outlined-light.mp4" poster="outlined-light.png"','id="after" src="drawn-light.mp4" poster="drawn-light.png"')
s=s.replace('id="before" src="light-after.mp4" poster="light-after-poster.png"','id="before" src="outlined-light.mp4" poster="outlined-light.png"')
s=s.replace('value="0"><output id="degrees">0°','value="15"><output id="degrees">15°')
s=s.replace('Native Unity renders: same fixed mesh, camera, material, soft shadows and light orbit. The contour is 4.5 pixels on the shadow side and narrows on the lit side. It does not cast shadows onto the rock.','Native Unity renders with the same camera and light orbit. The current study adds brush paint, a fitted drawing layer, small impact bowls and selected flattened ridge shoulders. Thin marks stay on the stone; large shadows move with the light.')
s=s.replace('<h2>Reference direction</h2>','''<h2>Turn the rock</h2><video id="turntable" src="drawn-turntable.mp4" poster="drawn-turntable.png" style="max-width:640px" controls muted loop playsinline preload="auto"></video><p class="small">The light stays fixed while the mesh turns. Crease strokes, broken crater rims and nicks wrap with the surface.</p>
<details><summary>Inspect the drawing without directional shadows</summary><img class="diag" style="max-width:640px" src="drawn-unlit.png" alt="Painted stone with tapered crease lines and small crater rims under flat illumination"><p>The base paint contains no black shadow shapes. The separate drawing occupies about 3% of the visible stone in this view.</p></details>
<h2>Reference direction</h2>''')
s=s.replace('The shadow behavior is corrected. The mesh still needs sharper, more deliberate planes to match the reference; this is not a finished art match.','The drawing now follows selected edges and recesses instead of scattered dark patches. The reference still has stronger sculpted planes and more deliberate line-weight variation; this remains an exploration.')
s=s.replace('src="outlined-scene.png"','src="drawn-scene.png"')
s=s.replace('<a href="outline-study-notes.md">Evidence and limitations</a>', '<a href="drawn-study-notes.md">Evidence and limitations</a> · <a href="outline-study.html">Previous clean stone</a>')
page.write_text(s,encoding='utf-8')
(out/'drawn-study-notes.md').write_text('''# Authored asteroid drawing

The base stone texture now uses directional gouache strokes and angular mineral patches, generated with the built-in ImageGen tool from the previous clean albedo. It contains no black shadow shapes. Exact prompt and provenance are in art/asteroid-study/README.md.

A separately editable Blender mesh supplies tapered ribbons fitted to selected recess rims and plane breaks. Six shallow impact bowls have partial angular rim strokes, with a few paired chisel nicks. Selected ridge shoulders are flattened locally. This is authored placement with no random scatter. Drawing receives lighting and does not cast shadows. The 4.5-pixel silhouette contour remains.

Current source: art/asteroid-study/AsteroidDrawnStudy.blend. Its two mesh objects export to AsteroidFractureStudy.fbx and AsteroidSurfaceDrawing.fbx. The study scenario attaches drawing in gameplay and inspection; production spawning/collision are unchanged.

## Verification

Final native graphics run 20260926-002556: 3 passed, zero failed. Fully lit base has zero black pixels; opposing lights change 15,671 of 29,626 surface pixels between dark and lit. Authored drawing affects 898 pixels, approximately 3.0% of visible surface. The original thin draft broke into dots at game size; increasing ribbon weight and surface offset made the strokes continuous.

The light sweep and turntable each contain 72 native Unity frames at 24 fps, with encoded video decoded successfully. Existing scene-lighting still uses the committed Lighting.prefab. Source frame directory: results/asteroid-light-study/20260926-072617. The previous comparison uses the older clean mesh and albedo with the same camera and light orbit, not identical geometry.

## Limits

The reference still has stronger sculpted planes and more deliberate line-weight variation. Shadowed linework can merge into shadow; the drawing is sparse by design. The scene lighting remains darker than the reference. High Fidelity is verified; the Performant pipeline disables main-light shadows. No production performance or final art acceptance is claimed. Ship assets are unchanged.
''',encoding='utf-8')
print(page)
