import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import skybox_preset
import skybox_unity


class PresetAndPublishingTests(unittest.TestCase):
    def test_approved_preset_roundtrip_is_independent_of_later_edits(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "sky.json"
            approved = skybox_preset.load(Path(__file__).resolve().parents[1] / "nebula-glow.json")
            skybox_preset.save(path, approved)
            loaded = skybox_preset.load(path)
            loaded["nebula"]["palette"][0][0] = 0.9
            self.assertEqual(skybox_preset.load(path), approved)
            self.assertEqual(approved["tiny_stars"]["brightness"], 0)
            self.assertEqual(approved["nebula"]["core_emission"], 1.5)

    def test_bad_presets_fail_before_scene_construction(self):
        for value in (-1, float("nan"), float("inf"), True):
            with self.subTest(value=value):
                preset = skybox_preset.defaults()
                preset["anchor_brightness"] = value
                with self.assertRaises(ValueError):
                    skybox_preset.parse(preset)
        for mutate in (
            lambda p: p.update(schema_version=2),
            lambda p: p["anchors"][0].update(direction=[0, 0, 0]),
            lambda p: p["nebula"].update(palette=[[0, 0, 0]]*4),
            lambda p: p["nebula"].update(stretch=[0, 1, 1]),
            lambda p: p.update(misspelled_setting=2),
        ):
            preset = skybox_preset.defaults()
            mutate(preset)
            with self.assertRaises(ValueError):
                skybox_preset.parse(preset)

    def test_publish_replaces_candidate_preserving_identity_and_final(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project = root / "Unity Project"
            (project / "Assets").mkdir(parents=True)
            (project / "ProjectSettings").mkdir()
            (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.1.8f1")
            source = root / "render"
            Path(str(source) + ".hdr").write_bytes(b"completed render")
            skybox_preset.save(str(source) + "_preset.json", skybox_preset.defaults(glow=True))
            Path(str(source) + "_render.json").write_text(json.dumps({"format":"HDR", "width":1024, "height":512}))
            candidate = skybox_unity.publish(source, project, "purple")
            meta = candidate.with_suffix(".hdr.meta")
            meta.write_text("guid: preserved")
            final = candidate.with_name("purple-8k.hdr")
            final.write_bytes(b"approved final")
            Path(str(source) + ".hdr").write_bytes(b"new completed render")
            self.assertEqual(skybox_unity.publish(source, project, "purple"), candidate)
            self.assertEqual(candidate.read_bytes(), b"new completed render")
            self.assertEqual(meta.read_text(), "guid: preserved")
            self.assertEqual(final.read_bytes(), b"approved final")
            with self.assertRaisesRegex(ValueError, "8K"):
                skybox_unity.publish(source, project, "purple", final=True)
            self.assertEqual(final.read_bytes(), b"approved final")
            with self.assertRaises(ValueError):
                skybox_unity.publish(source, project, "../escape")

    def test_partial_export_never_replaces_importable_hdr(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "Assets").mkdir()
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectVersion.txt").touch()
            source = root / "partial"
            Path(str(source) + "_render.json").write_text(json.dumps({"format":"HDR", "width":512, "height":256}))
            with self.assertRaises(FileNotFoundError):
                skybox_unity.publish(source, root, "test")
            self.assertFalse((root / skybox_unity.ASSET_FOLDER / "test-draft.hdr").exists())


if __name__ == "__main__":
    unittest.main()
