"""Blender authoring panel; rendering and preset interpretation live in the generator."""

import math
from pathlib import Path
import time

import bpy
from bpy.props import (CollectionProperty, EnumProperty, FloatProperty,
                       FloatVectorProperty, IntProperty, PointerProperty, StringProperty)
from bpy_extras.io_utils import ImportHelper, ExportHelper

from . import skybox_merged, skybox_preset, skybox_unity, skybox_preview


def longitude(star):
    x, _, z = star.direction
    return math.atan2(x, -z)


def latitude(star):
    x, y, z = star.direction
    return math.atan2(y, math.hypot(x, z))


def direction(lon, lat):
    return (math.cos(lat) * math.sin(lon), math.sin(lat), -math.cos(lat) * math.cos(lon))


class SkyboxStar(bpy.types.PropertyGroup):
    direction: FloatVectorProperty(size=3, default=(1, 0, 0))
    longitude: FloatProperty(name="Horizontal", subtype="ANGLE", min=-math.pi, max=math.pi,
                            get=longitude, set=lambda self, v: setattr(self, "direction", direction(v, latitude(self))))
    latitude: FloatProperty(name="Vertical", subtype="ANGLE", min=-math.pi/2, max=math.pi/2,
                           get=latitude, set=lambda self, v: setattr(self, "direction", direction(longitude(self), v)))
    radius: FloatProperty(name="Size", default=0.055, min=0.001, max=1, soft_max=0.2, precision=3)
    color: FloatVectorProperty(name="Color", subtype="COLOR", size=3, default=(1, 1, 1), min=0, max=1)
    strength: FloatProperty(name="Brightness", default=420, min=0, soft_max=1500)


class SkyboxSettings(bpy.types.PropertyGroup):
    variation: IntProperty(name="Cloud Variation", default=0, min=0, max=2147483647,
                          description="0 preserves the original clouds; other integers select repeatable cloud offsets")
    scale: FloatProperty(name="Cloud Scale", default=1, min=0.1, max=10, soft_max=3,
                         description="Higher values create smaller, more numerous cloud features")
    stretch: FloatVectorProperty(name="Stretch", size=3, default=(0.62, 1.05, 0.62), min=0.05, max=10, soft_max=3)
    rotation: FloatVectorProperty(name="Rotation", subtype="EULER", size=3, default=(0.20, -0.35, 0.58))
    coverage: FloatProperty(name="Cloud Coverage", default=0, min=-0.2, max=0.2,
                           description="Higher values create more dense gas; lower values leave more empty space")
    core_emission: FloatProperty(name="Core Emission", default=1.5, min=0, soft_max=4)
    palette0: FloatVectorProperty(name="Deep Violet", subtype="COLOR", size=3, min=0, max=1)
    palette1: FloatVectorProperty(name="Violet", subtype="COLOR", size=3, min=0, max=1)
    palette2: FloatVectorProperty(name="Blue", subtype="COLOR", size=3, min=0, max=1)
    palette3: FloatVectorProperty(name="Rose", subtype="COLOR", size=3, min=0, max=1)
    palette_scheme: EnumProperty(name="Color Family", items=[(key, value[0], "")
                                 for key, value in skybox_preset.PALETTES.items()])
    palette_seed: IntProperty(name="Palette Seed", default=0, min=0, max=2147483647)
    palette_variation: FloatProperty(name="Palette Variation", default=0.35, min=0, max=1,
                                   description="How far colors may drift from the chosen family")
    tiny_brightness: FloatProperty(name="Tiny Stars", default=0, min=0, soft_max=3)
    legacy_seed: FloatProperty(name="Legacy Tiny-Star Seed", default=7319)
    anchor_brightness: FloatProperty(name="Focal Stars", default=1, min=0, soft_max=4)
    anchors: CollectionProperty(type=SkyboxStar)
    selected_star: IntProperty(name="Star", default=1, min=1, max=5)
    unity_project: StringProperty(name="Unity Project", subtype="DIR_PATH")
    output_dir: StringProperty(name="Output Folder", subtype="DIR_PATH", default="")
    output_name: StringProperty(name="Sky Name", default="my-sky")
    draft_width: EnumProperty(name="Draft Size", items=[("512", "512", "Very quick shape checks"),
                              ("1024", "1K", "Shape and color"), ("2048", "2K", "More star detail")], default="1024")
    last_hdr: StringProperty(subtype="FILE_PATH")
    preview_image: PointerProperty(type=bpy.types.Image)
    status: StringProperty(default="Start from Nebula Glow, adjust, then render a draft.")


def apply_preset(settings, preset):
    nebula = preset["nebula"]
    for key in ("variation", "scale", "stretch", "rotation", "coverage", "core_emission"):
        setattr(settings, key, nebula[key])
    for index, color in enumerate(nebula["palette"]):
        setattr(settings, f"palette{index}", color)
    settings.tiny_brightness = preset["tiny_stars"]["brightness"]
    settings.legacy_seed = preset["tiny_stars"]["legacy_seed"]
    settings.anchor_brightness = preset["anchor_brightness"]
    settings.anchors.clear()
    for value in preset["anchors"]:
        star = settings.anchors.add()
        for key, item in value.items():
            setattr(star, key, item)


def get_settings(scene):
    settings = scene.skybox_authoring
    if not settings.output_dir:
        settings.output_dir = str(Path.home() / "Pictures" / "Skyboxes")
    if len(settings.anchors) == 0:
        apply_preset(settings, skybox_preset.defaults(glow=True))
    return settings


def to_preset(settings):
    return skybox_preset.parse({
        "schema_version": skybox_preset.SCHEMA_VERSION,
        "nebula": {
            "variation": settings.variation, "scale": settings.scale,
            "stretch": list(settings.stretch), "rotation": list(settings.rotation),
            "coverage": settings.coverage, "core_emission": settings.core_emission,
            "palette": [list(getattr(settings, f"palette{i}")) for i in range(4)],
        },
        "tiny_stars": {"brightness": settings.tiny_brightness, "legacy_seed": settings.legacy_seed},
        "anchor_brightness": settings.anchor_brightness,
        "anchors": [{"direction": list(star.direction), "radius": star.radius,
                     "color": list(star.color), "strength": star.strength} for star in settings.anchors],
    })


class SKYBOX_OT_load(bpy.types.Operator, ImportHelper):
    bl_idname = "skybox.load_preset"
    bl_label = "Load Preset"
    filename_ext = ".json"
    filter_glob: StringProperty(default="*.json", options={"HIDDEN"})

    def execute(self, context):
        try:
            preset = skybox_preset.load(self.filepath)
        except (OSError, ValueError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        apply_preset(get_settings(context.scene), preset)
        return {"FINISHED"}


class SKYBOX_OT_save(bpy.types.Operator, ExportHelper):
    bl_idname = "skybox.save_preset"
    bl_label = "Save Preset"
    filename_ext = ".json"
    filter_glob: StringProperty(default="*.json", options={"HIDDEN"})

    def execute(self, context):
        try:
            skybox_preset.save(self.filepath, to_preset(get_settings(context.scene)))
        except (OSError, ValueError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        return {"FINISHED"}


class SKYBOX_OT_glow(bpy.types.Operator):
    bl_idname = "skybox.nebula_glow"
    bl_label = "Load Nebula Glow"
    bl_description = "Restore the approved sky's authoring values"
    bl_options = {"UNDO"}

    def execute(self, context):
        apply_preset(get_settings(context.scene), skybox_preset.defaults(glow=True))
        return {"FINISHED"}


class SKYBOX_OT_palette(bpy.types.Operator):
    bl_idname = "skybox.palette"
    bl_label = "Generate Palette"
    bl_options = {"UNDO"}
    action: EnumProperty(items=[("BASE", "Use Scheme", "Use the exact named colors"),
                                ("SEED", "Apply Seed", "Reproduce this family's seeded variation"),
                                ("RANDOM", "Randomize", "Try the next seed within this color family")])

    def execute(self, context):
        settings = get_settings(context.scene)
        if self.action == "RANDOM":
            settings.palette_seed = (settings.palette_seed + 1) % 2147483648
        colors = skybox_preset.make_palette(settings.palette_scheme, settings.palette_seed,
                                            0 if self.action == "BASE" else settings.palette_variation)
        for index, color in enumerate(colors):
            setattr(settings, f"palette{index}", color)
        settings.status = "Palette updated. Refresh Draft to see it in the sky."
        return {"FINISHED"}


class SKYBOX_OT_render(bpy.types.Operator):
    bl_idname = "skybox.render"
    bl_label = "Render Skybox"
    final: bpy.props.BoolProperty(default=False)
    send_to_unity: bpy.props.BoolProperty(default=False)
    viewport: bpy.props.BoolProperty(default=False)
    active = False

    @classmethod
    def poll(cls, context):
        return not cls.active and not bpy.app.is_job_running("RENDER")

    def invoke(self, context, event):
        settings = get_settings(context.scene)
        try:
            self.preset = to_preset(settings)
            self.sky_name = skybox_unity.sky_name(settings.output_name)
            self.unity_project = skybox_unity.project_folder(bpy.path.abspath(settings.unity_project)) if self.send_to_unity else None
            folder = Path(bpy.path.abspath(settings.output_dir))
            folder.mkdir(parents=True, exist_ok=True)
        except (OSError, ValueError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        self.out_base = str(folder / (settings.output_name + ("-8k" if self.final else "-draft")))
        self.preview_space = context.space_data if self.viewport else None
        self.preview_session = skybox_preview.active
        self.preview_view = (skybox_preview.capture_view(self.preview_session.space)
                             if self.preview_session is not None else None)
        self.window = context.window
        self.previous = context.scene
        self.scene = skybox_merged.build_scene(self.preset, self.out_base,
                                               8192 if self.final else int(settings.draft_width),
                                               64 if self.final else 32)
        self.state = "rendering"
        self.started = time.perf_counter()
        bpy.app.handlers.render_complete.append(self.completed)
        bpy.app.handlers.render_cancel.append(self.cancelled)
        self.timer = context.window_manager.event_timer_add(0.2, window=self.window)
        type(self).active = True
        context.window_manager.modal_handler_add(self)
        try:
            display = context.preferences.view.render_display_type
            try:
                if self.viewport:
                    context.preferences.view.render_display_type = "NONE"
                bpy.ops.render.render("INVOKE_DEFAULT", write_still=True)
            finally:
                context.preferences.view.render_display_type = display
        except Exception:
            self.cleanup(context)
            raise
        return {"RUNNING_MODAL"}

    def completed(self, scene, *args):
        if scene == self.scene:
            self.state = "complete"

    def cancelled(self, scene, *args):
        if scene == self.scene:
            self.state = "cancelled"

    def modal(self, context, event):
        if event.type != "TIMER" or self.state == "rendering" or bpy.app.is_job_running("RENDER"):
            return {"PASS_THROUGH"}
        try:
            settings = self.previous.skybox_authoring
            if self.state == "cancelled":
                settings.status = "Render cancelled."
                return {"CANCELLED"}
            skybox_merged.save_outputs(self.scene, self.preset, self.out_base, "HDR", time.perf_counter()-self.started)
            skybox_preview.retain_image(settings, self.out_base)
            if self.preview_space is not None:
                skybox_preview.show_viewport(self.preview_space, settings.last_hdr)
            elif skybox_preview.active is not None:
                skybox_preview.active.refresh(settings.last_hdr)
            published = skybox_unity.publish(self.out_base, self.unity_project, self.sky_name, self.final) if self.send_to_unity else None
            settings.status = f"Sent {published.name}; switch to Unity > Tools > Skybox Preview." if published else f"Saved {self.out_base}.hdr"
            self.report({"INFO"}, settings.status)
            return {"FINISHED"}
        except Exception as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        finally:
            self.cleanup(context)

    def cleanup(self, context):
        bpy.app.handlers.render_complete.remove(self.completed)
        bpy.app.handlers.render_cancel.remove(self.cancelled)
        context.window_manager.event_timer_remove(self.timer)
        self.window.scene = self.previous
        skybox_merged.dispose_scene(self.scene)
        if self.preview_session is not None and self.preview_session is skybox_preview.active:
            skybox_preview.restore_view(self.preview_session.space, self.preview_view)
        type(self).active = False
        for area in self.window.screen.areas:
            area.tag_redraw()


class SKYBOX_OT_view_image(bpy.types.Operator):
    bl_idname = "skybox.view_image"
    bl_label = "View Last Render"

    @classmethod
    def poll(cls, context):
        return context.scene.skybox_authoring.preview_image is not None

    def execute(self, context):
        image = context.scene.skybox_authoring.preview_image
        bpy.ops.render.view_show("INVOKE_DEFAULT")
        skybox_preview.show_image(image)
        return {"FINISHED"}


class SKYBOX_OT_viewport(bpy.types.Operator):
    bl_idname = "skybox.viewport"
    bl_label = "Look Around Last Render"
    end: bpy.props.BoolProperty(default=False)

    @classmethod
    def poll(cls, context):
        return not SKYBOX_OT_render.active and context.area.type == "VIEW_3D"

    def execute(self, context):
        if self.end:
            skybox_preview.end_viewport()
        else:
            path = bpy.path.abspath(context.scene.skybox_authoring.last_hdr)
            if not Path(path).is_file():
                self.report({"ERROR"}, "Render a draft first, or click Refresh Draft")
                return {"CANCELLED"}
            skybox_preview.show_viewport(context.space_data, path)
        return {"FINISHED"}


class SKYBOX_PT_authoring(bpy.types.Panel):
    bl_label = "HDR Space Skybox"
    bl_idname = "SKYBOX_PT_authoring"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Skybox"

    def draw(self, context):
        layout = self.layout
        if SKYBOX_OT_render.active:
            layout.label(text="Rendering skybox. Esc in render view cancels.")
            return
        settings = context.scene.skybox_authoring
        if not settings.anchors:
            layout.operator("skybox.nebula_glow", text="Start with Nebula Glow")
            return
        row = layout.row(align=True)
        row.operator("skybox.load_preset")
        row.operator("skybox.save_preset")
        layout.operator("skybox.nebula_glow")
        box = layout.box()
        box.label(text="Nebula")
        for key in ("variation", "scale", "stretch", "rotation", "coverage"):
            box.prop(settings, key)
        box = layout.box()
        box.label(text="Nebula Colors")
        row = box.row(align=True)
        row.prop(settings, "palette_scheme", text="")
        row.operator("skybox.palette", text="Use Scheme").action = "BASE"
        box.prop(settings, "palette_variation")
        box.prop(settings, "palette_seed")
        row = box.row(align=True)
        row.operator("skybox.palette", text="Apply Seed").action = "SEED"
        row.operator("skybox.palette", text="Randomize").action = "RANDOM"
        row = box.row(align=True)
        for index in range(4):
            row.prop(settings, f"palette{index}", text="")
        box = layout.box()
        box.label(text="Stars and Emission")
        for key in ("tiny_brightness", "anchor_brightness", "core_emission"):
            box.prop(settings, key)
        box.label(text="Star glow uses Unity bloom.")
        box = layout.box()
        box.label(text="Focal Star Placement")
        box.prop(settings, "selected_star")
        star = settings.anchors[settings.selected_star - 1]
        for key in ("longitude", "latitude", "radius", "color", "strength"):
            box.prop(star, key)
        box = layout.box()
        box.label(text="Render")
        box.prop(settings, "output_dir")
        box.prop(settings, "output_name")
        box.prop(settings, "draft_width")
        row = box.row(align=True)
        row.operator("skybox.render", text="Render Draft").final = False
        row.operator("skybox.render", text="Export 8K HDR").final = True
        box.operator("skybox.view_image")
        box = layout.box()
        box.label(text="3D Preview")
        box.operator("skybox.render", text="Refresh Draft").viewport = True
        if skybox_preview.active is None:
            box.operator("skybox.viewport")
        else:
            box.operator("skybox.viewport", text="End Preview").end = True
        box.label(text="Middle-mouse drag: look around")
        box.label(text="Edit sliders, then Refresh Draft")
        box = layout.box()
        box.label(text="Unity Handoff")
        box.prop(settings, "unity_project")
        op = box.operator("skybox.render", text="Send Draft to Unity")
        op.send_to_unity = True
        op = box.operator("skybox.render", text="Send Final 8K")
        op.final = True
        op.send_to_unity = True
        box.label(text="Unity: Tools > Skybox Preview")
        layout.label(text=settings.status)


CLASSES = (SkyboxStar, SkyboxSettings, SKYBOX_OT_load, SKYBOX_OT_save,
           SKYBOX_OT_glow, SKYBOX_OT_palette, SKYBOX_OT_render, SKYBOX_OT_view_image,
           SKYBOX_OT_viewport, SKYBOX_PT_authoring)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.skybox_authoring = PointerProperty(type=SkyboxSettings)
    bpy.app.handlers.load_pre.append(skybox_preview.end_viewport)
    bpy.app.handlers.save_pre.append(skybox_preview.end_viewport)


def unregister():
    if SKYBOX_OT_render.active:
        raise RuntimeError("Finish or cancel the skybox render before disabling the add-on")
    skybox_preview.end_viewport()
    bpy.app.handlers.load_pre.remove(skybox_preview.end_viewport)
    bpy.app.handlers.save_pre.remove(skybox_preview.end_viewport)
    del bpy.types.Scene.skybox_authoring
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
