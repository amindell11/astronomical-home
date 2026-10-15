"""Paint a ship: Crimson's rules as Shadow, Light and Ink masks over palette regions.

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_paint.py -- --ship SHIP_JSON --out DIR
[--palette NAME] [--unity DIR]
Reads paint.json and palettes.json beside the source and needs a matching geometry lock. Regions
are the material slots of the visual role parts; every palette must name exactly that set. The
masks are baked (Cycles, emission) on temporary evaluated copies in symmetry-origin space with X
folded: a jittered diagonal sweep over each plate into a dark and a light third, a side-wall ramp,
an inset ink line with a bright rim inside it, fully inked thin walls, a quiet world-space brush
tile folded into Shadow and Light, and the accents paint.json lists. Written beside the source:
layers/Shadow.png, Light.png, Ink.png (and Emission.png when an accent emits) plus
layers/baseline/, the tool's own last output; on a re-run the owner's edits (mask minus
baseline) ride onto the new generation. Refuses masks that have no baseline and a baseline whose
UVs or texture size no longer match. --out gets the report, preview/Composite.png (review only;
never imported) and unity/<Name>/, a copy-ready bundle of mask textures and one _PaintLayers
material per palette per region; --unity reuses the GUIDs of files already in that visuals folder.
Prints SHIP_MASKS=<layers dir>.
"""

import json
from pathlib import Path
import struct
import sys
import zlib

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract
import ship_source
import ship_unity

BRUSH_MID = 0.625
SEED_MARGIN, SEED_SIZE = 12, 4096


class Graph:
    def __init__(self, material):
        material.use_nodes = True
        self.tree = material.node_tree
        self.tree.nodes.clear()

    def node(self, kind, **settings):
        node = self.tree.nodes.new(kind)
        for key, value in settings.items():
            setattr(node, key, value)
        return node

    def feed(self, socket, value):
        if isinstance(value, bpy.types.NodeSocket):
            self.tree.links.new(value, socket)
        elif isinstance(value, (tuple, list)):
            socket.default_value = (*value, 1.0) if len(socket.default_value) == 4 and len(value) == 3 else value
        else:
            socket.default_value = value

    def math(self, operation, a, b=None, c=None):
        node = self.node("ShaderNodeMath", operation=operation)
        for index, value in enumerate((a, b, c)):
            if value is not None:
                self.feed(node.inputs[index], value)
        return node.outputs[0]

    def vector(self, operation, a, b=None, scale=None):
        node = self.node("ShaderNodeVectorMath", operation=operation)
        self.feed(node.inputs[0], a)
        if b is not None:
            self.feed(node.inputs[1], b)
        if scale is not None:
            self.feed(node.inputs["Scale"], scale)
        return node.outputs["Value" if operation in ("DOT_PRODUCT", "LENGTH", "DISTANCE") else "Vector"]

    def mix(self, factor, a, b):
        node = self.node("ShaderNodeMix", data_type="FLOAT")
        self.feed(node.inputs[0], factor)
        self.feed(node.inputs[2], a)
        self.feed(node.inputs[3], b)
        return node.outputs[0]

    def mix_colour(self, factor, a, b):
        node = self.node("ShaderNodeMix", data_type="RGBA")
        self.feed(node.inputs[0], factor)
        self.feed(node.inputs[6], a)
        self.feed(node.inputs[7], b)
        return node.outputs[2]

    def separate(self, vector):
        node = self.node("ShaderNodeSeparateXYZ")
        self.feed(node.inputs[0], vector)
        return node.outputs["X"], node.outputs["Y"], node.outputs["Z"]

    def combine(self, x, y, z):
        node = self.node("ShaderNodeCombineXYZ")
        for socket, value in zip(node.inputs, (x, y, z)):
            self.feed(socket, value)
        return node.outputs[0]

    def colour(self, r, g, b):
        node = self.node("ShaderNodeCombineColor", mode="RGB")
        for socket, value in zip(node.inputs, (r, g, b)):
            self.feed(socket, value)
        return node.outputs[0]

    def ramp(self, value, stops):
        node = self.node("ShaderNodeValToRGB")
        node.color_ramp.interpolation = "EASE"
        elements = node.color_ramp.elements
        while len(elements) > 1:
            elements.remove(elements[-1])
        elements[0].position, elements[0].color = stops[0][0], (stops[0][1],) * 3 + (1.0,)
        for position, level in stops[1:]:
            elements.new(position).color = (level,) * 3 + (1.0,)
        self.feed(node.inputs[0], value)
        return node.outputs["Color"]

    def between(self, value, low, high):
        inside = None
        for bound, operation in ((low, "GREATER_THAN"), (high, "LESS_THAN")):
            if bound is None:
                continue
            test = self.math(operation, value, bound)
            inside = test if inside is None else self.math("MULTIPLY", inside, test)
        return inside

    def brush(self, image, folded, scale, blend):
        node = self.node("ShaderNodeTexImage", projection="BOX", projection_blend=blend, extension="REPEAT")
        node.image = image
        self.feed(node.inputs["Vector"], self.vector("SCALE", folded, scale=scale))
        return node.outputs["Color"]


def write_png(path, array):
    """8-bit PNG, grayscale (rows, columns) or RGB (rows, columns, 3); rows are bottom-up as Blender's."""
    rows = np.ascontiguousarray(array[::-1], np.uint8)
    height, width = rows.shape[:2]
    colour_type = 0 if rows.ndim == 2 else 2
    raw = b"".join(b"\x00" + row.tobytes() for row in rows)

    def chunk(tag, body):
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", width, height, 8, colour_type, 0, 0, 0)
    Path(path).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw, 6))
                           + chunk(b"IEND", b""))


def read_gray(path):
    return np.rint(ship_source.load_pixels(path)[..., 0] * 255).astype(np.uint8)


def _components(faces):
    """Connected face groups over shared edges; faces is a list of vertex index tuples."""
    parent = list(range(len(faces)))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    owner = {}
    for face_index, face in enumerate(faces):
        for a, b in zip(face, face[1:] + face[:1]):
            key = (a, b) if a < b else (b, a)
            if key in owner:
                parent[find(owner[key])] = find(face_index)
            else:
                owner[key] = face_index
    groups = {}
    for face_index in range(len(faces)):
        groups.setdefault(find(face_index), []).append(face_index)
    return list(groups.values())


class Part:
    """One visual part evaluated into symmetry-origin space: its mesh, outward cap and thickness."""

    def __init__(self, evaluation, name, paint):
        self.name = name
        self.mesh = evaluation.mesh(name)
        self.mesh.transform(evaluation.to_space(name))
        self.mesh.update()
        mesh = self.mesh
        self.materials = evaluation.materials(name)
        self.co = ship_source.read_array(mesh.vertices, "co", np.float64, 3).reshape(-1, 3)
        corners = ship_source.read_array(mesh.loops, "vertex_index", np.int32)
        starts = ship_source.read_array(mesh.polygons, "loop_start", np.int32)
        totals = ship_source.read_array(mesh.polygons, "loop_total", np.int32)
        self.faces = [tuple(int(v) for v in corners[s:s + t]) for s, t in zip(starts, totals)]
        self.normals = ship_source.read_array(mesh.polygons, "normal", np.float64, 3).reshape(-1, 3)
        self.areas = ship_source.read_array(mesh.polygons, "area", np.float64)
        self.material_index = ship_source.read_array(mesh.polygons, "material_index", np.int32)
        self.thickness = self._thickness()
        self.cap = self._cap(paint["plates"]["cap_facing"])

    def _thickness(self):
        thickest = 0.0
        for group in _components(self.faces):
            vertices = sorted({v for index in group for v in self.faces[index]})
            extent = np.ptp(self.co[vertices], axis=0)
            thickest = max(thickest, float(extent.min()))
        return thickest

    def _cap(self, cap_facing):
        """The outward cap: the up-or-down-facing face group farthest from the mid-plane.

        Returns None without one, else {"bounds": folded (min, max) XY, "edges": folded boundary segments,
        "faces": face count, "area": total area}.
        """
        candidates = [i for i in range(len(self.faces)) if abs(self.normals[i][2]) >= cap_facing]
        if not candidates:
            return None
        faces = [self.faces[i] for i in candidates]
        best, best_key = None, None
        for group in _components(faces):
            indices = [candidates[g] for g in group]
            area = self.areas[indices].sum()
            if area <= 0:
                continue
            centres = np.array([self.co[list(self.faces[i])].mean(axis=0) for i in indices])
            height = float((np.abs(centres[:, 2]) * self.areas[indices]).sum() / area)
            key = (round(height, 6), round(float(area), 9), float(centres[:, 0].mean() >= 0))
            if best_key is None or key > best_key:
                best, best_key = indices, key
        if best is None:
            return None
        seen = {}
        for index in best:
            face = self.faces[index]
            for a, b in zip(face, face[1:] + face[:1]):
                key = (a, b) if a < b else (b, a)
                seen[key] = seen.get(key, 0) + 1
        fold = lambda v: (abs(float(self.co[v][0])), float(self.co[v][1]))
        edges = [(fold(a), fold(b)) for (a, b), count in seen.items() if count == 1]
        vertices = sorted({v for index in best for v in self.faces[index]})
        folded = self.co[vertices][:, :2].copy()
        folded[:, 0] = np.abs(folded[:, 0])
        return {"bounds": (folded.min(axis=0), folded.max(axis=0)), "edges": edges,
                "faces": len(best), "area": float(best_key[1])}


def build_material(part, region, flags, paint, brush_image, base, emission_colour, accents):
    """Node graph for one part's faces in one region; returns (material, (graph, emitter, {masks, composite, emission}))."""
    material = bpy.data.materials.new(f"paint:{part.name}:{region}")
    g = Graph(material)
    geometry = g.node("ShaderNodeNewGeometry")
    x, y, z = g.separate(geometry.outputs["Position"])
    ax = g.math("ABSOLUTE", x)
    folded = g.combine(ax, y, z)
    brush = g.brush(brush_image, folded, paint["brush"]["scale"], paint["brush"]["blend"])
    factor = g.math("MULTIPLY_ADD", g.math("SUBTRACT", brush, BRUSH_MID), paint["brush"]["contrast"], 1.0)
    brush_dark = g.math("MAXIMUM", g.math("SUBTRACT", 1.0, factor), 0.0)
    brush_light = g.math("MAXIMUM", g.math("SUBTRACT", factor, 1.0), 0.0)
    _, _, nz = g.separate(geometry.outputs["Normal"])
    facing = g.math("ABSOLUTE", nz)
    shadow_ramp = g.math("SUBTRACT", 1.0, g.ramp(facing, paint["ramp"]))
    shadow_sweep, light_sweep = 0.0, 0.0
    if flags["sweep"] and part.cap:
        sweep = paint["sweep"]
        (minx, miny), (maxx, maxy) = part.cap["bounds"]
        sx = g.math("DIVIDE", g.math("SUBTRACT", ax, float(minx)), max(float(maxx - minx), 1e-3))
        sy = g.math("DIVIDE", g.math("SUBTRACT", y, float(miny)), max(float(maxy - miny), 1e-3))
        s = g.math("MULTIPLY_ADD", sx, sweep["weight_x"], g.math("MULTIPLY", sy, 1.0 - sweep["weight_x"]))
        s = g.math("MULTIPLY_ADD", g.math("SUBTRACT", brush, 0.65), sweep["jitter"], s)
        shadow_sweep = g.math("MULTIPLY", g.math("LESS_THAN", s, sweep["dark_below"]), 1.0 - sweep["dark"])
        light_sweep = g.math("MULTIPLY", g.math("GREATER_THAN", s, sweep["light_above"]), sweep["light"])
    kept = g.math("MULTIPLY", g.math("SUBTRACT", 1.0, shadow_ramp), g.math("SUBTRACT", 1.0, brush_dark))
    kept = g.math("MULTIPLY", kept, g.math("SUBTRACT", 1.0, shadow_sweep))
    shadow = g.math("SUBTRACT", 1.0, kept)
    light = g.math("MAXIMUM", light_sweep, brush_light)
    ink = 0.0
    if flags["plate"] and part.cap and part.cap["edges"]:
        flat = g.combine(ax, y, 0.0)
        distance = None
        for (ax0, ay0), (bx0, by0) in part.cap["edges"]:
            length = float(np.hypot(bx0 - ax0, by0 - ay0))
            if length < 1e-6:
                continue
            unit = ((bx0 - ax0) / length, (by0 - ay0) / length, 0.0)
            offset = g.vector("SUBTRACT", flat, (ax0, ay0, 0.0))
            along = g.math("MINIMUM", g.math("MAXIMUM", g.vector("DOT_PRODUCT", offset, unit), 0.0), length)
            nearest = g.vector("SCALE", unit, scale=along)
            gap = g.vector("LENGTH", g.vector("SUBTRACT", offset, nearest))
            distance = gap if distance is None else g.math("MINIMUM", distance, gap)
        if distance is not None:
            rim = g.math("LESS_THAN", distance, paint["ink"]["rim"])
            shadow = g.mix(rim, shadow, 0.0)
            light = g.mix(rim, light, paint["ink"]["rim_light"])
            ink = g.math("LESS_THAN", distance, paint["ink"]["inset"])
    if flags["ink_walls"] and part.thickness < paint["plates"]["thin_wall"]:
        ink = g.math("MAXIMUM", ink, g.math("LESS_THAN", facing, paint["ink"]["wall_facing"]))
    emission = 0.0
    for accent in accents:
        inside = None
        for axis, socket in (("x", ax), ("y", y), ("z", z)):
            if axis in accent["where"]:
                test = g.between(socket, *accent["where"][axis])
                inside = test if inside is None else g.math("MULTIPLY", inside, test)
        if accent["target"] == "light":
            light = g.mix(inside, light, accent["value"])
        elif accent["target"] == "ink":
            ink = g.mix(inside, ink, accent["value"])
        else:
            emission = g.mix(inside, emission, accent["value"])
    outputs = {"masks": g.colour(shadow, light, ink), "base": g.combine(*base),
               "emission_colour": g.combine(*(emission_colour or [0.0, 0.0, 0.0])),
               "emission": g.colour(emission, 0.0, 0.0)}
    emitter = g.node("ShaderNodeEmission")
    out = g.node("ShaderNodeOutputMaterial")
    g.tree.links.new(emitter.outputs[0], out.inputs["Surface"])
    target = g.node("ShaderNodeTexImage", name="BAKE TARGET")
    g.tree.nodes.active = target
    return material, (g, emitter, outputs)


def bake(scene, surface, graphs, image, which, margin, quantize=True):
    for material, (graph, emitter, outputs) in graphs.values():
        for link in list(emitter.inputs["Color"].links):
            graph.tree.links.remove(link)
        graph.tree.links.new(outputs[which], emitter.inputs["Color"])
        graph.tree.nodes["BAKE TARGET"].image = image
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.cycles.seed = 0
    scene.cycles.device = "CPU"
    for obj in scene.objects:
        obj.select_set(obj is surface)
    bpy.context.view_layer.objects.active = surface
    bpy.ops.object.bake(type="EMIT", use_clear=True, margin=margin)
    pixels = np.empty(len(image.pixels), np.float32)
    image.pixels.foreach_get(pixels)
    rgb = pixels.reshape(image.size[1], image.size[0], 4)[..., :3]
    return np.rint(rgb * 255).astype(np.uint8) if quantize else rgb


def main(argv):
    parser = contract.tool_parser("Bake a ship's Shadow, Light and Ink masks and its palette materials.")
    parser.add_argument("--palette", help="Palette for the composite preview (default: palettes.json default)")
    parser.add_argument("--unity", help="Existing visuals folder whose .meta GUIDs the bundle reuses")
    args = parser.parse_args(argv)
    ship, source = contract.load_ship(args.ship)
    folder = source.parent
    paint_path, palettes_path = folder / "paint.json", folder / "palettes.json"
    paint = contract.load_settings(paint_path, contract.parse_paint)
    palettes = contract.load_settings(palettes_path, contract.parse_palettes)
    brush_path = (folder / paint["brush"]["image"]).resolve()
    if args.unity and not Path(args.unity).is_dir():
        raise contract.UsageError(f"--unity: not a folder: {args.unity}")
    layers = folder / contract.LAYERS_DIR
    trailers = [("SHIP_MASKS", str(layers))]
    report = contract.Report("ship_paint", args.out, ship_source.blender_version(),
                             [source, paint_path, palettes_path, brush_path])
    out = report.path.parent
    for path in (source, paint_path, palettes_path, brush_path):
        if not path.is_file():
            raise report.refuse(f"Not found: {path}", trailers)
    palette = args.palette or palettes["default"]
    if palette not in palettes["palettes"]:
        raise contract.UsageError(f"--palette: not in palettes.json: {palette}")
    scene = ship_source.open_source(source)
    roles = ship_source.Roles(scene)
    if roles.errors:
        raise report.refuse("; ".join(error for _, error in roles.errors), trailers)
    names = sorted(name for role in contract.VISUAL_ROLES for name in roles.members.get(role, ())
                   if scene.objects[name].type == "MESH")
    if not names:
        raise report.refuse("No visual role parts", trailers)
    fingerprint = ship_source.fingerprint(scene)
    lock_path = folder / "lock.json"
    locked = json.loads(lock_path.read_text(encoding="utf-8")) if lock_path.is_file() else {}
    if locked.get("recipe") != contract.FINGERPRINT_RECIPE or locked.get("geometry") != fingerprint["components"]["geometry"]:
        raise report.refuse("ship_paint requires a matching geometry lock", trailers)
    evaluation = ship_source.Evaluation(scene)
    if not evaluation.has_origin:
        raise report.refuse(f"No {contract.SYMMETRY_ORIGIN} empty", trailers)
    parts = {name: Part(evaluation, name, paint) for name in names}
    regions = set(palettes["palettes"][palette])
    for name, part in parts.items():
        if part.mesh.uv_layers.get(contract.PAINT_UV) is None:
            raise report.refuse(f"{name} has no {contract.PAINT_UV} layer", trailers)
        used = np.unique(part.material_index)
        if len(used) and (used.max() >= len(part.materials) or any(part.materials[i] is None for i in used)):
            raise report.refuse(f"{name} has faces without a material", trailers)
    slots = {m.name for part in parts.values() for m in part.materials if m is not None}
    if slots != regions:
        raise report.refuse(f"Material slots {sorted(slots)} are not the palette regions {sorted(regions)}", trailers)
    if set(paint["regions"]) != regions:
        raise report.refuse(f"paint.json regions {sorted(paint['regions'])} are not the palette regions {sorted(regions)}", trailers)
    accents = {}
    for accent in paint["accents"]:
        if accent["part"] not in parts:
            raise report.refuse(f"accents: {accent['part']} is not a visual part", trailers)
        accents.setdefault(accent["part"], []).append(accent)
    emission_colours = {}
    for name, part_accents in accents.items():
        for accent in part_accents:
            if accent["target"] != "emission":
                continue
            for material in parts[name].materials:
                previous = emission_colours.setdefault(material.name, accent["color"])
                if previous != accent["color"]:
                    raise report.refuse(f"accents: region {material.name} has two emission colours", trailers)
    masks = list(contract.MASKS) + ([contract.EMISSION] if emission_colours else [])
    baseline = layers / contract.BASELINE_DIR
    baseline_record = baseline / "baseline.json"
    expected = {"schema_version": contract.SCHEMA_VERSION, "recipe": contract.FINGERPRINT_RECIPE,
                "texture_size": ship["texture_size"], "masks": masks,
                "geometry": fingerprint["components"]["geometry"], "uv": fingerprint["components"]["uv"]}
    edits = {}
    reused = baseline_record.is_file()
    try:
        present = any((layers / f"{mask}.png").exists() for mask in (*contract.MASKS, contract.EMISSION))
        if (present or (baseline.is_dir() and any(baseline.iterdir()))) and not reused:
            raise ValueError("Masks exist without a baseline")
        if reused:
            record = json.loads(baseline_record.read_text(encoding="utf-8"))
            if not isinstance(record, dict) or any(record.get(key) != value for key, value in expected.items()):
                raise ValueError("Baseline is incompatible with geometry, UVs, settings version or mask set")
            size = ship["texture_size"]
            for mask in masks:
                previous = read_gray(baseline / f"{mask}.png")
                current = read_gray(layers / f"{mask}.png")
                if previous.shape != (size, size) or current.shape != (size, size):
                    raise ValueError(f"{mask}: baseline or mask size differs from texture_size")
                edits[mask] = current.astype(np.int16) - previous.astype(np.int16)
    except (ValueError, OSError, RuntimeError) as error:
        raise report.refuse(f"Invalid mask baseline: {error}", trailers)

    brush_image = bpy.data.images.load(str(brush_path), check_existing=False)
    brush_image.colorspace_settings.name = "Non-Color"
    size = ship["texture_size"]
    colours = palettes["palettes"][palette]
    graphs, objects = {}, []
    for name, part in parts.items():
        mesh = part.mesh
        slot_materials = []
        for material in part.materials:
            region = material.name
            key = (name, region)
            if key not in graphs:
                graphs[key] = build_material(part, region, paint["regions"][region], paint, brush_image, colours[region][:3],
                                             emission_colours.get(region), accents.get(name, []))
            slot_materials.append(graphs[key][0])
        for index, material in enumerate(slot_materials):
            mesh.materials[index] = material
        layer = mesh.uv_layers[contract.PAINT_UV]
        mesh.uv_layers.active = layer
        layer.active_render = True
        obj = bpy.data.objects.new(f"paint surface {name}", mesh)
        evaluation.scene.collection.objects.link(obj)
        objects.append(obj)
    for obj in evaluation.scene.objects:
        obj.select_set(obj in objects)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    surface = bpy.context.view_layer.objects.active
    layer = surface.data.uv_layers[contract.PAINT_UV]
    surface.data.uv_layers.active = layer
    layer.active_render = True
    margin = max(2, round(SEED_MARGIN * size / SEED_SIZE))
    mask_image = bpy.data.images.new("ship paint masks", size, size, alpha=False)
    mask_image.colorspace_settings.name = "Non-Color"
    composite_image = bpy.data.images.new("ship paint composite", size, size, alpha=False)
    composite_image.colorspace_settings.name = "sRGB"
    baked = bake(evaluation.scene, surface, graphs, mask_image, "masks", margin)
    generated = {mask: baked[..., index] for index, mask in enumerate(contract.MASKS)}
    if emission_colours:
        generated[contract.EMISSION] = bake(evaluation.scene, surface, graphs, mask_image, "emission", margin)[..., 0]
    base = bake(evaluation.scene, surface, graphs, composite_image, "base", margin, quantize=False)
    emission_base = (bake(evaluation.scene, surface, graphs, composite_image, "emission_colour", margin, quantize=False)
                     if emission_colours else None)

    layers.mkdir(exist_ok=True)
    baseline.mkdir(exist_ok=True)
    edited = {}
    final = {}
    written = {}
    for mask in masks:
        new = generated[mask]
        edit = edits.get(mask)
        edited[mask] = int(np.count_nonzero(edit)) if edit is not None else 0
        if edit is not None:
            new = np.clip(new.astype(np.int16) + edit, 0, 255).astype(np.uint8)
        final[mask] = new
        write_png(layers / f"{mask}.png", new)
        write_png(baseline / f"{mask}.png", generated[mask])
        written[mask] = str(layers / f"{mask}.png")
    baseline_record.write_text(json.dumps({**expected, "blender_version": ship_source.blender_version()}, indent=2) + "\n",
                               encoding="utf-8")
    material = paint["material"]
    weights = {mask: final[mask][..., None].astype(np.float32) / 255 for mask in masks}
    composite = base * (1 - weights["Shadow"] * material["shadow_strength"])
    for mask, colour in (("Light", base + (1 - base) * material["light_lift"]),
                         ("Ink", np.array(material["ink_color"]))):
        weight = np.minimum(weights[mask] * material[f"{mask.lower()}_strength"], 1)
        composite = composite * (1 - weight) + colour * weight
    if emission_base is not None:
        weight = weights[contract.EMISSION] * material["emission_blend"]
        composite = composite * (1 - weight) + emission_base * weight
    composite = np.clip(composite, 0, 1)
    composite = np.where(composite <= 0.0031308, composite * 12.92, 1.055 * composite ** (1 / 2.4) - 0.055)
    composite = np.rint(composite * 255).astype(np.uint8)
    preview = out / "preview"
    preview.mkdir(exist_ok=True)
    write_png(preview / "Composite.png", composite)
    bundle_root, bundle_files = ship_unity.write_bundle(out, ship, written, palettes, paint, emission_colours, args.unity)
    part_records = {name: {"regions": sorted({m.name for m in part.materials}), "thickness": part.thickness,
                           "thin": part.thickness < paint["plates"]["thin_wall"],
                           "cap": None if part.cap is None else {"faces": part.cap["faces"], "area": part.cap["area"],
                                                                 "edges": len(part.cap["edges"])}}
                    for name, part in parts.items()}
    return report.finish(True, trailers, palette=palette, regions=sorted(regions), masks=written,
                         baseline="reused" if reused else "created", edited_texels=edited, parts=part_records,
                         accents=len(paint["accents"]), preview=str(preview / "Composite.png"),
                         unity={"root": str(bundle_root), "files": bundle_files})


if __name__ == "__main__":
    contract.run(main)
