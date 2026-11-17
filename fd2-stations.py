"""Station massing proof for #1384: three clay-and-light structures, tumbled.

Run: blender -b --factory-startup --python stations.py -- <outdir> [blockout|passes] [plain] [largewin] [brush=<tile.png>] [station[@skin[+dress]] ...]
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
INK_FINE = 0.055
DETAIL = True
NEW_LAMPS = True
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
    emit.name = "Emission"
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
        self.thin = False
        self.reach = 0.0
        self.clear = float("inf")
        self.low = Vector((1e9, 1e9, 1e9))
        self.high = Vector((-1e9, -1e9, -1e9))
        self.mounts = []
        self.chains = 0
        self.tall_ok = True
        self.part = "Part"

    def mount(self, origin, along, normal, size, chain=None):
        """A spot a faction's part kit can use: a face centre, the face's long axis and its outward normal.

        Stored in the station's unscaled space with the part it rides on, so dress turns with that part.
        """
        m = self.stack[0].inverted() @ self.stack[-1]
        rot = m.to_3x3()
        n = (rot @ Vector(normal)).normalized()
        a = rot @ Vector(along)
        a = (a - n * a.dot(n)).normalized()
        self.mounts.append({"part": self.current, "o": m @ Vector(origin), "a": a, "n": n,
                            "size": tuple(size), "chain": chain, "tall": self.tall_ok})

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

    def fine(self):
        """Small hardware gets a thin outline, or the outline swallows it."""
        builder = self

        class Scope:
            def __enter__(self):
                self.was = builder.thin
                builder.thin = True

            def __exit__(self, *exc):
                builder.thin = self.was

        return Scope()

    def emit(self, mat, verts, faces):
        part = self.parts[self.current]
        key = mat + ("|fine" if self.thin and mat not in GLOW and mat != "mark" else "")
        vs, fs = part["meshes"].setdefault(key, ([], []))
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
                   core_mat="slate", lamps=None, detail=True):
        """A dark core with n proud plates along it.

        windows names the faces per plate that carry amber panes; lamps names the faces
        that carry a dotted row of approach lights in the station's service colour.
        """
        pw, ph, pc = plate
        self.arc(core_mat, radius, a0, a1, *core, z=z, taper=1.0)
        span = a1 - a0
        seg = (span - joint * (n if closed else n - 1)) / n
        chain = self.chains
        self.chains += 1
        for i in range(n):
            s = a0 + i * (seg + joint)
            e = s + seg
            skin = mats[i % len(mats)]
            self.arc(skin, radius, s, e, pw, ph, pc, z=z)
            lit = tuple(windows[i % len(windows)]) + (tuple(lamps[i % len(lamps)]) if lamps else ())
            rows = [(f, "win", WINDOWS["pane"], WINDOWS["gap"], 0.07, WINDOWS["across"], off)
                    for f in windows[i % len(windows)] for off in WINDOWS["rows"]]
            if lamps:
                rows += [(f, "svc", 0.55, 0.8, 0.16, 0.2, 0.0) for f in lamps[i % len(lamps)]]
            row0, row1 = s + seg * 0.2, e - seg * 0.2
            for face, mat, pane, pane_gap, proud, across, off in rows:
                if face in ("out", "in"):
                    r = radius + (pw / 2 + 0.03) * (1 if face == "out" else -1)
                    size, zz = (proud * 2, ph * across, 0.02), z + off * ph
                else:
                    r = radius + off * pw
                    size, zz = (pw * across, proud * 2, 0.02), z + (ph / 2 + 0.03) * (1 if face == "top" else -1)
                pane_deg = math.degrees(pane / r)
                gap_deg = math.degrees(pane_gap / r)
                count = max(1, int((row1 - row0 + gap_deg) / (pane_deg + gap_deg)))
                used = count * pane_deg + (count - 1) * gap_deg
                start = (row0 + row1) / 2 - used / 2
                for k in range(count):
                    q = start + k * (pane_deg + gap_deg)
                    self.arc(mat, r, q, q + pane_deg, *size, z=zz, taper=1.0)
            if detail and DETAIL:
                self.plate_detail(radius, s, e, plate, z, skin, lit, random.Random(f"{radius:.2f}/{z:.2f}/{i}/{n}"))
            chord, mid = radians(abs(seg)) * radius, (s + e) / 2
            for face in ("out", "top", "bot"):
                if face in lit:
                    continue
                if face == "out":
                    o, nrm, size = polar(radius + pw / 2, mid, z), polar(1.0, mid), (chord * 0.75, ph * 0.75)
                else:
                    sgn = 1 if face == "top" else -1
                    o, nrm, size = polar(radius, mid, z + sgn * ph / 2), (0, 0, sgn), (chord * 0.75, pw * 0.75)
                self.mount(o, polar(1.0, mid + 90), nrm, size, chain=(chain, face, i))

    def plate_detail(self, radius, s, e, plate, z, skin, lit, rng):
        """Straps, a trim band, a raised panel and ink marks, varied per plate from a fixed seed."""
        pw, ph, pc = plate
        seg = e - s
        if radians(abs(seg)) * radius < 2.5:
            return
        deg = math.degrees(1.0 / radius)
        free = [f for f in ("out", "in", "top", "bot") if f not in lit]
        with self.fine():
            if rng.random() < 0.55:
                at = s + seg * rng.uniform(0.3, 0.7)
                self.arc("slate", radius, at - 0.07 * deg, at + 0.07 * deg, pw + 0.07, ph + 0.07, pc, z=z, taper=1.0)
            if rng.random() < 0.4:
                at = s + seg * (0.1 if rng.random() < 0.5 else 0.9)
                self.arc("canvas" if skin == "bone" else "bone", radius, at - 0.2 * deg, at + 0.2 * deg,
                         pw + 0.09, ph + 0.09, pc, z=z, taper=1.0)
            if free and rng.random() < 0.7:
                face = rng.choice(free)
                a, c = s + seg * rng.uniform(0.16, 0.3), e - seg * rng.uniform(0.16, 0.3)
                if face in ("out", "in"):
                    r = radius + (pw / 2 + 0.05) * (1 if face == "out" else -1)
                    self.arc(skin, r, a, c, 0.14, (ph - 2 * pc) * 0.8, 0.03, z=z, taper=1.0)
                else:
                    zz = z + (ph / 2 + 0.05) * (1 if face == "top" else -1)
                    self.arc(skin, radius, a, c, (pw - 2 * pc) * 0.8, 0.14, 0.03, z=zz, taper=1.0)
        for face in free:
            if rng.random() < 0.6:
                self.arc_marks(radius, s, e, plate, z, face, rng)

    def arc_marks(self, radius, s, e, plate, z, face, rng):
        """A cluster of ink strokes on one face of a plate: hatching, sometimes crossed, or a scratch."""
        pw, ph, pc = plate
        seg = e - s
        deg = math.degrees(1.0 / radius)
        at = s + seg * rng.uniform(0.15, 0.85)
        kind = rng.choice(("hatch", "hatch", "cross", "scratch"))
        count = 1 if kind == "scratch" else rng.randint(4, 8)
        length = rng.uniform(1.6, 3.0) if kind == "scratch" else rng.uniform(0.8, 1.5)
        lean = rng.choice((-1, 1)) * rng.uniform(0.5, 0.9)
        strokes = [(i * 0.27 + rng.uniform(-0.04, 0.04), lean * rng.uniform(0.85, 1.15)) for i in range(count)]
        if kind == "cross":
            strokes += [(i * 0.3 + 0.1, -lean) for i in range(max(2, count - 2))]
        across = (ph - 2 * pc) if face in ("out", "in") else (pw - 2 * pc)
        mid = rng.uniform(-0.25, 0.25) * across
        for shift, slope in strokes:
            along = length / math.hypot(1, slope)
            rise = max(-across * 0.45, min(across * 0.45, along * slope))
            a0 = at + (shift - along / 2) * deg
            a1 = a0 + along * deg
            v0, v1 = mid - rise / 2, mid + rise / 2
            if face in ("out", "in"):
                r = radius + (pw / 2 + 0.035) * (1 if face == "out" else -1)
                p0, p1 = polar(r, a0, z + v0), polar(r, a1, z + v1)
                up = polar(1.0, (a0 + a1) / 2)
            else:
                zz = z + (ph / 2 + 0.035) * (1 if face == "top" else -1)
                p0, p1 = polar(radius + v0, a0, zz), polar(radius + v1, a1, zz)
                up = (0, 0, 1)
            self.prism("mark", p0, p1, 0.065, 0.03, 0.005, up=up, taper=1.0)

    def flat_marks(self, origin, along, across, normal, size, rng, clusters=2):
        """Ink strokes on a flat face: origin is its centre, size its (along, across) extent."""
        origin, along, across, normal = (Vector(v) for v in (origin, along, across, normal))
        along, across, normal = along.normalized(), across.normalized(), normal.normalized()
        for _ in range(clusters):
            centre = origin + along * rng.uniform(-0.4, 0.4) * size[0] + across * rng.uniform(-0.25, 0.25) * size[1]
            kind = rng.choice(("hatch", "hatch", "cross", "scratch"))
            count = 1 if kind == "scratch" else rng.randint(4, 8)
            length = rng.uniform(1.6, 3.0) if kind == "scratch" else rng.uniform(0.8, 1.5)
            lean = rng.choice((-1, 1)) * rng.uniform(0.5, 0.9)
            strokes = [(i * 0.27 + rng.uniform(-0.04, 0.04), lean * rng.uniform(0.85, 1.15)) for i in range(count)]
            if kind == "cross":
                strokes += [(i * 0.3 + 0.1, -lean) for i in range(max(2, count - 2))]
            for shift, slope in strokes:
                run = length / math.hypot(1, slope)
                rise = max(-size[1] * 0.45, min(size[1] * 0.45, run * slope))
                p0 = centre + along * (shift - run / 2) - across * (rise / 2) + normal * 0.035
                p1 = centre + along * (shift + run / 2) + across * (rise / 2) + normal * 0.035
                self.prism("mark", p0, p1, 0.065, 0.03, 0.005, up=normal, taper=1.0)

    def pane_row(self, p0, p1, normal, across, height):
        """Window panes along a straight face from p0 to p1; across is the face's other axis, height its extent."""
        p0, p1, normal, across = Vector(p0), Vector(p1), Vector(normal).normalized(), Vector(across).normalized()
        length = (p1 - p0).length
        d = (p1 - p0).normalized()
        pane, gap = WINDOWS["pane"], WINDOWS["gap"]
        count = max(1, int((length + gap) / (pane + gap)))
        start = (length - (count * pane + (count - 1) * gap)) / 2
        for off in WINDOWS["rows"]:
            for k in range(count):
                a = p0 + d * (start + k * (pane + gap)) + across * (off * height) + normal * 0.05
                self.prism("win", a, a + d * pane, height * WINDOWS["across"], 0.14, 0.02, up=normal, taper=1.0)

    def hatch(self, r=0.7):
        """A crew hatch on the local XY plane, facing +Z."""
        with self.fine():
            self.lathe("canvas", [(r * 1.35, -0.05), (r * 1.35, 0.1), (r * 1.1, 0.2)], sides=8)
            self.lathe("slate", [(r, 0.18), (r, 0.26)], sides=8)
            self.prism("win", (r * 1.7, -0.18, 0.08), (r * 1.7, 0.18, 0.08), 0.2, 0.16, 0.03, taper=1.0)

    def vent(self, length=2.2, width=1.3, fins=5):
        """Cooling fins on the local XY plane, running along X and standing along +Z."""
        with self.fine():
            self.prism("canvas", (-length / 2, 0, 0.06), (length / 2, 0, 0.06), width, 0.16, 0.04, taper=1.0)
            for k in range(fins):
                x = -length / 2 + (k + 0.5) * length / fins
                self.prism("slate", (x, -width * 0.42, 0.3), (x, width * 0.42, 0.3), 0.1, 0.42, 0.02, taper=1.0)

    def rail(self, p0, p1, lift):
        """A handrail from p0 to p1, held off the surface along lift."""
        p0, p1, lift = Vector(p0), Vector(p1), Vector(lift)
        with self.fine():
            self.prism("slate", p0 + lift, p1 + lift, 0.1, 0.1, 0.02, up=lift.normalized(), taper=1.0)
            for q in (p0, p1, (p0 + p1) / 2):
                self.prism("slate", q, q + lift, 0.09, 0.09, 0.02, up=(p1 - p0).normalized(), taper=1.0)

    def mast(self, length=3.2, lamp="win"):
        """A thin mast along local +Z with a lamp at its tip."""
        with self.fine():
            self.prism("slate", (0, 0, 0), (0, 0, length), 0.16, 0.16, 0.03, taper=1.0)
            self.prism("canvas", (0, 0, length * 0.55), (0, 0, length * 0.62), 0.5, 0.5, 0.08, taper=1.0)
            self.prism(lamp, (0, 0, length), (0, 0, length + 0.22), 0.26, 0.26, 0.05, taper=1.0)

    def pod(self, length=6.5, r=1.9, body="bone", hatch=True, mast=True):
        """Crew capsule along local Z with rows of small windows, a hatch on one end and a mast on the other."""
        hz = length / 2
        nose = r * 0.55
        self.lathe(body, [(r * 0.5, -hz), (r, -hz + nose), (r, hz - nose), (r * 0.5, hz)], sides=10)
        for s in (1, -1):
            zc = s * (hz - nose - 0.25)
            self.lathe("slate", [(r * 1.1, zc - 0.2), (r * 1.1, zc + 0.2)], sides=10)
            self.lathe("canvas", [(r * 0.42, s * hz), (r * 0.34, s * (hz + 0.3))], sides=10)
        flat = r * cos(radians(18))
        for row, (zc, half) in enumerate(WINDOWS["pod"]):
            for k in range(10):
                if (k + row * 2) % 5 == 4:
                    continue
                ang = (k + 0.5) * 36
                n, t = polar(1.0, ang), polar(1.0, ang + 90)
                centre = n * (flat + 0.04) + Vector((0, 0, zc))
                self.prism("win", centre - t * (r * 0.2), centre + t * (r * 0.2), half * 2, 0.1, 0.02, up=n, taper=1.0)
        if DETAIL and hatch:
            with self.push(T(0, 0, hz + 0.3)):
                self.hatch(r * 0.3)
        if DETAIL and mast:
            with self.push(T(0, 0, -hz - 0.3) @ R("X", 180)):
                self.mast(r * 1.3)

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
            for key, (vs, fs) in spec["meshes"].items():
                mat, _, fine = key.partition("|")
                me = bpy.data.meshes.new(f"{part} / {mat}" + (" / fine" if fine else ""))
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
                ob["key"] = mat
                if mat in GLOW or mat == "mark":
                    ob.visible_shadow = False
                    continue
                twin = bpy.data.objects.new(me.name + " / ink", me)
                ink_col.objects.link(twin)
                twin.parent = pivot
                shell = twin.modifiers.new("ink", "DISPLACE")
                shell.direction, shell.strength, shell.mid_level = "NORMAL", (INK_FINE if fine else INK), 0.0
                twin.material_slots[0].link = "OBJECT"
                twin.material_slots[0].material = mats["ink"]
                twin.visible_shadow = False
                twin["key"] = "ink"
        return root, tris


def polar(radius, deg, z=0.0):
    return Vector((radius * cos(radians(deg)), radius * sin(radians(deg)), z))


def gimbal(b):
    """Repair: a broken ring with a second ring swung through it, so one always shows open."""
    ro, ri = 11.5, 8.2
    face, top = ro + 1.7, 1.5
    b.begin("Outer ring")
    b.plated_arc(ro, 35, 325, 6, 7, plate=(3.4, 3.0, 0.8), core=(2.3, 2.0, 0.5),
                 mats=["bone", "bone", "canvas", "bone", "ochre", "bone"],
                 windows=[("out", "top"), (), ("top", "bot", "out"), ("in",), (), ("out", "bot")],
                 lamps=[("in",), (), (), (), (), ("in",)])
    if DETAIL:
        # Pipes run round both faces of the ring, so neither side is the plain one.
        with b.fine():
            for r, a0, a1, z in ((ro + 0.45, 97, 126, top + 0.16), (ro - 0.2, 97, 126, top + 0.16),
                                 (ro + 0.3, 190, 262, -top - 0.16), (ro - 0.35, 190, 262, -top - 0.16)):
                b.arc("slate", r, a0, a1, 0.22, 0.22, 0.05, z=z, taper=1.0, step=8)
            for a, z, r in ((104, top + 0.12, ro + 0.12), (120, top + 0.12, ro + 0.12), (200, -top - 0.12, ro),
                            (229.5, -top - 0.12, ro), (255, -top - 0.12, ro)):
                b.arc("canvas", r, a - 0.8, a + 0.8, 1.2, 0.34, 0.06, z=z, taper=1.0)
        with b.push(R("Z", 252) @ T(ro, 0, top) @ R("Z", 90)):
            b.vent(2.4, 1.3)
        with b.push(R("Z", 110) @ T(ro, 0, -top) @ R("X", 180) @ R("Z", 90)):
            b.vent(2.0, 1.2, 4)
        with b.push(R("Z", 105) @ T(face, 0, 0) @ R("Y", 90)):
            b.hatch(0.6)
        b.rail(polar(face, 97, 1.0), polar(face, 113, 1.0), polar(0.32, 105))
        b.rail(polar(face, 97, -1.0), polar(face, 113, -1.0), polar(0.32, 105))
        with b.push(R("Z", 199) @ T(face - 0.1, 0, 0.7) @ R("Y", 68)):
            b.mast(3.6)
    for label, a, s in (("Berth light A", 35, -1), ("Berth light B", 325, 1)):
        b.begin(label, frame=R("Z", a) @ T(ro, 0, 0), parent="Outer ring")
        b.arc("slate", ro, a + s * 0.2, a + s * 3.0, 2.6, 2.3, 0.5, taper=1.0)
        if NEW_LAMPS:
            # Sunk into the housing behind a lip, so it never reads as a slab standing off the metal.
            b.arc("svc", ro, a + s * 2.4, a + s * 3.5, 1.7, 1.4, 0.3, taper=1.0)
            b.arc("slate", ro, a + s * 3.0, a + s * 3.35, 2.1, 1.8, 0.4, taper=1.0)
        else:
            b.arc("svc", ro, a + s * 3.0, a + s * 4.4, 1.7, 1.4, 0.3, taper=1.0)

    for label, a in (("Bearing A", 90), ("Bearing B", 270)):
        b.begin(label, frame=R("Z", a) @ T(ro, 0, 0), parent="Outer ring")
        b.arc("slate", ro, a - 7, a + 7, 4.0, 3.6, 0.9)
        b.prism("ochre", polar(ri + 0.9, a), polar(ro - 1.2, a), 1.5, 1.5, 0.35, taper=1.0)
        if DETAIL:
            with b.push(R("Z", a) @ T(ro + 2.0, 0, 0) @ R("Y", 90)):
                with b.fine():
                    b.lathe("canvas", [(1.0, -0.05), (1.0, 0.22), (0.7, 0.36)], sides=8)
                    b.lathe("slate", [(0.32, 0.34), (0.32, 0.46)], sides=6)

    b.begin("Inner ring", frame=R("Y", 62))
    # The inner ring's faces look into the berth, where the player flies in: nothing tall goes there.
    b.tall_ok = False
    with b.push(R("Y", 62)):
        b.plated_arc(ri, 50, 310, 4, 8, plate=(2.3, 2.1, 0.55), core=(1.5, 1.4, 0.35),
                     mats=["canvas", "bone", "bone", "blue"],
                     windows=[("top",), ("out",), ("bot",), ()])
        for a, s in ((50, -1), (310, 1)):
            if NEW_LAMPS:
                b.arc("svc", ri, a - s * 0.8, a + s * 0.8, 1.2, 1.0, 0.25, taper=1.0)
                b.arc("slate", ri, a, a + s * 0.35, 1.5, 1.3, 0.3, taper=1.0)
            else:
                b.arc("svc", ri, a + s * 0.2, a + s * 2.2, 1.2, 1.0, 0.25, taper=1.0)
        b.tall_ok = True
        for i, a in enumerate((125, 180, 235)):
            b.begin(f"Repair arm {i + 1}", frame=R("Z", a) @ T(ri - 1.0, 0, 0), parent="Inner ring")
            b.prism("slate", polar(ri - 1.0, a), polar(4.9, a), 0.7, 0.7, 0.18, taper=1.0)
            b.prism("canvas", polar(5.5, a), polar(4.4, a), 2.0, 1.4, 0.3)
            if NEW_LAMPS:
                b.prism("svc", polar(4.55, a), polar(4.25, a), 1.3, 0.8, 0.15, taper=1.0)
                b.prism("slate", polar(4.4, a), polar(4.32, a), 1.6, 1.05, 0.2, taper=1.0)
            else:
                b.prism("svc", polar(4.4, a), polar(4.15, a), 1.3, 0.8, 0.15, taper=1.0)
            if DETAIL:
                with b.fine():
                    b.prism("ochre", polar(6.5, a), polar(6.0, a), 1.0, 1.0, 0.2, taper=1.0)
                    for side in (-1, 1):
                        offset = polar(0.82, a + 90 * side)
                        b.prism("slate", polar(4.5, a) + offset, polar(3.75, a) + offset, 0.22, 0.5, 0.05, taper=1.0)
                    hose = polar(0.42, a + 90) + Vector((0, 0, 0.3))
                    b.prism("slate", polar(ri - 1.1, a) + hose, polar(5.4, a) + hose, 0.14, 0.14, 0.03, taper=1.0)

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
            rng = random.Random(f"magazine/{k}")
            b.begin(f"Magazine {k + 1}", frame=R("Z", slots[k]) @ T(radius, 0, z0))
            with b.push(R("Z", slots[k])):
                b.prism(skins[k], (radius, 0, lo), (radius, 0, hi), w, h, 0.7, up=up)
                for end, s in ((lo, -1), (hi, 1)):
                    b.prism("slate", (radius, 0, end - s * 0.4), (radius, 0, end + s * 0.3),
                            w * 0.74, h * 0.74, 0.4, up=up, taper=1.0)
                    if NEW_LAMPS:
                        b.prism("svc", (radius, 0, end + s * 0.15), (radius, 0, end + s * 0.45),
                                w * 0.46, h * 0.36, 0.1, up=up, taper=1.0)
                        b.prism("slate", (radius, 0, end + s * 0.3), (radius, 0, end + s * 0.36),
                                w * 0.56, h * 0.46, 0.12, up=up, taper=1.0)
                    else:
                        b.prism("svc", (radius, 0, end + s * 0.3), (radius, 0, end + s * 0.5),
                                w * 0.46, h * 0.36, 0.1, up=up, taper=1.0)
                face = radius + h / 2 - 0.05
                hatch = "bone" if skins[k] != "bone" else "canvas"
                b.prism(hatch, (face, 0, hi - length * 0.3), (face, 0, hi - length * 0.12), w * 0.5, 0.3, 0.1, up=up)
                if k % 2 == 0:
                    b.pane_row((face + 0.03, 0, lo + length * 0.08), (face + 0.03, 0, lo + length * 0.34),
                               normal=up, across=(0, 1, 0), height=w * 0.9)
                else:
                    b.prism(hatch, (face, 0, lo + length * 0.08), (face, 0, lo + length * 0.22), w * 0.5, 0.3, 0.1,
                            up=up)
                if DETAIL:
                    with b.fine():
                        for zc in (lo + length * 0.4, hi - length * 0.36):
                            b.prism("slate", (radius, 0, zc - 0.07), (radius, 0, zc + 0.07), w + 0.08, h + 0.08, 0.7,
                                    up=up, taper=1.0)
                        band = "bone" if skins[k] != "bone" else "brick"
                        b.prism(band, (radius, 0, hi - 1.6), (radius, 0, hi - 1.15), w + 0.1, h + 0.1, 0.7, up=up,
                                taper=1.0)
                        for side in (-1, 1):
                            for j in range(3):
                                zz = z0 - 1.6 + j * 1.3
                                b.prism("canvas" if skins[k] != "canvas" else "bone",
                                        (radius, side * (w / 2 + 0.02), zz), (radius, side * (w / 2 + 0.02), zz + 0.8),
                                        1.0, 0.12, 0.03, up=(0, side, 0), taper=1.0)
                    if k % 2:
                        b.rail((face + 0.05, -w * 0.28, z0 - 1.0), (face + 0.05, -w * 0.28, z0 + 1.0), (0.3, 0, 0))
                    b.flat_marks((face + 0.05, 0, z0), (0, 0, 1), (0, 1, 0), up, (length * 0.8, w * 0.5), rng, 3)
                    for side in (-1, 1):
                        b.flat_marks((radius, side * (w / 2), z0), (0, 0, 1), (1, 0, 0), (0, side, 0),
                                     (length * 0.8, h * 0.4), rng, 2)
                b.mount((face + 0.05, 0, z0 + length * 0.04), (0, 0, 1), up, (length * 0.22, w * 0.6),
                        chain=("magazine", k, "top"))
                for side in (-1, 1):
                    b.mount((radius, side * (w / 2), z0 + length * 0.1), (0, 0, 1), (0, side, 0),
                            (length * 0.4, h * 0.6), chain=("magazine", k, side))
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
        if DETAIL:
            for k in range(5):
                lo, hi = offsets[k] - lengths[k] / 2, offsets[k] + lengths[k] / 2
                for zr, label in ((6.0, "Ring fore"), (-6.0, "Ring aft")):
                    if lo < zr < hi:
                        b.begin(label)
                        with b.push(R("Z", slots[k])):
                            b.prism("slate", (radius + h / 2 - 0.1, 0, zr), (radius + h / 2 + 0.4, 0, zr), 1.7, 1.1, 0.2,
                                    up=(0, 0, 1), taper=1.0)

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
        for label, s, skin, trim in (("Spine fore", 1, "bone", "canvas"), ("Spine aft", -1, "canvas", "bone")):
            rng = random.Random(f"spine/{s}")
            b.begin(label, frame=T(0, 0, s * 7.0))
            b.prism(skin, (0, 0, s * 3.8), (0, 0, s * 10.2), 3.8, 3.8, 0.95)
            lo, hi = sorted((s * 4.6, s * 9.4))
            for az in (90, 270):
                with b.push(R("Z", az)):
                    b.pane_row((1.9, 0, lo), (1.9, 0, hi), normal=radial, across=(0, 1, 0), height=1.9)
            if DETAIL:
                with b.fine():
                    for zc in (s * 5.2, s * 8.7):
                        b.prism("slate", (0, 0, zc - 0.07), (0, 0, zc + 0.07), 3.88, 3.88, 0.95, taper=1.0)
                    b.prism(trim, (0, 0, s * 9.5), (0, 0, s * 9.95), 3.92, 3.92, 0.95, taper=1.0)
                    b.lathe("slate", [(2.75, s * 3.6), (2.75, s * 3.95)], sides=8)
                free = 180 if s > 0 else 0
                with b.push(R("Z", free) @ T(1.9, 0, s * 7.0) @ R("Y", 90)):
                    if s > 0:
                        b.hatch(0.55)
                    else:
                        b.vent(2.2, 1.2, 4)
            for az in (0, 180):
                with b.push(R("Z", az)):
                    b.mount((1.9, 0, s * 8.0), (0, 0, 1), radial, (3.2, 2.6), chain=("spine", s, az))
            if DETAIL:
                other = 0 if s > 0 else 180
                with b.push(R("Z", other)):
                    b.flat_marks((1.93, 0, s * 7.0), (0, 0, 1), (0, 1, 0), radial, (4.5, 1.6), rng, 3)
                    if s > 0:
                        b.rail((1.95, -0.6, s * 5.6), (1.95, -0.6, s * 8.2), (0.3, 0, 0))
        b.begin("Spine rods")
        for az in (60, 180, 300):
            b.prism("slate", polar(2.45, az, -3.9), polar(2.45, az, 3.9), 0.36, 0.36, 0.09, taper=1.0)

        collar = dict(plate=(1.3, 1.6, 0.35), core=(0.8, 1.0, 0.2), closed=True, detail=False)
        b.begin("Docking collar fore", frame=T(0, 0, 10.9))
        b.plated_arc(2.7, 10, 370, 5, 10, z=10.9, mats=["canvas", "bone", "ochre", "bone", "canvas"],
                     windows=[()], lamps=[("top",)], **collar)
        b.begin("Docking collar aft", frame=T(0, 0, -10.9))
        b.plated_arc(2.7, 40, 400, 5, 10, z=-10.9, mats=["bone", "canvas", "bone", "blue", "bone"],
                     windows=[()], lamps=[("bot",)], **collar)

        b.begin("Prize")
        core_gem(b, 3.0, 2.0)
        if DETAIL:
            with b.fine():
                b.arc("slate", 2.45, 0, 360, 0.3, 0.3, 0.07, taper=1.0, step=30)

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
            rng = random.Random(f"sail/{i}")
            # The boom turns with the sail's direction; the panels hang off it and carry the pitch.
            b.begin(f"Sail {i + 1}", frame=R("Z", az))
            with b.push(R("Z", az)):
                b.prism("slate", (2.3, 0, 0), (panels[-1][1] - 0.4, 0, 0), 0.7, 0.9, 0.18, up=(0, 1, 0), taper=1.0)
                if DETAIL:
                    b.prism("canvas", (2.4, 0, 0), (3.1, 0, 0), 1.3, 1.5, 0.3, up=(0, 1, 0))
                b.begin(f"Sail {i + 1} panels", frame=R("X", pitch), parent=f"Sail {i + 1}")
                with b.push(R("X", pitch)):
                    for r0, r1, span, zc, skin, cell in panels:
                        b.prism(skin, (r0, 0, zc), (r1, 0, zc), span, 0.9, 0.28, up=(0, 1, 0))
                        if not DETAIL:
                            for side in (1, -1):
                                b.prism(cell, (r0 + 0.7, side * 0.47, zc), (r1 - 0.7, side * 0.47, zc), span * 0.7,
                                        0.14, 0.04, up=(0, 1, 0), taper=1.0)
                            continue
                        # Cells in a grid, with one left out per face so it reads as a patched sail.
                        rows, cols = 2, max(2, round(span / 3.2))
                        cw, cz = (r1 - r0 - 1.0) / rows, span * 0.86 / cols
                        with b.fine():
                            for side in (1, -1):
                                skip = (rng.randrange(rows), rng.randrange(cols))
                                for a in range(rows):
                                    for c in range(cols):
                                        if (a, c) == skip:
                                            continue
                                        rc = r0 + 0.5 + (a + 0.5) * cw
                                        zz = zc - span * 0.43 + (c + 0.5) * cz
                                        b.prism(cell, (rc - cw / 2 + 0.12, side * 0.47, zz),
                                                (rc + cw / 2 - 0.12, side * 0.47, zz), cz - 0.24, 0.14, 0.04,
                                                up=(0, 1, 0), taper=1.0)
                                gap_r = r0 + 0.5 + (skip[0] + 0.5) * cw
                                gap_z = zc - span * 0.43 + (skip[1] + 0.5) * cz
                                b.flat_marks((gap_r, side * 0.47, gap_z), (1, 0, 0), (0, 0, 1), (0, side, 0),
                                             (cw * 0.7, cz * 0.6), rng, 1)
                            mid = (r0 + r1) / 2
                            b.prism("slate", (mid - 0.45, 0, 0), (mid + 0.45, 0, 0), 1.2, 1.15, 0.15, up=(0, 1, 0),
                                    taper=1.0)
                    r0, r1, span, zc = panels[-1][:4]
                    count = int(span / 1.7)
                    for j in range(count):
                        z = zc + (j - (count - 1) / 2) * 1.7
                        if NEW_LAMPS:
                            b.prism("svc", (r1, 0, z - 0.3), (r1, 0, z + 0.3), 0.6, 0.4, 0.08, up=radial, taper=1.0)
                            b.prism("slate", (r1 + 0.02, 0, z - 0.38), (r1 + 0.02, 0, z + 0.38), 0.74, 0.2, 0.06,
                                    up=radial, taper=1.0)
                        else:
                            b.prism("svc", (r1 + 0.05, 0, z - 0.3), (r1 + 0.05, 0, z + 0.3), 1.1, 0.5, 0.1,
                                    up=radial, taper=1.0)


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


# Faction part kits (#1592). A kit knows nothing about any station: it only reads the station's mounts.
def at(mt, shift=0.0, spin=0.0):
    """X along the mount's face, Y across it, Z out of it, at the mount centre slid `shift` along X."""
    n = mt["n"]
    a = Matrix.Rotation(radians(spin), 3, n) @ mt["a"]
    c = n.cross(a)
    o = mt["o"] + mt["a"] * shift
    return Matrix(((a.x, c.x, n.x, o.x), (a.y, c.y, n.y, o.y), (a.z, c.z, n.z, o.z), (0, 0, 0, 1)))


def spread(pool, count, rng, taken, mirror=False):
    """Up to `count` untaken mounts from `pool`, each as far as it can be from the ones already picked."""
    free = [m for m in pool if id(m) not in taken]
    picked = []
    while free and len(picked) < count:
        if not picked:
            m = rng.choice(free)
        else:
            m = max(free, key=lambda q: min((q["o"] - p["o"]).length for p in picked) * rng.uniform(0.85, 1.15))
        group = [m]
        if mirror:
            twin = min((q for q in free if q is not m), key=lambda q: (q["o"] + m["o"]).length, default=None)
            if twin is not None and (twin["o"] + m["o"]).length < 3.0:
                group.append(twin)
        for g in group:
            picked.append(g)
            free.remove(g)
            taken.add(id(g))
    return picked


def chains(mounts):
    """Neighbouring plates of one plated arc, face by face, as pairs a string or cable can span."""
    runs = {}
    for m in mounts:
        c = m["chain"]
        if c and isinstance(c[0], int):
            runs.setdefault((c[0], c[1]), []).append(m)
    pairs = []
    for row in runs.values():
        row.sort(key=lambda m: m["chain"][2])
        pairs += [(p, q) for p, q in zip(row, row[1:]) if q["chain"][2] == p["chain"][2] + 1]
    return pairs


def tall(mounts):
    return [m for m in mounts if m["tall"]]


def big(mounts, along=1.8, across=1.1):
    return [m for m in mounts if m["size"][0] >= along and m["size"][1] >= across]


def pole(b, length, lamp=None, w=0.3):
    with b.fine():
        b.prism("slate", (0, 0, 0), (0, 0, length), w, w, 0.06, taper=1.0)
        if lamp:
            b.prism(lamp, (0, 0, length), (0, 0, length + 0.45), 0.5, 0.5, 0.1, taper=1.0)


def string_between(b, m0, m1, height=1.4, count=7):
    """Two poles off neighbouring plates and a slack lamp string between their tips, bowed clear of the hull."""
    tips = []
    for m in (m0, m1):
        b.begin(m["part"])
        with b.push(at(m)):
            pole(b, height)
        tips.append(m["o"] + m["n"] * height)
    n = (m0["n"] + m1["n"]).normalized()
    turn = m0["n"].angle(m1["n"])
    d = (tips[1] - tips[0]).length
    dip = 0.0 if turn < 1e-3 else d / (2 * sin(turn / 2)) * (1 - cos(turn / 2))
    b.begin(m0["part"])
    p0, p1, sag = tips[0], tips[1], n * (dip + 0.5)
    pts = [p0.lerp(p1, t) + sag * (4 * t * (1 - t)) for t in (i / (count + 1) for i in range(count + 2))]
    with b.fine():
        for a, c in zip(pts, pts[1:]):
            b.prism("slate", a, c, 0.12, 0.12, 0.03, taper=1.0)
    for p in pts[1:-1]:
        b.prism("win", p - n * 0.22, p + n * 0.22, 0.42, 0.42, 0.08, taper=1.0)


def kit_shoal(b, mounts, rng):
    """Salvage-clan space: big scrap patches, cargo stacks, lamp strings on tall poles, lantern masts, a salvaged dish."""
    taken = set()
    accents = ("ochre", "brick", "blue")
    for mt in spread(big(mounts, 2.4, 1.4), 4, rng, taken):
        length = min(mt["size"][0], rng.uniform(3.0, 4.2))
        width = min(mt["size"][1], rng.uniform(1.8, 2.6))
        slide = rng.uniform(-0.3, 0.3) * max(0.0, mt["size"][0] - length)
        first = rng.choice(accents)
        second = rng.choice([a for a in accents if a != first])
        b.begin(mt["part"])
        with b.push(at(mt, slide, rng.uniform(-16, 16))):
            b.prism(first, (-length / 2, 0, 0.22), (length / 2, 0, 0.22), width, 0.32, 0.08, taper=1.0)
            x, y = length * 0.22, width * 0.18
            b.prism(second, (x - length * 0.22, y, 0.46), (x + length * 0.22, y, 0.46), width * 0.55, 0.2, 0.05,
                    taper=1.0)
            with b.fine():
                for px in (-length / 2 + 0.3, length / 2 - 0.3):
                    for py in (-width / 2 + 0.26, width / 2 - 0.26):
                        b.prism("slate", (px, py, 0.34), (px, py, 0.52), 0.2, 0.2, 0.04, taper=1.0)
    for mt in spread(big(mounts, 2.6, 1.2), 2, rng, taken):
        b.begin(mt["part"])
        with b.push(at(mt, 0.0, rng.uniform(-8, 8))):
            x, tallest = -1.5, 0.0
            for _ in range(rng.randint(2, 3)):
                s = rng.uniform(1.4, 1.9)
                b.prism(rng.choice(("ochre", "canvas", "blue", "brick")), (x + s / 2, 0, 0.0), (x + s / 2, 0, s),
                        s * rng.uniform(0.8, 1.0), s, 0.2, up=(1, 0, 0))
                tallest = max(tallest, s)
                x += s + 0.1
            with b.fine():
                b.prism("slate", (-1.65, 0, tallest * 0.6), (x + 0.1, 0, tallest * 0.6), 0.2, 0.2, 0.04, taper=1.0)
    pairs = [p for p in chains(tall(mounts)) if id(p[0]) not in taken and id(p[1]) not in taken]
    rng.shuffle(pairs)
    for m0, m1 in pairs[:3]:
        string_between(b, m0, m1, height=2.4)
        taken.update((id(m0), id(m1)))
    for mt in spread(tall(mounts), 2, rng, taken):
        high = rng.uniform(2.8, 3.4)
        b.begin(mt["part"])
        with b.push(at(mt)):
            pole(b, high, "win", 0.34)
            with b.fine():
                b.prism("slate", (-0.9, 0, high * 0.75), (0.9, 0, high * 0.75), 0.16, 0.16, 0.03, taper=1.0)
            for side in (-0.85, 0.85):
                b.prism("win", (side, 0, high * 0.75 - 0.55), (side, 0, high * 0.75 - 0.1), 0.42, 0.42, 0.08, taper=1.0)
    for mt in spread(big(tall(mounts), 2.0, 1.2), 1, rng, taken):
        b.begin(mt["part"])
        with b.push(at(mt) @ R("X", rng.uniform(-30, 30))):
            pole(b, 1.4, None, 0.4)
            b.lathe("canvas", [(0.3, 1.3), (1.9, 1.85), (2.05, 2.05)], sides=12)
            with b.fine():
                b.prism("slate", (0, 0, 1.3), (0, 0, 2.9), 0.18, 0.18, 0.04, taper=1.0)
            b.prism("win", (0, 0, 2.9), (0, 0, 3.2), 0.36, 0.36, 0.06, taper=1.0)


def kit_veil(b, mounts, rng):
    """Black-market tech space: faceted cladding with lit seams, tall sensor masts, cable runs, heat sinks."""
    taken = set()
    for mt in spread(big(mounts, 2.4, 1.4), 4, rng, taken):
        length = min(mt["size"][0], rng.uniform(3.0, 4.0))
        width = min(mt["size"][1], rng.uniform(1.6, 2.4))
        b.begin(mt["part"])
        with b.push(at(mt, rng.uniform(-0.3, 0.3) * max(0.0, mt["size"][0] - length))):
            b.prism("ochre", (-length / 2, 0, 0.26), (length / 2, 0, 0.26), width, 0.42, 0.2, taper=0.5,
                    tlen=length * 0.3)
            b.prism("win", (-length * 0.34, width * 0.32, 0.5), (length * 0.34, width * 0.32, 0.5), 0.2, 0.08,
                    0.02, taper=1.0)
            b.prism("win", (length * 0.1, -width * 0.36, 0.5), (length * 0.1, width * 0.32, 0.5), 0.16, 0.08,
                    0.02, taper=1.0)
    for mt in spread(tall(mounts), 4, rng, taken):
        high = rng.uniform(5.5, 8.0)
        b.begin(mt["part"])
        with b.push(at(mt)):
            b.prism("slate", (0, 0, 0), (0, 0, high), 0.56, 0.56, 0.12, taper=0.3, tlen=high)
            with b.fine():
                for h, span in ((high * 0.3, 2.0), (high * 0.55, 1.3)):
                    b.prism("slate", (-span / 2, 0, h), (span / 2, 0, h), 0.17, 0.17, 0.03, taper=1.0)
                    for s in (-1, 1):
                        b.prism("win", (s * span / 2, 0, h - 0.12), (s * span / 2, 0, h + 0.12), 0.22, 0.22, 0.04,
                                taper=1.0)
            b.prism("win", (0, 0, high - 0.1), (0, 0, high + 0.35), 0.4, 0.4, 0.08, taper=1.0)
    for mt in spread(big(mounts, 2.4, 1.0), 2, rng, taken):
        b.begin(mt["part"])
        with b.push(at(mt)):
            half = mt["size"][0] / 2
            with b.fine():
                for y in (-0.42, 0.0, 0.42):
                    b.prism("slate", (-half, y, 0.24), (half, y, 0.24), 0.3, 0.3, 0.06, taper=1.0)
                for x in (-half * 0.6, half * 0.6):
                    b.prism("slate", (x, -0.7, 0.24), (x, 0.7, 0.24), 0.26, 0.5, 0.05, taper=1.0)
    for mt in spread(big(mounts, 2.0, 1.2), 2, rng, taken):
        b.begin(mt["part"])
        with b.push(at(mt)):
            width = min(mt["size"][1], 2.0)
            b.prism("blue", (-1.3, 0, 0.1), (1.3, 0, 0.1), width, 0.2, 0.05, taper=1.0)
            for k in range(7):
                x = -1.14 + k * 0.38
                b.prism("blue", (x, 0, 0.12), (x, 0, 1.5), width * 0.9, 0.14, 0.02, up=(1, 0, 0), taper=1.0)


def kit_bastion(b, mounts, rng):
    """Warrior-clan space: heavy armour in matched pairs with a clan stripe, tall standards, painted chevrons."""
    taken = set()
    for mt in spread(big(mounts, 2.4, 1.4), 2, rng, taken, mirror=True):
        length = min(mt["size"][0], 3.6)
        width = min(mt["size"][1], 2.4)
        b.begin(mt["part"])
        with b.push(at(mt)):
            b.prism("ochre", (-length / 2, 0, 0.36), (length / 2, 0, 0.36), width, 0.7, 0.26, taper=0.85)
            b.prism("brick", (0, -width / 2 + 0.06, 0.74), (0, width / 2 - 0.06, 0.74), 0.6, 0.12, 0.02, taper=1.0)
    for mt in spread(tall(mounts), 4, rng, taken):
        b.begin(mt["part"])
        with b.push(at(mt)):
            b.prism("slate", (0, 0, 0), (0, 0, 6.0), 0.5, 0.5, 0.12, taper=1.0)
            b.prism("win", (0, 0, 6.0), (0, 0, 6.35), 0.44, 0.44, 0.1, taper=1.0)
            with b.fine():
                b.prism("slate", (0.0, 0, 5.85), (2.6, 0, 5.85), 0.16, 0.16, 0.03, taper=1.0)
            b.prism("ochre", (0.25, 0, 3.95), (2.45, 0, 3.95), 0.14, 3.6, 0.05, up=(0, 0, 1), taper=1.0)
            b.prism("brick", (0.25, 0, 4.4), (2.45, 0, 4.4), 0.18, 0.7, 0.02, up=(0, 0, 1), taper=1.0)
            b.prism("brick", (0.25, 0, 3.3), (2.45, 0, 3.3), 0.18, 0.26, 0.02, up=(0, 0, 1), taper=1.0)
    for mt in spread(big(mounts, 2.4, 1.6), 2, rng, taken):
        b.begin(mt["part"])
        with b.push(at(mt)):
            b.prism("ochre", (-1.3, 0, 0.14), (1.3, 0, 0.14), min(mt["size"][1], 1.9), 0.2, 0.06, taper=1.0)
            for s in (-1, 1):
                b.prism("brick", (s * 0.85, 0.6, 0.3), (0, -0.55, 0.3), 0.34, 0.1, 0.02, taper=1.0)


KITS = {"shoal": kit_shoal, "veil": kit_veil, "bastion": kit_bastion}

# Each faction recolours only the accent roles and the window light; bone, canvas and slate stay the station's.
ACCENTS = {
    "shoal": dict(ochre="#DC7A24", brick="#3B3433", blue="#7A8A76", win="#FF9548"),
    "veil": dict(ochre="#5B636E", brick="#2E333B", blue="#4D5868", win="#BA82FF"),
    "bastion": dict(ochre="#2E62C9", brick="#CFD8E6", blue="#1D3C88", win="#D7E6FF"),
}


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


# Window sizes. "large" is what gates 1 and 2 showed; "small" suits a hull seen 700 px across.
WINDOW_STYLES = {
    "large": {"pane": 1.0, "gap": 0.45, "across": 0.30, "rows": (0.0,), "pod": ((-0.42, 0.2), (0.42, 0.2))},
    "small": {"pane": 0.46, "gap": 0.24, "across": 0.13, "rows": (-0.17, 0.17),
              "pod": ((-0.62, 0.08), (-0.2, 0.08), (0.2, 0.08), (0.62, 0.08))},
}
WINDOWS = dict(WINDOW_STYLES["small"])

PASS_VIEWS = ("top", "tumble1")
PASS_RES = 912  # 38 u at 24 px per unit: the flight camera's closest zoom on a 720 px tall screen
PASS_GROUPS = {"A": ("bone", "canvas", "slate"), "B": ("ochre", "brick", "blue")}
CHANNELS = ((1, 0, 0, 1), (0, 1, 0, 1), (0, 0, 1, 1))


def flat_emit(name, colour, band=None):
    """A pass material: a pure channel colour, optionally only where the cel ramp is in one band."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs[0].default_value = colour
    nt.links.new(emit.outputs[0], out.inputs[0])
    if band:
        diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
        to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        cr = ramp.color_ramp
        cr.interpolation = "CONSTANT"
        cr.elements[0].position, cr.elements[0].color = 0.0, (0, 0, 0, 1)
        if band == "lit":
            cr.elements[1].position, cr.elements[1].color = SHADE_LIT, (1, 1, 1, 1)
        else:
            cr.elements[1].position, cr.elements[1].color = SHADE_MID, (1, 1, 1, 1)
            top = cr.elements.new(SHADE_LIT)
            top.color = (0, 0, 0, 1)
        nt.links.new(diff.outputs[0], to_rgb.inputs[0])
        nt.links.new(to_rgb.outputs[0], ramp.inputs[0])
        nt.links.new(ramp.outputs[0], emit.inputs[1])
    return m


BRUSH_TILE = 7.0  # world units one repeat of the ships' brush tile covers
HATCH_SCALE = 0.9  # wave scale: lines about 0.4 of a unit apart


def brush_emit(path):
    """A pass material: the ships' hand-painted brush tile, box-projected in object space."""
    m = flat_emit("pass.brush", (0.625, 0.625, 0.625, 1))
    if not path:
        return m
    nt = m.node_tree
    coords = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (1 / BRUSH_TILE,) * 3
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path)
    tex.image.colorspace_settings.name = "Non-Color"
    tex.projection, tex.projection_blend = "BOX", 0.25
    nt.links.new(coords.outputs["Object"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    nt.links.new(tex.outputs["Color"], nt.nodes["Emission"].inputs[0])
    return m


def hatch_emit():
    """A pass material: two sets of wobbly, broken hatch strokes, each kept to its own patches of hull.

    Red is the set drawn in half-tone and shadow. Green is the crossing set, drawn in shadow only.
    """
    m = flat_emit("pass.hatch", (0, 0, 0, 1))
    nt = m.node_tree
    coords = nt.nodes.new("ShaderNodeTexCoord")
    combine = nt.nodes.new("ShaderNodeCombineColor")

    def over(source, threshold):
        gate = nt.nodes.new("ShaderNodeMath")
        gate.operation = "GREATER_THAN"
        gate.inputs[1].default_value = threshold
        nt.links.new(source, gate.inputs[0])
        return gate.outputs[0]

    def both(a, b):
        mul = nt.nodes.new("ShaderNodeMath")
        mul.operation = "MULTIPLY"
        nt.links.new(a, mul.inputs[0])
        nt.links.new(b, mul.inputs[1])
        return mul.outputs[0]

    def noise(scale, shift):
        mapping = nt.nodes.new("ShaderNodeMapping")
        mapping.inputs["Location"].default_value = (shift, shift * 0.7, -shift)
        tex = nt.nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = scale
        tex.inputs["Detail"].default_value = 0.0
        nt.links.new(coords.outputs["Object"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        return tex.outputs[0]

    for channel, flip, patch, shift in ((0, (1, 1, 1), 0.5, 0.0), (1, (-1, 1, 0.6), 0.57, 13.0)):
        mapping = nt.nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = flip
        wave = nt.nodes.new("ShaderNodeTexWave")
        wave.wave_type, wave.bands_direction = "BANDS", "DIAGONAL"
        wave.inputs["Scale"].default_value = HATCH_SCALE
        wave.inputs["Distortion"].default_value = 1.6
        wave.inputs["Detail"].default_value = 1.0
        wave.inputs["Detail Scale"].default_value = 0.5
        nt.links.new(coords.outputs["Object"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], wave.inputs["Vector"])
        strokes = both(over(wave.outputs[0], 0.88), over(noise(3.0, shift + 5.0), 0.38))
        nt.links.new(both(strokes, over(noise(0.36, shift), patch)), combine.inputs[channel])
    nt.links.new(combine.outputs[0], nt.nodes["Emission"].inputs[0])
    return m


def render_passes(scene, root, tag, views, brush_path):
    """Coverage, shading-band, light, brush and hatch masks per view, so the look can change without Blender."""
    black = flat_emit("pass.black", (0, 0, 0, 1))
    ink0 = ink_material()
    ink0.node_tree.nodes["Emission"].inputs[0].default_value = (0, 0, 0, 1)
    hull = PASS_GROUPS["A"] + PASS_GROUPS["B"]
    passes = {}
    for group, keys in PASS_GROUPS.items():
        for kind in ("cov", "lit", "mid"):
            passes[kind + group] = {k: flat_emit(f"pass.{kind}.{k}", CHANNELS[i], None if kind == "cov" else kind)
                                    for i, k in enumerate(keys)}
    passes["emis"] = {k: flat_emit(f"pass.emis.{k}", CHANNELS[i]) for i, k in enumerate(("win", "svc", "core"))}
    brush, hatch = brush_emit(brush_path), hatch_emit()
    passes["brush"] = {k: brush for k in hull}
    passes["hatch"] = {k: hatch for k in hull}
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and "key" in o]
    scene.render.resolution_x = scene.render.resolution_y = PASS_RES
    for label, q in views:
        root.rotation_quaternion = q
        for name, table in passes.items():
            for ob in meshes:
                if ob["key"] == "ink":
                    ob.material_slots[0].material = ink0
                else:
                    ob.data.materials[0] = table.get(ob["key"], black)
            scene.render.filepath = f"{OUT}/{tag}-{label}-{name}.png"
            bpy.ops.render.render(write_still=True)
    scene.render.resolution_x = scene.render.resolution_y = RES


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
    scene.render.dither_intensity = 0.0

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
    shared["mark"] = flat_emit("mark", srgb("#0A0911"))
    random_views = tumbles(5)
    flags = ("blockout", "passes", "largewin", "plain", "oldlamps", "norender")
    specs = [s for s in ONLY if s not in flags and "=" not in s] or [n for n in STATIONS]
    if "largewin" in ONLY:
        WINDOWS.update(WINDOW_STYLES["large"])
    global DETAIL, NEW_LAMPS
    DETAIL = "plain" not in ONLY
    passes_only = "passes" in ONLY
    blockout = "blockout" in ONLY or passes_only
    turn = {axis: Quaternion(vec, math.pi / 2) for axis, vec in (("X", (1, 0, 0)), ("Y", (0, 1, 0)))}
    for spec in specs:
        name, _, rest = spec.partition("@")
        skin, _, dressed = rest.partition("+")
        skin = skin or "neutral"
        tag = name if skin == "neutral" else f"{name}-{skin}"
        tag += f"-{dressed}" if dressed else ""
        tag += "-plain" if "plain" in ONLY else ""
        build, svc, edge_axis = STATIONS[name]
        colours = dict(SKINS["neutral"], **ACCENTS[skin]) if dressed == "kit" else SKINS[skin]
        mats = dict(shared)
        mats.update({k: cel(f"{k}.{tag}", colours[k]) for k in PALETTE})
        mats["win"] = glow(f"win.{tag}", colours["win"])
        lit_colours = {"win": colours["win"], "svc": svc, "core": GLOW["core"]}

        def light(lit):
            for key, colour in lit_colours.items():
                mats[key].node_tree.nodes["Emission"].inputs[0].default_value = srgb(colour if lit else UNLIT)

        def assemble(scale, with_dress=True):
            builder = Builder(scale)
            build(builder)
            if dressed == "kit" and with_dress:
                KITS[skin](builder, list(builder.mounts), random.Random(f"{name}/{skin}"))
            elif dressed and with_dress:
                DRESS[(name, skin)](builder)
            return builder

        scale = 1.0
        if blockout:
            # Size every station to the footprint of the one in the game today, measured with the
            # accepted lamps and no dress, so neither a moved lamp nor a faction's parts rescale it.
            NEW_LAMPS = False
            probe = assemble(1.0, with_dress=False)
            scale = BLOCKOUT_SIZE / max(probe.high - probe.low)
        NEW_LAMPS = "oldlamps" not in ONLY
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
        if passes_only:
            ship.hide_render = True
            brush_path = next((s.split("=", 1)[1] for s in ONLY if s.startswith("brush=")), None)
            render_passes(scene, root, tag, [v for v in views if v[0] in PASS_VIEWS], brush_path)
            views = []
        for label, q in ([] if "norender" in ONLY else views):
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
        if not passes_only:
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
