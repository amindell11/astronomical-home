"""Run with Blender --background --python-exit-code 1 --python this_file -- --out DIR."""
import argparse
import array
import json
from pathlib import Path
import sys
import time

import bpy
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import skybox
from skybox import skybox_merged, skybox_panel, skybox_preset

args = argparse.ArgumentParser()
args.add_argument("--out", required=True)
out = Path(args.parse_args(sys.argv[sys.argv.index("--")+1:]).out).resolve()
out.mkdir(parents=True, exist_ok=True)
skybox.register()
original = bpy.context.scene
objects = list(original.objects)
settings = skybox_panel.get_settings(original)
settings.anchors[0].longitude = 1.0
settings.anchors[0].latitude = 0.5
assert abs(settings.anchors[0].longitude-1.0) < 1e-6
assert abs(settings.anchors[0].latitude-0.5) < 1e-6
skybox_panel.apply_preset(settings, skybox_preset.defaults(glow=True))
preset = skybox_panel.to_preset(settings)
skybox_preset.save(out / "roundtrip.json", preset)
assert skybox_preset.load(out / "roundtrip.json") == preset


def render(value, name):
    base = str(out / name)
    scene = skybox_merged.build_scene(value, base, 256, 16)
    try:
        start = time.perf_counter()
        bpy.ops.render.render(write_still=True)
        skybox_merged.save_outputs(scene, value, base, "HDR", time.perf_counter()-start)
        image = bpy.data.images.load(base+".hdr", check_existing=False)
        pixels = array.array("f", [0])*len(image.pixels)
        image.pixels.foreach_get(pixels)
        bpy.data.images.remove(image)
    finally:
        bpy.context.window.scene = original
        skybox_merged.dispose_scene(scene)
    assert list(original.objects) == objects
    return pixels


def difference(a, b):
    return sum(abs(x-y) for x,y in zip(a,b))/len(a)


baseline = render(preset, "approved")
assert max(baseline) > 1.0
repeat = render(preset, "repeat")
varied = skybox_preset.parse(preset)
varied["nebula"]["variation"] = 23
variation = render(varied, "variation")
recolored = skybox_preset.parse(preset)
recolored["nebula"]["palette"] = [[g,b,r] for r,g,b in recolored["nebula"]["palette"]]
colors = render(recolored, "palette")
relocated = skybox_preset.parse(preset)
for star in relocated["anchors"]:
    star["strength"] = 0
relocated["anchors"][0].update(direction=list(skybox_panel.direction(0.0,0.0)), strength=2000)
stars = render(relocated, "focal-placement")
peak = max(range(0,len(stars),4), key=lambda i: max(stars[i:i+3]))//4
assert abs(peak%256-128) <= 2 and abs(peak//256-64) <= 2, peak
noise = difference(baseline,repeat)
report = {"repeat_mean_difference":noise, "variation_mean_difference":difference(baseline,variation),
          "palette_mean_difference":difference(baseline,colors), "focal_mean_difference":difference(baseline,stars),
          "hdr_peak":max(baseline), "preset_roundtrip":True, "original_scene_preserved":True}
for key in ("variation_mean_difference", "palette_mean_difference", "focal_mean_difference"):
    assert report[key] > max(1e-4,10*noise), (key,report)
(out/"blender-validation.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
skybox.unregister()
print("SKYBOX_TESTS_PASS",json.dumps(report))
