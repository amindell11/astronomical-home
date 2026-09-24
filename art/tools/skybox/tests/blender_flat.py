"""Bounded real-Blender verification; run with --python-exit-code 1."""

import hashlib
import json
from pathlib import Path
import sys
import tempfile
import time
import zipfile

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from skybox import skybox_flat, skybox_merged, skybox_panel, skybox_preset

root = Path(__file__).resolve().parents[4] / "results/flat-background/blender"
root.mkdir(parents=True, exist_ok=True)
skybox_panel.register()
settings = skybox_panel.get_settings(bpy.context.scene)
settings.flat_background = True
settings.lock_palette0 = True
locked = tuple(settings.palette0)
bpy.ops.skybox.palette(action="RANDOM")
assert tuple(settings.palette0) == locked
preset = skybox_panel.to_preset(settings)
preset = skybox_preset.defaults(glow=True)
original = bpy.context.scene
reports = []
images = {}


def render(name, size, source=preset, stage="draft", height=None):
    base = root / name
    scene = skybox_flat.build_scene(source, base, size, height or size, 8)
    started = time.perf_counter()
    try:
        assert len(scene.objects) == 2
        bpy.ops.render.render(write_still=True)
        bundle_path = skybox_flat.save_outputs(scene, source, base, stage, time.perf_counter()-started)
        with zipfile.ZipFile(bundle_path) as bundle:
            manifest = json.loads(bundle.read("manifest.json"))
            raw = bundle.read("image.rgba16f")
        assert len(raw) == size * (height or size) * 8
        assert hashlib.sha256(raw).hexdigest() == manifest["pixel_sha256"]
        image = np.frombuffer(raw, dtype="<f2").astype(np.float32).reshape(height or size, size, 4)
        assert np.isfinite(image).all() and np.all(image[:, :, 3] == 1)
        rgb = image[:, :, :3]
        seam_x = np.mean(np.abs(rgb[:, 0]-rgb[:, -1]))
        seam_y = np.mean(np.abs(rgb[0]-rgb[-1]))
        step_x = np.mean(np.abs(rgb[:, 1:]-rgb[:, :-1]))
        step_y = np.mean(np.abs(rgb[1:]-rgb[:-1]))
        assert seam_x < 2 * step_x and seam_y < 2 * step_y, (seam_x, step_x, seam_y, step_y)
        reports.append({"size": [size, height or size], "stage": stage,
                        "max_linear_rgb": float(rgb.max()),
                        "percent_pixels_above_one": float(100 * np.mean(np.max(rgb, axis=2) > 1)),
                        "render_seconds": manifest["provenance"]["render_seconds"],
                        "seam_to_interior_x": float(seam_x/step_x),
                        "seam_to_interior_y": float(seam_y/step_y),
                        "raw_bytes": len(raw), "bundle_bytes": bundle_path.stat().st_size})
        return image, bundle_path
    finally:
        bpy.context.window.scene = original
        skybox_merged.dispose_scene(scene)


for size in (512, 1024, 2048):
    images[size], path = render(f"quality-{size}", size, stage="final" if size == 2048 else "draft")
_, rectangular = render("rectangular", 256, height=128)
changed_stars = skybox_preset.defaults(glow=True)
changed_stars["tiny_stars"]["brightness"] = 123
changed_stars["anchor_brightness"] = 456
repeat, repeat_path = render("repeat-star-independent", 512, changed_stars)
difference = np.abs(repeat - images[512])
assert np.all(difference <= np.spacing(np.maximum(repeat, images[512]).astype(np.float16)).astype(np.float32)), "Cloud output changed beyond half-float rounding"
reports.append({"star_independent_repeat_max_error": float(difference.max()),
                "star_independent_repeat_mean_error": float(difference.mean())})
for low in (512, 1024):
    high = images[low * 2].reshape(low, 2, low, 2, 4).mean(axis=(1, 3))
    error = np.abs(high[:, :, :3] - images[low][:, :, :3])
    reports.append({"comparison": f"{low} versus downsampled {low*2}",
                    "mean_linear_error": float(error.mean()), "p99_linear_error": float(np.percentile(error, 99))})
with tempfile.TemporaryDirectory() as folder:
    project = Path(folder)
    (project / "Assets").mkdir()
    (project / "ProjectSettings").mkdir()
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.1.8f1")
    target = skybox_flat.publish(repeat_path, project, "test")
    meta = target.with_suffix(".flatbg.meta")
    meta.write_text("guid: preserved")
    final = skybox_flat.publish(path, project, "test")
    final_hash = hashlib.sha256(final.read_bytes()).hexdigest()
    skybox_flat.publish(repeat_path, project, "test")
    assert meta.read_text() == "guid: preserved"
    assert hashlib.sha256(final.read_bytes()).hexdigest() == final_hash
    before = target.read_bytes()
    invalid = root / "invalid.flatbg"
    with zipfile.ZipFile(repeat_path) as source, zipfile.ZipFile(invalid, "w") as destination:
        for entry in source.namelist():
            destination.writestr(entry, b"bad" if entry == "image.rgba16f" else source.read(entry))
    try:
        skybox_flat.publish(invalid, project, "test")
        raise AssertionError("Corrupt bundle was published")
    except ValueError:
        pass
    assert target.read_bytes() == before
    invalid.unlink()
skybox_panel.unregister()
(root / "verification.json").write_text(json.dumps(reports, indent=2))
print("FLATBG_VERIFICATION=" + json.dumps(reports))
