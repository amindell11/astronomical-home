"""Station massing proof for #1384: three clay-and-light structures, tumbled.

Run: blender -b --factory-startup --python stations.py -- <outdir> [station ...]
"""
import math
import random
import sys
from math import cos, radians, sin

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

OUT = sys.argv[sys.argv.index("--") + 1]
ONLY = sys.argv[sys.argv.index("--") + 2:]

RES = 640
ORTHO = 38.0
INK = 0.17
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
    def __init__(self):
        self.parts = {}
        self.stack = [Matrix.Identity(4)]
        self.part = "Part"

    def push(self, m):
        builder = self

        class Scope:
            def __enter__(self):
                builder.stack.append(builder.stack[-1] @ m)

            def __exit__(self, *exc):
                builder.stack.pop()

        return Scope()

    def emit(self, mat, verts, faces):
        vs, fs = self.parts.setdefault((self.part, mat), ([], []))
        off, m = len(vs), self.stack[-1]
        vs.extend(m @ Vector(v) for v in verts)
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

    def finish(self, name, mats, collection):
        root = bpy.data.objects.new(name, None)
        collection.objects.link(root)
        tris = 0
        for (part, mat), (vs, fs) in self.parts.items():
            me = bpy.data.meshes.new(f"{part} - {mat}")
            me.from_pydata([v[:] for v in vs], [], fs)
            bm = bmesh.new()
            bm.from_mesh(me)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            tris += sum(len(f.verts) - 2 for f in bm.faces)
            bm.to_mesh(me)
            bm.free()
            me.materials.append(mats[mat])
            ob = bpy.data.objects.new(me.name, me)
            collection.objects.link(ob)
            ob.parent = root
            if mat in GLOW:
                ob.visible_shadow = False
                continue
            twin = bpy.data.objects.new(me.name + " ink", me)
            collection.objects.link(twin)
            twin.parent = root
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
    b.part = "Outer ring"
    b.plated_arc(ro, 35, 325, 6, 7, plate=(3.4, 3.0, 0.8), core=(2.3, 2.0, 0.5),
                 mats=["bone", "bone", "canvas", "bone", "ochre", "bone"],
                 windows=[("out", "top"), (), ("top", "bot", "out"), ("in",), (), ("out", "bot")],
                 lamps=[("in",), (), (), (), (), ("in",)])
    b.part = "Berth lights"
    for a, s in ((35, -1), (325, 1)):
        b.arc("slate", ro, a + s * 0.2, a + s * 3.0, 2.6, 2.3, 0.5, taper=1.0)
        b.arc("svc", ro, a + s * 3.0, a + s * 4.4, 1.7, 1.4, 0.3, taper=1.0)

    b.part = "Gimbal bearings"
    for a in (90, 270):
        b.arc("slate", ro, a - 7, a + 7, 4.0, 3.6, 0.9)
        b.prism("ochre", polar(ri + 0.9, a), polar(ro - 1.2, a), 1.5, 1.5, 0.35, taper=1.0)

    with b.push(R("Y", 62)):
        b.part = "Inner ring"
        b.plated_arc(ri, 50, 310, 4, 8, plate=(2.3, 2.1, 0.55), core=(1.5, 1.4, 0.35),
                     mats=["canvas", "bone", "bone", "blue"],
                     windows=[("top",), ("out",), ("bot",), ()])
        b.part = "Repair arms"
        for a in (125, 180, 235):
            b.prism("slate", polar(ri - 1.0, a), polar(4.9, a), 0.7, 0.7, 0.18, taper=1.0)
            b.prism("canvas", polar(5.5, a), polar(4.4, a), 2.0, 1.4, 0.3)
            b.prism("svc", polar(4.4, a), polar(4.15, a), 1.3, 0.8, 0.15, taper=1.0)
        for a, s in ((50, -1), (310, 1)):
            b.arc("svc", ri, a + s * 0.2, a + s * 2.2, 1.2, 1.0, 0.25, taper=1.0)

    b.part = "Crew pod"
    px = -(ro + 1.7 + 0.35 + 1.9)
    with b.push(T(px, 0, 0) @ R("X", 90)):
        b.pod(6.5, 1.9)
    for y in (-1.7, 1.7):
        b.prism("slate", (-(ro + 1.5), y, 0), (px + 1.2, y, 0), 1.1, 1.1, 0.25, taper=1.0)

    b.part = "Tanks"
    with b.push(R("Z", 118) @ T(ro, 0, 1.5 + 0.8) @ R("X", 90)):
        b.tank("blue", 4.4, 0.85)
    with b.push(R("Z", 248) @ T(ro, 0, -(1.5 + 0.8)) @ R("X", 90)):
        b.tank("canvas", 3.6, 0.85)


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
    with b.push(R("Y", 90)):
        for k in range(5):
            z0, length = offsets[k], lengths[k]
            lo, hi = z0 - length / 2, z0 + length / 2
            with b.push(R("Z", slots[k])):
                b.part = f"Magazine {k + 1}"
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

        b.part = "Rings"
        ring = dict(plate=(1.3, 2.6, 0.35), core=(0.8, 1.5, 0.2), closed=True)
        b.plated_arc(radius + h / 2 + 0.9, -26, 334, 6, 8, z=6.0, mats=["bone", "canvas", "bone", "bone", "ochre", "bone"],
                     windows=[(), ("out",), (), ()], **ring)
        b.plated_arc(radius + h / 2 + 0.9, -26, 334, 6, 8, z=-6.0, mats=["canvas", "bone", "bone", "blue", "bone", "canvas"],
                     windows=[("out",), (), (), (), ()], **ring)

        b.part = "Crew pod"
        with b.push(R("Z", 180)):
            with b.push(T(radius + 0.3, 0, 3.4)):
                b.pod(7.0, 2.0)
            with b.push(T(radius, 0, -5.8)):
                b.tank("blue", 5.6, 1.45)
            b.prism("slate", (radius + 1.4, 0, -6.0), (radius + 2.1, 0, -6.0), 1.2, 1.2, 0.25, up=(0, 0, 1),
                    taper=1.0)


STATIONS = {
    "gimbal": (gimbal, "#5BFF86", "X"),
    "lantern": (lantern, "#FFE25A", "Y"),
    "drum": (drum, "#FF3B52", "Y"),
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
    mats = {k: cel(k, v) for k, v in PALETTE.items()}
    mats.update({k: glow(k, v) for k, v in GLOW.items()})
    mats["ink"] = ink_material()

    def light(lit, svc):
        for key in ("win", "svc", "core"):
            colour = (svc if key == "svc" else GLOW[key]) if lit else UNLIT
            mats[key].node_tree.nodes["Emission"].inputs[0].default_value = srgb(colour)

    random_views = tumbles(5)
    for name, (build, svc, edge_axis) in STATIONS.items():
        if ONLY and name not in ONLY:
            continue
        b = Builder()
        build(b)
        root, tris = b.finish(name, mats, scene.collection)
        ship = ship_marker(mats, scene.collection)
        root.rotation_mode = "QUATERNION"
        views = [("face", Quaternion()), ("reverse", Quaternion((1, 0, 0), math.pi)),
                 ("edge", Quaternion((1, 0, 0) if edge_axis == "X" else (0, 1, 0), math.pi / 2))]
        views += [(f"tumble{i + 1}", q) for i, q in enumerate(random_views)]
        for label, q in views:
            root.rotation_quaternion = q
            ship.hide_render = label != "face"
            for lit in (True, False):
                if not lit and label not in ("face", "tumble1", "tumble2", "tumble3"):
                    continue
                light(lit, svc)
                scene.render.filepath = f"{OUT}/{name}-{label}-{'lit' if lit else 'dark'}.png"
                bpy.ops.render.render(write_still=True)
        root.rotation_quaternion = Quaternion()
        light(True, svc)
        ship.hide_render = False
        bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/{name}.blend", copy=True)
        print(f"STATION {name} tris={tris} parts={len(b.parts)}")
        for ob in list(scene.collection.objects):
            if ob.type in ("MESH", "EMPTY"):
                bpy.data.objects.remove(ob, do_unlink=True)
        for me in list(bpy.data.meshes):
            if me.users == 0:
                bpy.data.meshes.remove(me)


main()
