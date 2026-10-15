import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import ship_contract as contract

VALID = {"schema_version": 1, "name": "Fixture", "source": "Fixture.blend", "texture_size": 1024,
         "uv_density_max_ratio": 2.0, "contour_roles": ["hull"],
         "exemptions": [{"rule": "scale", "parts": ["Wing"], "reason": "Owner-approved stretch."}]}
VALID_PALETTES = {"color_space": "linear-sRGB", "default": "test",
                  "palettes": {"test": {"Zinc": [0.7, 0.7, 0.72, 1], "Glass": [0.03, 0.04, 0.06, 1]},
                               "alt": {"Zinc": [0.2, 0.5, 0.3, 1], "Glass": [0.03, 0.04, 0.06, 1]}}}
VALID_PAINT = {
    "schema_version": 1,
    "brush": {"image": "brush.png", "scale": 1.3, "contrast": 3.2, "blend": 0.25},
    "ramp": [[0.1, 0.38], [0.55, 0.65], [0.92, 1.0]],
    "sweep": {"weight_x": 0.65, "dark_below": 0.33, "light_above": 0.66, "jitter": 0.5, "dark": 0.61, "light": 1.0},
    "ink": {"inset": 0.003, "rim": 0.0065, "rim_light": 0.35, "wall_facing": 0.65},
    "plates": {"cap_facing": 0.9, "thin_wall": 0.12},
    "regions": {"Zinc": {"plate": True, "sweep": True, "ink_walls": False},
                "Glass": {"plate": False, "sweep": False, "ink_walls": False}},
    "canopy_glass": ["Canopy"], "canopy_frames": [],
    "accents": [{"part": "Core", "target": "light", "where": {"x": [0.025, 0.05]}, "value": 1.0},
                {"part": "Core", "target": "emission", "where": {"y": [None, -0.9]}, "value": 1.0, "color": [0.1, 0.66, 0.13]}],
    "material": {"shadow_strength": 1.0, "light_strength": 1.0, "ink_strength": 1.0, "light_lift": 0.25,
                 "ink_color": [0.019, 0.023, 0.029], "emission_blend": 1.0,
                 "lighting": {"_ShadowThreshold": 0.35, "_ShadowColor": [0.5, 0.6, 0.72]}},
}


def mutated(change, valid=VALID):
    value = copy.deepcopy(valid)
    change(value)
    return value


class ShipJsonTests(unittest.TestCase):
    def test_valid_ship_parses_and_resolves_source_beside_ship_json(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "ship.json"
            path.write_text(json.dumps(VALID), encoding="utf-8")
            ship, source = contract.load_ship(path)
        self.assertEqual(ship, VALID)
        self.assertEqual(source, (Path(folder) / "Fixture.blend").resolve())

    def test_rejections(self):
        cases = {
            "unknown field": lambda v: v.update(extra=1),
            "missing field": lambda v: v.pop("exemptions"),
            "schema version": lambda v: v.update(schema_version=2),
            "name": lambda v: v.update(name="ship 3"),
            "source suffix": lambda v: v.update(source="Fixture.fbx"),
            "texture not power of two": lambda v: v.update(texture_size=1000),
            "texture bool": lambda v: v.update(texture_size=True),
            "ratio not above 1": lambda v: v.update(uv_density_max_ratio=1),
            "ratio nan": lambda v: v.update(uv_density_max_ratio=float("nan")),
            "contour collider": lambda v: v.update(contour_roles=["collider"]),
            "contour duplicate": lambda v: v.update(contour_roles=["hull", "hull"]),
            "exemption unknown rule": lambda v: v["exemptions"][0].update(rule="looks_wrong"),
            "exemption empty parts": lambda v: v["exemptions"][0].update(parts=[]),
            "exemption blank reason": lambda v: v["exemptions"][0].update(reason=" "),
            "exemption extra field": lambda v: v["exemptions"][0].update(expires="never"),
        }
        for label, change in cases.items():
            with self.subTest(label), self.assertRaises(ValueError):
                contract.parse_ship(mutated(change))

    def test_exemptions_split_findings_by_rule_and_part(self):
        findings = [{"rule": "scale", "part": "Wing", "message": "m"},
                    {"rule": "scale", "part": "Fin", "message": "m"},
                    {"rule": "name", "part": "Wing", "message": "m"}]
        open_findings, exempted = contract.apply_exemptions(findings, VALID["exemptions"])
        self.assertEqual([(f["rule"], f["part"]) for f in open_findings], [("scale", "Fin"), ("name", "Wing")])
        self.assertEqual(exempted[0]["reason"], "Owner-approved stretch.")


class PaintSettingsTests(unittest.TestCase):
    def test_valid_settings_parse(self):
        self.assertEqual(contract.parse_palettes(VALID_PALETTES), VALID_PALETTES)
        self.assertEqual(contract.parse_paint(VALID_PAINT), VALID_PAINT)

    def test_palettes_rejections(self):
        cases = {
            "unknown field": lambda v: v.update(notes="x"),
            "color space": lambda v: v.update(color_space="sRGB"),
            "default missing": lambda v: v.update(default="none"),
            "no palettes": lambda v: v.update(palettes={}),
            "regions differ": lambda v: v["palettes"]["alt"].pop("Glass"),
            "colour length": lambda v: v["palettes"]["test"].update(Zinc=[1, 1, 1]),
            "colour range": lambda v: v["palettes"]["test"].update(Zinc=[1, 1, 1, 2]),
            "colour bool": lambda v: v["palettes"]["test"].update(Zinc=[True, 1, 1, 1]),
        }
        for label, change in cases.items():
            with self.subTest(label), self.assertRaises(ValueError):
                contract.parse_palettes(mutated(change, VALID_PALETTES))

    def test_paint_rejections(self):
        cases = {
            "unknown field": lambda v: v.update(wear=True),
            "canopy type": lambda v: v.update(canopy_glass="Canopy"),
            "canopy blank": lambda v: v.update(canopy_glass=[""]),
            "canopy duplicate": lambda v: v.update(canopy_frames=["Canopy"]),
            "missing field": lambda v: v.pop("accents"),
            "schema version": lambda v: v.update(schema_version=2),
            "brush extra": lambda v: v["brush"].update(rotate=1),
            "brush scale zero": lambda v: v["brush"].update(scale=0),
            "brush blend above 1": lambda v: v["brush"].update(blend=1.5),
            "ramp one stop": lambda v: v.update(ramp=[[0.5, 0.5]]),
            "ramp not increasing": lambda v: v.update(ramp=[[0.5, 0.5], [0.5, 1.0]]),
            "ramp value range": lambda v: v.update(ramp=[[0.1, 0.5], [0.9, 1.5]]),
            "sweep thirds crossed": lambda v: v["sweep"].update(dark_below=0.7),
            "sweep missing": lambda v: v["sweep"].pop("jitter"),
            "ink rim below inset": lambda v: v["ink"].update(rim=0.001),
            "ink wall facing zero": lambda v: v["ink"].update(wall_facing=0),
            "plates cap facing above 1": lambda v: v["plates"].update(cap_facing=1.2),
            "plates thin wall negative": lambda v: v["plates"].update(thin_wall=-0.1),
            "regions empty": lambda v: v.update(regions={}),
            "region flag missing": lambda v: v["regions"]["Zinc"].pop("sweep"),
            "region flag not bool": lambda v: v["regions"]["Zinc"].update(plate=1),
            "accent target": lambda v: v["accents"][0].update(target="glow"),
            "accent extra field": lambda v: v["accents"][0].update(side="L"),
            "accent colour on light": lambda v: v["accents"][0].update(color=[1, 1, 1]),
            "accent emission without colour": lambda v: v["accents"][1].pop("color"),
            "accent where axis": lambda v: v["accents"][0].update(where={"w": [0, 1]}),
            "accent where empty": lambda v: v["accents"][0].update(where={}),
            "accent where crossed": lambda v: v["accents"][0].update(where={"x": [0.5, 0.1]}),
            "accent where unbounded": lambda v: v["accents"][0].update(where={"x": [None, None]}),
            "accent where shape": lambda v: v["accents"][0].update(where={"x": [0.1]}),
            "accent value range": lambda v: v["accents"][0].update(value=2),
            "material strength range": lambda v: v["material"].update(shadow_strength=3),
            "material ink colour": lambda v: v["material"].update(ink_color=[0, 0]),
            "material lighting unknown": lambda v: v["material"]["lighting"].update(_WearStrength=1),
            "material lighting colour shape": lambda v: v["material"]["lighting"].update(_ShadowColor=[1, 1]),
        }
        for label, change in cases.items():
            with self.subTest(label), self.assertRaises(ValueError):
                contract.parse_paint(mutated(change, VALID_PAINT))

    def test_settings_parse_failure_is_a_usage_error(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "paint.json"
            path.write_text(json.dumps(mutated(lambda v: v.update(wear=True), VALID_PAINT)), encoding="utf-8")
            with self.assertRaises(contract.UsageError):
                contract.load_settings(path, contract.parse_paint)
            for name, parser in (("paint.json", contract.parse_paint), ("palettes.json", contract.parse_palettes)):
                with self.subTest(required=name), self.assertRaises(contract.UsageError):
                    contract.load_settings(Path(folder) / "missing" / name, parser)


class RoleGrammarTests(unittest.TestCase):
    def test_static_reserved_and_plain_collections(self):
        self.assertEqual(contract.parse_role("role.hull"), ("hull", None))
        self.assertEqual(contract.parse_role("role.sockets"), ("sockets", None))
        self.assertEqual(contract.parse_role("role.move.wing.L"), ("move", "wing.L"))
        self.assertEqual(contract.parse_role("role.debris.nose"), ("debris", "nose"))
        self.assertIsNone(contract.parse_role("editing"))

    def test_unknown_roles_are_errors(self):
        for name in ("role.wings", "role.hull.001", "role.move.", "role.debris.a b", "role."):
            with self.subTest(name), self.assertRaises(ValueError):
                contract.parse_role(name)


class RunnerTests(unittest.TestCase):
    def run_tool(self, body):
        script = (f"import sys; sys.path.insert(0, {str(TOOLS)!r}); import ship_contract as c\n{body}\n"
                  "c.run(main, ['x', '--', '--ship', 's.json'])")
        return subprocess.run([sys.executable, "-c", script], capture_output=True, text=True)

    def test_argparse_errors_exit_1_with_empty_stdout(self):
        result = self.run_tool("def main(argv):\n    c.tool_parser('t').parse_args(argv)")
        self.assertEqual((result.returncode, result.stdout), (1, ""))

    def test_refusal_exits_3_with_trailers_only_on_stdout(self):
        with tempfile.TemporaryDirectory() as folder:
            result = self.run_tool(f"def main(argv):\n    print('noise')\n"
                                   f"    raise c.Report('t', {folder!r}, '5.1', []).refuse('no')")
            report = json.loads((Path(folder) / "t.json").read_text(encoding="utf-8"))
        self.assertEqual(result.returncode, 3)
        self.assertEqual(result.stdout.splitlines(), ["SHIP_VERDICT=fail", f"SHIP_REPORT={Path(folder).resolve() / 't.json'}"])
        self.assertEqual((report["verdict"], report["refusal"], report["recipe"]), ("fail", "no", contract.FINGERPRINT_RECIPE))


if __name__ == "__main__":
    unittest.main()
