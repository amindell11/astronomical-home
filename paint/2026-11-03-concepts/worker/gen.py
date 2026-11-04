"""Faction palette swaps generator (issue #1329, slice 2). Deterministic; re-runnable.

Re-running keeps the guid of any .meta it already wrote. Writes manifest.json beside itself
for selfcheck.py.
"""
import json
import os
import re
import sys
import uuid

REPO = "D:/amind/git/agent-8"
SHIPS = REPO + "/src/Asteroids3D/Assets/Visuals/Ships"
HERE = os.path.dirname(os.path.abspath(__file__))

ELITE = "#fc12d8"
GLOW = {"shoal": "#fe861a", "veil": "#a8f63b", "bastion": "#2bd2f7", "elite": ELITE}


def srgb(v):
    return 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055


def lin(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def hex_rgb(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def hex_lin6(h):
    return [round(lin(c), 6) for c in hex_rgb(h)]


def lift(c, L):
    return c + (1 - c) * L


def lum(rgb_lin):
    return 0.2126 * rgb_lin[0] + 0.7152 * rgb_lin[1] + 0.0722 * rgb_lin[2]


def fmt(v):
    s = f"{v:.8g}"
    if "e" in s:
        s = f"{v:.10f}".rstrip("0").rstrip(".")
    return s


# ---------- file helpers (preserve newline style exactly) ----------

def read_text(path):
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write_text(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


COLOR_RE = re.compile(r"^(    - (\w+): )\{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}(\r?)$")


def parse_colors(text):
    out = {}
    for line in text.split("\n"):
        m = COLOR_RE.match(line)
        if m:
            out[m.group(2)] = [float(m.group(i)) for i in (3, 4, 5, 6)]
    return out


def set_props(text, name, colors):
    """colors: {prop: [r,g,b] or [r,g,b,a]}; omitted alpha keeps the source alpha text."""
    lines = text.split("\n")
    seen = set()
    for i, line in enumerate(lines):
        m = COLOR_RE.match(line)
        if m and m.group(2) in colors:
            v = colors[m.group(2)]
            a = fmt(v[3]) if len(v) == 4 else m.group(6)
            lines[i] = f"{m.group(1)}{{r: {fmt(v[0])}, g: {fmt(v[1])}, b: {fmt(v[2])}, a: {a}}}{m.group(7)}"
            seen.add(m.group(2))
    missing = set(colors) - seen
    if missing:
        raise SystemExit(f"missing colour props {missing}")
    n = 0
    for i, line in enumerate(lines):
        if line.startswith("  m_Name: ") and not line.rstrip("\r").endswith("m_Name: "):
            cr = "\r" if line.endswith("\r") else ""
            lines[i] = f"  m_Name: {name}{cr}"
            n += 1
    if n != 1:
        raise SystemExit(f"expected exactly one non-empty m_Name, found {n}")
    return "\n".join(lines)


GUID_RE = re.compile(r"^guid: [0-9a-f]{32}(\r?)$", re.M)


def existing_guid(meta_path):
    if os.path.exists(meta_path):
        m = re.search(r"^guid: ([0-9a-f]{32})", read_text(meta_path), re.M)
        if m:
            return m.group(1)
    return None


def write_meta(src_meta, dst_meta):
    g = existing_guid(dst_meta) or uuid.uuid4().hex
    text = read_text(src_meta)
    new, n = GUID_RE.subn(lambda m: f"guid: {g}{m.group(1)}", text)
    if n != 1:
        raise SystemExit(f"guid line count {n} in {src_meta}")
    write_text(dst_meta, new)


FOLDER_META_SRC = SHIPS + "/Valis/Materials/jade-iris.meta"


def ensure_folder(folder):
    os.makedirs(folder, exist_ok=True)
    write_meta(FOLDER_META_SRC, folder + ".meta")
    MANIFEST["folders"].append(os.path.relpath(folder, REPO).replace("\\", "/"))


MANIFEST = {"materials": [], "folders": [], "palettes": {}, "notes": {}}


def emit_mat(src, dst, colors, picks):
    """picks: {prop: description of the pick} for the self-check."""
    text = read_text(src)
    name = os.path.splitext(os.path.basename(dst))[0]
    write_text(dst, set_props(text, name, colors))
    write_meta(src + ".meta", dst + ".meta")
    MANIFEST["materials"].append({
        "src": os.path.relpath(src, REPO).replace("\\", "/"),
        "dst": os.path.relpath(dst, REPO).replace("\\", "/"),
        "expected": {k: list(v) for k, v in colors.items()},
        "picks": picks,
    })


# ---------- palettes.json (format-preserving) ----------

def dump_multiline(obj, nl):
    return json.dumps(obj, indent=2, ensure_ascii=False).replace("\n", nl)


def dump_nightshade(obj, nl):
    out = ["{"]
    out.append(f'  "color_space": {json.dumps(obj["color_space"])},')
    out.append(f'  "default": {json.dumps(obj["default"])},')
    out.append('  "palettes": {')
    pals = list(obj["palettes"].items())
    for pi, (pname, regions) in enumerate(pals):
        out.append(f"    {json.dumps(pname)}: {{")
        items = list(regions.items())
        for ri, (rname, val) in enumerate(items):
            comma = "," if ri < len(items) - 1 else ""
            out.append(f"      {json.dumps(rname)}: [{', '.join(json.dumps(x) for x in val)}]{comma}")
        out.append("    }" + ("," if pi < len(pals) - 1 else ""))
    out.append("  }")
    out.append("}")
    return nl.join(out)


def update_palettes(ship, dumper, new_palettes):
    path = f"{REPO}/art/ships/{ship}/palettes.json"
    raw = read_text(path)
    nl = "\r\n" if "\r\n" in raw else "\n"
    trail = raw[len(raw.rstrip("\r\n")):]
    data = json.loads(raw)
    for p in new_palettes:
        data["palettes"].pop(p, None)  # idempotent re-run
    base = dumper(data, nl) + trail
    # Re-serialising without the new palettes must reproduce the HEAD file (format check).
    orig = git_show(f"art/ships/{ship}/palettes.json").replace("\r\n", "\n").replace("\n", nl)
    if base != orig and base.rstrip("\r\n") != orig.rstrip("\r\n"):
        raise SystemExit(f"serializer does not reproduce {path}")
    for p, regions in new_palettes.items():
        data["palettes"][p] = regions
    write_text(path, dumper(data, nl) + trail)
    MANIFEST["palettes"][ship] = new_palettes


def git_show(rel):
    import subprocess
    return subprocess.run(["git", "-C", REPO, "show", f"HEAD:{rel}"], capture_output=True,
                          text=True, encoding="utf-8", check=True).stdout


def lin_entry(h, alpha):
    return hex_lin6(h) + [alpha]


# ---------- Crimson ----------

def crimson():
    pal_path = f"{REPO}/art/ships/crimson/palettes.json"
    cr = json.loads(read_text(pal_path))["palettes"]["crimson"]
    region_order = list(cr.keys())
    picks = {
        "Silver": ("#f2a71a", "#2d2e36", "#eeeeee"),
        "SilverStructure": ("#f2a71a", "#2c2e35", "#234eb3"),
        "Red": ("#171614", "#9fe535", "#234eb3"),
        "RedStructure": ("#171614", "#9fe535", "#234eb3"),
        "Frame": ("#171614", "#9fe535", "#234eb3"),
        "Charcoal": ("#3c3e43", "#1d1e23", "#1f2441"),
        "Rim": ("#3a3a3e", "#1d1e23", "#ededed"),
    }
    sorted_names = sorted(cr.keys())
    src = SHIPS + "/Crimson/Hull paint.mat"
    today = parse_colors(read_text(src))
    order_check = []
    for i, n in enumerate(sorted_names):
        expect = [srgb(c) for c in cr[n][:3]]
        got = today[f"_PaintRegionColor{i}"][:3]
        order_check.append({"index": i, "region": n, "today": got,
                            "srgb_of_linear": expect,
                            "maxdiff": max(abs(a - b) for a, b in zip(got, expect))})
    MANIFEST["notes"]["crimson_order"] = order_check
    if any(o["maxdiff"] > 0.02 for o in order_check):
        raise SystemExit("Crimson region order check failed: " + json.dumps(order_check, indent=1))
    ensure_folder(SHIPS + "/Crimson/Materials")
    new_pals = {}
    for pi, pal in enumerate(("shoal", "veil", "bastion")):
        ensure_folder(f"{SHIPS}/Crimson/Materials/{pal}")
        colors, pdesc = {}, {}
        for i, n in enumerate(sorted_names):
            if n in picks:
                h = picks[n][pi]
                colors[f"_PaintRegionColor{i}"] = hex_rgb(h)
                pdesc[f"_PaintRegionColor{i}"] = f"{n} {h} (hex/255)"
        emit_mat(src, f"{SHIPS}/Crimson/Materials/{pal}/Hull paint.mat", colors, pdesc)
        regions = {}
        for n in region_order:
            regions[n] = lin_entry(picks[n][pi], 1) if n in picks else list(cr[n])
        new_pals[pal] = regions
    update_palettes("crimson", dump_multiline, new_pals)


# ---------- Valis ----------

def valis():
    cr = json.loads(read_text(f"{REPO}/art/ships/valis/palettes.json"))["palettes"]["jade-iris"]
    picks = {
        "shoal": {"Ivory": "#f1a619", "Cool gray": "#d28c0c", "Edge": "#d78c08",
                  "Graphite": "#171412", "Lavender": "#363737", "Engine": "#39393d"},
        "shoal-elite": {"Ivory": "#f1a619", "Cool gray": "#d28c0c", "Edge": "#d78c08",
                        "Graphite": "#171412", "Lavender": ELITE, "Engine": "#39393d"},
    }
    glow = {"shoal": GLOW["shoal"], "shoal-elite": ELITE}
    srcdir = SHIPS + "/Valis/Materials/jade-iris"
    lav_em = parse_colors(read_text(srcdir + "/Lavender.mat"))["_EmissionColor"]
    target_y = lum([lin(c) for c in lav_em[:3]])
    MANIFEST["notes"]["valis_lavender_emission_today"] = lav_em
    MANIFEST["notes"]["valis_lavender_emission_lumY_linear"] = target_y
    new_pals = {}
    for pal, regs in picks.items():
        ensure_folder(f"{SHIPS}/Valis/Materials/{pal}")
        for mat in ("Ivory", "Cool gray", "Lavender", "Graphite", "Edge", "Engine"):
            colors = {"_PaperColor": hex_rgb(regs[mat])}
            pdesc = {"_PaperColor": f"{mat} {regs[mat]} (hex/255)"}
            if mat == "Lavender":
                g = [lin(c) for c in hex_rgb(glow[pal])]
                k = target_y / lum(g)
                colors["_EmissionColor"] = [srgb(c * k) for c in g] + [lav_em[3]]
                pdesc["_EmissionColor"] = f"glow {glow[pal]} scaled x{k:.4f} (linear) to lumY {target_y:.5f}"
            emit_mat(f"{srcdir}/{mat}.mat", f"{SHIPS}/Valis/Materials/{pal}/{mat}.mat", colors, pdesc)
        regions = {}
        for n in cr.keys():
            regions[n] = list(cr[n]) if n == "Canopy" else lin_entry(regs[n], 1.0)
        new_pals[pal] = regions
    update_palettes("valis", dump_multiline, new_pals)


# ---------- Nightshade ----------

def nightshade():
    cr = json.loads(read_text(f"{REPO}/art/ships/nightshade/palettes.json"))["palettes"]["nightshade"]
    L = json.loads(read_text(f"{REPO}/art/ships/nightshade/paint.json"))["material"]["light_lift"]
    srcdir = SHIPS + "/Nightshade/Materials/nightshade"
    # Rule check against today's materials.
    worst = 0.0
    for n, v in cr.items():
        c = parse_colors(read_text(f"{srcdir}/{n}.mat"))
        exp_p = [srgb(x) for x in v[:3]]
        exp_l = [srgb(lift(x, L)) for x in v[:3]]
        worst = max(worst, max(abs(a - b) for a, b in zip(c["_PaintLightColor"][:3], exp_l)),
                    max(abs(a - b) for a, b in zip(c["_PaperColor"][:3], exp_p)))
    rule_ok = worst <= 0.002
    MANIFEST["notes"]["nightshade_L"] = L
    MANIFEST["notes"]["nightshade_rule_maxdiff"] = worst
    MANIFEST["notes"]["nightshade_rule_ok"] = rule_ok
    picks = {
        "veil": {"Nightshade Midnight": "#1c1d22", "Nightshade Violet": "#2c2e36",
                 "Nightshade Plum": "#9ee636", "Nightshade Glass": "#a1e535", "Nightshade Edge": "#1e1f24"},
        "veil-elite": {"Nightshade Midnight": "#1c1d22", "Nightshade Violet": "#2c2e36",
                       "Nightshade Plum": ELITE, "Nightshade Glass": "#a1e535", "Nightshade Edge": "#1e1f24"},
    }
    new_pals = {}
    for pal, regs in picks.items():
        ensure_folder(f"{SHIPS}/Nightshade/Materials/{pal}")
        for n in cr.keys():
            src = f"{srcdir}/{n}.mat"
            colors, pdesc = {}, {}
            if n in regs:
                lp = hex_lin6(regs[n])
                colors["_PaperColor"] = [srgb(x) for x in lp]
                pdesc["_PaperColor"] = f"{n} {regs[n]} srgb(linear6)"
                if rule_ok:
                    colors["_PaintLightColor"] = [srgb(lift(x, L)) for x in lp]
                    pdesc["_PaintLightColor"] = f"{n} {regs[n]} srgb(lift(linear6, {L}))"
            emit_mat(src, f"{SHIPS}/Nightshade/Materials/{pal}/{n}.mat", colors, pdesc)
        regions = {}
        for n in cr.keys():
            regions[n] = lin_entry(regs[n], 1.0) if n in regs else list(cr[n])
        new_pals[pal] = regions
    update_palettes("nightshade", dump_nightshade, new_pals)


# ---------- Bulwark ----------

def bulwark():
    cr = json.loads(read_text(f"{REPO}/art/ships/bulwark/palettes.json"))["palettes"]["plum-bone"]
    L_json = json.loads(read_text(f"{REPO}/art/ships/bulwark/paint.json"))["material"]["light_lift"]
    srcdir = SHIPS + "/Bulwark/Materials/plum-bone"
    mats = ["Bulwark Bone", "Bulwark EdgeCoat", "Bulwark Flap", "Bulwark Guard Plum",
            "Bulwark Armor Charcoal", "Bulwark Structure", "Bulwark Metal"]
    # Derive L from today's paper/light pairs.
    Ls = []
    em_today = {}
    for m in mats:
        c = parse_colors(read_text(f"{srcdir}/{m}.mat"))
        em_today[m] = c["_EmissionColor"]
        for p, l in zip(c["_PaperColor"][:3], c["_PaintLightColor"][:3]):
            lp, ll = lin(p), lin(l)
            Ls.append((ll - lp) / (1 - lp))
    L_derived = sum(Ls) / len(Ls)
    L = L_json
    worst = 0.0
    for m in mats:
        c = parse_colors(read_text(f"{srcdir}/{m}.mat"))
        exp = [srgb(lift(lin(p), L)) for p in c["_PaperColor"][:3]]
        worst = max(worst, max(abs(a - b) for a, b in zip(c["_PaintLightColor"][:3], exp)))
    MANIFEST["notes"]["bulwark_L_derived_mean"] = L_derived
    MANIFEST["notes"]["bulwark_L_derived_range"] = [min(Ls), max(Ls)]
    MANIFEST["notes"]["bulwark_L_paint_json"] = L_json
    MANIFEST["notes"]["bulwark_L_used"] = L
    MANIFEST["notes"]["bulwark_rule_maxdiff"] = worst
    if abs(L_derived - L_json) > 0.005 or worst > 0.002:
        raise SystemExit(f"Bulwark L mismatch derived={L_derived} json={L_json} worst={worst}")

    SUB, COAT = "#1c2547", "#2450b8"
    table = {
        "bastion": {
            "Bulwark Bone": (SUB, COAT), "Bulwark EdgeCoat": (SUB, COAT), "Bulwark Flap": ("#eeeeee", "#eeeeee"),
            "Bulwark Guard Plum": "#eeeeee", "Bulwark Armor Charcoal": SUB, "Bulwark Structure": SUB,
            "Bulwark Metal": "#e6e6e6"},
        "bastion-elite": {
            "Bulwark Bone": (SUB, COAT), "Bulwark EdgeCoat": (SUB, COAT), "Bulwark Flap": (ELITE, ELITE),
            "Bulwark Guard Plum": ELITE, "Bulwark Armor Charcoal": SUB, "Bulwark Structure": SUB,
            "Bulwark Metal": "#e6e6e6"},
    }
    new_pals = {}
    for pal, spec in table.items():
        ensure_folder(f"{SHIPS}/Bulwark/Materials/{pal}")
        for m in mats:
            v = spec[m]
            colors, pdesc = {}, {}
            if isinstance(v, tuple):
                sub, coat = hex_lin6(v[0]), hex_lin6(v[1])
                colors["_PaperColor"] = [srgb(x) for x in sub]
                colors["_PaintRegionColor0"] = [srgb(x) for x in sub]
                colors["_PaintRegionColor1"] = [srgb(x) for x in coat]
                colors["_PaintLightColor"] = [srgb(lift(x, L)) for x in sub]
                pdesc["_PaperColor"] = f"substrate {v[0]} srgb(linear6)"
                pdesc["_PaintRegionColor0"] = f"substrate {v[0]} srgb(linear6)"
                pdesc["_PaintRegionColor1"] = f"coat {v[1]} srgb(linear6)"
                pdesc["_PaintLightColor"] = f"substrate {v[0]} srgb(lift(linear6, {L}))"
            else:
                lp = hex_lin6(v)
                colors["_PaperColor"] = [srgb(x) for x in lp]
                colors["_PaintLightColor"] = [srgb(lift(x, L)) for x in lp]
                pdesc["_PaperColor"] = f"{v} srgb(linear6)"
                pdesc["_PaintLightColor"] = f"{v} srgb(lift(linear6, {L}))"
            gh = ELITE if (pal == "bastion-elite" and m in ("Bulwark Guard Plum", "Bulwark Flap")) else GLOW["bastion"]
            et = em_today[m]
            ty = lum([lin(c) for c in et[:3]])
            g = [lin(c) for c in hex_rgb(gh)]
            k = ty / lum(g)
            colors["_EmissionColor"] = [srgb(c * k) for c in g] + [et[3]]
            pdesc["_EmissionColor"] = f"glow {gh} scaled x{k:.4f} (linear) to lumY {ty:.5f}"
            emit_mat(f"{srcdir}/{m}.mat", f"{SHIPS}/Bulwark/Materials/{pal}/{m}.mat", colors, pdesc)
        flap = spec["Bulwark Flap"][0]
        guard = spec["Bulwark Guard Plum"]
        role = {
            "Bulwark Plum": SUB, "Bulwark Bone": SUB, "Bulwark Plum Secondary": SUB, "Bulwark Bone Secondary": SUB,
            "Bulwark Flap": flap, "Bulwark Flap Secondary": flap, "Bulwark Guard Plum": guard,
            "Bulwark Guard Bone": COAT,
            "Bulwark Structure": SUB, "Bulwark Structure Secondary": SUB, "Bulwark EdgeCoat": SUB,
            "Bulwark Armor Charcoal": SUB, "Bulwark Metal": "#e6e6e6",
            "Bulwark Glass": None, "Bulwark Glass Secondary": None,
        }
        if set(role) != set(cr):
            raise SystemExit(f"Bulwark region set mismatch {set(role) ^ set(cr)}")
        new_pals[pal] = {n: (list(cr[n]) if role[n] is None else lin_entry(role[n], 1.0)) for n in cr}
    update_palettes("bulwark", dump_multiline, new_pals)


# ---------- Flames ----------

def flames():
    src = SHIPS + "/_Shared/ThrusFlame_Blue.mat"
    for name, h in (("Shoal", GLOW["shoal"]), ("Veil", GLOW["veil"]), ("Bastion", GLOW["bastion"]), ("Elite", ELITE)):
        c = hex_rgb(h) + [1]
        colors = {"_BaseColor": c, "_Color": c, "_EmissionColor": c}
        pdesc = {k: f"{h} (hex/255, a 1)" for k in colors}
        emit_mat(src, f"{SHIPS}/_Shared/ThrusFlame_{name}.mat", colors, pdesc)


if __name__ == "__main__":
    crimson()
    valis()
    nightshade()
    bulwark()
    flames()
    with open(os.path.join(HERE, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(MANIFEST, f, indent=1)
    print(f"materials: {len(MANIFEST['materials'])}, folders: {len(MANIFEST['folders'])}")
    print(json.dumps(MANIFEST["notes"], indent=1))
