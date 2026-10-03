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


def mutated(change):
    value = copy.deepcopy(VALID)
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
