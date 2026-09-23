"""Run windowed Blender --factory-startup --python this_file -- --hdr SKY.hdr --out DIR."""
import argparse
import json
import math
from pathlib import Path
import sys
import traceback

import bpy
from mathutils import Euler

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from skybox import skybox_preview

parser = argparse.ArgumentParser()
parser.add_argument("--hdr", required=True)
parser.add_argument("--out", required=True)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
out = Path(args.out).resolve()
out.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.view.show_splash = False
bpy.context.preferences.view.smooth_view = 0
window = bpy.context.window
area = next(a for a in window.screen.areas if a.type == "VIEW_3D")
space = area.spaces.active
region = next(r for r in area.regions if r.type == "WINDOW")
original_lens = space.lens
phase = 0
samples = []


def capture(name):
    path = out / (name + ".png")
    with bpy.context.temp_override(window=window, area=area, region=region):
        bpy.ops.screen.screenshot_area(filepath=str(path))
    image = bpy.data.images.load(str(path), check_existing=False)
    width, height = image.size
    pixels = list(image.pixels)
    values = [pixels[(y * width + x) * 4 + channel]
              for y in range(height // 3, height * 2 // 3, 13)
              for x in range(width // 3, width * 2 // 3, 13)
              for channel in range(3)]
    bpy.data.images.remove(image)
    return values


def tick():
    global phase
    try:
        if phase == 0:
            skybox_preview.show_viewport(space, str(Path(args.hdr).resolve()))
            space.region_3d.view_rotation = Euler((math.pi / 2, 0, 0)).to_quaternion()
        elif phase == 1:
            samples.append(capture("object-forward"))
            space.region_3d.view_rotation = Euler((math.pi / 2, 0, math.pi / 2)).to_quaternion()
        elif phase == 2:
            samples.append(capture("object-right"))
            bpy.ops.object.mode_set(mode="EDIT")
            space.region_3d.view_rotation = Euler((math.pi / 2, 0, math.pi / 2)).to_quaternion()
        elif phase == 3:
            samples.append(capture("edit-right"))
            space.region_3d.view_rotation = Euler((math.pi / 2, 0, 0)).to_quaternion()
        elif phase == 4:
            samples.append(capture("edit-forward"))
            delta = sum(abs(a - b) for a, b in zip(samples[0], samples[1])) / len(samples[0])
            mode_delta = sum(abs(a - b) for a, b in zip(samples[1], samples[2])) / len(samples[1])
            edit_delta = sum(abs(a - b) for a, b in zip(samples[2], samples[3])) / len(samples[2])
            assert delta > .01 and edit_delta > .01, "Sky image stayed fixed while the view rotated 90 degrees"
            skybox_preview.end_viewport()
            assert space.lens == original_lens, "End Preview did not restore the original lens"
            result = {"rotation_image_difference": delta, "edit_rotation_difference": edit_delta,
                      "mode_image_difference": mode_delta, "passed": True}
            (out / "result.json").write_text(json.dumps(result), encoding="utf-8")
            print("ROTATION_RESULT", result, flush=True)
            bpy.ops.wm.quit_blender()
            return None
        area.tag_redraw()
        phase += 1
        return 2
    except Exception:
        result = {"passed": False, "error": traceback.format_exc()}
        (out / "result.json").write_text(json.dumps(result), encoding="utf-8")
        traceback.print_exc()
        bpy.ops.wm.quit_blender()
        return None


bpy.app.timers.register(tick, first_interval=2)
