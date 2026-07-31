"""Unit tests for the calibration-bundle boundary (stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_bundle -v
"""
import json
import tempfile
import unittest
from pathlib import Path

from eval_bundle import Thresholds, load_bundle


def v1_payload():
    return {
        "bundleId": "eval-bundle-v1",
        "seeds": [2001, 2002, 2003, 2004, 2005],
        "episodesPerSeed": 3,
        "thresholds": {"alertEvaderWins": 10, "evaderEpisodes": 15,
                       "minTotalWins": 55, "totalEpisodes": 75},
        "armingPredicate": "healthy",
        "kWatch": 1,
        "kConfirm": 2,
        "kBank": 5,
        "executionMode": "parallel",
    }


class CommittedBundleV1(unittest.TestCase):
    """Pin test: v1 IS today's protocol verbatim; recalibration means a v2 file, not an edit."""

    def test_v1_carries_the_frozen_values(self):
        bundle = load_bundle()
        self.assertEqual("eval-bundle-v1", bundle.bundle_id)
        self.assertEqual((2001, 2002, 2003, 2004, 2005), bundle.seeds)
        self.assertEqual(3, bundle.episodes_per_seed)
        self.assertEqual(Thresholds(10, 15, 55, 75), bundle.thresholds)
        self.assertEqual("healthy", bundle.arming_predicate)
        self.assertEqual((1, 2, 5), (bundle.k_watch, bundle.k_confirm, bundle.k_bank))
        self.assertEqual("parallel", bundle.execution_mode)


class BundleValidation(unittest.TestCase):
    def setUp(self):
        self.path = Path(tempfile.mkdtemp()) / "bundle.json"

    def load(self, payload):
        self.path.write_text(json.dumps(payload))
        return load_bundle(self.path)

    def assert_refuses(self, payload):
        with self.assertRaises(SystemExit):
            self.load(payload)

    def test_valid_payload_loads(self):
        self.assertEqual("eval-bundle-v1", self.load(v1_payload()).bundle_id)

    def test_missing_file_fails(self):
        with self.assertRaises(SystemExit):
            load_bundle(self.path / "nowhere.json")

    def test_every_field_is_required(self):
        for name in v1_payload():
            payload = v1_payload()
            del payload[name]
            self.assert_refuses(payload)

    def test_seed_count_must_match_the_evader_cell(self):
        payload = v1_payload()
        payload["seeds"] = [2001, 2002, 2003]
        self.assert_refuses(payload)

    def test_duplicate_seeds_refuse(self):
        payload = v1_payload()
        payload["seeds"] = [2001, 2001, 2002, 2003, 2004]
        self.assert_refuses(payload)

    def test_total_must_be_whole_per_opponent_blocks(self):
        payload = v1_payload()
        payload["thresholds"]["totalEpisodes"] = 76
        self.assert_refuses(payload)

    def test_thresholds_must_be_ints(self):
        payload = v1_payload()
        payload["thresholds"]["alertEvaderWins"] = "10"
        self.assert_refuses(payload)

    def test_alert_threshold_must_sit_inside_the_cell(self):
        payload = v1_payload()
        payload["thresholds"]["alertEvaderWins"] = 15
        self.assert_refuses(payload)

    def test_unknown_arming_predicate_refuses(self):
        payload = v1_payload()
        payload["armingPredicate"] = "always"
        self.assert_refuses(payload)

    def test_replicate_counts_have_floors(self):
        for name, bad in (("kWatch", 0), ("kConfirm", 0), ("kBank", 1)):
            payload = v1_payload()
            payload[name] = bad
            self.assert_refuses(payload)

    def test_bool_is_not_an_int(self):
        payload = v1_payload()
        payload["kWatch"] = True
        self.assert_refuses(payload)


if __name__ == "__main__":
    unittest.main()
