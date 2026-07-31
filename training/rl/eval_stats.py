"""Statistical primitives behind the eval verdict layer: Wilson score intervals for
pooled replicate cells, exact McNemar for paired A/B episode outcomes, and the mean
difference over replicate totals. Pure functions, stdlib only (frozen: no scipy/numpy).

Caveat (frozen brief, blindsider 1): episodes cluster within seeds, so a pooled Wilson
interval mildly understates uncertainty; the gate thresholds are calibrated on the same
clustered structure, which absorbs it.
"""
from math import comb, sqrt
from statistics import mean, stdev
from typing import NamedTuple

# Matches EvalProtocol.WilsonLowerBound's z, so Python and the C# informational bound quote one interval.
Z95 = 1.959964


def wilson_interval(wins: int, trials: int, z: float = Z95) -> tuple:
    """Wilson score 95% interval on a binomial proportion; callers count draws as non-wins."""
    if trials <= 0:
        raise ValueError(f"wilson_interval needs trials >= 1, got {trials}")
    if not 0 <= wins <= trials:
        raise ValueError(f"wins {wins} outside 0..{trials}")
    p = wins / trials
    z2 = z * z
    denom = 1 + z2 / trials
    center = (p + z2 / (2 * trials)) / denom
    margin = z * sqrt(p * (1 - p) / trials + z2 / (4 * trials * trials)) / denom
    return (max(0.0, center - margin), min(1.0, center + margin))


def binom_cdf(k: int, n: int, p: float = 0.5) -> float:
    """Exact P(X <= k) for X ~ Binomial(n, p)."""
    if k < 0:
        return 0.0
    return sum(comb(n, i) * p ** i * (1 - p) ** (n - i) for i in range(min(k, n) + 1))


def mcnemar_exact_p(b: int, c: int) -> float:
    """Two-sided exact McNemar p over discordant-pair counts: under H0 the b one-sided
    wins among the b + c discordant pairs are Binomial(b + c, 1/2)."""
    if b < 0 or c < 0:
        raise ValueError(f"discordant counts must be >= 0, got {b}, {c}")
    n = b + c
    if n == 0:
        return 1.0
    return min(1.0, 2.0 * binom_cdf(min(b, c), n))


class MeanDiff(NamedTuple):
    mean_a: float
    mean_b: float
    diff: float
    se: float


def mean_difference(a, b) -> MeanDiff:
    """Difference of means over two replicate-total samples, with the Welch standard error
    (NaN when either side has a single replicate — no spread to estimate from)."""
    if not a or not b:
        raise ValueError("mean_difference needs non-empty samples")
    mean_a, mean_b = mean(a), mean(b)
    se = (sqrt(stdev(a) ** 2 / len(a) + stdev(b) ** 2 / len(b))
          if len(a) > 1 and len(b) > 1 else float("nan"))
    return MeanDiff(mean_a, mean_b, mean_a - mean_b, se)
