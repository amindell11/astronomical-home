from pathlib import Path
import base64
import json

root = Path(__file__).resolve().parent
shared = root.parents[1] / "combat-hud" / "shared"
fonts = (shared / "type.css").read_text(encoding="utf-8")
for font in (shared / "fonts").glob("*.ttf"):
    fonts = fonts.replace(f"fonts/{font.name}", "data:font/ttf;base64," + base64.b64encode(font.read_bytes()).decode("ascii"))
data = {
    "stage": "data:image/jpeg;base64," + base64.b64encode((root / "inputs" / "clean-stage-pro.jpg").read_bytes()).decode("ascii"),
    "types": json.loads((shared / "type-candidates.json").read_text(encoding="utf-8")),
    "licenses": {p.name: p.read_text(encoding="utf-8") for p in (shared / "fonts").glob("*-license.txt")},
}
page = (root / "template.html").read_text(encoding="utf-8")
for token, content in {
    "/*__FONTS__*/": fonts,
    "/*__ROLES__*/": (shared / "roles.css").read_text(encoding="utf-8"),
    "/*__ENGINE__*/": (shared / "personality.js").read_text(encoding="utf-8"),
    "/*__APP__*/": (root / "console.js").read_text(encoding="utf-8"),
    "__EMBEDDED__": json.dumps(data),
}.items():
    page = page.replace(token, content)
output = root / "index.html"
output.write_text(page, encoding="utf-8")
print(f"WROTE={output}")
