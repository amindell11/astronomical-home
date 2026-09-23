"""Display completed images independently of the temporary render scene."""

import shutil
import tempfile
from pathlib import Path

import bpy
from bpy.app.handlers import persistent


def capture_view(space):
    return {key: getattr(space.region_3d, key).copy() if key in {"view_rotation", "view_location"}
            else getattr(space.region_3d, key)
            for key in ("view_perspective", "view_rotation", "view_location", "view_distance")}


def restore_view(space, view):
    for key, value in view.items():
        setattr(space.region_3d, key, value)


class ViewportPreview:
    shading_keys = ("type", "studio_light", "use_scene_world", "use_scene_lights",
                    "studiolight_background_alpha", "studiolight_background_blur",
                    "studiolight_intensity", "studiolight_rotate_z", "use_studiolight_view_rotation")

    def __init__(self, space):
        self.space = space
        self.shading = {key: getattr(space.shading, key) for key in self.shading_keys}
        self.visibility = {p.identifier: getattr(space, p.identifier)
                           for p in space.bl_rna.properties if p.identifier.startswith("show_object_viewport_")}
        self.overlays = space.overlay.show_overlays
        self.view = capture_view(space)
        self.directory = tempfile.TemporaryDirectory(prefix="skybox-preview-")
        self.path = Path(self.directory.name) / (Path(self.directory.name).name + ".hdr")
        self.light = None

    def refresh(self, path):
        if self.light is not None:
            bpy.context.preferences.studio_lights.remove(self.light)
        shutil.copyfile(path, self.path)
        self.light = bpy.context.preferences.studio_lights.load(str(self.path), "WORLD")
        shading = self.space.shading
        shading.type = "MATERIAL"
        shading.studio_light = self.light.name
        shading.use_scene_world = False
        shading.use_scene_lights = False
        shading.studiolight_background_alpha = 1
        shading.studiolight_background_blur = 0
        shading.studiolight_intensity = 1
        shading.studiolight_rotate_z = 0
        shading.use_studiolight_view_rotation = False
        self.space.overlay.show_overlays = False
        self.space.region_3d.view_perspective = "PERSP"
        for key in self.visibility:
            setattr(self.space, key, False)

    def close(self):
        try:
            self.space.shading.type = "MATERIAL"
            # The HDR enum is available only while material preview is active.
            for key in self.shading_keys[1:]:
                if key != "studio_light" or self.shading["type"] in {"MATERIAL", "RENDERED"}:
                    setattr(self.space.shading, key, self.shading[key])
            self.space.shading.type = self.shading["type"]
            if self.shading["type"] not in {"MATERIAL", "RENDERED"}:
                self.space.shading.studio_light = self.shading["studio_light"]
            self.space.overlay.show_overlays = self.overlays
            restore_view(self.space, self.view)
            for key, value in self.visibility.items():
                setattr(self.space, key, value)
        except ReferenceError:
            pass
        if self.light is not None:
            bpy.context.preferences.studio_lights.remove(self.light)
        self.directory.cleanup()


active = None


def show_viewport(space, path):
    global active
    if active is not None and active.space != space:
        end_viewport()
    if active is None:
        active = ViewportPreview(space)
    active.refresh(path)


@persistent
def end_viewport(*args):
    global active
    if active is not None:
        preview, active = active, None
        preview.close()


def retain_image(settings, out_base):
    path = out_base + "_preview.png"
    if settings.preview_image is None:
        settings.preview_image = bpy.data.images.load(path, check_existing=False)
    else:
        settings.preview_image.filepath = path
        settings.preview_image.reload()
    settings.last_hdr = out_base + ".hdr"
    show_image(settings.preview_image)


def show_image(image):
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "IMAGE_EDITOR" and area.spaces.active.image is not None:
                if area.spaces.active.image.type == "RENDER_RESULT":
                    area.spaces.active.image = image
                    area.tag_redraw()
