"""Unit tests for the eval stats primitives against independently known values.

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_stats -v
"""
import math
import unittest

from eval_stats import binom_cdf, mcnemar_exact_p, mean_difference, wilson_interval


class WilsonInterval(unittest.TestCase):
    def test_symmetric_midpoint_case(self):
        # Textbook Wilson 95% for 5/10.
        lb, ub = wilson_interval(5, 10)
        self.assertAlmostEqual(0.2366, lb, places=3)
        self.assertAlmostEqual(0.7634, ub, places=3)

    def test_zero_wins_upper_bound_is_z2_over_n_plus_z2(self):
        lb, ub = wilson_interval(0, 10)
        self.assertEqual(0.0, lb)
        z2 = 1.959964 ** 2
        self.assertAlmostEqual(z2 / (10 + z2), ub, places=6)

    def test_all_wins_mirrors_zero_wins(self):
        lb, ub = wilson_interval(10, 10)
        zero_lb, zero_ub = wilson_interval(0, 10)
        self.assertAlmostEqual(1.0 - zero_ub, lb, places=9)
        self.assertAlmostEqual(1.0, ub, places=12)

    def test_bounds_stay_in_the_unit_interval(self):
        for wins in range(16):
            lb, ub = wilson_interval(wins, 15)
            self.assertTrue(0.0 <= lb <= ub <= 1.0)

    def test_more_trials_tighten_the_interval(self):
        single = wilson_interval(10, 15)
        pooled = wilson_interval(30, 45)
        self.assertLess(pooled[1] - pooled[0], single[1] - single[0])

    def test_zero_trials_raise(self):
        with self.assertRaises(ValueError):
            wilson_interval(0, 0)

    def test_wins_out_of_range_raise(self):
        with self.assertRaises(ValueError):
            wilson_interval(16, 15)


class BinomCdf(unittest.TestCase):
    def test_exact_fair_coin_values(self):
        self.assertAlmostEqual(11 / 16, binom_cdf(2, 4), places=12)
        self.assertAlmostEqual(1.0, binom_cdf(4, 4), places=12)
        self.assertAlmostEqual(1 / 16, binom_cdf(0, 4), places=12)

    def test_negative_k_is_zero(self):
        self.assertEqual(0.0, binom_cdf(-1, 4))


class McNemarExact(unittest.TestCase):
    def test_no_discordant_pairs_is_no_evidence(self):
        self.assertEqual(1.0, mcnemar_exact_p(0, 0))

    def test_one_sided_sweep(self):
        self.assertAlmostEqual(0.0625, mcnemar_exact_p(5, 0), places=12)

    def test_known_exact_value(self):
        # b=1, c=4: 2 * P(X <= 1 | n=5, p=.5) = 2 * 6/32.
        self.assertAlmostEqual(0.375, mcnemar_exact_p(1, 4), places=12)

    def test_symmetry(self):
        self.assertEqual(mcnemar_exact_p(2, 7), mcnemar_exact_p(7, 2))

    def test_balanced_counts_clamp_to_one(self):
        self.assertEqual(1.0, mcnemar_exact_p(3, 3))

    def test_negative_counts_raise(self):
        with self.assertRaises(ValueError):
            mcnemar_exact_p(-1, 2)


class MeanDifference(unittest.TestCase):
    def test_diff_and_welch_se(self):
        diff = mean_difference([71, 72, 70], [68, 69, 67])
        self.assertAlmostEqual(71.0, diff.mean_a)
        self.assertAlmostEqual(68.0, diff.mean_b)
        self.assertAlmostEqual(3.0, diff.diff)
        self.assertAlmostEqual(math.sqrt(2 / 3), diff.se, places=9)

    def test_single_replicate_has_no_se(self):
        self.assertTrue(math.isnan(mean_difference([71], [68]).se))

    def test_empty_samples_raise(self):
        with self.assertRaises(ValueError):
            mean_difference([], [1])


if __name__ == "__main__":
    unittest.main()
