import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import flat_preset
import flat_unity

TOOL = Path(__file__).resolve().parents[1]
STAR_KEYS = {"tiny_stars", "anchor_brightness", "anchors"}


def v1_preset():
    value = copy.deepcopy(flat_preset.DEFAULT)
    value.update(schema_version=1, tiny_stars={"brightness": 0.0, "legacy_seed": 7319.0}, anchor_brightness=1.0,
                 anchors=[{"direction": [1, 0, 0], "radius": 0.05, "color": [1, 1, 1], "strength": 400.0}] * 5)
    return value


class PresetAndPublishingTests(unittest.TestCase):
    def test_approved_preset_roundtrip_is_independent_of_later_edits(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "sky.json"
            approved, _ = flat_preset.load(TOOL / "illustrated-blue.json")
            flat_preset.save(path, approved)
            loaded, _ = flat_preset.load(path)
            loaded["nebula"]["palette"][0][0] = 0.9
            self.assertEqual(flat_preset.load(path)[0], approved)
            self.assertEqual(approved["nebula"]["variation"], 17)

    def test_illustrated_blue_is_schema_2(self):
        raw = json.loads((TOOL / "illustrated-blue.json").read_text(encoding="utf-8"))
        self.assertEqual(raw["schema_version"], 2)
        self.assertEqual(set(raw), {"schema_version", "nebula"})
        preset, migration = flat_preset.load(TOOL / "illustrated-blue.json")
        self.assertEqual(preset, raw)
        self.assertEqual(migration, [])

    def test_v1_preset_drops_star_keys_with_migration_report(self):
        preset, migration = flat_preset.upgrade(v1_preset())
        self.assertEqual(preset, flat_preset.DEFAULT)
        self.assertEqual(migration, flat_preset.MIGRATION)
        self.assertTrue(any("tiny_stars, anchor_brightness and anchors" in line for line in migration))

    def test_v1_preset_still_rejects_unknown_fields(self):
        for mutate in (lambda p: p.update(misspelled_setting=2), lambda p: p.pop("anchors"),
                       lambda p: p["nebula"].update(scale=float("nan"))):
            preset = v1_preset()
            mutate(preset)
            with self.assertRaises(ValueError):
                flat_preset.upgrade(preset)

    def test_v2_write_has_no_star_keys_and_reads_without_migration(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "sky.json"
            flat_preset.save(path, flat_preset.upgrade(v1_preset())[0])
            raw = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(raw["schema_version"], 2)
            self.assertFalse(STAR_KEYS & set(raw))
            self.assertEqual(flat_preset.load(path), (raw, []))
            with self.assertRaises(ValueError):
                flat_preset.save(path, v1_preset())

    def test_palettes_are_repeatable_and_keep_dark_base_and_brightness(self):
        approved = flat_preset.defaults()["nebula"]["palette"]
        self.assertEqual(flat_preset.make_palette("GLOW"), approved)
        for scheme in flat_preset.PALETTES:
            base = flat_preset.make_palette(scheme)
            for seed in range(20):
                colors = flat_preset.make_palette(scheme, seed, 1)
                self.assertEqual(colors, flat_preset.make_palette(scheme, seed, 1))
                self.assertNotEqual(colors, flat_preset.make_palette(scheme, seed+1, 1))
                self.assertEqual(flat_preset.make_palette(scheme, seed, 0), base)
                for original, changed in zip(base, colors):
                    self.assertAlmostEqual(max(original), max(changed))
                self.assertLess(max(colors[0]), min(max(c) for c in colors[1:]))
                preset = flat_preset.defaults()
                preset["nebula"]["palette"] = colors
                self.assertEqual(flat_preset.parse(preset), preset)
        self.assertEqual(flat_preset.defaults()["nebula"]["palette"], approved)

    def test_bad_presets_fail_before_scene_construction(self):
        for value in (-1, float("nan"), float("inf"), True):
            with self.subTest(value=value):
                preset = flat_preset.defaults()
                preset["nebula"]["core_emission"] = value
                with self.assertRaises(ValueError):
                    flat_preset.upgrade(preset)
        for mutate in (
            lambda p: p.update(schema_version=3),
            lambda p: p.update(schema_version=True),
            lambda p: p.update(anchor_brightness=1.0),
            lambda p: p["nebula"].update(palette=[[0, 0, 0]]*4),
            lambda p: p["nebula"].update(stretch=[0, 1, 1]),
            lambda p: p.update(misspelled_setting=2),
        ):
            preset = flat_preset.defaults()
            mutate(preset)
            with self.assertRaises(ValueError):
                flat_preset.upgrade(preset)

    def test_flat_publish_replaces_sidecar_and_exr_only_for_matching_stage(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "Assets").mkdir()
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectVersion.txt").touch()
            source = root / "clouds-final"
            Path(str(source) + ".json").write_text(json.dumps({"stage": "final"}))
            Path(str(source) + ".exr").write_bytes(b"final clouds")
            published = flat_unity.publish(source, root, "clouds", True, **flat_unity.FLAT)
            self.assertEqual(published, root / flat_unity.FLAT_FOLDER / "clouds-final.exr")
            self.assertEqual(published.read_bytes(), b"final clouds")
            self.assertTrue(published.with_suffix(".json").is_file())
            meta = published.with_suffix(".exr.meta")
            meta.write_text("guid: preserved")
            Path(str(source) + ".exr").write_bytes(b"new clouds")
            self.assertEqual(flat_unity.publish(source, root, "clouds", True, **flat_unity.FLAT), published)
            self.assertEqual(published.read_bytes(), b"new clouds")
            self.assertEqual(meta.read_text(), "guid: preserved")
            with self.assertRaisesRegex(ValueError, "stage"):
                flat_unity.publish(source, root, "clouds", False, **flat_unity.FLAT)
            self.assertFalse((root / flat_unity.FLAT_FOLDER / "clouds-draft.exr").exists())
            with self.assertRaises(ValueError):
                flat_unity.publish(source, root, "../escape", True, **flat_unity.FLAT)

    def test_partial_export_never_replaces_importable_exr(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "Assets").mkdir()
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectVersion.txt").touch()
            source = root / "partial"
            Path(str(source) + ".json").write_text(json.dumps({"stage": "draft"}))
            with self.assertRaises(FileNotFoundError):
                flat_unity.publish(source, root, "test", False, **flat_unity.FLAT)
            self.assertFalse((root / flat_unity.FLAT_FOLDER / "test-draft.json").exists())


if __name__ == "__main__":
    unittest.main()
