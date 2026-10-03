from pathlib import Path
import shutil,sys,json
source=Path(sys.argv[1])
out=Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
page=out/'comparison.html'
s=page.read_text(encoding='utf-8')
if not (out/'shadow-study.html').exists():
 (out/'shadow-study.html').write_text(s,encoding='utf-8')
shutil.copy2(source.with_suffix('.mp4'),out/'outlined-light.mp4')
shutil.copy2(source/'f_00000.png',out/'outlined-light.png')
shutil.copy2(source/'scene-lighting.png',out/'outlined-scene.png')
shutil.copy2(source/'outlined-left.png',out/'outlined-left.png')
shutil.copy2(source/'outlined-right.png',out/'outlined-right.png')
s=s.replace('Shadows follow the light.','Thick contour. Moving shadows.')
s=s.replace('The black regions now come from the recessed mesh and real-time lighting. The texture contains only stone color. Hold the rock still and move the light to compare with the previous painted-black version.','The thicker outline is back around the silhouette. Both views use the corrected stone texture and light-responsive shadows; only the outer contour changes.')
s=s.replace('Current · lighting-driven shadows','Outline on · 4.5-pixel contour').replace('Previous · black shadows painted into texture','Outline off · same lighting')
s=s.replace('src="light-before.mp4" poster="light-before-poster.png"','src="light-after.mp4" poster="light-after-poster.png"')
s=s.replace('id="after" src="light-after.mp4" poster="light-after-poster.png"','id="after" src="outlined-light.mp4" poster="outlined-light.png"')
s=s.replace('Native Unity renders: same fixed mesh, camera and light orbit. Current shadows use soft filtering; previous shadows used hard filtering. The outer contour is omitted in this diagnostic so the interior darkness is easy to inspect.','Native Unity renders: same fixed mesh, camera, material, soft shadows and light orbit. The contour is 4.5 pixels on the shadow side and narrows on the lit side. It does not cast shadows onto the rock.')
a=s.index('<details><summary>Check the surface with lighting flattened')
b=s.index('<h2>Reference direction',a)
s=s[:a]+s[b:]
s=s.replace('src="light-scene.png"','src="outlined-scene.png"')
s=s.replace('The asteroid study retains its thicker outer contour in the scene.','The outline settings match the asteroid study in the scene.')
s=s.replace('<a href="light-study-notes.md">Evidence and limitations</a>', '<a href="outline-study-notes.md">Evidence and limitations</a> · <a href="shadow-study.html">Shadow correction comparison</a>')
page.write_text(s,encoding='utf-8')
(out/'outline-study-notes.md').write_text('''# Outline restored

Native Unity capture using the current clean-albedo asteroid and the existing Drawn Contour shader. The shell uses the same mesh, 4.5-pixel shadow-side width, 0.6 lit-side width fraction and near-black color as DrawnAsteroidPaintScenario. Shell casting/receiving shadows is disabled.

Outline-on and outline-off clips share mesh pose, camera, surface material and soft-shadow light orbit. Both contain 72 native frames at 24 fps. Capture-only fixture setup was temporary; no lasting project changes were required because the scenario already includes this contour.

The existing scene-lighting still is also refreshed with the contour. Reference matching and scene-lighting limitations from light-study-notes.md remain.

Capture source: '''+str(source)+'\n',encoding='utf-8')
print(page)
