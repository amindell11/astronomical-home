import json
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

from trainer_runtime.contract import (
    CheckpointManifestEntry,
    RunManifest,
    checkpoint_manifest_path,
    manifest_path,
    read_checkpoint_manifest,
    read_manifest,
    read_summaries,
    summaries_path,
    write_manifest,
)


class RunContractTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.results_dir = Path(self.temp.name).resolve()
        self.run_id = "contract-run"
        self.run_dir = self.results_dir / self.run_id
        self.run_dir.mkdir()

    def tearDown(self):
        self.temp.cleanup()

    def test_manifest_round_trip_preserves_owned_fields(self):
        expected = RunManifest(
            run_id=self.run_id,
            behavior="ShipCombat",
            results_dir=self.results_dir,
            started_at=datetime(2026, 8, 5, 18, 30, tzinfo=timezone.utc),
            max_steps=4000,
            mode="self-play",
            config_hash="a" * 64,
        )
        path = manifest_path(self.results_dir, self.run_id)

        write_manifest(path, expected)

        self.assertEqual(expected, read_manifest(path))

    def test_summary_reader_ignores_only_an_unterminated_tail(self):
        complete = {
            "behavior": "ShipCombat",
            "step": 1000,
            "elapsedSeconds": 12.5,
            "maxSteps": 4000,
            "meanReward": -0.25,
            "rewardStdDev": 0.5,
            "elo": None,
            "isTraining": True,
        }
        path = summaries_path(self.run_dir)
        path.write_text(json.dumps(complete) + "\n{\"step\":", encoding="utf-8")

        summaries = read_summaries(path)

        self.assertEqual(1, len(summaries))
        self.assertEqual(1000, summaries[0].step)
        self.assertEqual(-0.25, summaries[0].mean_reward)

    def test_checkpoint_manifest_dedupes_by_step_last_line_wins(self):
        path = checkpoint_manifest_path(self.run_dir)
        path.write_text(
            '{"step":100,"onnx":"ShipCombat/old.onnx","pt":"ShipCombat/old.pt",'
            '"completedAt":"2026-08-06T01:00:00Z"}\n'
            '{"step":100,"onnx":"ShipCombat/new.onnx","pt":"ShipCombat/new.pt",'
            '"completedAt":"2026-08-06T02:00:00Z"}\n'
            '{"step":200,"onnx":"ShipCombat/200.onnx","pt":"ShipCombat/200.pt",'
            '"completedAt":"2026-08-06T03:00:00Z"}\n{"step":',
            encoding="utf-8",
        )

        entries = read_checkpoint_manifest(path)

        self.assertEqual([100, 200], [entry.step for entry in entries])
        self.assertEqual(Path("ShipCombat/new.onnx"), entries[0].onnx)


if __name__ == "__main__":
    unittest.main()
