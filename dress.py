"""Redress + layout mock on the armoured console (#1521 evidence 5a099e2), at the game camera.

Usage: blender -b console-armored.blend --python dress.py -- <out.png> <flags>
flags (comma list): sky, holo, lamps, screens, bloom, side, guard, lever  (or 'none')
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Matrix, Quaternion, Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUT, FLAGS = args[0], set(args[1].split(","))
PARAMS = dict(f.split("=", 1) for f in FLAGS if "=" in f)
HERE = Path(__file__).resolve().parent

# Knobs
SKY_GAMMA = 1.9
SKY_EMISSION = 0.06
HOLO_CYAN = (0.50, 0.92, 1.00, 1.0)
HOLO_CONTOUR_STRENGTH = 8.0
SPILL_CYAN = (0.45, 0.85, 1.00)
SPILL_ENERGY = 520.0
NAVY = (0.006, 0.016, 0.05, 1.0)   # linear, added under the in-game screen content
SCREEN_GAIN = 1.1
LAMP_STRENGTH = 9.0                 # production amber status glass is 3.5
LEGEND_GLOW = 1.4
LAUNCH_SELF_GLOW = 0.5              # keeps Launch orange under the cyan spill
SIDE_SCALE = 0.72                   # side-on hologram size relative to the pass-28 pose
SIDE_CENTRE = Vector((0.0, 0.3, 4.5))
SIDE_LENGTH = 6.2                   # nose to tail, world units: fills the gap between the terminals
ANGLE_YAW = -math.radians(float(__import__("os").environ.get("MOCK_YAW", "18")))  # left monitor yaw is 18, mirrored
ANGLE_PIVOT = Vector((6.65, 0.24, 0.0))   # weapon terminal footprint centre
AIR_DENSITY = 0.030                 # cabin haze; more lifts the sky blacks
HOLO_GLOW = 160.0                   # cyan light the hologram throws on the console, W
DOWNLIGHT_CONE = 260.0              # header downlight spots that show as cones in the haze, W
SCREEN_SPILL = 22.0                 # navy light each screen throws on its bezel, W
GRIME = 0.22                        # paint grime strength
CHIPS = 0.35                        # worn-edge chip strength
CREASE = 0.45                       # dirt settled in creases and inside corners
STRIPE_SCALE = 2.2                  # hazard stripe frequency on the Launch guard
BUILD = 0.60                        # storyboard: how much of the ship has scanned in at stage 2
# stencil text, pixel on the untouched layout, letter height
STENCILS = [("WEAPONS", 1426, 791, 0.26), ("COOLANT", 612, 818, 0.17)]

s = bpy.context.scene
s.cycles.samples = int(__import__("os").environ.get("MOCK_SAMPLES", "96"))
s.render.resolution_percentage = int(__import__("os").environ.get("MOCK_PCT", "100"))
s.cycles.use_denoising = True
if PARAMS.get("engine") == "eevee":
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            s.render.engine = eng
            break
        except TypeError:
            continue
    s.eevee.taa_render_samples = int(__import__("os").environ.get("MOCK_SAMPLES", "32"))
    for attr, val in (("use_volumetric_shadows", True), ("volumetric_tile_size", "4"), ("volumetric_samples", 96),
                      ("use_shadows", True), ("use_raytracing", True)):
        if hasattr(s.eevee, attr):
            setattr(s.eevee, attr, val)
    print("ENGINE", s.render.engine)
prefs = bpy.context.preferences.addons["cycles"].preferences
for backend in ("OPTIX", "CUDA", "HIP", "ONEAPI"):
    try:
        prefs.compute_device_type = backend
    except TypeError:
        continue
    prefs.get_devices()
    if any(d.type != "CPU" for d in prefs.devices):
        for d in prefs.devices:
            d.use = d.type != "CPU"
        s.cycles.device = "GPU"
        print("DEVICE", backend)
        break

mats, objs = bpy.data.materials, bpy.data.objects
W, H = s.render.resolution_x, s.render.resolution_y


def node(mat, node_type):
    return next(n for n in mat.node_tree.nodes if n.type == node_type)


def set_principled(name, color=None, emission=None, emission_color=None):
    p = node(mats[name], "BSDF_PRINCIPLED")
    if color:
        p.inputs["Base Color"].default_value = color
    if emission is not None:
        p.inputs["Emission Strength"].default_value = emission
    if emission_color:
        p.inputs["Emission Color"].default_value = emission_color


def ink_collections():
    return [bpy.data.collections["20 - Console ink selection"], bpy.data.collections["21 - Console hardware ink"]]


def add_mesh(name, bm, materials, bevel=0.03):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in materials:
        me.materials.append(mats[m] if isinstance(m, str) else m)
    o = objs.new(name, me)
    for c in ink_collections():
        c.objects.link(o)
    if bevel:
        mod = o.modifiers.new("Bevel", "BEVEL")
        mod.width, mod.segments, mod.limit_method = bevel, 2, "ANGLE"
    return o


def box(name, size, matrix, materials, bevel=0.03, taper_front=0.0):
    """Box centred on its base; taper_front lowers the front (-y) top edge by that much."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], (v.co.z + 0.5) * size[2]))
        if taper_front and v.co.z > 0 and v.co.y < 0:
            v.co.z -= taper_front
    o = add_mesh(name, bm, materials, bevel)
    o.matrix_world = matrix
    return o


def cylinder(name, radius, depth, matrix, materials, bevel=0.02):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=radius, radius2=radius, depth=depth)
    for v in bm.verts:
        v.co.z += depth / 2
    o = add_mesh(name, bm, materials, bevel)
    o.matrix_world = matrix
    return o


# ---------------------------------------------------------------- layout
if "side" in FLAGS:
    # The bright contours are baked per pose, so the profile pose comes in whole from pass 28
    # (ship in profile, nose right) and replaces this file's hologram, scaled to fit between the terminals.
    for o in s.objects:
        if "Vanguard" in o.name:
            o.hide_render = True
    # Only what rendered in pass 28: the file also keeps hidden reference copies of the ship.
    shown = {line.split("'")[1] for line in (HERE / "inspect3.txt").read_text().splitlines()
             if line.startswith("VIS '") and "Vanguard" in line}
    with bpy.data.libraries.load(str(HERE / "console-src.blend"), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in shown or n == "07 | Hologram assembly"]
    appended = set(dst.objects)
    for o in appended:
        s.collection.objects.link(o)
        for sl in o.material_slots:
            base = sl.material.name.rsplit(".", 1)[0] if sl.material else None
            if base in mats and sl.material.name != base:
                sl.link = "OBJECT"
                sl.material = mats[base]
    # The assembly's pass-28 parent stays behind, so put it back where pass 28 had it in world space;
    # the contour curves are unparented there and already land right.
    holo28 = next(o for o in appended if o.name.startswith("07 | Hologram assembly"))
    holo28.parent = None
    holo28.matrix_world = Matrix(((1.15942, 0.0, 0.0, 2.35), (0.0, 1.15078, -0.1413, 3.44928),
                                  (0.0, 0.1413, 1.15078, 7.22045), (0.0, 0.0, 0.0, 1.0)))
    tops = [o for o in appended if o.parent not in appended]
    # Measure where it landed, then fit it: SIDE_LENGTH long, centred on SIDE_CENTRE.
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in appended if o.type in ("MESH", "CURVE") for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    fit = (Matrix.Translation(SIDE_CENTRE) @ Matrix.Scale(SIDE_LENGTH / (hi.x - lo.x), 4)
           @ Matrix.Translation(-(lo + hi) / 2))
    for o in tops:
        o.matrix_world = fit @ o.matrix_world
    print("SIDE appended", len(appended), "landed", [round(v, 2) for v in lo], [round(v, 2) for v in hi])

if "guard" in FLAGS:
    # Raised cheeks either side of Launch: the button sits below their crest.
    for side in (-1, 1):
        box(f"Mock | Launch guard cheek {side}", (0.42, 1.70, 0.60),
            Matrix.Translation((side * 1.74, -1.9, 0.42)), ["21 | Warm limestone paint"], taper_front=0.26)
        box(f"Mock | Launch guard cheek cap {side}", (0.46, 0.36, 0.08),
            Matrix.Translation((side * 1.74, -1.24, 0.99)), ["21 | Graphite chassis"], bevel=0.02)

if "lever" in FLAGS:
    # A forward-leaning T-handle in Launch's recess; the button and its legend come out.
    objs["08 | Launch broad beveled button"].hide_render = True
    legend = objs["19 | Launch legend"]
    pivot = Vector((0.0, -1.85, 0.55))
    lean = math.radians(55)                       # toward the camera, clear of the projector lens
    shaft_dir = Vector((0.0, -math.sin(lean), math.cos(lean)))
    rot = Matrix.Rotation(lean, 4, "X")           # local +Z -> shaft_dir
    box("Mock | Lever plate", (2.6, 1.35, 0.12), Matrix.Translation((0, -1.9, 0.42)), ["21 | Graphite chassis"])
    box("Mock | Lever slot", (0.34, 1.05, 0.04), Matrix.Translation((0, -1.9, 0.54)), ["21 | Black elastomer"], bevel=0.01)
    cylinder("Mock | Lever boot", 0.26, 0.22, Matrix.Translation(pivot) @ rot, ["21 | Black elastomer"])
    shaft_len = 1.25
    cylinder("Mock | Lever shaft", 0.085, shaft_len, Matrix.Translation(pivot) @ rot, ["21 | Brushed warm nickel"])
    grip_centre = pivot + shaft_dir * (shaft_len + 0.17)
    box("Mock | Lever grip", (1.7, 0.36, 0.34), Matrix.Translation(grip_centre - shaft_dir * 0.17) @ rot,
        [mats["22 | Launch broad beveled button / Launch amber resin"]], bevel=0.06)
    face = grip_centre + shaft_dir * 0.175
    legend.parent = None
    legend.matrix_world = Matrix.Translation(face) @ rot @ Matrix.Diagonal((1.0, 1.0, 1.0, 1.0))

bpy.context.view_layer.update()

if "dock" in FLAGS:
    # Two launch gates outside the window, in front of the sky plane (y ~44): the corridor the ship leaves by.
    gate_mat = bpy.data.materials.new("Mock gate hull")
    gate_mat.use_nodes = True
    gp = gate_mat.node_tree.nodes["Principled BSDF"]
    gp.inputs["Base Color"].default_value = (0.025, 0.027, 0.032, 1)
    gp.inputs["Metallic"].default_value, gp.inputs["Roughness"].default_value = 0.4, 0.45

    def glow_mat(name, col, strength):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        p = m.node_tree.nodes["Principled BSDF"]
        p.inputs["Base Color"].default_value = col
        p.inputs["Emission Color"].default_value = col
        p.inputs["Emission Strength"].default_value = strength
        return m

    marker = glow_mat("Mock gate marker", (1.0, 0.55, 0.15, 1), 10.0)
    beacon = glow_mat("Mock gate beacon", (1.0, 0.1, 0.05, 1), 18.0)

    def plain_box(name, size, centre, mat):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=size, verts=bm.verts)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(mat)
        o = objs.new(name, me)
        s.collection.objects.link(o)
        o.location = centre
        return o

    def bulb(name, centre, r, mat):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(mat)
        o = objs.new(name, me)
        s.collection.objects.link(o)
        o.location = centre

    for gy, w, h, cz, t in ((24.0, 22.0, 13.0, 3.4, 1.0), (38.0, 19.0, 11.5, 3.9, 0.8)):
        hw, hh = w / 2, h / 2
        plain_box("Mock | Gate top", (w, t, t), (0, gy, cz + hh), gate_mat)
        plain_box("Mock | Gate bottom", (w, t, t), (0, gy, cz - hh), gate_mat)
        plain_box("Mock | Gate left", (t, t, h), (-hw, gy, cz), gate_mat)
        plain_box("Mock | Gate right", (t, t, h), (hw, gy, cz), gate_mat)
        n = int((w - 2) / 1.6)
        for i in range(n + 1):
            x = -hw + 1 + i * (w - 2) / n
            bulb("Mock | Gate marker", (x, gy - t / 2 - 0.05, cz - hh + t / 2 + 0.05), 0.09, marker)
            bulb("Mock | Gate marker", (x, gy - t / 2 - 0.05, cz + hh - t / 2 - 0.05), 0.09, marker)
        for sx in (-1, 1):
            for sz in (-1, 1):
                bulb("Mock | Gate beacon", (sx * hw, gy - t / 2 - 0.1, cz + sz * hh), 0.18, beacon)

STRIPE_MAT = None


def toon(name, colour=None):
    """A flat painted material shaped like the house paint: mostly emission, a little diffuse."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Strength"].default_value = 0.42
    mix = nt.nodes.new("ShaderNodeMixShader")
    mix.inputs["Fac"].default_value = 0.64
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(diff.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    if colour:
        diff.inputs["Color"].default_value = colour
        em.inputs["Color"].default_value = colour
    return m, nt, diff, em


def hazard_material():
    global STRIPE_MAT
    if STRIPE_MAT is None:
        m, nt, diff, em = toon("Mock hazard stripes")
        tc = nt.nodes.new("ShaderNodeTexCoord")
        wave = nt.nodes.new("ShaderNodeTexWave")
        wave.wave_type, wave.bands_direction, wave.wave_profile = "BANDS", "DIAGONAL", "SAW"
        wave.inputs["Scale"].default_value = STRIPE_SCALE
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "CONSTANT"
        ramp.color_ramp.elements[0].color = (0.85, 0.42, 0.04, 1)
        ramp.color_ramp.elements[1].position = 0.5
        ramp.color_ramp.elements[1].color = (0.03, 0.028, 0.025, 1)
        nt.links.new(tc.outputs["Object"], wave.inputs["Vector"])
        nt.links.new(wave.outputs["Fac"], ramp.inputs["Fac"])
        nt.links.new(ramp.outputs["Color"], diff.inputs["Color"])
        nt.links.new(ramp.outputs["Color"], em.inputs["Color"])
        STRIPE_MAT = m
    return STRIPE_MAT


if "shutter" in PARAMS:
    # An external blast shutter over the viewport, lifted by the given fraction (0 closed, 1 gone).
    lift = float(PARAMS["shutter"]) * 12.6
    for i in range(13):
        z = -1.2 + i * 0.95 + lift
        slat_mats = [hazard_material()] if i == 0 else ["21 | Graphite chassis"]
        box(f"Mock | Shutter slat {i}", (23.6, 0.2, 0.86), Matrix.Translation((0, 4.5, z)), slat_mats, bevel=0.03)

bpy.context.view_layer.update()

# ---------------------------------------------------------------- materials
if "sky" in FLAGS:
    m = mats["19 | Distant nebula"]
    nt = m.node_tree
    tex = node(m, "TEX_IMAGE")
    links = [l for l in nt.links if l.from_node == tex and l.from_socket.name == "Color"]
    g = nt.nodes.new("ShaderNodeGamma")
    g.inputs["Gamma"].default_value = SKY_GAMMA
    nt.links.new(tex.outputs["Color"], g.inputs["Color"])
    for l in links:
        nt.links.new(g.outputs["Color"], l.to_socket)
    node(m, "EMISSION").inputs["Strength"].default_value = SKY_EMISSION

if "holo" in FLAGS:
    node(mats["HOLO / bright contour"], "EMISSION").inputs["Color"].default_value = HOLO_CYAN
    node(mats["HOLO / bright contour"], "EMISSION").inputs["Strength"].default_value = HOLO_CONTOUR_STRENGTH
    for name, em in (("19 | Projected hull outlines", 4.0), ("19 | Projector optical emitter", 5.0)):
        if name in mats:
            set_principled(name, color=HOLO_CYAN, emission=em, emission_color=HOLO_CYAN)
        else:
            print("HOLO MISSING", name)
    for slot in objs["11 | Neutral projector frustum"].material_slots:
        for n in slot.material.node_tree.nodes:
            for key in ("Color", "Emission Color", "Base Color"):
                if key in n.inputs and n.inputs[key].type == "RGBA" and not n.inputs[key].is_linked:
                    n.inputs[key].default_value = HOLO_CYAN
            if n.type in ("EMISSION", "PRINCIPLED_VOLUME", "EMISSION_VOLUME") and "Strength" in n.inputs:
                n.inputs["Strength"].default_value *= 1.8
        print("BEAM MATERIAL", slot.material.name)
    spill = objs["19 | Projection spill"].data
    spill.color, spill.energy = SPILL_CYAN, SPILL_ENERGY
    # The cyan spill lands on Launch's top face and bleaches it; light-link Launch out of it.
    excl = bpy.data.collections.new("Mock | spill excludes")
    excl.objects.link(objs["08 | Launch broad beveled button"])
    objs["19 | Projection spill"].light_linking.receiver_collection = excl
    for co in excl.collection_objects:
        co.light_linking.link_state = "EXCLUDE"
    resin = "22 | Launch broad beveled button / Launch amber resin"
    set_principled(resin, emission=LAUNCH_SELF_GLOW * (2.2 if PARAMS.get("engine") == "eevee" else 1.0),
                   emission_color=tuple(node(mats[resin], "BSDF_PRINCIPLED").inputs["Base Color"].default_value))

if "screens" in FLAGS:
    # Every screen shows the in-game screenshot's content for that screen, over navy glass.
    shot = bpy.data.images.load(str(HERE / "current.png"))
    cam = s.camera

    def pixel_bbox(o):
        pts = [world_to_camera_view(s, cam, o.matrix_world @ Vector(c)) for c in o.bound_box]
        return min(p.x for p in pts) * W, (1 - max(p.y for p in pts)) * H, max(p.x for p in pts) * W, (1 - min(p.y for p in pts)) * H

    def screen_material(name, glass, rect, glued):
        m = mats["19 | Main rectangular display"].copy()
        m.name = name
        nt = m.node_tree
        for n in [n for n in nt.nodes if n.type in ("TEX_IMAGE", "UVMAP", "MIX_RGB")]:
            nt.nodes.remove(n)
        p = node(m, "BSDF_PRINCIPLED")
        p.inputs["Base Color"].default_value = (0, 0, 0, 1)
        p.inputs["Emission Strength"].default_value = SCREEN_GAIN
        p.inputs["Specular IOR Level"].default_value = 0.15   # keep cabin lights from hot-spotting bare glass
        p.inputs["Roughness"].default_value = 0.45
        bx0, by0, bx1, by1 = pixel_bbox(glass)
        cx0, cy0, cx1, cy1 = rect
        su, sv = (cx1 - cx0) / (bx1 - bx0), (cy1 - cy0) / (by1 - by0)
        tc = nt.nodes.new("ShaderNodeTexCoord")
        img = nt.nodes.new("ShaderNodeTexImage")
        img.image, img.extension = shot, "EXTEND"
        add = nt.nodes.new("ShaderNodeMix")
        add.data_type, add.blend_type = "RGBA", "ADD"
        add.inputs["Factor"].default_value = 1.0
        add.inputs[7].default_value = NAVY
        if glued:
            # Content follows the glass if the terminal moves: an affine map from the glass's own
            # coordinates to the screenshot, fitted at today's pose.
            mw, bb = glass.matrix_world, glass.bound_box
            x0, x1 = min(c[0] for c in bb), max(c[0] for c in bb)
            y0, y1 = min(c[1] for c in bb), max(c[1] for c in bb)
            zm = sum(c[2] for c in bb) / 8

            def uv(x, y):
                p = world_to_camera_view(s, cam, mw @ Vector((x, y, zm)))
                sx, sy = cx0 + (p.x * W - bx0) * su, cy0 + ((1 - p.y) * H - by0) * sv
                return sx / W, 1 - sy / H

            u00, u10, u01 = uv(x0, y0), uv(x1, y0), uv(x0, y1)
            comb = nt.nodes.new("ShaderNodeCombineXYZ")
            for axis in (0, 1):
                a = (u10[axis] - u00[axis]) / (x1 - x0)
                b = (u01[axis] - u00[axis]) / (y1 - y0)
                dot = nt.nodes.new("ShaderNodeVectorMath")
                dot.operation = "DOT_PRODUCT"
                dot.inputs[1].default_value = (a, b, 0)
                addc = nt.nodes.new("ShaderNodeMath")
                addc.inputs[1].default_value = u00[axis] - a * x0 - b * y0
                nt.links.new(tc.outputs["Object"], dot.inputs[0])
                nt.links.new(dot.outputs["Value"], addc.inputs[0])
                nt.links.new(addc.outputs[0], comb.inputs[axis])
            nt.links.new(comb.outputs[0], img.inputs["Vector"])
        else:
            mp = nt.nodes.new("ShaderNodeMapping")
            mp.inputs["Scale"].default_value = (su, sv, 1)
            mp.inputs["Location"].default_value = (cx0 / W - bx0 / W * su, 1 - cy0 / H - sv + by0 * sv / H, 0)
            nt.links.new(tc.outputs["Window"], mp.inputs["Vector"])
            nt.links.new(mp.outputs["Vector"], img.inputs["Vector"])
        fade = nt.nodes.new("ShaderNodeMix")
        fade.name = "Mock content fade"
        fade.data_type, fade.blend_type = "RGBA", "MIX"
        fade.inputs["Factor"].default_value = 1.0
        fade.inputs[6].default_value = (0, 0, 0, 1)
        nt.links.new(img.outputs["Color"], fade.inputs[7])
        nt.links.new(fade.outputs[2], add.inputs[6])
        nt.links.new(add.outputs[2], p.inputs["Emission Color"])
        print("SCREEN", name, "glass px", [round(v) for v in (bx0, by0, bx1, by1)], "-> shot", rect)
        return m

    # (glass object, slot material to replace or None for whole object, in-game rect in current.png)
    SCREENS = [
        ("07 | Main rectangular display", "19 | Main rectangular display", None),
        ("07 | Detail rectangular display", "19 | Detail rectangular display", None),
        ("30 | Primary blank glass", None, (1263, 485, 1584, 593)),
        ("30 | Ordnance blank glass", None, (1265, 631, 1590, 751)),
    ]
    for oname, slot_mat, rect in SCREENS:
        o = objs[oname]
        glued = rect is not None or "cam" in PARAMS or "anim" in PARAMS
        rect = rect or tuple(round(v) for v in pixel_bbox(o))   # left terminal matches the game exactly
        m = screen_material("Mock screen / " + oname, o, rect, glued)
        o.data = o.data.copy()
        if slot_mat:
            i = next(i for i, sl in enumerate(o.material_slots) if sl.material.name == slot_mat)
            o.data.materials[i] = m
        else:
            o.data.materials.clear()
            o.data.materials.append(m)
            for poly in o.data.polygons:
                poly.material_index = 0
    set_principled("Dark display glass", color=NAVY, emission=0.15, emission_color=NAVY)

# ---------------------------------------------------------------- lamps
SKIP = ("Vanguard", "11 | Neutral projector frustum", "25 | Viewport glass", "19 | Distant space", "Mock | Haze",
        "Mock | Mote", "Mock | Stencil", "Mock | Shutter", "Mock | Gate")


def visible(o):
    return not o.hide_render and not any(c.hide_render for c in o.users_collection) and not any(k in o.name for k in SKIP)


def pixel_ray(px, py):
    cam = s.camera
    tr, br, bl, tl = [cam.matrix_world @ v for v in cam.data.view_frame(scene=s)]
    point = tl + (tr - tl) * (px / W) + (bl - tl) * (py / H)
    origin = cam.matrix_world.translation
    return origin, (point - origin).normalized()


def raycast_visible(origin, direction):
    dg = bpy.context.evaluated_depsgraph_get()
    for _ in range(16):
        hit, loc, normal, _i, obj, _m = s.ray_cast(dg, origin, direction)
        if not hit:
            return None
        if visible(obj):
            return loc, normal, obj.name
        origin = loc + direction * 1e-3
    return None


LAMP_COLOURS = {"g": (0.15, 1.0, 0.35, 1.0), "a": (1.0, 0.49, 0.09, 1.0), "r": (1.0, 0.12, 0.06, 1.0),
                "w": (1.0, 0.80, 0.55, 1.0)}
LAMP_GLOW = {"w": 2.2}             # header downlights: a warm glow, not stage lights
# Each lamp belongs to a control, on a flat face, in a production lamp socket.
LAMPS = [
    ("power", 661, 873, "r", "Launch armed, left"),
    ("power", 1011, 873, "r", "Launch armed, right"),
    ("power", 1062, 852, "g", "projection status 1"),
    ("power", 1086, 852, "g", "projection status 2"),
    ("power", 1110, 852, "a", "projection status 3"),
    ("brow", 1299, 447, "a", "weapon terminal brow light"),
]
SOCKETS = {"power": ("08 | Power light socket", "08 | Power light lens"),
           "brow": ("08 | Brow light socket", "08 | Brow light lens")}

LAMPS_ON = []   # (object the lamp sits on, the objects that make the lamp)
HEADER_DOWN = []   # header downlight (position, direction)
HEADER_LIGHTS = []   # header downlight lamp objects, left to right
lens_mats = {}


def add_lamp(kind, loc, normal, key, energy=3.0):
    if key not in lens_mats:
        m = mats["19 | Amber status glass"].copy()
        m.name = "Mock lamp lens " + key
        set_principled(m.name, color=LAMP_COLOURS[key], emission_color=LAMP_COLOURS[key],
                       emission=LAMP_GLOW.get(key, LAMP_STRENGTH))
        lens_mats[key] = m
    sock, lens = objs[SOCKETS[kind][0]], objs[SOCKETS[kind][1]]
    rel = sock.matrix_world.inverted() @ lens.matrix_world
    scale = Matrix.Diagonal(sock.matrix_world.to_scale().to_4d())
    rot = normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
    m_sock = Matrix.Translation(loc + normal * 0.03) @ rot @ scale
    new_sock = sock.copy()
    new_sock.parent = None
    new_sock.matrix_world = m_sock
    new_lens = lens.copy()
    new_lens.data = lens.data.copy()
    new_lens.data.materials[0] = lens_mats[key]
    new_lens.parent = None
    new_lens.matrix_world = m_sock @ rel
    for c in sock.users_collection:
        c.objects.link(new_sock)
    for c in lens.users_collection:
        c.objects.link(new_lens)
    pl = bpy.data.lights.new("Mock lamp light", "POINT")
    pl.energy, pl.color, pl.shadow_soft_size = energy, LAMP_COLOURS[key][:3], 0.05
    lo = objs.new("Mock lamp light", pl)
    lo.location = loc + normal * 0.25
    s.collection.objects.link(lo)
    return [new_sock, new_lens, lo]


if "lamps" in FLAGS:
    set_principled("19 | Amber status glass", emission=LAMP_STRENGTH)
    legends = node(mats["19 | Ivory legends"], "BSDF_PRINCIPLED")
    legends.inputs["Emission Color"].default_value = legends.inputs["Base Color"].default_value
    legends.inputs["Emission Strength"].default_value = LEGEND_GLOW
    set_principled("25 | Launch letter backlight", emission=5.0)
    placed = 0
    for kind, px, py, key, why in LAMPS:
        hit = raycast_visible(*pixel_ray(px, py))
        if hit is None:
            print("LAMP MISS", why)
            continue
        loc, normal, on = hit
        LAMPS_ON.append((on, add_lamp(kind, loc, normal, key)))
        placed += 1
        print("LAMP", why, "on", on)
    print("LAMPS placed", placed, "of", len(LAMPS))

if "header" in FLAGS:
    # Frame detail under the viewport header: brackets in the header's paint, a conduit run through
    # them, a centre boss with a marker lamp, and warm downlights between the brackets.
    def bracket(name, x, width, drop_front, drop_back, y_front=3.35, y_back=3.95, top=10.42):
        bm = bmesh.new()
        profile = [(y_back, top), (y_front, top), (y_front, top - drop_front), (y_back, top - drop_back)]
        left = [bm.verts.new((x - width / 2, y, z)) for y, z in profile]
        right = [bm.verts.new((x + width / 2, y, z)) for y, z in profile]
        bm.faces.new(left[::-1])
        bm.faces.new(right)
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((left[i], left[j], right[j], right[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return add_mesh(name, bm, ["21 | Warm limestone paint"], bevel=0.025)

    for x in (-9.0, -6.2, -3.1, 3.1, 6.2, 9.0):
        bracket(f"Mock | Header bracket {x}", x, 0.32, 0.40, 0.78)
    bracket("Mock | Header centre boss", 0.0, 1.5, 0.55, 0.78)
    cylinder("Mock | Header conduit", 0.075, 18.8,
             Matrix.Translation((-9.4, 3.62, 10.12)) @ Matrix.Rotation(math.radians(90), 4, "Y"),
             ["21 | Graphite chassis"])
    bolt, bolt_sock = objs["08 | Large round captive bolt"], objs["08 | Captive bolt socket"]
    for bx in (-0.52, 0.52):
        for src in (bolt_sock, bolt):
            c = src.copy()
            c.parent = None
            rot = Vector((0, -1, 0)).to_track_quat("Z", "Y").to_matrix().to_4x4()
            c.matrix_world = (Matrix.Translation((bx, 3.33 - (0.02 if src is bolt else 0), 10.18)) @ rot
                              @ Matrix.Diagonal(src.matrix_world.to_scale().to_4d()))
            for col in src.users_collection:
                col.objects.link(c)
    if "lamps" in FLAGS:
        add_lamp("brow", Vector((0.0, 3.33, 10.18)), Vector((0, -1, 0)), "a")
        down = Vector((0, -0.5, -0.866))
        for i, x in enumerate((-7.6, -4.65, -1.6, 1.6, 4.65, 7.6)):
            key = f"w{i}" if "anim" in PARAMS else "w"
            LAMP_COLOURS[key], LAMP_GLOW[key] = LAMP_COLOURS["w"], LAMP_GLOW["w"]
            HEADER_LIGHTS.append(add_lamp("brow", Vector((x, 4.15, 10.39)), down, key, energy=5.0))
            HEADER_DOWN.append((Vector((x, 4.0, 10.25)), down))

if "air" in FLAGS:
    # Cabin haze so the beam, downlights and lamps hang in the air; the hologram and screens throw light.
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    me = bpy.data.meshes.new("Mock | Haze")
    bm.to_mesh(me)
    bm.free()
    haze = objs.new("Mock | Haze", me)
    s.collection.objects.link(haze)
    haze.location, haze.scale = (0, -0.6, 5.0), (23.0, 9.2, 12.0)
    hm = bpy.data.materials.new("Mock haze")
    hm.use_nodes = True
    hnt = hm.node_tree
    hnt.nodes.clear()
    vol = hnt.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (0.65, 0.78, 1.0, 1)
    vol.inputs["Density"].default_value = AIR_DENSITY
    vol.inputs["Anisotropy"].default_value = 0.45
    hout = hnt.nodes.new("ShaderNodeOutputMaterial")
    hnt.links.new(vol.outputs[0], hout.inputs["Volume"])
    me.materials.append(hm)

    def add_light(name, kind, loc, colour, energy, size=0.5, aim=None):
        ld = bpy.data.lights.new(name, kind)
        ld.color, ld.energy = colour, energy
        if kind == "AREA":
            ld.size = size
        else:
            ld.shadow_soft_size = size
        lo = objs.new(name, ld)
        s.collection.objects.link(lo)
        lo.location = loc
        if aim is not None:   # area lights shine down their local -Z
            lo.rotation_mode = "QUATERNION"
            lo.rotation_quaternion = (-aim).to_track_quat("Z", "Y")
        lo.visible_camera = False
        return lo

    holo_pts = [o.matrix_world @ Vector(c) for o in s.objects
                if "Vanguard" in o.name and o.type == "MESH" and not o.hide_render for c in o.bound_box]
    holo_c = sum(holo_pts, Vector()) / len(holo_pts)
    add_light("Mock | Hologram glow", "POINT", holo_c, HOLO_CYAN[:3], HOLO_GLOW, size=1.4)
    for pos, aim in HEADER_DOWN:
        cone = add_light("Mock | Downlight cone", "SPOT", pos, (1.0, 0.80, 0.55), DOWNLIGHT_CONE, size=0.15)
        cone.data.spot_size, cone.data.spot_blend = math.radians(48), 0.7
        cone.rotation_mode = "QUATERNION"
        cone.rotation_quaternion = (-aim).to_track_quat("Z", "Y")
    haze_out = bpy.data.collections.new("Mock | haze excluded")
    haze_out.objects.link(haze)
    for co in haze_out.collection_objects:
        co.light_linking.link_state = "EXCLUDE"
    for name in ("19 | Screen bounce", "19 | Soft ambient readability", "19 | Faint starlight"):
        objs[name].visible_volume_scatter = False   # fill lights light surfaces, not the air
    cam_pos = s.camera.matrix_world.translation
    for oname in ("07 | Main rectangular display", "07 | Detail rectangular display",
                  "30 | Primary blank glass", "30 | Ordnance blank glass"):
        o = objs[oname]
        big = max(o.data.polygons, key=lambda p: p.area)
        n = (o.matrix_world.to_3x3() @ big.normal).normalized()
        c = o.matrix_world @ big.center
        if n.dot(cam_pos - c) < 0:
            n = -n
        pts = [o.matrix_world @ Vector(k) for k in o.bound_box]
        size = max((max(p[i] for p in pts) - min(p[i] for p in pts)) for i in range(3))
        lo = add_light("Mock | Screen spill", "AREA", c + n * 0.25, (0.35, 0.55, 1.0), SCREEN_SPILL, size=size * 0.8, aim=n)
        lo.visible_volume_scatter = False   # tint the bezel, not the air in front of the glass
        LAMPS_ON.append((oname, [lo]))
    if "motes" in FLAGS:   # dust in the beam; dropped from the chosen look
        import random
        rnd = random.Random(7)
        mote = bpy.data.materials.new("Mock mote")
        mote.use_nodes = True
        mnode = mote.node_tree.nodes["Principled BSDF"]
        mnode.inputs["Base Color"].default_value = (0.7, 0.95, 1.0, 1)
        mnode.inputs["Emission Color"].default_value = (0.7, 0.95, 1.0, 1)
        mnode.inputs["Emission Strength"].default_value = 6.0
        for _ in range(46):
            z = rnd.uniform(1.6, 6.4)
            spread = 0.9 + (z - 1.6) * 0.42           # inside the widening beam
            x, y = rnd.uniform(-spread, spread), rnd.uniform(-spread * 0.45, spread * 0.45)
            bm = bmesh.new()
            bmesh.ops.create_icosphere(bm, subdivisions=1, radius=rnd.uniform(0.012, 0.026))
            me = bpy.data.meshes.new("Mock | Mote")
            bm.to_mesh(me)
            bm.free()
            me.materials.append(mote)
            o = objs.new("Mock | Mote", me)
            s.collection.objects.link(o)
            o.location = (x, y, z)

if "wear" in FLAGS:
    # Grime in the paint, lighter chips on worn edges, hazard stripes on the Launch guard, stencils.
    for name in ("21 | Warm limestone paint", "21 | Paint edge", "21 | Secondary cover paint"):
        m = mats.get(name)
        if m is None or "A few drawn surface marks" not in m.node_tree.nodes:
            print("WEAR SKIP", name)
            continue
        nt = m.node_tree
        src = nt.nodes["A few drawn surface marks"].outputs["Color"]
        targets = [l.to_socket for l in nt.links if l.from_socket == src]
        tc = nt.nodes["Local paint coordinates"]
        grime_noise = nt.nodes.new("ShaderNodeTexNoise")
        grime_noise.inputs["Scale"].default_value = 0.35
        grime_noise.inputs["Detail"].default_value = 2.0
        grime_ramp = nt.nodes.new("ShaderNodeValToRGB")
        grime_ramp.color_ramp.elements[0].position, grime_ramp.color_ramp.elements[1].position = 0.40, 0.85
        nt.links.new(tc.outputs["Object"], grime_noise.inputs["Vector"])
        nt.links.new(grime_noise.outputs["Fac"], grime_ramp.inputs["Fac"])
        gfac = nt.nodes.new("ShaderNodeMath")
        gfac.operation = "MULTIPLY"
        gfac.inputs[1].default_value = GRIME
        nt.links.new(grime_ramp.outputs["Color"], gfac.inputs[0])
        crease_geo = nt.nodes.new("ShaderNodeNewGeometry")
        crease = nt.nodes.new("ShaderNodeValToRGB")
        crease.color_ramp.elements[0].position, crease.color_ramp.elements[0].color = 0.40, (1, 1, 1, 1)
        crease.color_ramp.elements[1].position, crease.color_ramp.elements[1].color = 0.49, (0, 0, 0, 1)
        nt.links.new(crease_geo.outputs["Pointiness"], crease.inputs["Fac"])
        cfac_crease = nt.nodes.new("ShaderNodeMath")
        cfac_crease.operation = "MULTIPLY"
        cfac_crease.inputs[1].default_value = CREASE
        nt.links.new(crease.outputs["Color"], cfac_crease.inputs[0])
        dirt = nt.nodes.new("ShaderNodeMath")
        dirt.operation, dirt.use_clamp = "ADD", True
        nt.links.new(gfac.outputs[0], dirt.inputs[0])
        nt.links.new(cfac_crease.outputs[0], dirt.inputs[1])
        gfac = dirt
        grime = nt.nodes.new("ShaderNodeMix")
        grime.data_type, grime.blend_type = "RGBA", "MULTIPLY"
        grime.inputs[7].default_value = (0.60, 0.54, 0.46, 1)
        nt.links.new(gfac.outputs[0], grime.inputs["Factor"])
        nt.links.new(src, grime.inputs[6])
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        edge_ramp = nt.nodes.new("ShaderNodeValToRGB")
        edge_ramp.color_ramp.elements[0].position, edge_ramp.color_ramp.elements[1].position = 0.57, 0.63
        chip_noise = nt.nodes.new("ShaderNodeTexNoise")
        chip_noise.inputs["Scale"].default_value = 14.0
        chip_ramp = nt.nodes.new("ShaderNodeValToRGB")
        chip_ramp.color_ramp.elements[0].position, chip_ramp.color_ramp.elements[1].position = 0.48, 0.58
        nt.links.new(geo.outputs["Pointiness"], edge_ramp.inputs["Fac"])
        nt.links.new(tc.outputs["Object"], chip_noise.inputs["Vector"])
        nt.links.new(chip_noise.outputs["Fac"], chip_ramp.inputs["Fac"])
        cfac = nt.nodes.new("ShaderNodeMath")
        cfac.operation = "MULTIPLY"
        nt.links.new(edge_ramp.outputs["Color"], cfac.inputs[0])
        nt.links.new(chip_ramp.outputs["Color"], cfac.inputs[1])
        cfac2 = nt.nodes.new("ShaderNodeMath")
        cfac2.operation = "MULTIPLY"
        cfac2.inputs[1].default_value = CHIPS
        nt.links.new(cfac.outputs[0], cfac2.inputs[0])
        chip = nt.nodes.new("ShaderNodeMix")
        chip.data_type, chip.blend_type = "RGBA", "MIX"
        chip.inputs[7].default_value = (0.88, 0.82, 0.70, 1)
        nt.links.new(cfac2.outputs[0], chip.inputs["Factor"])
        nt.links.new(grime.outputs[2], chip.inputs[6])
        for t in targets:
            nt.links.new(chip.outputs[2], t)
        print("WEAR", name)
    ink = toon("Mock stencil ink", (0.03, 0.025, 0.02, 1))[0]
    font = objs["19 | Launch legend"].data.font

    def frame(n):
        ref = Vector((0, 1, 0)) if abs(n.z) > 0.7 else Vector((0, 0, 1))
        y = (ref - ref.dot(n) * n).normalized()
        x = y.cross(n)
        return Matrix((x, y, n)).transposed().to_4x4()

    def stencil(text, loc, n, size, host=None):
        cu = bpy.data.curves.new("Mock stencil " + text, "FONT")
        cu.body, cu.font, cu.size = text, font, size
        cu.align_x, cu.align_y = "CENTER", "CENTER"
        cu.materials.append(ink)
        o = objs.new("Mock | Stencil " + text, cu)
        s.collection.objects.link(o)
        o.matrix_world = Matrix.Translation(loc + n * 0.012) @ frame(n)
        if host:
            LAMPS_ON.append((host, [o]))
        print("STENCIL", text, "on", host)

    for text, px, py, size in STENCILS:
        hit = raycast_visible(*pixel_ray(px, py))
        if hit is None:
            print("STENCIL MISS", text)
            continue
        loc, n, on = hit
        stencil(text, loc, n, size, host=on)
    if "header" in FLAGS:
        stencil("DOCK 07", Vector((0.0, 3.33, 9.98)), Vector((0, -1, 0)), 0.15)

if "angle" in FLAGS:
    # Yaw the weapon terminal toward the centre, mirroring the left monitor's 18 degrees.
    term = objs["31 | Weapon terminal frame placement"]

    def under(o, root):
        while o:
            if o == root:
                return True
            o = o.parent
        return False

    turn = (Matrix.Translation(ANGLE_PIVOT) @ Matrix.Rotation(ANGLE_YAW, 4, "Z")
            @ Matrix.Translation(-ANGLE_PIVOT))
    movers = [term, objs["31 | Terminal fitted lower skirt"]]
    movers += [o for on, made in LAMPS_ON if under(objs[on], term) for o in made]
    for o in movers:
        o.matrix_world = turn @ o.matrix_world

BOOT = next((int(f[4:]) for f in FLAGS if f.startswith("boot") and f[4:].isdigit()), None)
if BOOT is not None:
    # Entry storyboard: 0 dark cabin, 1 lamps and screens wake, 2 projector lights and the ship scans in.
    def emission(m, v):
        for n in m.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                n.inputs["Emission Strength"].default_value = v
            elif n.type == "EMISSION":
                n.inputs["Strength"].default_value = v

    screens = [m for m in mats if m.name.startswith("Mock screen /")]
    lenses = [mats["19 | Amber status glass"]] + [m for m in mats if m.name.startswith("Mock lamp lens")]
    holo_objs = [o for o in s.objects if "Vanguard" in o.name and not o.hide_render]
    lamp_lights = [o for o in s.objects if o.type == "LIGHT" and o.name.startswith("Mock lamp light")]
    if BOOT <= 1:
        for o in holo_objs + [objs["11 | Neutral projector frustum"]] + [o for o in s.objects if o.name.startswith("Mock | Mote")]:
            o.hide_render = True
        objs["19 | Projection spill"].data.energy = 0.0
        if "19 | Projector optical emitter" in mats:
            emission(mats["19 | Projector optical emitter"], 0.0)
        if "Mock | Hologram glow" in objs:
            objs["Mock | Hologram glow"].data.energy = 0.0
    if BOOT == 0:
        for m in screens + lenses + [mats["25 | Launch letter backlight"], mats["19 | Ivory legends"],
                                     mats["22 | Launch broad beveled button / Launch amber resin"], mats["Dark display glass"]]:
            emission(m, 0.0)
        for o in lamp_lights + [o for o in s.objects if o.name.startswith(("Mock | Screen spill", "Mock | Downlight cone"))]:
            o.data.energy = 0.0
        for m in lenses:   # unlit glass reads dark, not pale
            p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            p.inputs["Base Color"].default_value = tuple(c * 0.12 for c in p.inputs["Base Color"].default_value[:3]) + (1,)
        objs["19 | Screen bounce"].data.energy = 0.0
    if BOOT == 1:
        for m in screens:   # screens wake to bare navy before the content arrives
            add = next(n for n in m.node_tree.nodes if n.type == "MIX" and n.blend_type == "ADD")
            for l in list(add.inputs[6].links):
                m.node_tree.links.remove(l)
            add.inputs[6].default_value = (0, 0, 0, 1)
    if BOOT == 2:
        objs["19 | Projection spill"].data.energy = SPILL_ENERGY * 0.6
        span = [(o.matrix_world @ Vector(c)).z for o in holo_objs for c in o.bound_box]
        cut = min(span) + BUILD * (max(span) - min(span))
        for m in {sl.material for o in holo_objs for sl in o.material_slots if sl.material}:
            nt = m.node_tree
            out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output)
            orig = out.inputs["Surface"].links[0].from_socket
            geo = nt.nodes.new("ShaderNodeNewGeometry")
            sep = nt.nodes.new("ShaderNodeSeparateXYZ")
            nt.links.new(geo.outputs["Position"], sep.inputs[0])
            above = nt.nodes.new("ShaderNodeMath")
            above.operation = "GREATER_THAN"
            above.inputs[1].default_value = cut
            nt.links.new(sep.outputs["Z"], above.inputs[0])
            clear = nt.nodes.new("ShaderNodeBsdfTransparent")
            mix = nt.nodes.new("ShaderNodeMixShader")
            nt.links.new(above.outputs[0], mix.inputs["Fac"])
            nt.links.new(orig, mix.inputs[1])
            nt.links.new(clear.outputs[0], mix.inputs[2])
            near = nt.nodes.new("ShaderNodeMath")
            near.operation = "GREATER_THAN"
            near.inputs[1].default_value = cut - 0.14
            nt.links.new(sep.outputs["Z"], near.inputs[0])
            band = nt.nodes.new("ShaderNodeMath")
            band.operation = "SUBTRACT"
            nt.links.new(near.outputs[0], band.inputs[0])
            nt.links.new(above.outputs[0], band.inputs[1])
            gain = nt.nodes.new("ShaderNodeMath")
            gain.operation = "MULTIPLY"
            gain.inputs[1].default_value = 14.0
            nt.links.new(band.outputs[0], gain.inputs[0])
            em = nt.nodes.new("ShaderNodeEmission")
            em.inputs["Color"].default_value = HOLO_CYAN
            nt.links.new(gain.outputs[0], em.inputs["Strength"])
            addsh = nt.nodes.new("ShaderNodeAddShader")
            nt.links.new(mix.outputs[0], addsh.inputs[0])
            nt.links.new(em.outputs[0], addsh.inputs[1])
            nt.links.new(addsh.outputs[0], out.inputs["Surface"])
        print("BOOT build cut", round(cut, 2))
    print("BOOT stage", BOOT)

if "cam" in PARAMS:
    # Settle-in: the view starts higher and further back, looking down, and lowers into the seat.
    f = float(PARAMS["cam"])
    cam = s.camera
    end_loc, end_rot = cam.matrix_world.to_translation(), cam.matrix_world.to_quaternion()
    start_loc = end_loc + Vector((0.0, -1.3, 1.8))
    start_rot = end_rot @ Quaternion((1, 0, 0), -0.13)
    ease = f * f * (3 - 2 * f)
    cam.matrix_world = (Matrix.Translation(start_loc.lerp(end_loc, ease))
                        @ start_rot.slerp(end_rot, ease).to_matrix().to_4x4())

if "bloom" in FLAGS:
    ng = s.compositing_node_group
    out = next(n for n in ng.nodes if n.type == "GROUP_OUTPUT")
    src = out.inputs[0].links[0].from_socket
    glare = ng.nodes.new("CompositorNodeGlare")
    for name, val in (("Type", "Bloom"), ("Threshold", 1.0), ("Strength", 0.6), ("Size", 0.6), ("Quality", "High")):
        glare.inputs[name].default_value = val
    ng.links.new(src, glare.inputs["Image"])
    ng.links.new(glare.outputs["Image"], out.inputs[0])

def run_animatic(out_dir, fps, seconds, first=0, last=None):
    """Settle-in + power-on: the view lowers into the seat while the cabin wakes, then the ship scans in."""
    import os

    def clamp(v):
        return max(0.0, min(1.0, v))

    def ramp(t, a, b):
        return clamp((t - a) / (b - a))

    def ease(v):
        return v * v * (3 - 2 * v)

    def flick(t, on):   # a lamp that catches, drops out, then holds
        if t < on:
            return 0.0
        if t < on + 0.05:
            return 1.0
        if t < on + 0.10:
            return 0.15
        return 1.0

    def em_inputs(m):
        found = []
        for n in m.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED" and not n.inputs["Emission Strength"].is_linked:
                found.append(n.inputs["Emission Strength"])
            elif n.type == "EMISSION" and not n.inputs["Strength"].is_linked:
                found.append(n.inputs["Strength"])
        return [(sock, sock.default_value) for sock in found]

    def base_input(m):
        p = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        return (p.inputs["Base Color"], tuple(p.inputs["Base Color"].default_value)) if p else None

    def group(mat_names, light_objs=()):
        ms = [mats[n] for n in mat_names if n in mats]
        return {"em": [e for m in ms for e in em_inputs(m)],
                "lights": [(o, o.data.energy) for o in light_objs]}

    def set_group(g, v):
        for sock, orig in g["em"]:
            sock.default_value = orig * v
        for o, orig in g["lights"]:
            o.data.energy = orig * v

    cones = sorted((o for o in s.objects if o.name.startswith("Mock | Downlight cone")), key=lambda o: o.location.x)
    downs = []
    for i, made in enumerate(HEADER_LIGHTS):
        g = group([f"Mock lamp lens w{i}"], [made[2]] + ([cones[i]] if i < len(cones) else []))
        g["base"] = [base_input(mats[f"Mock lamp lens w{i}"])]
        downs.append(g)
    header_objs = {o for made in HEADER_LIGHTS for o in made}
    deck_mats = ["19 | Amber status glass", "Mock lamp lens r", "Mock lamp lens g", "Mock lamp lens a"]
    lamps = group(deck_mats, [o for o in s.objects if o.type == "LIGHT" and o.name.startswith("Mock lamp light")
                              and o not in header_objs])
    lamps["base"] = [base_input(mats[n]) for n in deck_mats if n in mats]
    legend = group(["25 | Launch letter backlight", "19 | Ivory legends",
                    "22 | Launch broad beveled button / Launch amber resin"])
    screen_mats = [m.name for m in mats if m.name.startswith("Mock screen /")]
    navy = group(screen_mats + ["Dark display glass"],
                 [o for o in s.objects if o.name.startswith("Mock | Screen spill")] + [objs["19 | Screen bounce"]])
    fades = {side: [m.node_tree.nodes["Mock content fade"].inputs["Factor"] for m in mats
                    if m.name.startswith("Mock screen /") and key in m.name]
             for side, key in (("left", "07 |"), ("right", "30 |"))}
    lens = group(["19 | Projector optical emitter"])
    frustum = objs["11 | Neutral projector frustum"]
    beam_density = [(n.inputs["Density"], n.inputs["Density"].default_value) for sl in frustum.material_slots
                    for n in sl.material.node_tree.nodes if n.type == "PRINCIPLED_VOLUME" and not n.inputs["Density"].is_linked]
    spill = group([], [objs["19 | Projection spill"]])
    glow = group(["Mock mote"], [objs["Mock | Hologram glow"]] if "Mock | Hologram glow" in objs else [])
    motes = [o for o in s.objects if o.name.startswith("Mock | Mote")]

    holo_objs = [o for o in s.objects if "Vanguard" in o.name and not o.hide_render]
    shapes = [o for o in holo_objs if o.type in ("MESH", "CURVE")]
    span = [(o.matrix_world @ Vector(c)).z for o in shapes for c in o.bound_box]
    z0, z1 = min(span) - 0.25, max(span) + 0.25
    mode = PARAMS.get("holo", "scan")
    line_mats = {sl.material for o in shapes if o.type == "CURVE" for sl in o.material_slots if sl.material}
    all_mats = {sl.material for o in shapes for sl in o.material_slots if sl.material}
    curves = [o.data for o in shapes if o.type == "CURVE"]
    ctl = {}

    def math(nt, op, a=None, b=None):
        n = nt.nodes.new("ShaderNodeMath")
        n.operation = op
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, float):
                n.inputs[i].default_value = v
            else:
                nt.links.new(v, n.inputs[i])
        return n

    for m in all_mats:
        # hide = max(1 - alpha, above the scan cut, above the dissolve threshold); edges of the cut glow.
        nt = m.node_tree
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output)
        orig = out.inputs["Surface"].links[0].from_socket
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Position"], sep.inputs[0])
        tc = nt.nodes.new("ShaderNodeTexCoord")
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 6.0
        noise.inputs["Detail"].default_value = 2.0
        nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
        fade = math(nt, "SUBTRACT", 1.0, 1.0)
        above = math(nt, "GREATER_THAN", sep.outputs["Z"], 100.0)
        near = math(nt, "GREATER_THAN", sep.outputs["Z"], 99.86)
        gate = math(nt, "GREATER_THAN", noise.outputs["Fac"], 2.0)
        gate2 = math(nt, "GREATER_THAN", noise.outputs["Fac"], 1.95)
        hide = math(nt, "MAXIMUM", math(nt, "MAXIMUM", fade.outputs[0], above.outputs[0]).outputs[0], gate.outputs[0])
        clear = nt.nodes.new("ShaderNodeBsdfTransparent")
        mix = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(hide.outputs[0], mix.inputs["Fac"])
        nt.links.new(orig, mix.inputs[1])
        nt.links.new(clear.outputs[0], mix.inputs[2])
        band = math(nt, "MAXIMUM", math(nt, "SUBTRACT", near.outputs[0], above.outputs[0]).outputs[0],
                    math(nt, "SUBTRACT", gate2.outputs[0], gate.outputs[0]).outputs[0])
        gain = math(nt, "MULTIPLY", band.outputs[0], 14.0)
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = HOLO_CYAN
        nt.links.new(gain.outputs[0], em.inputs["Strength"])
        addsh = nt.nodes.new("ShaderNodeAddShader")
        nt.links.new(mix.outputs[0], addsh.inputs[0])
        nt.links.new(em.outputs[0], addsh.inputs[1])
        nt.links.new(addsh.outputs[0], out.inputs["Surface"])
        ctl[m] = {"alpha": fade.inputs[1], "cut": above.inputs[1], "cut2": near.inputs[1],
                  "thr": gate.inputs[1], "thr2": gate2.inputs[1]}

    group = set(holo_objs) | {objs["07 | Hologram assembly"]}
    tops = [(o, o.matrix_world.copy()) for o in group if o.parent not in group]
    lens_obj = objs["09 | Projector lens"]
    lens_pts = [lens_obj.matrix_world @ Vector(c) for c in lens_obj.bound_box]
    lens_pt = Vector((sum(p.x for p in lens_pts) / 8, sum(p.y for p in lens_pts) / 8, max(p.z for p in lens_pts)))
    mote_z = [(o, o.location.z) for o in s.objects if o.name.startswith("Mock | Mote")]

    def set_all(key, v, which=None):
        for m, c in ctl.items():
            if which is None or m in which:
                c[key].default_value = v

    def back_out(x):   # ease out with a small overshoot
        c1 = 1.0
        c3 = c1 + 1
        return 1 + c3 * (x - 1) ** 3 + c1 * (x - 1) ** 2

    def holo_apply(t):
        if mode == "scan":
            b = ramp(t, 3.0, 4.3)
            cut = z0 + b * (z1 - z0)
            set_all("cut", cut)
            set_all("cut2", cut - 0.14)
            return b
        if mode == "trace":
            draw = ease(ramp(t, 3.0, 3.75))
            for cu in curves:
                cu.bevel_factor_end = max(draw, 0.0005)
            set_all("alpha", 1.0 if t >= 3.0 else 0.0, line_mats)
            set_all("alpha", ease(ramp(t, 3.55, 4.25)), all_mats - line_mats)
            return ramp(t, 3.0, 4.25)
        if mode == "grow":
            x = ramp(t, 3.0, 3.9)
            k = max(back_out(x), 0.02)
            m = Matrix.Translation(lens_pt) @ Matrix.Scale(k, 4) @ Matrix.Translation(-lens_pt)
            for o, m0 in tops:
                o.matrix_world = m @ m0
            set_all("alpha", ease(ramp(t, 3.0, 3.3)))
            return x
        if mode == "dissolve":
            d = ramp(t, 3.0, 4.2)
            thr = 0.12 + d * 0.95
            set_all("thr", thr)
            set_all("thr2", thr - 0.018)   # a thin sparkle edge; wider floods the ship white
            for o, z in mote_z:
                o.location.z = 1.6 + ((z - 1.6) + max(0.0, t - 2.8) * 1.8) % 4.8
            return d
        raise ValueError(mode)

    cam = s.camera
    end_loc, end_rot = cam.matrix_world.to_translation(), cam.matrix_world.to_quaternion()
    start_loc = end_loc + Vector((0.0, -1.3, 1.8))
    start_rot = end_rot @ Quaternion((1, 0, 0), -0.13)

    def set_base(g, v):
        for item in g.get("base", []):
            if item:
                sock, orig = item
                sock.default_value = tuple(c * (0.12 + 0.88 * v) for c in orig[:3]) + (1,)

    order = [2, 3, 1, 4, 0, 5]   # downlights catch from the centre outward
    frames = int(fps * seconds)
    os.makedirs(out_dir, exist_ok=True)
    bpy.context.view_layer.freestyle_settings.use_view_map_cache = True
    objs["19 | Projection spill"].data.volume_factor = 0.3   # EEVEE haze takes light much harder than Cycles
    if "Mock | Hologram glow" in objs:
        objs["Mock | Hologram glow"].data.volume_factor = 0.5
    for f in range(first, frames if last is None else min(frames, last)):
        t = f / fps
        c = ease(ramp(t, 0.15, 2.2))
        cam.matrix_world = Matrix.Translation(start_loc.lerp(end_loc, c)) @ start_rot.slerp(end_rot, c).to_matrix().to_4x4()
        for k, i in enumerate(order[:len(downs)]):
            v = flick(t, 1.5 + 0.1 * k)
            set_group(downs[i], v)
            set_base(downs[i], v)
        v = flick(t, 0.7)
        set_group(lamps, v)
        set_base(lamps, v)
        set_group(legend, ramp(t, 0.8, 1.2))
        nv = ramp(t, 1.7, 1.95) * (0.3 if 1.85 < t < 1.9 else 1.0)
        set_group(navy, nv)
        for sock in fades["left"]:
            sock.default_value = ramp(t, 2.2, 2.4)
        for sock in fades["right"]:
            sock.default_value = ramp(t, 2.35, 2.55)
        set_group(lens, ramp(t, 2.7, 2.85))
        bv = ramp(t, 2.8, 3.2)
        for sock, orig in beam_density:
            sock.default_value = orig * bv
        frustum.hide_render = bv < 0.01
        set_group(spill, bv)
        build = holo_apply(t)
        set_group(glow, build)
        for o in motes:
            o.hide_render = bv < 0.05
        s.render.filepath = os.path.join(out_dir, f"f{f:04d}.png")
        bpy.ops.render.render(write_still=True)
        print("FRAME", f, "of", frames, flush=True)


if "anim" in PARAMS:
    fps_, secs_ = (float(v) for v in PARAMS["anim"].split("x"))
    s.render.image_settings.file_format = "PNG"
    lo_, hi_ = (int(v) for v in PARAMS.get("frames", "0:100000").split(":"))
    run_animatic(OUT, int(fps_), secs_, lo_, hi_)
    raise SystemExit(0)

s.render.filepath = OUT
s.render.image_settings.file_format = "PNG"
bpy.ops.render.render(write_still=True)
print("WROTE", OUT, sorted(FLAGS))
