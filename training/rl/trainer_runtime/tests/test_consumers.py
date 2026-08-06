import importlib.util
import json
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

from trainer_runtime.contract import RunManifest, manifest_path, summaries_path, write_manifest

RL_DIR = Path(__file__).resolve().parents[2]
SERVER_PATH = RL_DIR.parent.parent / "dev" / "rl-status" / "server.py"


def load_status_server():
    spec = importlib.util.spec_from_file_location("rl_status_server_for_test", SERVER_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class StructuredConsumerTests(unittest.TestCase):
    def test_dashboard_reads_manifest_and_jsonl_not_summary_stdout(self):
        server = load_status_server()
        with tempfile.TemporaryDirectory() as temp:
            results_dir = Path(temp).resolve()
            run_id = "dashboard-run"
            run_dir = results_dir / run_id
            run_dir.mkdir()
            manifest = RunManifest(
                run_id=run_id,
                behavior="ShipCombat",
                results_dir=results_dir,
                started_at=datetime.now(timezone.utc),
                max_steps=4000,
                mode="hybrid",
                config_hash="b" * 64,
            )
            write_manifest(manifest_path(results_dir, run_id), manifest)
            summary = {
                "behavior": "ShipCombat",
                "step": 2000,
                "elapsedSeconds": 20.0,
                "maxSteps": 4000,
                "meanReward": 0.125,
                "rewardStdDev": 0.25,
                "elo": 1205.0,
                "isTraining": True,
            }
            summaries_path(run_dir).write_text(json.dumps(summary) + "\n", encoding="utf-8")
            (results_dir / f"{run_id}-parallel-trainer.log").write_text(
                "ShipCombat. Step: 9999. Time Elapsed: 1 s. Mean Reward: 999.\n"
                "Parameter 'field_density_scale' is in lesson '2' and has value '1.5'.\n",
                encoding="utf-8",
            )
            server.TRAINING_RESULTS = results_dir

            state = server.trainer_state(run_id)

            self.assertEqual([(2000, 20.0, 0.125, 1205.0)], state["summaries"])
            self.assertEqual(4000, state["max_steps"])
            self.assertEqual("hybrid", state["mode"])
            self.assertEqual(("2", "1.5"), state["lessons"]["field_density_scale"])


if __name__ == "__main__":
    unittest.main()
