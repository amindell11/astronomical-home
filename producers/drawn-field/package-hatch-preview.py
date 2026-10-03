from pathlib import Path
import shutil, sys, re

preview = Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
producer = Path(sys.argv[1])
summary = Path(sys.argv[2])
archive = preview / 'bold-field-study.html'
html = archive.read_text(encoding='utf-8-sig')
for name in ['field-overview.png', 'field-scene-lights.png', 'all-ten-front.png', 'all-ten-reverse.png', 'field-flight.mp4', 'field-flight.png', 'field-study-notes.md']:
    html = re.sub(r'(?<![\w-])' + re.escape(name), 'pre-hatch-' + name, html)
html = html.replace("href='all-ten-'+id", "href='pre-hatch-all-ten-'+id")
archive.write_text(html, encoding='utf-8')

html = (preview / 'comparison.html').read_text(encoding='utf-8-sig')
html = html.replace('More lines. Stronger ink.', 'The finishing marks.')
html = html.replace('More lines follow the ridges and creases, with wider near-black strokes and a heavier outline. The revised stone palette also pulls back the yellow-orange cast.', 'Fine hatch bundles, crossing scuffs and little chisel ticks now sit beside the heavier creases. The same stone palette and contour weight carry through this pass.')
html = html.replace('href="field-first-pass.html">Previous field pass', 'href="bold-field-study.html">Previous line-weight comparison')
html = html.replace('Before · previous field style', 'Before · bold contours')
html = html.replace('After · more, bolder lines and muted warmth', 'After · fine hatching and scuffs')
html = html.replace('Before: previous field style', 'Before: bold contours without the fine hatching')
html = html.replace('After: current field style', 'After: added fine cross-hatching and chisel marks')
html = html.replace('previous-all-ten-front.png', 'pre-hatch-all-ten-front.png').replace('previous-field-overview.png', 'pre-hatch-field-overview.png')
html = html.replace('Before is the field pass shown before your latest feedback.', 'Before is the immediately preceding pass: the same bold contours and cooler palette, without this fine detail layer.')
html = html.replace('Current pass: near-black drawing, broader strokes and a less saturated warm stone palette. Click the image for full size.', 'Current pass: fine hatching over the accepted bold drawing and muted stone palette. Click the image for full size.')
html = html.replace('Previous pass: finer graphite lines and the stronger yellow-orange cast. Same field seed and camera framing; captures run separately.', 'Previous pass: the same bold graphite and stone palette, before the fine hatching. Same seed and framing; captures run separately.')
html = html.replace('Shallow impact bowls and grouped scrapes add detail', 'Fine hatch bundles, shallow impact bowls and grouped scrapes add detail')
for name in ['field-overview.png', 'field-scene-lights.png', 'all-ten-front.png', 'all-ten-reverse.png', 'flight.csv', 'shapes.json', 'alignment.csv']:
    shutil.copy2(producer / name, preview / name)
shutil.copy2(producer / 'f_00000.png', preview / 'field-flight.png')
shutil.copy2(producer.with_suffix('.mp4'), preview / 'field-flight.mp4')
shutil.copy2(summary, preview / 'field-test-summary.json')
notes = (preview / 'pre-hatch-field-study-notes.md').read_text(encoding='utf-8-sig')
notes = notes.replace('20260926-204112', producer.name).replace('20260926-134050-summary.json', summary.name)
notes += '\nFine-detail follow-up: 14 separated surface patches per shape add 32–63 short strokes, including cross-hatching and chisel ticks. Their Fine crosshatching vertex group remains independently selectable in Blender. Only drawing FBXs, Blender sources and their README changed. The immediate before/after baseline is e9b86bf3; palette, contours, base mesh and normal maps are identical. The earlier line-weight comparison is preserved in bold-field-study.html.\n'
(preview / 'field-study-notes.md').write_text(notes, encoding='utf-8')
(preview / 'comparison.html').write_text(html, encoding='utf-8')
print(preview / 'comparison.html')
