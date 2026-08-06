import json
import tempfile
import unittest
from collections import defaultdict
from pathlib import Path
from types import SimpleNamespace

from mlagents.trainers.training_status import GlobalTrainingStatus, StatusType

from trainer_runtime.contract import checkpoint_manifest_path, read_checkpoint_manifest
from trainer_runtime.publish import AtomicTorchModelSaver, CheckpointCommitter


class FakeModule:
    def state_dict(self):
        return {"weight": 7}


class PublishAtomicityTests(unittest.TestCase):
    def setUp(self):
        GlobalTrainingStatus.saved_state = defaultdict(lambda: {})
        self.temp = tempfile.TemporaryDirectory()
        self.run_dir = Path(self.temp.name)
        self.behavior_dir = self.run_dir / "ShipCombat"
        self.settings = SimpleNamespace(init_path=None, keep_checkpoints=2)

    def tearDown(self):
        self.temp.cleanup()

    def saver(self, hook=lambda _stage: None):
        saver = AtomicTorchModelSaver(
            self.settings, str(self.behavior_dir), stage_hook=hook
        )
        saver.modules["fake"] = FakeModule()

        def export(stem, _behavior):
            Path(stem + ".onnx").write_bytes(b"onnx")

        saver.export = export
        return saver

    def test_saver_publishes_interval_artifacts_and_only_stages_resume_pointer(self):
        stages = []
        saver = self.saver(stages.append)

        onnx, auxiliary = saver.save_checkpoint("ShipCombat", 100)

        self.assertEqual(["interval_pt", "interval_onnx", "pointer_staged"], stages)
        self.assertTrue(Path(onnx).exists())
        self.assertTrue(Path(auxiliary[0]).exists())
        self.assertTrue((self.behavior_dir / "checkpoint.pt.tmp").exists())
        self.assertFalse((self.behavior_dir / "checkpoint.pt").exists())

    def test_saver_kill_points_never_expose_an_uncommitted_pointer(self):
        for kill_stage in ("interval_pt", "interval_onnx", "pointer_staged"):
            with self.subTest(kill_stage=kill_stage):
                case_dir = self.run_dir / kill_stage / "ShipCombat"
                settings = SimpleNamespace(init_path=None, keep_checkpoints=2)

                def hook(stage):
                    if stage == kill_stage:
                        raise SimulatedKill(stage)

                saver = AtomicTorchModelSaver(settings, str(case_dir), stage_hook=hook)
                saver.modules["fake"] = FakeModule()
                saver.export = lambda stem, _behavior: Path(stem + ".onnx").write_bytes(b"onnx")

                with self.assertRaises(SimulatedKill):
                    saver.save_checkpoint("ShipCombat", 100)

                self.assertFalse((case_dir / "checkpoint.pt").exists())

    def test_commit_tail_orders_manifest_status_then_pointer_and_mirrors_elo(self):
        saver = self.saver()
        saver.save_checkpoint("ShipCombat", 100)
        stages = []
        status_path = self.run_dir / "run_logs" / "training_status.json"
        committer = CheckpointCommitter(
            self.run_dir, status_path, resume=False, stage_hook=stages.append
        )
        trainer = SimpleNamespace(brain_name="ShipCombat", current_elo=1234.5)

        committer.commit(saver, trainer)

        self.assertEqual(["manifest", "status", "pointer"], stages)
        self.assertTrue((self.behavior_dir / "checkpoint.pt").exists())
        entries = read_checkpoint_manifest(checkpoint_manifest_path(self.run_dir))
        self.assertEqual([100], [entry.step for entry in entries])
        state = json.loads(status_path.read_text(encoding="utf-8"))
        self.assertEqual(1234.5, state["ShipCombat"][StatusType.ELO.value])

    def test_commit_tail_kill_points_preserve_last_pointer(self):
        for kill_stage in ("manifest", "status", "pointer"):
            with self.subTest(kill_stage=kill_stage):
                case_dir = self.run_dir / kill_stage
                saver = AtomicTorchModelSaver(
                    self.settings, str(case_dir / "ShipCombat")
                )
                saver.modules["fake"] = FakeModule()
                saver.export = lambda stem, _behavior: Path(stem + ".onnx").write_bytes(b"onnx")
                saver.save_checkpoint("ShipCombat", 200)

                def hook(stage):
                    if stage == kill_stage:
                        raise SimulatedKill(stage)

                committer = CheckpointCommitter(
                    case_dir, case_dir / "run_logs" / "training_status.json",
                    resume=False, stage_hook=hook,
                )
                with self.assertRaises(SimulatedKill):
                    committer.commit(saver, SimpleNamespace(brain_name="ShipCombat"))

                pointer = case_dir / "ShipCombat" / "checkpoint.pt"
                self.assertEqual(kill_stage == "pointer", pointer.exists())

    def test_resume_open_repairs_torn_manifest_before_append(self):
        path = checkpoint_manifest_path(self.run_dir)
        path.write_text(
            '{"step":1,"onnx":"ShipCombat/1.onnx","pt":"ShipCombat/1.pt",'
            '"completedAt":"2026-08-06T01:00:00Z"}\n{"step":',
            encoding="utf-8",
        )
        saver = self.saver()
        saver.save_checkpoint("ShipCombat", 2)
        committer = CheckpointCommitter(
            self.run_dir, self.run_dir / "run_logs" / "training_status.json", resume=True
        )

        committer.commit(saver, SimpleNamespace(brain_name="ShipCombat"))

        self.assertEqual([1, 2], [entry.step for entry in read_checkpoint_manifest(path)])


class SimulatedKill(Exception):
    pass


if __name__ == "__main__":
    unittest.main()
