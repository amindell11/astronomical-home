from pathlib import Path
import base64
import json

root = Path(__file__).resolve().parents[1]
source = root / "tuners" / "source"
fonts = (root / "shared" / "type.css").read_text(encoding="utf-8")
for font in (root / "shared" / "fonts").glob("*.ttf"):
    encoded = base64.b64encode(font.read_bytes()).decode("ascii")
    fonts = fonts.replace(f"fonts/{font.name}", f"data:font/ttf;base64,{encoded}")
captures = {}
for key, name in {"bright": "locale3-bright.png", "copper": "locale1-gameplay.png", "blueCombat": "blue-combat.png"}.items():
    captures[key] = "data:image/png;base64," + base64.b64encode((root / "inputs" / name).read_bytes()).decode("ascii")
roles = {"ink": "#f4ebd9", "shield": "#70dff5", "hull": "#96e59b", "threat": "#ff7b72", "friendly": "#43d7c4", "objective": "#dba6ff", "neutral": "#a9aeb4", "backing": "#171b22", "dimOpacity": 0.55}
template = (source / "template.html").read_text(encoding="utf-8")
replacements = {
    "__FONT_LICENSES__": json.dumps({p.name: p.read_text(encoding="utf-8") for p in (root / "shared" / "fonts").glob("*-license.txt")}),
    "/*__FONTS__*/": fonts,
    "/*__ROLES__*/": (root / "shared" / "roles.css").read_text(encoding="utf-8"),
    "/*__ENGINE__*/": (root / "shared" / "personality.js").read_text(encoding="utf-8"),
    "/*__HUD__*/": (source / "hud.js").read_text(encoding="utf-8"),
    "/*__APP__*/": (source / "app.js").read_text(encoding="utf-8"),
}
for token, value in replacements.items():
    template = template.replace(token, value)
for view in ["personality", "color"]:
    page = template.replace("__EMBEDDED__", json.dumps({"view": view, "captures": captures, "roles": roles}))
    path = root / "tuners" / f"{view}.html"
    path.write_text(page, encoding="utf-8")
    print(f"WROTE={path}")
