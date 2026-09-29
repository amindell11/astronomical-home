"""Blender authoring panel; rendering and preset interpretation live in the generator."""

import math
import random
from pathlib import Path
import time

import bpy
from bpy.props import BoolProperty, EnumProperty, FloatProperty, FloatVectorProperty, IntProperty, PointerProperty, StringProperty
from bpy_extras.io_utils import ImportHelper, ExportHelper

from . import flat_preset, flat_render, flat_unity


NEBULA_FIELDS = ("variation", "scale", "stretch", "rotation", "coverage", "core_emission")
COLOR_FIELDS = tuple(f"palette{i}" for i in range(4))


class FlatBackgroundSettings(bpy.types.PropertyGroup):
    __annotations__ = {}
    for key in NEBULA_FIELDS + COLOR_FIELDS:
        __annotations__[f"lock_{key}"] = BoolProperty(
            name="Lock", description="Keep this setting during randomization and palette generation")
    flat_final_size: EnumProperty(name="Final Size", items=[("1024", "1K", "Compact"),
                                 ("2048", "2K", "Default cloud detail"), ("4096", "4K", "High detail")], default="2048")
    variation: IntProperty(name="Cloud Variation", default=0, min=0, max=2147483647,
                          description="0 preserves the original clouds; other integers select repeatable cloud offsets")
    scale: FloatProperty(name="Cloud Scale", default=1, min=0.1, max=10, soft_max=3,
                         description="Higher values create smaller, more numerous cloud features")
    stretch: FloatVectorProperty(name="Stretch", size=3, default=(0.62, 1.05, 0.62), min=0.05, max=10, soft_max=3)
    rotation: FloatVectorProperty(name="Rotation", subtype="EULER", size=3, default=(0.20, -0.35, 0.58))
    coverage: FloatProperty(name="Cloud Coverage", default=0, min=-0.2, max=0.2,
                           description="Higher values create more dense gas; lower values leave more empty space")
    coverage_level: FloatProperty(name="Cloud Coverage", min=-100, max=100, step=100, precision=1,
                                  get=lambda self: self.coverage * 500,
                                  set=lambda self, value: setattr(self, "coverage", value / 500),
                                  description="Relative coverage: 0 is the original sky; negative is sparse, positive is dense")
    core_emission: FloatProperty(name="Core Emission", default=1.5, min=0, soft_max=4)
    palette0: FloatVectorProperty(name="Shadow", subtype="COLOR", size=3, min=0, max=1,
                                  default=flat_preset.DEFAULT["nebula"]["palette"][0])
    palette1: FloatVectorProperty(name="Cloud", subtype="COLOR", size=3, min=0, max=1,
                                  default=flat_preset.DEFAULT["nebula"]["palette"][1])
    palette2: FloatVectorProperty(name="Highlight", subtype="COLOR", size=3, min=0, max=1,
                                  default=flat_preset.DEFAULT["nebula"]["palette"][2])
    palette3: FloatVectorProperty(name="Accent", subtype="COLOR", size=3, min=0, max=1,
                                  default=flat_preset.DEFAULT["nebula"]["palette"][3])
    palette_scheme: EnumProperty(name="Color Family", items=[(key, value[0], "")
                                 for key, value in flat_preset.PALETTES.items()])
    palette_seed: IntProperty(name="Palette Seed", default=0, min=0, max=2147483647)
    palette_variation: FloatProperty(name="Palette Variation", default=0.35, min=0, max=1,
                                   description="How far colors may drift from the chosen family")
    unity_project: StringProperty(name="Unity Project", subtype="DIR_PATH")
    output_dir: StringProperty(name="Output Folder", subtype="DIR_PATH", default="")
    output_name: StringProperty(name="Sky Name", default="my-sky")
    draft_width: EnumProperty(name="Draft Size", items=[("512", "512", "Very quick shape checks"),
                              ("1024", "1K", "Shape and color"), ("2048", "2K", "More cloud detail")], default="1024")
    preview_image: PointerProperty(type=bpy.types.Image)
    status: StringProperty(default="Start from Nebula Glow, adjust, then render a draft.")


def apply_preset(settings, preset):
    nebula = preset["nebula"]
    for key in NEBULA_FIELDS:
        setattr(settings, key, nebula[key])
    for index, color in enumerate(nebula["palette"]):
        setattr(settings, f"palette{index}", color)


def get_settings(scene):
    settings = scene.flat_background
    if not settings.output_dir:
        settings.output_dir = str(Path.home() / "Pictures" / "FlatBackgrounds")
    return settings


def to_preset(settings):
    return flat_preset.parse({
        "schema_version": flat_preset.SCHEMA_VERSION,
        "nebula": {
            "variation": settings.variation, "scale": settings.scale,
            "stretch": list(settings.stretch), "rotation": list(settings.rotation),
            "coverage": settings.coverage_level / 500, "core_emission": settings.core_emission,
            "palette": [list(getattr(settings, f"palette{i}")) for i in range(4)],
        },
    })


def show_image(image):
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "IMAGE_EDITOR" and area.spaces.active.image is not None:
                if area.spaces.active.image.type == "RENDER_RESULT":
                    area.spaces.active.image = image
                    area.tag_redraw()


class FLATBG_OT_load(bpy.types.Operator, ImportHelper):
    bl_idname = "flat_background.load_preset"
    bl_label = "Load Preset"
    filename_ext = ".json"
    filter_glob: StringProperty(default="*.json", options={"HIDDEN"})

    def execute(self, context):
        try:
            preset, migration = flat_preset.load(self.filepath)
        except (OSError, ValueError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        apply_preset(get_settings(context.scene), preset)
        if migration:
            self.report({"WARNING"}, " ".join(migration))
        return {"FINISHED"}


class FLATBG_OT_save(bpy.types.Operator, ExportHelper):
    bl_idname = "flat_background.save_preset"
    bl_label = "Save Preset"
    filename_ext = ".json"
    filter_glob: StringProperty(default="*.json", options={"HIDDEN"})

    def execute(self, context):
        try:
            flat_preset.save(self.filepath, to_preset(get_settings(context.scene)))
        except (OSError, ValueError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        return {"FINISHED"}


class FLATBG_OT_glow(bpy.types.Operator):
    bl_idname = "flat_background.nebula_glow"
    bl_label = "Load Nebula Glow"
    bl_description = "Restore the default authoring values"
    bl_options = {"UNDO"}

    def execute(self, context):
        apply_preset(get_settings(context.scene), flat_preset.defaults())
        return {"FINISHED"}


class FLATBG_OT_palette(bpy.types.Operator):
    bl_idname = "flat_background.palette"
    bl_label = "Generate Palette"
    bl_options = {"UNDO"}
    action: EnumProperty(items=[("BASE", "Use Scheme", "Use the exact named colors"),
                                ("SEED", "Apply Seed", "Reproduce this family's seeded variation"),
                                ("RANDOM", "Randomize", "Try the next seed within this color family")])

    def execute(self, context):
        settings = get_settings(context.scene)
        if self.action == "RANDOM":
            settings.palette_seed = (settings.palette_seed + 1) % 2147483648
        colors = flat_preset.make_palette(settings.palette_scheme, settings.palette_seed,
                                          0 if self.action == "BASE" else settings.palette_variation)
        for index, color in enumerate(colors):
            if not getattr(settings, f"lock_palette{index}"):
                setattr(settings, f"palette{index}", color)
        settings.status = "Palette updated. Render a draft to see it."
        return {"FINISHED"}


class FLATBG_OT_color_adjust(bpy.types.Operator):
    bl_idname = "flat_background.color_adjust"
    bl_label = "Adjust Nebula Colors"
    bl_options = {"UNDO"}
    channel: EnumProperty(items=[("HUE", "Hue", "Rotate hues by 10 degrees"),
                                 ("SATURATION", "Saturation", "Change saturation by 5 percentage points"),
                                 ("VALUE", "Brightness", "Multiply color brightness by 1.1 or its inverse")])
    decrease: BoolProperty(default=False)

    @classmethod
    def poll(cls, context):
        if context.mode != "OBJECT":
            cls.poll_message_set("Switch to Object Mode for reliable setting Undo")
            return False
        return True

    def execute(self, context):
        settings = get_settings(context.scene)
        colors = flat_preset.adjust_palette(
            [list(getattr(settings, key)) for key in COLOR_FIELDS], self.channel, self.decrease)
        for key, color in zip(COLOR_FIELDS, colors):
            setattr(settings, key, color)
        settings.status = "Colors adjusted. Render a draft to see the result."
        return {"FINISHED"}


class FLATBG_OT_randomize(bpy.types.Operator):
    bl_idname = "flat_background.randomize"
    bl_label = "Randomize Unlocked"
    bl_description = "Explore unlocked clouds, colors and core emission; keep output and render settings"
    bl_options = {"UNDO"}

    @classmethod
    def poll(cls, context):
        if context.mode != "OBJECT":
            cls.poll_message_set("Switch to Object Mode for reliable setting Undo")
            return False
        return True

    def execute(self, context):
        settings = get_settings(context.scene)
        rng = random.Random()
        values = {
            "variation": rng.randrange(2147483648), "scale": rng.uniform(0.65, 1.6),
            "stretch": [rng.uniform(0.4, 1.5) for _ in range(3)],
            "rotation": [rng.uniform(-math.pi, math.pi) for _ in range(3)],
            "coverage": rng.uniform(-0.07, 0.07), "core_emission": rng.uniform(0.6, 2.5),
        }
        settings.palette_seed = (settings.palette_seed + 1) % 2147483648
        colors = flat_preset.make_palette(settings.palette_scheme, settings.palette_seed,
                                          settings.palette_variation)
        values.update(zip(COLOR_FIELDS, colors))
        for key, value in values.items():
            if not getattr(settings, f"lock_{key}"):
                setattr(settings, key, value)
        settings.status = "Unlocked settings randomized. Render a draft to see the result."
        return {"FINISHED"}


def locked_control(layout, settings, key):
    row = layout.row(align=True)
    row.prop(settings, "coverage_level" if key == "coverage" else key)
    row.prop(settings, f"lock_{key}", text="", emboss=False,
             icon="LOCKED" if getattr(settings, f"lock_{key}") else "UNLOCKED")


class FLATBG_OT_render(bpy.types.Operator):
    bl_idname = "flat_background.render"
    bl_label = "Render Flat Background"
    final: bpy.props.BoolProperty(default=False)
    send_to_unity: bpy.props.BoolProperty(default=False)
    active = False

    @classmethod
    def poll(cls, context):
        return not cls.active and not bpy.app.is_job_running("RENDER")

    def invoke(self, context, event):
        settings = get_settings(context.scene)
        try:
            self.preset = to_preset(settings)
            self.sky_name = flat_unity.sky_name(settings.output_name)
            self.unity_project = flat_unity.project_folder(bpy.path.abspath(settings.unity_project)) if self.send_to_unity else None
            folder = Path(bpy.path.abspath(settings.output_dir))
            folder.mkdir(parents=True, exist_ok=True)
        except (OSError, ValueError) as exc:
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        self.out_base = str(folder / (settings.output_name + ("-final" if self.final else "-draft")))
        self.window = context.window
        self.previous = context.scene
        size = int(settings.flat_final_size if self.final else settings.draft_width)
        self.scene = flat_render.build_scene(self.preset, self.out_base, size, size, 8)
        self.state = "rendering"
        self.started = time.perf_counter()
        bpy.app.handlers.render_complete.append(self.completed)
        bpy.app.handlers.render_cancel.append(self.cancelled)
        self.timer = context.window_manager.event_timer_add(0.2, window=self.window)
        type(self).active = True
        context.window_manager.modal_handler_add(self)
        try:
            bpy.ops.render.render("INVOKE_DEFAULT", write_still=True)
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
            settings = self.previous.flat_background
            if self.state == "cancelled":
                settings.status = "Render cancelled."
                return {"CANCELLED"}
            flat_render.save_outputs(self.scene, self.preset, self.out_base,
                                     "final" if self.final else "draft", time.perf_counter()-self.started)
            image = bpy.data.images.load(self.out_base + "_preview.png", check_existing=False)
            old = settings.preview_image
            settings.preview_image = image
            if old is not None and old.users == 0:
                bpy.data.images.remove(old)
            published = (flat_unity.publish(self.out_base, self.unity_project, self.sky_name, self.final)
                         if self.send_to_unity else Path(self.out_base + ".exr"))
            settings.status = f"Saved {published.name}."
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
        flat_render.dispose_scene(self.scene)
        type(self).active = False
        for area in self.window.screen.areas:
            area.tag_redraw()


class FLATBG_OT_view_image(bpy.types.Operator):
    bl_idname = "flat_background.view_image"
    bl_label = "View Last Render"

    @classmethod
    def poll(cls, context):
        return context.scene.flat_background.preview_image is not None

    def execute(self, context):
        image = context.scene.flat_background.preview_image
        bpy.ops.render.view_show("INVOKE_DEFAULT")
        show_image(image)
        return {"FINISHED"}


class FLATBG_PT_authoring(bpy.types.Panel):
    bl_label = "Flat Background"
    bl_idname = "FLATBG_PT_authoring"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Flat Background"

    def draw(self, context):
        layout = self.layout
        if FLATBG_OT_render.active:
            layout.label(text="Rendering flat background. Esc in render view cancels.")
            return
        settings = context.scene.flat_background
        row = layout.row(align=True)
        row.operator("flat_background.load_preset")
        row.operator("flat_background.save_preset")
        layout.operator("flat_background.nebula_glow")
        layout.label(text="Starless clouds repeating in both axes.")
        if context.mode != "OBJECT":
            layout.label(text="Group edits need Object Mode for Undo.")
            layout.operator("object.mode_set", text="Switch to Object Mode").mode = "OBJECT"
        box = layout.box()
        box.label(text="Nebula")
        box.operator("flat_background.randomize", text="Randomize Nebula")
        for key in NEBULA_FIELDS:
            locked_control(box, settings, key)
        box = layout.box()
        box.label(text="Nebula Colors")
        row = box.row(align=True)
        row.prop(settings, "palette_scheme", text="")
        row.operator("flat_background.palette", text="Use Scheme").action = "BASE"
        box.prop(settings, "palette_variation")
        box.prop(settings, "palette_seed")
        row = box.row(align=True)
        row.operator("flat_background.palette", text="Apply Seed").action = "SEED"
        row.operator("flat_background.palette", text="Randomize").action = "RANDOM"
        for key in COLOR_FIELDS:
            locked_control(box, settings, key)
        for channel, label in (("HUE", "Hue"), ("SATURATION", "Saturation"), ("VALUE", "Brightness")):
            row = box.row(align=True)
            for decrease, sign in ((True, "-"), (False, "+")):
                op = row.operator("flat_background.color_adjust", text=f"{label} {sign}")
                op.channel = channel
                op.decrease = decrease
        box = layout.box()
        box.label(text="Render")
        box.prop(settings, "output_dir")
        box.prop(settings, "output_name")
        box.prop(settings, "draft_width")
        box.prop(settings, "flat_final_size")
        row = box.row(align=True)
        row.operator("flat_background.render", text="Render Draft").final = False
        row.operator("flat_background.render", text="Export Final").final = True
        box.operator("flat_background.view_image")
        box = layout.box()
        box.label(text="Unity Handoff")
        box.prop(settings, "unity_project")
        op = box.operator("flat_background.render", text="Send Draft to Unity")
        op.send_to_unity = True
        op = box.operator("flat_background.render", text="Send Final")
        op.final = True
        op.send_to_unity = True
        box.label(text="Unity: assign its material on Environment")
        layout.label(text=settings.status)


CLASSES = (FlatBackgroundSettings, FLATBG_OT_load, FLATBG_OT_save, FLATBG_OT_glow, FLATBG_OT_palette,
           FLATBG_OT_color_adjust, FLATBG_OT_randomize, FLATBG_OT_render, FLATBG_OT_view_image, FLATBG_PT_authoring)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.flat_background = PointerProperty(type=FlatBackgroundSettings)


def unregister():
    if FLATBG_OT_render.active:
        raise RuntimeError("Finish or cancel the flat background render before disabling the add-on")
    del bpy.types.Scene.flat_background
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
