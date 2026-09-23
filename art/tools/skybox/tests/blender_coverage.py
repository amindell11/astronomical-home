"""Run in background Blender with --python-exit-code 1 --python this_file -- --out DIR."""
import argparse
import json
import math
from pathlib import Path
import sys
import time

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import skybox
from skybox import skybox_merged, skybox_panel, skybox_preset

parser = argparse.ArgumentParser()
parser.add_argument('--out', required=True)
out = Path(parser.parse_args(sys.argv[sys.argv.index('--') + 1:]).out).resolve()
out.mkdir(parents=True, exist_ok=True)
skybox.register()
original = bpy.context.scene
settings = skybox_panel.get_settings(original)
report = {}
for coverage in (-0.2, -0.03424816578626633, 0, 0.07, 0.2):
    preset = skybox_preset.defaults(glow=True)
    preset['nebula']['coverage'] = coverage
    skybox_panel.apply_preset(settings, preset)
    value = skybox_panel.to_preset(settings)
    assert abs(value['nebula']['coverage'] - coverage) < 1e-8
    skybox_preset.save(out / 'roundtrip.json', value)
    assert skybox_preset.load(out / 'roundtrip.json') == value
settings.coverage_level = 0
baseline = skybox_panel.to_preset(settings)
settings.coverage_level = 1
fine = skybox_panel.to_preset(settings)
assert 0.00199 < fine['nebula']['coverage'] - baseline['nebula']['coverage'] < 0.00201
fine['nebula']['coverage'] = baseline['nebula']['coverage']
assert fine == baseline
for level in (-100, 0, 100):
    settings.coverage_level = level
    value = skybox_panel.to_preset(settings)
    assert value['nebula']['coverage'] == level / 500
    base = str(out / f'coverage-{level}')
    scene = skybox_merged.build_scene(value, base, 256, 16)
    try:
        start = time.perf_counter()
        bpy.ops.render.render(write_still=True)
        skybox_merged.save_outputs(scene, value, base, 'HDR', time.perf_counter() - start)
        image = bpy.data.images.load(base + '.hdr', check_existing=False)
        pixels = list(image.pixels)
        assert all(math.isfinite(v) for v in pixels)
        report[str(level)] = {'coverage': value['nebula']['coverage'], 'peak': max(pixels)}
        bpy.data.images.remove(image)
    finally:
        bpy.context.window.scene = original
        skybox_merged.dispose_scene(scene)
report['passed'] = True
(out / 'result.json').write_text(json.dumps(report), encoding='utf-8')
skybox.unregister()
print('COVERAGE_TESTS_PASSED')
