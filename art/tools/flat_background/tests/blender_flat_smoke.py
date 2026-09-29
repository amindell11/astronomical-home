"""Run with Blender -b --python-exit-code 1 -P this_file -- --out DIR."""
import argparse
import json
from pathlib import Path
import shutil
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from flat_background import flat_preset, flat_render, flat_unity

TOOL = Path(__file__).resolve().parents[1]
SIZE = 96

parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True)
out = Path(parser.parse_args(sys.argv[sys.argv.index("--") + 1:]).out).resolve()
out.mkdir(parents=True, exist_ok=True)

preset, migration = flat_preset.load(TOOL / "illustrated-blue.json")
assert migration == [], migration
assert not {"tiny_stars", "anchor_brightness", "anchors"} & set(preset), preset

original = bpy.context.window.scene
objects = list(original.objects)
scenes = set(bpy.data.scenes)
base = str(out / "illustrated-blue-draft")
scene = flat_render.build_scene(preset, base, SIZE, SIZE, 4)
try:
    started = time.perf_counter()
    bpy.ops.render.render(write_still=True)
    flat_render.save_outputs(scene, preset, base, "draft", time.perf_counter() - started, migration)
finally:
    bpy.context.window.scene = original
    flat_render.dispose_scene(scene)
assert bpy.context.window.scene == original and list(original.objects) == objects, "open scene not restored"
assert set(bpy.data.scenes) == scenes, "render scene leaked"

exr = Path(base + ".exr")
assert exr.is_file(), exr
image = bpy.data.images.load(str(exr), check_existing=False)
assert image.is_float and tuple(image.size) == (SIZE, SIZE), (image.is_float, tuple(image.size))
pixels = np.empty(len(image.pixels), np.float32)
image.pixels.foreach_get(pixels)
bpy.data.images.remove(image)
rgb = pixels.reshape(SIZE, SIZE, 4)[..., :3]
luminance = rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)
assert luminance.std() > 1e-3, "EXR is uniform"
padded = np.pad(luminance, 1, mode="wrap")
neighbours = np.max([padded[1 + dy:1 + dy + SIZE, 1 + dx:1 + dx + SIZE]
                     for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx], axis=0)
isolated_peaks = int(np.count_nonzero(luminance > 3 * neighbours + 0.02))
assert isolated_peaks == 0, f"{isolated_peaks} star-like single-pixel peaks"

sidecar = json.loads(Path(base + ".json").read_text(encoding="utf-8"))
assert (sidecar["schema_version"], sidecar["stage"], sidecar["width"], sidecar["height"]) == (1, "draft", SIZE, SIZE), sidecar
assert list(sidecar["palette"]) == ["base", "primary", "secondary", "accent"], sidecar["palette"]
assert all(len(color) == 3 for color in sidecar["palette"].values())
provenance = sidecar["provenance"]
assert provenance["generator"] == "4D torus clouds v1", provenance
assert provenance["preset"] == preset and provenance["preset"]["schema_version"] == 2, provenance["preset"]
assert provenance["migration"] == [], provenance["migration"]

project = out / "unity-project"
shutil.rmtree(project, ignore_errors=True)
(project / "Assets").mkdir(parents=True)
(project / "ProjectSettings").mkdir()
(project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.0.0f1\n")
published = flat_unity.publish(base, project, "illustrated-blue", False, **flat_unity.FLAT)
generated = project / "Assets/Visuals/Environment/Flat/Generated"
assert published == generated / "illustrated-blue-draft.exr", published
assert published.read_bytes() == exr.read_bytes()
assert json.loads((generated / "illustrated-blue-draft.json").read_text(encoding="utf-8")) == sidecar
staging = project / "Library/FlatBackgroundAuthoring"
assert staging.is_dir() and not any(staging.iterdir()), "staging missing or left behind"

report = {"size": SIZE, "max_rgb": float(rgb.max()), "luminance_std": float(luminance.std()),
          "isolated_peaks": isolated_peaks, "published": str(published)}
print("FLATBG_SMOKE_PASS", json.dumps(report))
