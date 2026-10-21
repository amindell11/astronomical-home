#!/usr/bin/env python3
"""Derive each ship's handling and durability numbers from the live prefabs and module assets.

Reads the chassis variants under Assets/Prefabs/Ships, the engine and shield assets and the
keystone kit prefab they point at, and the laser's damage and cadence, and prints one table per
ship: the authored stats, then the metrics a pilot feels (acceleration, terminal speed against the
cap, time to the cap, yaw spin-up, re-aim bound, stopping distance, boost delta-v, effective hit
points, laser time-to-kill, collision delta-v per impulse). Run it against two checkouts to get a
before/after delta for a tuning PR:

    python3 scripts/balance/ship_profiles.py --root <checkout> [--ships Vanguard,Bulwark] [--markdown]

The movement model follows MovementController: forces each fixed step, Rigidbody linear damping as
an exponential drag, maxLinearVelocity as a hard cap, the Dash keystone as an impulse. Nothing here
is a source of truth; the owning assets are.
"""
from __future__ import annotations

import argparse
import math
import re
from dataclasses import dataclass, field
from pathlib import Path

ASSETS = Path("src/Asteroids3D/Assets")
SHIP_PREFABS = ["Vanguard", "Bulwark", "Valis", "Crimson", "Nightshade"]
LASER_PREFAB = ASSETS / "Prefabs/Weapons/Projectiles/Laser.prefab"
LASER_WEAPON = ASSETS / "Prefabs/Weapons/Lasers.prefab"

ENGINE_FIELDS = ["maxSpeed", "maxYawRate", "forwardForce", "reverseForce", "yawTorque", "angularDrag",
                 "bankTorque", "bankDamping", "minStrafeForce", "maxStrafeForce", "linearDrag"]
DASH_FIELDS = ["impulse", "cooldown"]
SHIELD_FIELDS = ["maxShield", "shieldRegenDelay", "shieldRegenRate"]
CHASSIS_FIELDS = ["mass", "maxHealth", "maxBankAngle"]

FLOAT = r"-?\d+(?:\.\d+)?(?:e-?\d+)?"


def guid_of(path: Path) -> str:
    return re.search(r"^guid: ([0-9a-f]{32})", (path.parent / (path.name + ".meta")).read_text(), re.M).group(1)


def scalar_fields(text: str, names: list[str]) -> dict[str, float]:
    out = {}
    for n in names:
        m = re.search(rf"^\s*{n}: ({FLOAT})\s*$", text, re.M)
        if m:
            out[n] = float(m.group(1))
    return out


@dataclass
class Module:
    name: str
    guid: str
    fields: dict[str, float]


def load_modules(root: Path, folder: str, names: list[str], pattern: str = "*.asset") -> dict[str, Module]:
    mods = {}
    for p in sorted((root / ASSETS / folder).glob(pattern)):
        mods[guid_of(p)] = Module(p.stem, guid_of(p), scalar_fields(p.read_text(), names))
    return mods


@dataclass
class Ship:
    name: str
    chassis: dict[str, float]
    engine: Module
    shield: Module
    keystone: Module
    inertia_z: float
    root_scale: float
    notes: list[str] = field(default_factory=list)


def base_component_ids(base_text: str) -> tuple[str, str]:
    """fileIDs of ShipBase's Ship MonoBehaviour and its Rigidbody, which variants target."""
    ship_id = rb_id = None
    for block in base_text.split("--- !u!"):
        head = block.split("\n", 1)[0]
        m = re.match(r"(\d+) &(\d+)", head)
        if not m:
            continue
        kind, fid = m.group(1), m.group(2)
        if kind == "114" and re.search(r"^\s*engine: \{fileID", block, re.M):
            ship_id = fid
        if kind == "54":
            rb_id = fid
    if not ship_id or not rb_id:
        raise SystemExit("ShipBase.prefab: could not find the Ship component or the Rigidbody")
    return ship_id, rb_id


def modifications(text: str) -> list[tuple[str, str, str, str]]:
    """(target fileID, propertyPath, value, objectReference guid) per m_Modifications entry."""
    out = []
    for m in re.finditer(
        r"- target: \{fileID: (-?\d+), guid: [0-9a-f]+,\s*type: 3\}\s*\n\s*propertyPath: (\S+)\s*\n\s*value: ([^\n]*)\n\s*objectReference: \{fileID: (?:-?\d+)(?:,\s*guid: ([0-9a-f]+),\s*type: \d)?\}",
        text):
        out.append((m.group(1), m.group(2), m.group(3).strip(), m.group(4) or ""))
    return out


def load_ships(root: Path, names: list[str]) -> list[Ship]:
    engines = load_modules(root, "Settings/Ships/Engines", ENGINE_FIELDS)
    shields = load_modules(root, "Settings/Ships/Shields", SHIELD_FIELDS)
    keystones = load_modules(root, "Prefabs/Keystones", DASH_FIELDS, "*.prefab")
    base = (root / ASSETS / "Prefabs/Ships/ShipBase.prefab").read_text()
    ship_id, rb_id = base_component_ids(base)
    base_chassis = scalar_fields(base, CHASSIS_FIELDS)
    base_engine = re.search(r"^\s*engine: \{fileID: \d+, guid: ([0-9a-f]+)", base, re.M).group(1)
    base_shield = re.search(r"^\s*shield: \{fileID: \d+, guid: ([0-9a-f]+)", base, re.M).group(1)
    ships = []
    for name in names:
        text = (root / ASSETS / "Prefabs/Ships" / f"{name}.prefab").read_text()
        chassis = dict(base_chassis)
        engine, shield, keystone, inertia_z, scale = base_engine, base_shield, None, None, 1.0
        for target, prop, value, ref in modifications(text):
            if target == ship_id and prop in CHASSIS_FIELDS:
                chassis[prop] = float(value)
            elif target == ship_id and prop == "engine" and ref:
                engine = ref
            elif target == ship_id and prop == "shield" and ref:
                shield = ref
            elif target == ship_id and prop == "keystone" and ref:
                keystone = ref
            elif prop == "m_InertiaTensor.z":
                inertia_z = float(value)
        notes = []
        if inertia_z is None:
            notes.append("inertia not pinned: Unity derives it from the collider")
            inertia_z = chassis["mass"]
        if keystone is None:
            raise SystemExit(f"{name}.prefab: no keystone wired")
        ships.append(Ship(name, chassis, engines[engine], shields[shield], keystones[keystone], inertia_z, scale, notes))
    return ships


def laser(root: Path) -> dict[str, float]:
    """Damage per shot, seconds between shots, and the heat fields Heat.ShotsToOverheat reads."""
    w = scalar_fields((root / LASER_WEAPON).read_text(), ["fireRate", "heatPerShot", "maxHeat", "coolingRate", "coolDownDelay"])
    w["damage"] = scalar_fields((root / LASER_PREFAB).read_text(), ["damage"])["damage"]
    return w


def shots_before_overheat(w: dict[str, float]) -> float:
    """Shots that fit before the next one overheats, mirroring Heat.ShotsToOverheat; inf when cooling keeps pace."""
    if w["heatPerShot"] >= w["maxHeat"]:
        return 0
    net = w["heatPerShot"] - w["coolingRate"] * max(0.0, w["fireRate"] - w["coolDownDelay"])
    if net <= 0:
        return math.inf
    return math.ceil((w["maxHeat"] - w["heatPerShot"]) / net)


def stopping_distance(v0: float, a_rev: float, drag: float) -> float:
    """Reverse thrust plus drag from v0 to rest, integrated at 1 ms."""
    v, x, dt = v0, 0.0, 0.001
    while v > 0.01:
        v -= (a_rev + drag * v) * dt
        x += max(v, 0.0) * dt
    return x


def time_to_speed(target: float, a: float, drag: float) -> float:
    """Seconds for v(t) = (a/drag)(1 - e^(-drag t)) to reach target; inf when the terminal speed is below it."""
    vt = a / drag
    if target >= vt:
        return math.inf
    return -math.log(1.0 - target / vt) / drag


def metrics(s: Ship, w: dict[str, float]) -> dict[str, float]:
    e, sh, k, c = s.engine.fields, s.shield.fields, s.keystone.fields, s.chassis
    m, drag = c["mass"], e["linearDrag"]
    a_fwd, a_rev = e["forwardForce"] / m, e["reverseForce"] / m
    cap = e["maxSpeed"]
    v_term = a_fwd / drag
    v_top = min(cap, v_term)
    t_cap = time_to_speed(cap, a_fwd, drag)
    t_90 = time_to_speed(0.9 * v_top, a_fwd, drag)
    cap_rad = math.radians(e["maxYawRate"])
    spin_up = cap_rad * s.inertia_z / e["yawTorque"]
    reaim_90 = 90.0 / e["maxYawRate"] + spin_up
    ehp = c["maxHealth"] + sh["maxShield"]
    dps = w["damage"] / w["fireRate"]
    shots = math.ceil(ehp / w["damage"])
    magazine = shots_before_overheat(w)
    return {
        "accel fwd (u/s²)": a_fwd,
        "accel rev (u/s²)": a_rev,
        "strafe accel rest..cap (u/s²)": (e["maxStrafeForce"] / m, e["minStrafeForce"] / m),
        "terminal speed (u/s)": v_term,
        "top speed reached (u/s)": v_top,
        "cap (u/s)": cap,
        "time to cap (s)": t_cap,
        "time to 90% of top (s)": t_90,
        "stop from top (u)": stopping_distance(v_top, a_rev, drag),
        "yaw cap (deg/s)": e["maxYawRate"],
        "yaw spin-up to cap (s)": spin_up,
        "90° re-aim bound (s)": reaim_90,
        "boost Δv (u/s)": k["impulse"] / m,
        "boost cooldown (s)": k["cooldown"],
        "momentum at top (kg·u/s)": m * v_top,
        "hull": c["maxHealth"],
        "shield": sh["maxShield"],
        "EHP": ehp,
        "shield reset (s, delay + refill)": sh["shieldRegenDelay"] + sh["maxShield"] / sh["shieldRegenRate"],
        "laser shots to kill": shots,
        "laser shots before overheat": magazine,
        "laser heat cycles to kill": 1 if math.isinf(magazine) else math.ceil(shots / magazine),
        "laser TTK sustained (s)": ehp / dps,
        "rock Δv per 1000 impulse (u/s)": 1000.0 / m,
    }


def fmt(v) -> str:
    if isinstance(v, tuple):
        return " .. ".join(fmt(x) for x in v)
    if isinstance(v, float):
        if math.isinf(v):
            return "never"
        return f"{v:.2f}" if abs(v) < 100 else f"{v:.0f}"
    return str(v)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    ap.add_argument("--root", default=".", help="repo checkout to read (default: cwd)")
    ap.add_argument("--ships", default=",".join(SHIP_PREFABS))
    ap.add_argument("--markdown", action="store_true", help="one markdown table, ships as columns")
    ap.add_argument("--override", help="JSON: {ship: {mass, maxHealth, inertia_z, engine: {...}, shield: {...}, engineName, shieldName}} applied before deriving")
    args = ap.parse_args()
    root = Path(args.root).resolve()
    names = [n.strip() for n in args.ships.split(",") if n.strip()]
    ships = load_ships(root, names)
    if args.override:
        import json
        for s_ in ships:
            o = json.loads(Path(args.override).read_text()).get(s_.name, {})
            for k in CHASSIS_FIELDS:
                if k in o:
                    s_.chassis[k] = float(o[k])
            if "inertia_z" in o:
                s_.inertia_z = float(o["inertia_z"])
            if "engine" in o:
                s_.engine = Module(o.get("engineName", s_.engine.name + "*"), "", {**s_.engine.fields, **{k: float(v) for k, v in o["engine"].items()}})
            if "shield" in o:
                s_.shield = Module(o.get("shieldName", s_.shield.name + "*"), "", {**s_.shield.fields, **{k: float(v) for k, v in o["shield"].items()}})
    w = laser(root)
    rows = {s.name: metrics(s, w) for s in ships}
    authored = {s.name: {**{k: s.chassis[k] for k in CHASSIS_FIELDS}, "engine": s.engine.name,
                         "shield": s.shield.name, "keystone": s.keystone.name, "yaw inertia (pinned)": s.inertia_z,
                         **{f"engine.{k}": v for k, v in s.engine.fields.items()},
                         **{f"keystone.{k}": v for k, v in s.keystone.fields.items()},
                         **{f"shield.{k}": v for k, v in s.shield.fields.items()}} for s in ships}
    print(f"laser: {w['damage']:g} dmg every {w['fireRate']:g} s, {w['heatPerShot']:g} heat/shot of {w['maxHeat']:g}, "
          f"cooling {w['coolingRate']:g}/s after {w['coolDownDelay']:g} s; drag and caps per engine asset")
    if args.markdown:
        for title, table in (("Authored", authored), ("Derived", rows)):
            keys = list(next(iter(table.values())).keys())
            print(f"\n### {title}\n\n| | " + " | ".join(names) + " |\n|---|" + "---|" * len(names))
            for k in keys:
                print(f"| {k} | " + " | ".join(fmt(table[n][k]) for n in names) + " |")
    else:
        for s in ships:
            print(f"\n== {s.name}" + (f"  ({'; '.join(s.notes)})" if s.notes else ""))
            for k, v in authored[s.name].items():
                print(f"  {k:36s} {fmt(v)}")
            print("  --")
            for k, v in rows[s.name].items():
                print(f"  {k:36s} {fmt(v)}")


if __name__ == "__main__":
    main()
