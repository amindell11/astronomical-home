"""Station massing proof for #1384: three clay-and-light structures, tumbled.

Run: blender -b --factory-startup --python stations.py -- <outdir> [blockout] [station[@skin[+dress]] ...]
"""
import json
import math
import random
import sys
from pathlib import Path
from math import cos, radians, sin

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

OUT = sys.argv[sys.argv.index("--") + 1]
ONLY = sys.argv[sys.argv.index("--") + 2:]

RES = 640
ORTHO = 38.0
INK = 0.17
BLOCKOUT_SIZE = 29.0
SHADE_MID, SHADE_LIT = 0.36, 0.62

PALETTE = {
    "bone": "#DDD2BC",
    "canvas": "#9C927F",
    "slate": "#3B3F52",
    "ochre": "#C7A25A",
    "brick": "#B0705A",
    "blue": "#7088A6",
}
GLOW = {"win": "#FFC45A", "svc": "#FFFFFF", "core": "#FFE25A", "ship": "#1AFFD9"}
UNLIT = "#1E2030"


def srgb(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return (*[(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c], 1.0)


def colour_sockets(sockets):
    return [s for s in sockets if s.type == "RGBA"]


def cel(name, hexcol):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    cr.interpolation = "CONSTANT"
    cr.elements[0].position, cr.elements[0].color = 0.0, (0, 0, 0, 1)
    cr.elements[1].position, cr.elements[1].color = SHADE_MID, (0.55, 0.55, 0.55, 1)
    top = cr.elements.new(SHADE_LIT)
    top.color = (1, 1, 1, 1)
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    base = srgb(hexcol)
    a, b = colour_sockets(mix.inputs)[:2]
    a.default_value = (base[0] * 0.40, base[1] * 0.40, base[2] * 0.62, 1.0)
    b.default_value = base
    m.diffuse_color = base
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(diff.outputs[0], to_rgb.inputs[0])
    nt.links.new(to_rgb.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], mix.inputs[0])
    nt.links.new(colour_sockets(mix.outputs)[0], emit.inputs[0])
    nt.links.new(emit.outputs[0], out.inputs[0])
    return m


def glow(name, hexcol):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.name = "Emission"
    emit.inputs[0].default_value = srgb(hexcol)
    m.diffuse_color = srgb(hexcol)
    nt.links.new(emit.outputs[0], out.inputs[0])
    return m


def ink_material():
    m = bpy.data.materials.new("ink")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs[0].default_value = srgb("#0A0911")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mix.inputs[0])
    nt.links.new(clear.outputs[0], mix.inputs[1])
    nt.links.new(emit.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    return m


def T(x, y, z):
    return Matrix.Translation((x, y, z))


def R(axis, deg):
    return Matrix.Rotation(radians(deg), 4, axis)


def chamfered(w, h, c):
    hw, hh = w / 2, h / 2
    return [(hw - c, -hh), (hw, -hh + c), (hw, hh - c), (hw - c, hh),
            (-hw + c, hh), (-hw, hh - c), (-hw, -hh + c), (-hw + c, -hh)]


def loft(rings):
    n = len(rings[0])
    verts = [v for ring in rings for v in ring]
    faces = []
    for i in range(len(rings) - 1):
        for j in range(n):
            a, b = i * n + j, i * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    last = (len(rings) - 1) * n
    faces.append(tuple(range(n - 1, -1, -1)))
    faces.append(tuple(range(last, last + n)))
    return verts, faces


class Builder:
    """Collects geometry into named parts. Each part has a pivot, so it moves as one piece in Blender."""

    def __init__(self, scale=1.0):
        self.parts = {}
        self.stack = [Matrix.Scale(scale, 4)]
        self.current = None
        self.reach = 0.0
        self.clear = float("inf")
        self.low = Vector((1e9, 1e9, 1e9))
        self.high = Vector((-1e9, -1e9, -1e9))
        self.part = "Part"

    def begin(self, name, frame=None, parent=None, at_root=False):
        """Start or resume a part. Its pivot is the current transform times frame, without scale."""
        if name not in self.parts:
            base = self.stack[0] if at_root else self.stack[-1]
            loc, rot, _ = (base @ (frame or Matrix.Identity(4))).decompose()
            self.parts[name] = {"frame": Matrix.LocRotScale(loc, rot, None), "parent": parent, "meshes": {}}
        self.current = name

    @property
    def part(self):
        return self.current

    @part.setter
    def part(self, name):
        self.begin(name, at_root=True)

    def push(self, m):
        builder = self

        class Scope:
            def __enter__(self):
                builder.stack.append(builder.stack[-1] @ m)

            def __exit__(self, *exc):
                builder.stack.pop()

        return Scope()

    def emit(self, mat, verts, faces):
        part = self.parts[self.current]
        vs, fs = part["meshes"].setdefault(mat, ([], []))
        off, m, inv = len(vs), self.stack[-1], part["frame"].inverted()
        for v in verts:
            world = m @ Vector(v)
            self.reach = max(self.reach, world.length)
            if mat not in GLOW:
                self.clear = min(self.clear, world.length)
            self.low = Vector(map(min, self.low, world))
            self.high = Vector(map(max, self.high, world))
            vs.append(inv @ world)
        fs.extend(tuple(i + off for i in f) for f in faces)

    def arc(self, mat, radius, a0, a1, w, h, c, z=0.0, step=12.0, taper=0.87, tlen=None):
        """Chamfered section swept round the local Z axis; w is radial, h is along Z."""
        span = a1 - a0
        sign = 1.0 if span >= 0 else -1.0
        prof = chamfered(w, h, c)

        def ring(a, s):
            ca, sa = cos(radians(a)), sin(radians(a))
            return [Vector(((radius + u * s) * ca, (radius + u * s) * sa, z + v * s)) for u, v in prof]

        n = max(1, round(abs(span) / step))
        angles = [a0 + span * i / n for i in range(n + 1)]
        if taper >= 1.0:
            rings = [ring(a, 1.0) for a in angles]
        else:
            arc_len = radius * radians(abs(span))
            tl = min(tlen if tlen is not None else c * 0.85, arc_len * 0.25)
            da = math.degrees(tl / radius) * sign
            inner = [a for a in angles[1:-1] if (a - a0 - da) * sign > 0.5 and (a1 - da - a) * sign > 0.5]
            rings = ([ring(a0, taper), ring(a0 + da, 1.0)] + [ring(a, 1.0) for a in inner]
                     + [ring(a1 - da, 1.0), ring(a1, taper)])
        self.emit(mat, *loft(rings))

    def prism(self, mat, p0, p1, w, h, c, up=(0, 0, 1), taper=0.88, tlen=None):
        """Chamfered section from p0 to p1; h runs along `up`, w across it."""
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        length = d.length
        d.normalize()
        upv = Vector(up)
        if abs(d.dot(upv)) > 0.95:
            upv = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))
        u = upv.cross(d).normalized()
        v = d.cross(u)
        prof = chamfered(w, h, c)

        def ring(t, s):
            o = p0 + d * t
            return [o + u * (a * s) + v * (b * s) for a, b in prof]

        if taper >= 1.0:
            rings = [ring(0, 1.0), ring(length, 1.0)]
        else:
            tl = min(tlen if tlen is not None else c, length * 0.3)
            rings = [ring(0, taper), ring(tl, 1.0), ring(length - tl, 1.0), ring(length, taper)]
        self.emit(mat, *loft(rings))

    def lathe(self, mat, prof, sides=10, phase=0.0):
        """Rings of (radius, z) round the local Z axis."""
        rings = [[Vector((r * cos(2 * math.pi * (k + phase) / sides), r * sin(2 * math.pi * (k + phase) / sides), z))
                  for k in range(sides)] for r, z in prof]
        self.emit(mat, *loft(rings))

    def plated_arc(self, radius, a0, a1, n, joint, plate, core, mats, windows, z=0.0, closed=False,
                   core_mat="slate", lamps=None):
        """A dark core with n proud plates along it.

        windows names the faces per plate that carry amber panes; lamps names the faces
        that carry a dotted row of approach lights in the station's service colour.
        """
        pw, ph, pc = plate
        self.arc(core_mat, radius, a0, a1, *core, z=z, taper=1.0)
        span = a1 - a0
        seg = (span - joint * (n if closed else n - 1)) / n
        for i in range(n):
            s = a0 + i * (seg + joint)
            e = s + seg
            self.arc(mats[i % len(mats)], radius, s, e, pw, ph, pc, z=z)
            rows = [(f, "win", 1.0, 0.45, 0.07, 0.30) for f in windows[i % len(windows)]]
            if lamps:
                rows += [(f, "svc", 0.55, 0.8, 0.16, 0.2) for f in lamps[i % len(lamps)]]
            row0, row1 = s + seg * 0.2, e - seg * 0.2
            for face, mat, pane, pane_gap, proud, across in rows:
                if face in ("out", "in"):
                    r = radius + (pw / 2 + 0.03) * (1 if face == "out" else -1)
                    size, zz = (proud * 2, ph * across, 0.02), z
                else:
                    r = radius
                    size, zz = (pw * across, proud * 2, 0.02), z + (ph / 2 + 0.03) * (1 if face == "top" else -1)
                pane_deg = math.degrees(pane / r)
                gap_deg = math.degrees(pane_gap / r)
                count = max(1, int((row1 - row0 + gap_deg) / (pane_deg + gap_deg)))
                used = count * pane_deg + (count - 1) * gap_deg
                start = (row0 + row1) / 2 - used / 2
                for k in range(count):
                    p = start + k * (pane_deg + gap_deg)
                    self.arc(mat, r, p, p + pane_deg, *size, z=zz, taper=1.0)

    def pod(self, length=6.5, r=1.9, body="bone"):
        """Crew capsule along local Z with two window bands."""
        hz = length / 2
        nose = r * 0.55
        self.lathe(body, [(r * 0.5, -hz), (r, -hz + nose), (r, hz - nose), (r * 0.5, hz)], sides=10)
        for s in (1, -1):
            zc = s * (hz - nose - 0.25)
            self.lathe("slate", [(r * 1.1, zc - 0.2), (r * 1.1, zc + 0.2)], sides=10)
            self.lathe("canvas", [(r * 0.42, s * hz), (r * 0.34, s * (hz + 0.3))], sides=10)
        for zc in (-0.42, 0.42):
            self.lathe("win", [(r + 0.04, zc - 0.2), (r + 0.04, zc + 0.2)], sides=10)

    def tank(self, mat, length, r):
        hz = length / 2
        self.lathe(mat, [(r * 0.45, -hz), (r, -hz + r * 0.6), (r, hz - r * 0.6), (r * 0.45, hz)], sides=8)
        self.lathe("slate", [(r * 1.12, -0.18), (r * 1.12, 0.18)], sides=8)

    def finish(self, name, mats, scene):
        """Build the objects: a root, an empty per part at its pivot, and a mesh per material under it."""
        parts_col = bpy.data.collections.new(name)
        ink_col = bpy.data.collections.new("Ink preview (render only)")
        for col in (parts_col, ink_col):
            scene.collection.children.link(col)
        ink_col.hide_viewport = True
        ink_col.hide_select = True

        root = bpy.data.objects.new(name, None)
        root.empty_display_size = 2.0
        parts_col.objects.link(root)
        empties, tris = {}, 0
        for part, spec in self.parts.items():
            if not spec["meshes"]:
                continue
            pivot = bpy.data.objects.new(part, None)
            pivot.empty_display_type = "ARROWS"
            pivot.empty_display_size = 1.2
            parts_col.objects.link(pivot)
            empties[part] = pivot
        for part, pivot in empties.items():
            spec = self.parts[part]
            parent = spec["parent"] if spec["parent"] in empties else None
            pivot.parent = empties[parent] if parent else root
            above = self.parts[parent]["frame"] if parent else Matrix.Identity(4)
            pivot.matrix_basis = above.inverted() @ spec["frame"]
            for mat, (vs, fs) in spec["meshes"].items():
                me = bpy.data.meshes.new(f"{part} / {mat}")
                me.from_pydata([v[:] for v in vs], [], fs)
                bm = bmesh.new()
                bm.from_mesh(me)
                bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
                tris += sum(len(f.verts) - 2 for f in bm.faces)
                bm.to_mesh(me)
                bm.free()
                me.materials.append(mats[mat])
                ob = bpy.data.objects.new(me.name, me)
                parts_col.objects.link(ob)
                ob.parent = pivot
                if mat in GLOW:
                    ob.visible_shadow = False
                    continue
                twin = bpy.data.objects.new(me.name + " / ink", me)
                ink_col.objects.link(twin)
                twin.parent = pivot
                shell = twin.modifiers.new("ink", "DISPLACE")
                shell.direction, shell.strength, shell.mid_level = "NORMAL", INK, 0.0
                twin.material_slots[0].link = "OBJECT"
                twin.material_slots[0].material = mats["ink"]
                twin.visible_shadow = False
        return root, tris


def polar(radius, deg, z=0.0):
    return Vector((radius * cos(radians(deg)), radius * sin(radians(deg)), z))


def gimbal(b):
    """Repair: a broken ring with a second ring swung through it, so one always shows open."""
    ro, ri = 11.5, 8.2
    b.begin("Outer ring")
    b.plated_arc(ro, 35, 325, 6, 7, plate=(3.4, 3.0, 0.8), core=(2.3, 2.0, 0.5),
                 mats=["bone", "bone", "canvas", "bone", "ochre", "bone"],
                 windows=[("out", "top"), (), ("top", "bot", "out"), ("in",), (), ("out", "bot")],
                 lamps=[("in",), (), (), (), (), ("in",)])
    for label, a, s in (("Berth light A", 35, -1), ("Berth light B", 325, 1)):
        b.begin(label, frame=R("Z", a) @ T(ro, 0, 0), parent="Outer ring")
        b.arc("slate", ro, a + s * 0.2, a + s * 3.0, 2.6, 2.3, 0.5, taper=1.0)
        b.arc("svc", ro, a + s * 3.0, a + s * 4.4, 1.7, 1.4, 0.3, taper=1.0)

    for label, a in (("Bearing A", 90), ("Bearing B", 270)):
        b.begin(label, frame=R("Z", a) @ T(ro, 0, 0), parent="Outer ring")
        b.arc("slate", ro, a - 7, a + 7, 4.0, 3.6, 0.9)
        b.prism("ochre", polar(ri + 0.9, a), polar(ro - 1.2, a), 1.5, 1.5, 0.35, taper=1.0)

    b.begin("Inner ring", frame=R("Y", 62))
    with b.push(R("Y", 62)):
        b.plated_arc(ri, 50, 310, 4, 8, plate=(2.3, 2.1, 0.55), core=(1.5, 1.4, 0.35),
                     mats=["canvas", "bone", "bone", "blue"],
                     windows=[("top",), ("out",), ("bot",), ()])
        for a, s in ((50, -1), (310, 1)):
            b.arc("svc", ri, a + s * 0.2, a + s * 2.2, 1.2, 1.0, 0.25, taper=1.0)
        for i, a in enumerate((125, 180, 235)):
            b.begin(f"Repair arm {i + 1}", frame=R("Z", a) @ T(ri - 1.0, 0, 0), parent="Inner ring")
            b.prism("slate", polar(ri - 1.0, a), polar(4.9, a), 0.7, 0.7, 0.18, taper=1.0)
            b.prism("canvas", polar(5.5, a), polar(4.4, a), 2.0, 1.4, 0.3)
            b.prism("svc", polar(4.4, a), polar(4.15, a), 1.3, 0.8, 0.15, taper=1.0)

    px = -(ro + 1.7 + 0.35 + 1.9)
    b.begin("Crew pod", frame=T(px, 0, 0) @ R("X", 90))
    with b.push(T(px, 0, 0) @ R("X", 90)):
        b.pod(6.5, 1.9)
    for y in (-1.7, 1.7):
        b.prism("slate", (-(ro + 1.5), y, 0), (px + 1.2, y, 0), 1.1, 1.1, 0.25, taper=1.0)

    for label, a, z, skin, length in (("Tank A", 118, 2.3, "blue", 4.4), ("Tank B", 248, -2.3, "canvas", 3.6)):
        frame = R("Z", a) @ T(ro, 0, z) @ R("X", 90)
        b.begin(label, frame=frame, parent="Outer ring")
        with b.push(frame):
            b.tank(skin, length, 0.85)


def lantern(b):
    """Mod station: a ribbed cage holding one lit prize, with one rib cut open as the way in."""
    belly, end_z, collar_r = 11.5, 11.5, 3.8
    with b.push(R("Y", 90)):
        b.part = "Collars"
        collar = dict(plate=(1.7, 2.2, 0.45), core=(1.1, 1.4, 0.3), closed=True)
        b.plated_arc(collar_r, 15, 375, 6, 9, z=end_z, mats=["bone", "canvas", "bone", "ochre", "bone", "canvas"],
                     windows=[("out",), (), (), ("out",), (), ()], lamps=[("in",)], **collar)
        b.plated_arc(collar_r, 45, 405, 6, 9, z=-end_z, mats=["canvas", "bone", "brick", "bone", "canvas", "bone"],
                     windows=[(), ("out",), (), (), ("out",), ()], lamps=[("in",)], **collar)

        # Each rib is a circular arc from one collar, out to the belly, back to the other collar.
        foot = collar_r + 0.6
        centre = (foot ** 2 + end_z ** 2 - belly ** 2) / (2 * (foot - belly))
        rho = belly - centre
        half = math.degrees(math.asin(end_z / rho))
        rib = dict(plate=(1.4, 3.4, 0.4), core=(0.9, 1.6, 0.25))
        b.part = "Ribs"
        for i, az in enumerate((20, 110, 200, 290)):
            with b.push(R("Z", az) @ T(centre, 0, 0) @ R("X", 90)):
                if i == 2:
                    b.plated_arc(rho, -half, -24, 1, 0, mats=["bone"], windows=[("out",)], lamps=[("in",)], **rib)
                    b.plated_arc(rho, 24, half, 1, 0, mats=["canvas"], windows=[()], lamps=[("in",)], **rib)
                    for a, s in ((-24, 1), (24, -1)):
                        b.arc("slate", rho, a - s * 0.2, a + s * 1.6, 1.8, 2.6, 0.4, taper=1.0)
                        b.arc("svc", rho, a + s * 1.6, a + s * 2.6, 1.2, 1.8, 0.3, taper=1.0)
                elif i == 1:
                    b.arc("slate", rho, -half, half, *rib["core"], taper=1.0)
                    b.plated_arc(rho, -half, -17, 1, 0, mats=["canvas"], windows=[("top",)], **rib)
                    b.plated_arc(rho, 17, half, 1, 0, mats=["bone"], windows=[("out",)], **rib)
                else:
                    b.plated_arc(rho, -half, half, 3, 7, mats=[["bone", "blue", "bone"], ["canvas", "bone", "brick"]][i // 3],
                                 windows=[[("out",), ("top", "bot"), ()], [("bot",), (), ("out",)]][i // 3], **rib)

        b.part = "Crew pod"
        with b.push(R("Z", 110) @ T(belly + 0.2, 0, 0)):
            b.pod(7.0, 2.1)

        b.part = "Core"
        b.lathe("core", [(0.35, -3.2), (2.1, -1.2), (2.1, 1.2), (0.35, 3.2)], sides=8)
        b.arc("slate", 3.4, 0, 360, 0.6, 0.6, 0.14, taper=1.0, step=30)
        for az in (0, 90, 180, 270):
            b.prism("canvas", polar(2.0, az), polar(3.3, az), 0.55, 0.9, 0.12, taper=1.0)
        for az in (20, 110, 290):
            b.prism("slate", polar(3.6, az), polar(belly - 0.6, az), 0.38, 0.38, 0.08, taper=1.0)

        b.part = "Tanks"
        with b.push(R("Z", 250) @ T(collar_r + 0.85 + 0.95, 0, end_z) @ R("X", 90)):
            b.tank("blue", 3.8, 0.9)
        with b.push(R("Z", 70) @ T(collar_r + 0.85 + 0.8, 0, -end_z) @ R("X", 90)):
            b.tank("ochre", 3.2, 0.75)


def drum(b):
    """Armory: long magazines slung between two rings round an open bore, with one slot given to the crew."""
    radius, w, h = 7.4, 4.2, 3.4
    slots = [0, 60, 120, 240, 300]
    offsets = [0.0, 3.0, -2.5, 2.0, -4.0]
    lengths = [24.0, 19.0, 23.0, 20.0, 16.0]
    skins = ["canvas", "bone", "brick", "bone", "canvas"]
    up = (1, 0, 0)
    # The magazines are staggered, so the bore centre is shifted to keep both ends the same distance out.
    with b.push(R("Y", 90) @ T(0, 0, 0.75)):
        for k in range(5):
            z0, length = offsets[k], lengths[k]
            lo, hi = z0 - length / 2, z0 + length / 2
            b.begin(f"Magazine {k + 1}", frame=R("Z", slots[k]) @ T(radius, 0, z0))
            with b.push(R("Z", slots[k])):
                b.prism(skins[k], (radius, 0, lo), (radius, 0, hi), w, h, 0.7, up=up)
                for end, s in ((lo, -1), (hi, 1)):
                    b.prism("slate", (radius, 0, end - s * 0.4), (radius, 0, end + s * 0.3),
                            w * 0.74, h * 0.74, 0.4, up=up, taper=1.0)
                    b.prism("svc", (radius, 0, end + s * 0.3), (radius, 0, end + s * 0.5),
                            w * 0.46, h * 0.36, 0.1, up=up, taper=1.0)
                face = radius + h / 2 - 0.05
                hatch = "bone" if skins[k] != "bone" else "canvas"
                b.prism(hatch, (face, 0, hi - length * 0.3), (face, 0, hi - length * 0.08), w * 0.6, 0.3, 0.1, up=up)
                if k % 2 == 0:
                    for j in range(3):
                        zz = lo + length * 0.1 + j * 1.5
                        b.prism("win", (face + 0.08, 0, zz), (face + 0.08, 0, zz + 1.0), w * 0.42, 0.14, 0.02,
                                up=up, taper=1.0)
                else:
                    b.prism(hatch, (face, 0, lo + length * 0.08), (face, 0, lo + length * 0.26), w * 0.6, 0.3, 0.1,
                            up=up)
                bore = radius - h / 2 - 0.1
                for j in range(5):
                    zz = z0 + (j - 2) * 1.6
                    b.prism("svc", (bore, 0, zz - 0.3), (bore, 0, zz + 0.3), 0.7, 0.3, 0.05, up=up, taper=1.0)

        ring = dict(plate=(1.3, 2.6, 0.35), core=(0.8, 1.5, 0.2), closed=True)
        b.begin("Ring fore", frame=T(0, 0, 6.0))
        b.plated_arc(radius + h / 2 + 0.9, -26, 334, 6, 8, z=6.0, mats=["bone", "canvas", "bone", "bone", "ochre", "bone"],
                     windows=[(), ("out",), (), ()], **ring)
        b.begin("Ring aft", frame=T(0, 0, -6.0))
        b.plated_arc(radius + h / 2 + 0.9, -26, 334, 6, 8, z=-6.0, mats=["canvas", "bone", "bone", "blue", "bone", "canvas"],
                     windows=[("out",), (), (), (), ()], **ring)

        b.begin("Crew pod", frame=R("Z", 180) @ T(radius + 0.3, 0, 3.4))
        with b.push(R("Z", 180) @ T(radius + 0.3, 0, 3.4)):
            b.pod(7.0, 2.0)
        b.begin("Tank", frame=R("Z", 180) @ T(radius, 0, -5.8))
        with b.push(R("Z", 180)):
            with b.push(T(radius, 0, -5.8)):
                b.tank("blue", 5.6, 1.45)
            b.prism("slate", (radius + 1.4, 0, -6.0), (radius + 2.1, 0, -6.0), 1.2, 1.2, 0.25, up=(0, 0, 1),
                    taper=1.0)


def core_gem(b, half=3.0, r=2.0):
    b.lathe("core", [(0.35, -half), (r, -half * 0.37), (r, half * 0.37), (0.35, half)], sides=8)


def clam(b):
    """Mod station: two solid plated shells hinged open like a clam, with the prize in the mouth."""
    gap = 0.5
    shells = ((1, 8.6, "canvas", "ochre", ["bone", "bone", "blue", "bone", "canvas"]),
              (-1, 8.0, "canvas", "blue", ["bone", "brick", "bone", "bone", "canvas"]))
    with b.push(T(-8.5, 0, 0)):
        for s, radius, body, cap, skins in shells:
            with b.push(R("Y", -s * 21) @ T(radius + 1.6, 0, 0)):
                b.part = "Upper shell" if s > 0 else "Lower shell"
                top = radius - 1.3
                b.lathe(body, [(radius - 0.8, s * gap), (radius, s * (gap + 0.8)), (radius, s * (gap + 2.0)),
                               (top, s * (gap + 3.0))], sides=12)
                b.lathe("slate", [(radius - 1.5, s * (gap - 0.12)), (radius - 1.5, s * (gap + 0.2))], sides=12)
                # Wedge plates across the back of the shell, so it reads as a solid shield and not a ring.
                b.plated_arc(top * 0.62, 20, 380, 5, 9, plate=(top * 0.66, 0.9, 0.3), core=(top * 0.5, 0.5, 0.15),
                             z=s * (gap + 3.05), closed=True, mats=skins,
                             windows=[("top",) if s > 0 else ("bot",), (), (), ("top",) if s > 0 else ("bot",), ()])
                b.lathe(cap, [(top * 0.3, s * (gap + 3.0)), (top * 0.24, s * (gap + 4.1))], sides=8)
                for k in range(12):
                    if k % 4 != 3:
                        b.arc("win", radius + 0.03, k * 30 + 9, k * 30 + 16, 0.14, 0.7, 0.02, z=s * (gap + 1.4),
                              taper=1.0)
                for k in range(16):
                    b.arc("svc", radius - 2.3, k * 22.5, k * 22.5 + 6, 0.7, 0.3, 0.05, z=s * (gap - 0.15), taper=1.0)
                for y in (-2.7, 2.7):
                    b.prism("slate", (-radius - 1.6, y, s * (gap + 1.3)), (-radius + 0.6, y, s * (gap + 1.3)),
                            1.8, 2.0, 0.4, taper=1.0)
                b.part = "Tanks"
                with b.push(T(-radius * 0.45, s * 0.1, s * (gap + 3.9)) @ R("X", 90)):
                    b.tank("blue" if s > 0 else "ochre", 4.6, 0.8)

        b.part = "Hinge and crew pod"
        with b.push(R("X", 90)):
            b.pod(8.4, 2.1)

        b.part = "Core"
        with b.push(T(8.6, 0, 0) @ R("X", 90)):
            core_gem(b)
        b.prism("slate", (2.0, 0, 0), (6.8, 0, 0), 0.5, 0.5, 0.12, taper=1.0)
        for y in (-1.5, 1.5):
            b.prism("canvas", (6.0, y, 0), (7.4, y, 0), 0.9, 0.6, 0.12, taper=1.0)


def kite(b):
    """Mod station: three broad pitched sails off a split spine, with the prize in the split."""
    radial = (1, 0, 0)
    with b.push(R("Y", 90)):
        for label, s, skin, panes in (("Spine fore", 1, "bone", (5.2, 6.7, 8.2)), ("Spine aft", -1, "canvas", (6.2, 7.7))):
            b.begin(label, frame=T(0, 0, s * 7.0))
            b.prism(skin, (0, 0, s * 3.8), (0, 0, s * 10.2), 3.8, 3.8, 0.95)
            for az in (90, 270):
                with b.push(R("Z", az)):
                    for z in panes:
                        z0 = z if s > 0 else -z
                        b.prism("win", (1.96, 0, z0), (1.96, 0, z0 + 1.0), 1.3, 0.14, 0.02, up=radial, taper=1.0)
        b.begin("Spine rods")
        for az in (60, 180, 300):
            b.prism("slate", polar(1.3, az, -4.2), polar(1.3, az, 4.2), 0.42, 0.42, 0.1, taper=1.0)

        collar = dict(plate=(1.3, 1.6, 0.35), core=(0.8, 1.0, 0.2), closed=True)
        b.begin("Docking collar fore", frame=T(0, 0, 10.9))
        b.plated_arc(2.7, 10, 370, 5, 10, z=10.9, mats=["canvas", "bone", "ochre", "bone", "canvas"],
                     windows=[()], lamps=[("top",)], **collar)
        b.begin("Docking collar aft", frame=T(0, 0, -10.9))
        b.plated_arc(2.7, 40, 400, 5, 10, z=-10.9, mats=["bone", "canvas", "bone", "blue", "bone"],
                     windows=[()], lamps=[("bot",)], **collar)

        b.begin("Prize")
        core_gem(b, 3.0, 2.0)

        b.begin("Crew pod", frame=R("Z", 60) @ T(4.6, 0, 6.8))
        with b.push(R("Z", 60) @ T(4.6, 0, 6.8)):
            b.pod(6.0, 1.9)
            b.prism("slate", (-2.9, 0, 0), (-1.6, 0, 0), 1.1, 1.1, 0.25, up=(0, 1, 0), taper=1.0)
        for label, az, r, z, skin, length, rad in (("Tank A", 300, 3.2, -6.6, "blue", 4.6, 1.0),
                                                   ("Tank B", 180, 3.1, -7.2, "ochre", 3.4, 0.85)):
            b.begin(label, frame=R("Z", az) @ T(r, 0, z))
            with b.push(R("Z", az) @ T(r, 0, z)):
                b.tank(skin, length, rad)

        sails = [(0, 24, [(3.4, 8.0, 16.0, 0.0, "bone", "blue"), (8.4, 13.0, 11.0, 1.0, "canvas", "blue")]),
                 (120, 24, [(3.4, 8.8, 14.0, -1.0, "canvas", "slate"), (9.2, 12.2, 8.0, -2.0, "bone", "blue")]),
                 (240, 24, [(3.4, 7.2, 17.0, 1.0, "bone", "blue"), (7.6, 11.0, 9.0, 3.0, "brick", "slate")])]
        for i, (az, pitch, panels) in enumerate(sails):
            # The boom turns with the sail's direction; the panels hang off it and carry the pitch.
            b.begin(f"Sail {i + 1}", frame=R("Z", az))
            with b.push(R("Z", az)):
                b.prism("slate", (1.9, 0, 0), (panels[-1][1] - 0.4, 0, 0), 0.7, 0.9, 0.18, up=(0, 1, 0), taper=1.0)
                b.begin(f"Sail {i + 1} panels", frame=R("X", pitch), parent=f"Sail {i + 1}")
                with b.push(R("X", pitch)):
                    for r0, r1, span, zc, skin, cell in panels:
                        b.prism(skin, (r0, 0, zc), (r1, 0, zc), span, 0.9, 0.28, up=(0, 1, 0))
                        for side in (1, -1):
                            b.prism(cell, (r0 + 0.7, side * 0.47, zc), (r1 - 0.7, side * 0.47, zc), span * 0.7, 0.14,
                                    0.04, up=(0, 1, 0), taper=1.0)
                    r0, r1, span, zc = panels[-1][:4]
                    count = int(span / 1.7)
                    for j in range(count):
                        z = zc + (j - (count - 1) / 2) * 1.7
                        b.prism("svc", (r1 + 0.05, 0, z - 0.3), (r1 + 0.05, 0, z + 0.3), 1.1, 0.5, 0.1, up=radial,
                                taper=1.0)


def diabolo(b):
    """Mod station: two plated funnels, mouths outward, tied at the waist by the lit prize."""
    waist = 1.8
    with b.push(R("Y", 90)):
        for s, mouth, length, skin, band in ((1, 9.5, 10.5, "bone", "canvas"), (-1, 8.0, 8.5, "canvas", "bone")):
            b.part = "Wide funnel" if s > 0 else "Narrow funnel"
            lip = s * (waist + length)
            b.lathe(skin, [(3.0, s * waist), (mouth, lip)], sides=8)
            b.lathe(band, [(mouth * 0.8 + 0.2, s * (waist + length * 0.72)), (mouth + 0.2, lip + s * 0.05)], sides=8)
            b.lathe("ochre" if s > 0 else "brick", [(3.0 + (mouth - 3.0) * 0.22 + 0.2, s * (waist + length * 0.22)),
                                                    (3.0 + (mouth - 3.0) * 0.4 + 0.2, s * (waist + length * 0.4))],
                    sides=8)
            b.lathe("slate", [(mouth - 0.9, lip - s * 0.3), (mouth - 0.9, lip + s * 0.14)], sides=8)
            for k in range(12):
                b.arc("svc", mouth - 2.0, k * 30, k * 30 + 9, 0.6, 0.3, 0.05, z=lip + s * 0.2, taper=1.0)
            for k in range(8):
                if k % 3 == 0:
                    continue
                with b.push(R("Z", k * 45 + 22.5)):
                    mid = 3.0 + (mouth - 3.0) * 0.56
                    b.prism("win", (mid * 0.94, 0, s * (waist + length * 0.5)),
                            (mid * 1.02, 0, s * (waist + length * 0.62)), 1.4, 0.2, 0.03, up=(1, 0, 0), taper=1.0)

        b.part = "Core"
        b.lathe("slate", [(2.5, -waist), (2.5, waist)], sides=8)
        b.lathe("core", [(2.95, -0.8), (2.95, 0.8)], sides=8)

        b.part = "Crew pod"
        with b.push(R("Z", 67.5) @ T(3.0 + 6.5 * 0.5 + 2.3, 0, waist + 5.0)):
            b.pod(6.0, 1.9)
        b.part = "Tanks"
        with b.push(R("Z", 247.5) @ T(3.0 + 5.0 * 0.5 + 1.2, 0, -(waist + 4.2))):
            b.tank("blue", 4.4, 0.95)


GIMBAL_RING = 11.5
GIMBAL_FACE = GIMBAL_RING + 1.7


def string_lights(b, p0, p1, sag, count=9):
    """A slack cable of small lamps between two points, bowed along sag."""
    p0, p1, sag = Vector(p0), Vector(p1), Vector(sag)
    pts = [p0.lerp(p1, t) + sag * (4 * t * (1 - t)) for t in (i / (count + 1) for i in range(count + 2))]
    for a, c in zip(pts, pts[1:]):
        b.prism("slate", a, c, 0.14, 0.14, 0.03, taper=1.0)
    for p in pts[1:-1]:
        b.prism("win", p - Vector((0, 0, 0.28)), p + Vector((0, 0, 0.28)), 0.5, 0.5, 0.1, taper=1.0)


def dress_shoal(b):
    """Salvage clans: patches over the seams, strapped cargo, lean-tos and strings of lamps."""
    ro, face = GIMBAL_RING, GIMBAL_FACE
    b.part = "Shoal patches"
    for a0, a1, mat, z in ((123, 138, "brick", 0.35), (222, 236, "blue", -0.3), (50, 62, "ochre", 0.1)):
        b.arc(mat, face + 0.12, a0, a1, 0.24, 2.1, 0.05, z=z)
    for a0, a1, mat, s in ((196, 212, "brick", 1), (300, 313, "blue", 1), (150, 164, "ochre", -1), (56, 70, "brick", -1)):
        b.arc(mat, ro + 0.15 * s, a0, a1, 2.2, 0.24, 0.05, z=s * 1.62)

    b.part = "Shoal cargo"
    for ang, s in ((151, 1), (291, -1)):
        with b.push(R("Z", ang)):
            b.prism("ochre", (face - 0.1, -1.3, 0.3 * s), (face + 1.6, -1.3, 0.3 * s), 1.7, 1.7, 0.25)
            b.prism("canvas", (face - 0.1, 0.6, -0.4 * s), (face + 1.3, 0.6, -0.4 * s), 1.5, 2.0, 0.25)
            b.prism("blue", (face + 1.3, 0.5, 0.1), (face + 2.6, 0.5, 0.1), 1.3, 1.3, 0.2)
            b.prism("slate", (face + 0.6, -2.3, 0), (face + 0.6, 1.7, 0), 0.25, 2.6, 0.05, up=(0, 0, 1), taper=1.0)

    b.part = "Shoal lean-tos"
    for ang, s, mat in ((64, 1, "brick"), (252, -1, "canvas")):
        with b.push(R("Z", ang)):
            b.prism(mat, (ro - 0.3, 0, s * 1.4), (ro - 0.3, 0, s * 3.3), 2.0, 1.9, 0.3, up=(1, 0, 0))
            b.prism("bone", (ro - 0.3, 0, s * 3.2), (ro - 0.3, 0, s * 3.55), 2.5, 2.4, 0.2, up=(1, 0, 0))
            b.prism("win", (ro + 0.68, 0, s * 2.0), (ro + 0.68, 0, s * 2.8), 1.0, 0.14, 0.02, up=(1, 0, 0), taper=1.0)
            b.prism("slate", (ro - 0.9, 0.5, s * 3.5), (ro - 0.9, 0.5, s * 4.9), 0.32, 0.32, 0.07, taper=1.0)

    b.part = "Shoal lamp strings"
    for a0, a1, z in ((44, 104, 0.7), (256, 316, -0.7)):
        for a in (a0, a1):
            b.prism("slate", polar(face - 0.1, a, z), polar(face + 1.8, a, z), 0.3, 0.3, 0.07, taper=1.0)
        mid = polar(1.0, (a0 + a1) / 2)
        string_lights(b, polar(face + 1.8, a0, z), polar(face + 1.8, a1, z), mid * 3.4)


def dress_veil(b):
    """Tech order: seams shrouded over, heat sinks, cable runs and sensor masts."""
    ro, face = GIMBAL_RING, GIMBAL_FACE
    b.part = "Veil shrouds"
    for j in (130.5, 229.5):
        b.arc("bone", ro, j - 8, j + 8, 3.8, 3.4, 1.0)
    b.arc("canvas", ro, 30, 44, 3.8, 3.4, 1.0)
    b.arc("canvas", ro, 316, 330, 3.8, 3.4, 1.0)

    b.part = "Veil heat sinks"
    for start in (146, 196, 296):
        b.arc("canvas", face + 0.15, start - 1.5, start + 18.5, 0.4, 2.9, 0.1)
        for k in range(6):
            a = start + k * 3.2
            b.arc("slate", face + 1.0, a, a + 1.3, 1.6, 2.5, 0.12, taper=1.0)

    b.part = "Veil masts"
    for ang, length, z in ((207, 9.5, 0.7), (213, 6.0, -0.8), (64, 7.0, -0.5)):
        b.prism("slate", polar(face - 0.2, ang, z), polar(face + length, ang, z), 0.4, 0.4, 0.09, taper=0.25,
                tlen=length)
        b.prism("win", polar(face + length - 0.2, ang, z), polar(face + length + 0.3, ang, z), 0.3, 0.3, 0.06,
                taper=1.0)

    b.part = "Veil cables"
    for r in (ro + 0.9, ro + 0.4):
        b.arc("slate", r, 46, 172, 0.32, 0.32, 0.08, z=1.72, taper=1.0, step=8)
    for r in (ro - 0.7, ro - 0.2):
        b.arc("slate", r, 188, 314, 0.32, 0.32, 0.08, z=-1.72, taper=1.0, step=8)


def dress_bastion(b):
    """Warrior clans: armour over the hull in matched pairs, clan standards and gate towers."""
    ro, face = GIMBAL_RING, GIMBAL_FACE
    b.part = "Bastion armour"
    for s, e in ((41, 73), (139, 171), (189, 221), (287, 319)):
        b.arc("ochre", face + 0.22, s, e, 0.6, 3.5, 0.5)
        b.arc("bone", face + 0.56, s + 12, e - 12, 0.2, 1.1, 0.08)
        for zs in (1, -1):
            b.arc("ochre", ro + 0.5, s + 3, e - 3, 1.5, 0.5, 0.2, z=zs * 1.72)

    b.part = "Bastion standards"
    for a in (90, 270):
        b.prism("slate", polar(face + 0.2, a), polar(face + 5.0, a), 0.5, 0.5, 0.12, taper=1.0)
        b.prism("ochre", polar(face + 1.4, a), polar(face + 4.8, a), 0.3, 4.4, 0.1)
        b.prism("bone", polar(face + 2.7, a), polar(face + 3.5, a), 0.44, 4.4, 0.1, taper=1.0)

    b.part = "Bastion gate towers"
    for a in (38, 322):
        b.prism("bone", polar(ro, a, -4.3), polar(ro, a, 4.3), 1.7, 1.7, 0.4)
        for zs in (1, -1):
            b.prism("ochre", polar(ro, a, zs * 3.3), polar(ro, a, zs * 4.9), 2.1, 2.1, 0.5)
            b.prism("win", polar(ro, a, zs * 4.9), polar(ro, a, zs * 5.15), 1.0, 1.0, 0.2, taper=1.0)


DRESS = {("gimbal", "shoal"): dress_shoal, ("gimbal", "veil"): dress_veil, ("gimbal", "bastion"): dress_bastion}


STATIONS = {
    "gimbal": (gimbal, "#5BFF86", "X"),
    "lantern": (lantern, "#FFE25A", "Y"),
    "drum": (drum, "#FF3B52", "Y"),
    "clam": (clam, "#FFE25A", "X"),
    "kite": (kite, "#FFE25A", "Y"),
    "diabolo": (diabolo, "#FFE25A", "Y"),
}

# A skin swaps the hull colours and the window light; shapes and service lights stay.
SKINS = {
    "neutral": dict(PALETTE, win="#FFC45A"),
    "shoal": dict(bone="#D9B47C", canvas="#8E6E4C", slate="#1B191D", ochre="#F0861A", brick="#A5482E",
                  blue="#5E6F6A", win="#FFC45A"),
    "veil": dict(bone="#5C636C", canvas="#474D57", slate="#15171C", ochre="#3B4149", brick="#4D525B",
                 blue="#30373F", win="#A8F63B"),
    "bastion": dict(bone="#EEF1F5", canvas="#C7D1DE", slate="#1D2A4B", ochre="#2B6FD6", brick="#2B6FD6",
                    blue="#1F4FA8", win="#BFF1FF"),
}


def tumbles(count, seed=1384):
    rng = random.Random(seed)
    out = []
    for _ in range(count):
        u1, u2, u3 = rng.random(), rng.random(), rng.random()
        out.append(Quaternion((math.sqrt(1 - u1) * sin(2 * math.pi * u2), math.sqrt(1 - u1) * cos(2 * math.pi * u2),
                               math.sqrt(u1) * sin(2 * math.pi * u3), math.sqrt(u1) * cos(2 * math.pi * u3))))
    return out


def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 24
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = RES
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    world = bpy.data.worlds.new("fill")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.55, 0.6, 0.85, 1.0)
    bg.inputs[1].default_value = 0.22
    scene.world = world

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ORTHO
    cam.data.clip_end = 200
    cam.location = (0, 0, 80)
    scene.collection.objects.link(cam)
    scene.camera = cam

    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.14
    sun.data.angle = radians(1.0)
    sun.rotation_euler = Vector((0.58, -0.6, -0.55)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun)
    return scene


def ship_marker(mats, collection):
    me = bpy.data.meshes.new("Player ship scale")
    me.from_pydata([(0, 1.2, 0), (-0.8, -0.9, 0), (0, -0.45, 0), (0.8, -0.9, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(mats["ship"])
    ob = bpy.data.objects.new(me.name, me)
    ob.location = (0, 0, 40)
    ob.visible_shadow = False
    collection.objects.link(ob)
    return ob


def main():
    scene = setup_scene()
    shared = {k: glow(k, v) for k, v in GLOW.items() if k in ("svc", "core", "ship")}
    shared["ink"] = ink_material()
    random_views = tumbles(5)
    specs = [s for s in ONLY if s != "blockout"] or [n for n in STATIONS]
    blockout = "blockout" in ONLY
    turn = {axis: Quaternion(vec, math.pi / 2) for axis, vec in (("X", (1, 0, 0)), ("Y", (0, 1, 0)))}
    for spec in specs:
        name, _, rest = spec.partition("@")
        skin, _, dressed = rest.partition("+")
        skin = skin or "neutral"
        tag = name if skin == "neutral" else f"{name}-{skin}"
        tag += "-dress" if dressed else ""
        build, svc, edge_axis = STATIONS[name]
        colours = SKINS[skin]
        mats = dict(shared)
        mats.update({k: cel(f"{k}.{tag}", colours[k]) for k in PALETTE})
        mats["win"] = glow(f"win.{tag}", colours["win"])
        lit_colours = {"win": colours["win"], "svc": svc, "core": GLOW["core"]}

        def light(lit):
            for key, colour in lit_colours.items():
                mats[key].node_tree.nodes["Emission"].inputs[0].default_value = srgb(colour if lit else UNLIT)

        def assemble(scale):
            builder = Builder(scale)
            build(builder)
            if dressed:
                DRESS[(name, skin)](builder)
            return builder

        scale = 1.0
        if blockout:
            # Size every station to the footprint of the one in the game today.
            probe = assemble(1.0)
            scale = BLOCKOUT_SIZE / max(probe.high - probe.low)
        b = assemble(scale)
        root, tris = b.finish(tag, mats, scene)
        ref = bpy.data.collections.new("Reference")
        scene.collection.children.link(ref)
        ship = ship_marker(mats, ref)
        if blockout:
            trigger = bpy.data.objects.new("Trigger sphere, radius 16 u", None)
            trigger.empty_display_type, trigger.empty_display_size = "SPHERE", 16.0
            ref.objects.link(trigger)
        root.rotation_mode = "QUATERNION"
        if blockout:
            views = [("top", Quaternion()), ("front", turn["X"]), ("side", turn["Y"]),
                     ("reverse", Quaternion((1, 0, 0), math.pi))]
            marked, dark_views = ("top", "front", "side"), ("top", "tumble1", "tumble2", "tumble3")
        else:
            views = [("face", Quaternion()), ("reverse", Quaternion((1, 0, 0), math.pi)), ("edge", turn[edge_axis])]
            marked, dark_views = ("face",), ("face", "tumble1", "tumble2", "tumble3")
        views += [(f"tumble{i + 1}", q) for i, q in enumerate(random_views)]
        for label, q in views:
            root.rotation_quaternion = q
            ship.hide_render = label not in marked
            for lit in (True, False):
                if not lit and label not in dark_views:
                    continue
                light(lit)
                scene.render.filepath = f"{OUT}/{tag}-{label}-{'lit' if lit else 'dark'}.png"
                bpy.ops.render.render(write_still=True)
        root.rotation_quaternion = Quaternion()
        light(True)
        ship.hide_render = False
        bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/{tag}.blend", copy=True)
        size = b.high - b.low
        facts = {"tag": tag, "scale": round(scale, 3), "reach": round(b.reach, 2), "clear": round(b.clear, 2),
                 "size": [round(v, 1) for v in size], "tris": tris,
                 "parts": [p for p, s in b.parts.items() if s["meshes"] and not s["parent"]],
                 "part_count": sum(1 for s in b.parts.values() if s["meshes"])}
        Path(f"{OUT}/{tag}.json").write_text(json.dumps(facts, indent=1), encoding="utf-8")
        print(f"STATION {tag} tris={tris} parts={facts['part_count']} scale={scale:.3f} reach={b.reach:.2f}")
        for ob in list(bpy.data.objects):
            if ob.type in ("MESH", "EMPTY"):
                bpy.data.objects.remove(ob, do_unlink=True)
        for col in list(bpy.data.collections):
            bpy.data.collections.remove(col)
        for me in list(bpy.data.meshes):
            if me.users == 0:
                bpy.data.meshes.remove(me)


main()
