"""Ship tool contract shared by every ship tool; pure Python so it is testable without Blender.

Exit codes: 0 pass, 3 findings or refusal, 1 error (bad invocation, invalid ship.json, crash).
stdout carries only KEY=value trailers, written last: SHIP_VERDICT=pass|fail and
SHIP_REPORT=<absolute report path> on exits 0 and 3, plus SHIP_FBX= (ship_export),
SHIP_LOCK= (ship_lock) and SHIP_MASKS= (ship_paint). Everything else, Blender's own output
included, goes to stderr.
"""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import sys
import traceback

SCHEMA_VERSION = 1
FINGERPRINT_RECIPE = "ship-fp-1"
PAINT_UV = "PaintUV"
SYMMETRY_ORIGIN = "SymmetryOrigin"
IGNORE = "ignore"
CONTOUR = "Contour"
EXIT_PASS, EXIT_ERROR, EXIT_FINDINGS = 0, 1, 3

VISUAL_ROLES = {"hull": "Hull", "canopy": "Canopy", "cores": "Cores", "ink": "Ink"}
STATIC_ROLES = {**VISUAL_ROLES, "collider": "Collider", "sockets": "Sockets"}
RESERVED_ROLES = ("move", "debris")
RULES = ("roles", "origin", "mirror", "modifier_order", "modifier_type", "scale", "ngon", "normals",
         "name", "visual_role", "collider", "missing_file", "unit_scale", "uv_layer", "uv_density")
INVALIDATED_BY_GEOMETRY = ("uv", "masks", "breakup")
SHIP_FIELDS = ("schema_version", "name", "source", "texture_size", "uv_density_max_ratio",
               "contour_roles", "exemptions")
EXEMPTION_FIELDS = ("rule", "parts", "reason")
_RESERVED_ID = re.compile(r"[A-Za-z0-9_]+(\.[LR])?")

MASKS = ("Shadow", "Light", "Ink")
EMISSION = "Emission"
LAYERS_DIR = "layers"
BASELINE_DIR = "baseline"
PALETTE_COLOR_SPACE = "linear-sRGB"
PALETTES_FIELDS = ("color_space", "default", "palettes")
PAINT_FIELDS = ("schema_version", "brush", "ramp", "sweep", "ink", "plates", "regions", "accents", "material")
BRUSH_FIELDS = ("image", "scale", "contrast", "blend")
SWEEP_FIELDS = ("weight_x", "dark_below", "light_above", "jitter", "dark", "light")
INK_FIELDS = ("inset", "rim", "rim_light", "wall_facing")
PLATES_FIELDS = ("cap_facing", "thin_wall")
REGION_FLAGS = ("plate", "sweep", "ink_walls")
ACCENT_FIELDS = ("part", "target", "where", "value")
ACCENT_TARGETS = ("light", "ink", "emission")
MATERIAL_FIELDS = ("shadow_strength", "light_strength", "ink_strength", "light_lift", "ink_color",
                   "emission_blend", "lighting")
LIGHTING_FLOATS = ("_ShadowThreshold", "_ShadowSoftness", "_CastShadowStrength", "_AmbientStrength",
                   "_PaletteLighting", "_SpecularStrength", "_PaintSaturation")
LIGHTING_COLORS = ("_ShadowColor",)


def parse_role(collection_name):
    """Return None for a non-role collection, else (kind, id); id is None for static roles."""
    if not collection_name.startswith("role."):
        return None
    rest = collection_name[len("role."):]
    if rest in STATIC_ROLES:
        return rest, None
    kind, _, ident = rest.partition(".")
    if kind in RESERVED_ROLES and _RESERVED_ID.fullmatch(ident):
        return kind, ident
    raise ValueError(f"Unknown role collection {collection_name!r}")


def _fields(value, expected, path):
    if not isinstance(value, dict) or set(value) != set(expected):
        raise ValueError(f"{path}: expected exactly the fields {', '.join(expected)}")


def _text(value, path):
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{path}: expected a non-empty string")


def parse_ship(value):
    _fields(value, SHIP_FIELDS, "ship.json")
    if type(value["schema_version"]) is not int or value["schema_version"] != SCHEMA_VERSION:
        raise ValueError(f"ship.json: unsupported schema_version; expected {SCHEMA_VERSION}")
    if not isinstance(value["name"], str) or not re.fullmatch(r"[A-Z][A-Za-z0-9]*", value["name"]):
        raise ValueError("name: expected a capitalized alphanumeric ship name")
    _text(value["source"], "source")
    if not value["source"].endswith(".blend"):
        raise ValueError("source: expected a .blend path")
    size = value["texture_size"]
    if type(size) is not int or size < 1 or size & (size - 1):
        raise ValueError("texture_size: expected a power of two")
    ratio = value["uv_density_max_ratio"]
    if isinstance(ratio, bool) or not isinstance(ratio, (int, float)) or not math.isfinite(ratio) or ratio <= 1:
        raise ValueError("uv_density_max_ratio: expected a finite number greater than 1")
    roles = value["contour_roles"]
    if not isinstance(roles, list) or len(set(roles)) != len(roles) or not set(roles) <= set(VISUAL_ROLES):
        raise ValueError(f"contour_roles: expected distinct names from {', '.join(VISUAL_ROLES)}")
    if not isinstance(value["exemptions"], list):
        raise ValueError("exemptions: expected a list")
    for index, exemption in enumerate(value["exemptions"]):
        path = f"exemptions[{index}]"
        _fields(exemption, EXEMPTION_FIELDS, path)
        if exemption["rule"] not in RULES:
            raise ValueError(f"{path}.rule: expected one of {', '.join(RULES)}")
        parts = exemption["parts"]
        if not isinstance(parts, list) or not parts or len(set(map(str, parts))) != len(parts):
            raise ValueError(f"{path}.parts: expected a non-empty list of distinct names")
        for part in parts:
            _text(part, f"{path}.parts")
        _text(exemption["reason"], f"{path}.reason")
    return json.loads(json.dumps(value))


def load_ship(path):
    """Return (ship, absolute source path); source resolves against the ship.json folder."""
    path = Path(path).resolve()
    with open(path, encoding="utf-8-sig") as handle:
        ship = parse_ship(json.load(handle))
    return ship, (path.parent / ship["source"]).resolve()


def _number(value, path, low=None, high=None, exclusive_low=False):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"{path}: expected a finite number")
    if low is not None and (value <= low if exclusive_low else value < low):
        raise ValueError(f"{path}: expected a number {'above' if exclusive_low else 'at least'} {low}")
    if high is not None and value > high:
        raise ValueError(f"{path}: expected a number at most {high}")
    return float(value)


def _color(value, path, count):
    if not isinstance(value, list) or len(value) != count:
        raise ValueError(f"{path}: expected {count} numbers in 0..1")
    return [_number(v, path, 0, 1) for v in value]


def _flag(value, path):
    if not isinstance(value, bool):
        raise ValueError(f"{path}: expected true or false")
    return value


def parse_palettes(value):
    """Validate palettes.json: linear-sRGB region colours, every palette naming one region set."""
    _fields(value, PALETTES_FIELDS, "palettes.json")
    if value["color_space"] != PALETTE_COLOR_SPACE:
        raise ValueError(f"color_space: expected {PALETTE_COLOR_SPACE!r}")
    palettes = value["palettes"]
    if not isinstance(palettes, dict) or not palettes:
        raise ValueError("palettes: expected at least one palette")
    regions = None
    for name, colours in palettes.items():
        _text(name, "palettes")
        if not isinstance(colours, dict) or not colours:
            raise ValueError(f"palettes.{name}: expected region colours")
        for region, colour in colours.items():
            _text(region, f"palettes.{name}")
            _color(colour, f"palettes.{name}.{region}", 4)
        if regions is None:
            regions = set(colours)
        elif set(colours) != regions:
            raise ValueError(f"palettes.{name}: regions differ from the other palettes")
    if value["default"] not in palettes:
        raise ValueError("default: expected the name of a palette")
    return json.loads(json.dumps(value))


def parse_paint(value):
    """Validate paint.json, the per-ship knobs of ship_paint; unknown fields and keys are rejected."""
    optional = ("canopy_glass", "canopy_frames", "shadow_coverage", "shadow_depth", "side_wall_depth")
    fields = PAINT_FIELDS + tuple(key for key in optional if isinstance(value, dict) and key in value)
    _fields(value, fields, "paint.json")
    assigned = set()
    for key in ("canopy_glass", "canopy_frames"):
        names = value.get(key, [])
        if not isinstance(names, list):
            raise ValueError(f"{key}: expected a list of part names")
        for name in names:
            _text(name, key)
            if name in assigned:
                raise ValueError(f"{key}: duplicate canopy part {name}")
            assigned.add(name)
    if type(value["schema_version"]) is not int or value["schema_version"] != SCHEMA_VERSION:
        raise ValueError(f"paint.json: unsupported schema_version; expected {SCHEMA_VERSION}")
    brush = value["brush"]
    _fields(brush, BRUSH_FIELDS, "brush")
    _text(brush["image"], "brush.image")
    _number(brush["scale"], "brush.scale", 0, exclusive_low=True)
    _number(brush["contrast"], "brush.contrast", 0)
    _number(brush["blend"], "brush.blend", 0, 1)
    ramp = value["ramp"]
    if not isinstance(ramp, list) or len(ramp) < 2:
        raise ValueError("ramp: expected at least two [facing, value] stops")
    previous = -1.0
    for index, stop in enumerate(ramp):
        if not isinstance(stop, list) or len(stop) != 2:
            raise ValueError(f"ramp[{index}]: expected [facing, value]")
        facing = _number(stop[0], f"ramp[{index}].facing", 0, 1)
        _number(stop[1], f"ramp[{index}].value", 0, 1)
        if facing <= previous:
            raise ValueError("ramp: expected strictly increasing facings")
        previous = facing
    sweep = value["sweep"]
    _fields(sweep, SWEEP_FIELDS, "sweep")
    for key in SWEEP_FIELDS:
        _number(sweep[key], f"sweep.{key}", 0, 1 if key != "jitter" else None)
    if sweep["dark_below"] >= sweep["light_above"]:
        raise ValueError("sweep: expected dark_below below light_above")
    value = json.loads(json.dumps(value))
    for key, default in (("shadow_coverage", sweep["dark_below"]), ("shadow_depth", 1 - sweep["dark"]),
                         ("side_wall_depth", 1.0)):
        value.setdefault(key, default)
        _number(value[key], key, 0, 1)
    if value["shadow_coverage"] >= sweep["light_above"]:
        raise ValueError("shadow_coverage: expected below sweep.light_above")
    ink = value["ink"]
    _fields(ink, INK_FIELDS, "ink")
    _number(ink["inset"], "ink.inset", 0)
    if _number(ink["rim"], "ink.rim", 0) < ink["inset"]:
        raise ValueError("ink.rim: expected at least ink.inset")
    _number(ink["rim_light"], "ink.rim_light", 0, 1)
    _number(ink["wall_facing"], "ink.wall_facing", 0, 1, exclusive_low=True)
    plates = value["plates"]
    _fields(plates, PLATES_FIELDS, "plates")
    _number(plates["cap_facing"], "plates.cap_facing", 0, 1, exclusive_low=True)
    _number(plates["thin_wall"], "plates.thin_wall", 0)
    regions = value["regions"]
    if not isinstance(regions, dict) or not regions:
        raise ValueError("regions: expected at least one region")
    for region, flags in regions.items():
        _text(region, "regions")
        _fields(flags, REGION_FLAGS, f"regions.{region}")
        for flag in REGION_FLAGS:
            _flag(flags[flag], f"regions.{region}.{flag}")
    accents = value["accents"]
    if not isinstance(accents, list):
        raise ValueError("accents: expected a list")
    for index, accent in enumerate(accents):
        path = f"accents[{index}]"
        if not isinstance(accent, dict):
            raise ValueError(f"{path}: expected an accent")
        fields = ACCENT_FIELDS + (("color",) if accent.get("target") == "emission" else ())
        _fields(accent, fields, path)
        _text(accent["part"], f"{path}.part")
        if accent["target"] not in ACCENT_TARGETS:
            raise ValueError(f"{path}.target: expected one of {', '.join(ACCENT_TARGETS)}")
        where = accent["where"]
        if not isinstance(where, dict) or not where or not set(where) <= {"x", "y", "z"}:
            raise ValueError(f"{path}.where: expected bounds on some of x, y, z")
        for axis, bounds in where.items():
            if not isinstance(bounds, list) or len(bounds) != 2:
                raise ValueError(f"{path}.where.{axis}: expected [low, high]; either may be null")
            low, high = (None if b is None else _number(b, f"{path}.where.{axis}") for b in bounds)
            if low is None and high is None:
                raise ValueError(f"{path}.where.{axis}: expected at least one bound")
            if low is not None and high is not None and low >= high:
                raise ValueError(f"{path}.where.{axis}: expected low below high")
        _number(accent["value"], f"{path}.value", 0, 1)
        if "color" in accent:
            _color(accent["color"], f"{path}.color", 3)
    material = value["material"]
    _fields(material, MATERIAL_FIELDS, "material")
    for key in ("shadow_strength", "light_strength", "ink_strength"):
        _number(material[key], f"material.{key}", 0, 2)
    _number(material["light_lift"], "material.light_lift", 0, 1)
    _color(material["ink_color"], "material.ink_color", 3)
    _number(material["emission_blend"], "material.emission_blend", 0, 1)
    lighting = material["lighting"]
    if not isinstance(lighting, dict):
        raise ValueError("material.lighting: expected shader property values")
    for key, setting in lighting.items():
        if key in LIGHTING_FLOATS:
            _number(setting, f"material.lighting.{key}")
        elif key in LIGHTING_COLORS:
            _color(setting, f"material.lighting.{key}", 3)
        else:
            raise ValueError(f"material.lighting.{key}: not a lighting property of the drawn surface shader")
    return json.loads(json.dumps(value))


def load_settings(path, parser):
    """Parse a settings file with `parser`; a parse failure is a UsageError (exit 1)."""
    try:
        with open(path, encoding="utf-8-sig") as handle:
            return parser(json.load(handle))
    except (ValueError, OSError) as error:
        raise UsageError(f"{Path(path).name}: {error}")


def apply_exemptions(findings, exemptions):
    """Split findings ({rule, part, message}) into (open, exempted); exempted ones carry the reason."""
    reasons = {(e["rule"], part): e["reason"] for e in exemptions for part in e["parts"]}
    open_findings, exempted = [], []
    for finding in findings:
        reason = reasons.get((finding["rule"], finding["part"]))
        if reason is None:
            open_findings.append(finding)
        else:
            exempted.append({**finding, "reason": reason})
    return open_findings, exempted


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


class Refusal(Exception):
    def __init__(self, outcome):
        super().__init__("refused")
        self.outcome = outcome


class UsageError(Exception):
    pass


class ArgumentParser(argparse.ArgumentParser):
    def error(self, message):
        raise UsageError(message)


def tool_parser(description):
    parser = ArgumentParser(description=description)
    parser.add_argument("--ship", required=True, help="Path to the ship's ship.json")
    parser.add_argument("--out", required=True, help="Directory for the report and any outputs")
    return parser


class Report:
    """A tool's JSON report. Every finish re-hashes the sources and fails loudly if a tool changed one."""

    def __init__(self, tool, out_dir, blender_version, sources):
        out_dir = Path(out_dir).resolve()
        out_dir.mkdir(parents=True, exist_ok=True)
        self.path = out_dir / f"{tool}.json"
        self.sources = {Path(path).resolve(): sha256_file(path) for path in sources if Path(path).is_file()}
        first = next(iter(self.sources.values()), None)
        self.data = {"schema_version": SCHEMA_VERSION, "tool": tool, "recipe": FINGERPRINT_RECIPE,
                     "blender_version": blender_version, "source_sha256": first}

    def _write(self, verdict, trailers):
        for path, digest in self.sources.items():
            if sha256_file(path) != digest:
                raise RuntimeError(f"Source changed during the run: {path}")
        self.data["verdict"] = verdict
        self.path.write_text(json.dumps(self.data, indent=2, allow_nan=False) + "\n", encoding="utf-8")
        return (EXIT_PASS if verdict == "pass" else EXIT_FINDINGS,
                [("SHIP_VERDICT", verdict), ("SHIP_REPORT", str(self.path)), *trailers])

    def finish(self, passed, trailers=(), **fields):
        self.data.update(fields)
        return self._write("pass" if passed else "fail", trailers)

    def refuse(self, reason, trailers=()):
        self.data["refusal"] = reason
        print(f"Refused: {reason}", file=sys.stderr)
        return Refusal(self._write("fail", trailers))


def tool_argv(argv=None):
    argv = sys.argv if argv is None else argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def run(main, argv=None):
    """Run main(args) -> (code, trailers) with stdout routed to stderr, then write trailers and exit."""
    sys.stdout.flush()
    trailer_fd = os.dup(1)
    os.dup2(2, 1)
    trailers = []
    try:
        code, trailers = main(tool_argv(argv))
    except Refusal as refusal:
        code, trailers = refusal.outcome
    except UsageError as error:
        print(f"Usage error: {error}", file=sys.stderr)
        code = EXIT_ERROR
    except Exception:
        traceback.print_exc()
        code, trailers = EXIT_ERROR, []
    sys.stdout.flush()
    sys.stderr.flush()
    os.write(trailer_fd, "".join(f"{key}={value}\n" for key, value in trailers).encode("utf-8"))
    os.close(trailer_fd)
    sys.exit(code)
