from pathlib import Path

here = Path(__file__).parent
js = (here / "script.js").read_text(encoding="utf-8").replace("/*PICKS*/", (here / "picks.js").read_text(encoding="utf-8")).replace("/*DATA*/null", (here / "data.json").read_text(encoding="utf-8"))
page = (here / "template_head.html").read_text(encoding="utf-8") + "<script>\n" + js + "</script>\n"
(here / "out").mkdir(exist_ok=True)
(here / "out" / "keystone_trails.html").write_text(page, encoding="utf-8")
print(len(page))
