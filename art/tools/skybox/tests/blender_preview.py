"""Run in a windowed Blender with --factory-startup --python this_file -- --out DIR."""
import argparse
import json
from pathlib import Path
import sys
import traceback

import bpy
from mathutils import Quaternion

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import skybox
from skybox import skybox_panel, skybox_preview, skybox_preset

parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True)
out = Path(parser.parse_args(sys.argv[sys.argv.index("--")+1:]).out).resolve()
out.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.view.show_splash = False
skybox.register()
original = bpy.context.scene
settings = skybox_panel.get_settings(original)
settings.output_dir = str(out)
settings.output_name = "preview-test"
settings.draft_width = "512"
before_palette = skybox_panel.to_preset(settings)
settings.palette_scheme = "TEAL"
assert bpy.ops.skybox.palette(action="BASE") == {"FINISHED"}
assert bpy.ops.skybox.palette(action="RANDOM") == {"FINISHED"}
assert settings.palette_seed == 1
palette = skybox_panel.to_preset(settings)["nebula"]["palette"]
assert bpy.ops.skybox.palette(action="SEED") == {"FINISHED"}
after_palette = skybox_panel.to_preset(settings)
assert after_palette["nebula"]["palette"] == palette
after_palette["nebula"]["palette"] = before_palette["nebula"]["palette"]
assert after_palette == before_palette
skybox_panel.apply_preset(settings, before_palette)
objects = list(original.objects)
world = original.world
window = bpy.context.window
area = next(a for a in window.screen.areas if a.type == "VIEW_3D")
space = area.spaces.active
region = next(r for r in area.regions if r.type == "WINDOW")
shading = {key: getattr(space.shading, key) for key in skybox_preview.ViewportPreview.shading_keys}
overlays = space.overlay.show_overlays
lens = space.lens
view_rotation = space.region_3d.view_rotation.copy()
view_location = space.region_3d.view_location.copy()
view_distance = space.region_3d.view_distance
view_perspective = space.region_3d.view_perspective
visibility = {p.identifier: getattr(space, p.identifier) for p in space.bl_rna.properties
              if p.identifier.startswith("show_object_viewport_")}
bpy.context.preferences.view.render_display_type = "WINDOW"
phase = 0
attempts = 0
settled = False
first_pixels = None


def invoke(target_area=None, **kwargs):
    global settled
    settled = False
    target_area = target_area or area
    target_region = next(r for r in target_area.regions if r.type == "WINDOW")
    with bpy.context.temp_override(window=window, area=target_area, region=target_region):
        assert bpy.ops.skybox.render("INVOKE_DEFAULT", **kwargs) == {"RUNNING_MODAL"}


def tick():
    global phase, attempts, first_pixels, settled, other_space
    attempts += 1
    try:
        assert attempts < 300, "Preview test timed out"
        if phase == 0:
            invoke()
            phase = 1
            return .2
        if skybox_panel.SKYBOX_OT_render.active:
            return .2
        if not settled:
            settled = True
            return 1
        assert window.scene == original
        assert list(original.objects) == objects
        assert original.world == world
        assert not any(s.name.startswith("Skybox Render") for s in bpy.data.scenes)
        if phase == 1:
            images = [a.spaces.active.image for w in bpy.context.window_manager.windows
                      for a in w.screen.areas if a.type == "IMAGE_EDITOR"]
            assert any(i and i.has_data and i.size[0] == 512 for i in images), "Completed render disappeared"
            with bpy.context.temp_override(window=window, area=area, region=region):
                assert bpy.ops.skybox.view_image() == {"FINISHED"}
            first_pixels = list(settings.preview_image.pixels)
            settings.variation = 17
            invoke(viewport=True)
            phase = 2
            return .2
        if phase == 2:
            assert skybox_preview.active is not None
            assert space.shading.type == "MATERIAL"
            assert space.shading.selected_studio_light.path == str(skybox_preview.active.path)
            assert space.shading.studiolight_background_alpha == 1
            assert all(not getattr(space, key) for key in visibility)
            assert list(settings.preview_image.pixels) != first_pixels
            space.region_3d.view_rotation = Quaternion((0, 0, 1), .7)
            phase = 3
            area.tag_redraw()
            return 3
        if phase == 3:
            assert max(abs(a-b) for a, b in zip(space.region_3d.view_rotation, Quaternion((0, 0, 1), .7))) < 1e-5
            settings.variation = 18
            invoke(viewport=True)
            phase = 4
            return .2
        if phase == 4:
            assert max(abs(a-b) for a, b in zip(space.region_3d.view_rotation, Quaternion((0, 0, 1), .7))) < 1e-5
            with bpy.context.temp_override(window=window, area=area, region=region):
                bpy.ops.screen.screenshot(filepath=str(out / "viewport.png"))
            light_path = skybox_preview.active.path
            skybox_preview.end_viewport()
            assert all(getattr(space.shading, key) == value for key, value in shading.items())
            assert space.overlay.show_overlays == overlays
            assert space.lens == lens
            assert max(abs(a-b) for a, b in zip(space.region_3d.view_rotation, view_rotation)) < 1e-5
            assert (space.region_3d.view_location - view_location).length < 1e-5
            assert space.region_3d.view_distance == view_distance
            assert space.region_3d.view_perspective == view_perspective
            assert all(getattr(space, key) == value for key, value in visibility.items())
            assert not light_path.exists()
            assert original.world == world and list(original.objects) == objects
            assert settings.preview_image.has_data and settings.preview_image.size[0] == 512
            skybox_preview.show_viewport(space, settings.last_hdr)
            bpy.ops.wm.save_as_mainfile(filepath=str(out / "preview-test.blend"))
            assert skybox_preview.active is None
            assert all(getattr(space.shading, key) == value for key, value in shading.items())
            skybox_preview.show_viewport(space, settings.last_hdr)
            space.region_3d.view_rotation = Quaternion((1, 0, 0), .4)
            other_area = next(a for a in window.screen.areas if a.type == "PROPERTIES")
            other_area.type = "VIEW_3D"
            other_space = other_area.spaces.active
            invoke(target_area=other_area, viewport=True)
            phase = 5
            return .2
        if phase == 5:
            assert skybox_preview.active.space == other_space
            assert max(abs(a-b) for a, b in zip(space.region_3d.view_rotation, view_rotation)) < 1e-5
            assert all(getattr(space.shading, key) == value for key, value in shading.items())
            skybox_preview.end_viewport()
            (out / "result.json").write_text(json.dumps({"passed": True, "persistent_image": True,
                                                       "viewport_refreshed": True, "scene_preserved": True}))
            print("BLENDER_PREVIEW_PASSED", flush=True)
            bpy.ops.wm.quit_blender()
            return None
    except Exception:
        error = traceback.format_exc()
        (out / "result.json").write_text(json.dumps({"passed": False, "error": error}))
        traceback.print_exc()
        bpy.ops.wm.quit_blender()
        return None
    return .2


bpy.app.timers.register(tick, first_interval=2)
