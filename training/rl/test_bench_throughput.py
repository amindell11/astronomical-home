"""Unit tests for bench_throughput's arm selection and the stock-arm summary reader.

    .venv\\Scripts\\python -m unittest test_bench_throughput -v
"""
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

import bench_throughput
from trainer_runtime.contract import config_sha256

STOCK_LOG = """\
 Version information:
  ml-agents: 1.1.0,
[INFO] Listening on port 5006. Start training by pressing the Play button in the Unity Editor.
[INFO] ShipCombat. Step: 4000. Time Elapsed: 62.917 s. No episode was completed since last summary. Training.
[INFO] ShipCombat. Step: 8000. Time Elapsed: 101.250 s. Mean Reward: -1.023. Std of Reward: 1.911. Training.
[INFO] ShipCombat. Step: 12000. Time Elapsed: 140.083 s. Mean Reward: 0.412. Std of Reward: 1.208. Training. ELO: 1207.334.
noise line without a summary
[INFO] Exported results/rl-training/bench-x/ShipCombat/ShipCombat-24000.onnx
"""


class StockSummaryReaderTests(unittest.TestCase):
    def test_parses_step_elapsed_from_all_line_variants(self):
        with tempfile.TemporaryDirectory() as tmp:
            log = Path(tmp) / "bench-x-parallel-trainer.log"
            log.write_text(STOCK_LOG, encoding="utf-8")
            pairs = bench_throughput.parse_stock_summaries(log)
        self.assertEqual([(4000, 62.917), (8000, 101.250), (12000, 140.083)], pairs)

    def test_missing_log_reads_as_no_summaries(self):
        pairs = bench_throughput.parse_stock_summaries(Path("does/not/exist.log"))
        self.assertEqual([], pairs)

    def test_steady_rate_from_stock_pairs_drops_boot_interval(self):
        pairs = [(4000, 40.0), (8000, 100.0), (12000, 140.0), (16000, 180.0)]
        rate, spread, intervals = bench_throughput.steady_steps_per_second(pairs)
        self.assertEqual(2, intervals)
        self.assertAlmostEqual(100.0, rate)
        self.assertAlmostEqual(0.0, spread)


class ArmSelectionTests(unittest.TestCase):
    def test_stock_arm_reads_trainer_log_and_hashes_config_itself(self):
        with tempfile.TemporaryDirectory() as tmp:
            config = Path(tmp) / "bench-x.yaml"
            config.write_text("behaviors: {}\n", encoding="utf-8")
            log = Path(tmp) / "bench-x-parallel-trainer.log"
            log.write_text(STOCK_LOG, encoding="utf-8")
            original = bench_throughput.trainer_log_path
            bench_throughput.trainer_log_path = lambda run_id: log
            try:
                summaries, config_hash = bench_throughput.collect_run_metrics(
                    "ml-agents", "bench-x", config)
            finally:
                bench_throughput.trainer_log_path = original
            expected_hash = config_sha256(config)
        self.assertEqual(3, len(summaries))
        self.assertEqual(expected_hash, config_hash)

    def test_runtime_flag_reaches_the_launcher_command(self):
        args = SimpleNamespace(num_envs=6, num_arenas=1, trainer_runtime="ml-agents",
                               self_play=False, initialize_from=None, env=None,
                               microbatch_worker_cap=6)
        cmd = bench_throughput.launch_command(args, Path("bench-x.yaml"))
        self.assertIn("ml-agents", bench_throughput.TRAINER_RUNTIMES)
        runtime_at = cmd.index("--trainer-runtime")
        self.assertEqual("ml-agents", cmd[runtime_at + 1])
        self.assertNotIn("--microbatch-worker-cap", cmd)

    def test_owned_cap_reaches_the_launcher_command(self):
        args = SimpleNamespace(num_envs=6, num_arenas=1, trainer_runtime="owned",
                               self_play=False, initialize_from=None, env=None,
                               microbatch_worker_cap=6)
        cmd = bench_throughput.launch_command(args, Path("bench-x.yaml"))
        cap_at = cmd.index("--microbatch-worker-cap")
        self.assertEqual("6", cmd[cap_at + 1])

    def test_reads_microbatch_fill_from_owned_timers(self):
        with tempfile.TemporaryDirectory() as tmp:
            original = bench_throughput.RESULTS
            bench_throughput.RESULTS = Path(tmp)
            run_logs = Path(tmp) / "run" / "run_logs"
            run_logs.mkdir(parents=True)
            metadata = {
                key: "1.5" if "mean" in key else "6"
                for key in bench_throughput.MICROBATCH_TIMER_FIELDS
            }
            (run_logs / "timers.json").write_text(
                json.dumps({"metadata": metadata}), encoding="utf-8"
            )
            try:
                metrics = bench_throughput.collect_microbatch_metrics("owned", "run")
            finally:
                bench_throughput.RESULTS = original
        self.assertEqual(1.5, metrics["microbatch_mean_workers_per_forward"])
        self.assertEqual(6, metrics["microbatch_policy_forward_count"])


if __name__ == "__main__":
    unittest.main()
