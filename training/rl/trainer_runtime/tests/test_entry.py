import hashlib
import os
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from mlagents.trainers.stats import StatsSummary
from mlagents_envs.side_channel.stats_side_channel import StatsAggregationMethod

from run_parallel import MLAGENTS, trainer_command
from trainer_runtime.contract import manifest_path, read_manifest, read_summaries, summaries_path
from trainer_runtime.entry import _mode, owned_stats_writers, refuse_conflicting_restore
from trainer_runtime.stats_writer import JsonlStatsWriter


class OwnedEntryTests(unittest.TestCase):
    def test_restore_conflict_fails_before_env_args(self):
        with self.assertRaisesRegex(SystemExit, "mutually exclusive"):
            refuse_conflicting_restore(["config.yaml", "--resume", "--initialize-from", "seed"])

    def test_restore_words_forwarded_to_unity_are_not_runtime_flags(self):
        refuse_conflicting_restore([
            "config.yaml", "--resume", "--env-args", "--initialize-from", "unity-value"
        ])

    def test_launcher_runtime_selector_uses_owned_module_or_stock_executable(self):
        self.assertEqual([os.sys.executable, "-m", "trainer_runtime.entry"], trainer_command("owned"))
        self.assertEqual([str(MLAGENTS)], trainer_command("ml-agents"))

    def test_hybrid_mode_is_explicit_instead_of_inferred_from_elo(self):
        with patch.dict(os.environ, {"RL_HYBRID_SCRIPTED_WORKERS": "2"}):
            self.assertEqual("hybrid", _mode(self_play=True))

    def test_plugin_writes_manifest_and_structured_summary(self):
        with tempfile.TemporaryDirectory() as temp:
            results_dir = Path(temp).resolve()
            run_dir = results_dir / "owned-run"
            run_dir.mkdir()
            config = results_dir / "config.yaml"
            config.write_text("behaviors: {}\n", encoding="utf-8")
            settings = SimpleNamespace(max_steps=4000, self_play=None)
            checkpoint = SimpleNamespace(
                results_dir=str(results_dir), run_id="owned-run", resume=False
            )
            options = SimpleNamespace(
                behaviors={"ShipCombat": settings}, checkpoint_settings=checkpoint
            )

            writers = owned_stats_writers(
                options, config, datetime(2026, 8, 5, 19, 0, tzinfo=timezone.utc)
            )
            writers[0].write_stats("ShipCombat", {
                "Environment/Cumulative Reward": StatsSummary(
                    [-1.0, 1.0], StatsAggregationMethod.AVERAGE
                ),
                "Self-play/ELO": StatsSummary([1210.0], StatsAggregationMethod.MOST_RECENT),
                "Is Training": StatsSummary([1.0], StatsAggregationMethod.MOST_RECENT),
            }, 1000)

            manifest = read_manifest(manifest_path(results_dir, "owned-run"))
            summaries = read_summaries(summaries_path(manifest.run_dir))
            self.assertEqual("ShipCombat", manifest.behavior)
            self.assertEqual("scripted", manifest.mode)
            self.assertEqual(hashlib.sha256(config.read_bytes()).hexdigest(), manifest.config_hash)
            self.assertEqual(1000, summaries[0].step)
            self.assertEqual(0.0, summaries[0].mean_reward)
            self.assertEqual(1.0, summaries[0].reward_std_dev)
            self.assertEqual(1210.0, summaries[0].elo)
            self.assertTrue(summaries[0].is_training)

    def test_resume_keeps_elapsed_monotonic_across_legs(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "summaries.jsonl"
            first_leg = JsonlStatsWriter(path, "ShipCombat", 4000, resume=False)
            first_leg.started -= 500.0
            first_leg.write_stats("ShipCombat", {}, 1000)

            resumed = JsonlStatsWriter(path, "ShipCombat", 4000, resume=True)
            resumed.write_stats("ShipCombat", {}, 2000)

            rows = read_summaries(path)
            self.assertEqual([1000, 2000], [row.step for row in rows])
            self.assertGreaterEqual(rows[1].elapsed_seconds, rows[0].elapsed_seconds)


if __name__ == "__main__":
    unittest.main()
