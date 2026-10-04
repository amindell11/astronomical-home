"""Ship tool contract shared by every ship tool; pure Python so it is testable without Blender.

Exit codes: 0 pass, 3 findings or refusal, 1 error (bad invocation, invalid ship.json, crash).
stdout carries only KEY=value trailers, written last: SHIP_VERDICT=pass|fail and
SHIP_REPORT=<absolute report path> on exits 0 and 3, plus SHIP_FBX= (ship_export) and
SHIP_LOCK= (ship_lock). Everything else, Blender's own output included, goes to stderr.
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
