#if UNITY_EDITOR
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Verifies the knot-based, time-correlated sampler noise introduced in A1. A correlated
    /// draw should hold a coherent maneuver (constant strafe sign) over several consecutive
    /// steps far more often than i.i.d. per-step Gaussian noise would. Tests are deterministic
    /// (fixed rngSeed) and differential where possible so they're robust to the sampler's
    /// stochastic details.
    /// </summary>
    [Category("MPC")]
    public class MpcCorrelatedNoiseEditModeTests
    {
        private const int Horizon = 17;
        private const int Samples = 2048;
        private const int Window = 5;      // consecutive steps that must share a sign
        private const uint Seed = 424242u;

        // Runs GenerateCandidatesJob with an all-zero warm start (so each control equals the
        // pure noise delta) and returns the flat candidate buffer.
        private static Control[] Generate(int noiseKnots)
        {
            var warmStart = new NativeArray<Control>(Horizon, Allocator.TempJob);
            var candidates = new NativeArray<Control>(Samples * Horizon, Allocator.TempJob);
            try
            {
                new GenerateCandidatesJob
                {
                    warmStart = warmStart,        // all default => zero
                    candidates = candidates,
                    horizon = Horizon,
                    noiseStd = 0.75f,
                    boostSampleProbability = 0f,
                    rngSeed = Seed,
                    noiseKnots = noiseKnots,
                }.Schedule(Samples, 1).Complete();

                var result = new Control[Samples * Horizon];
                candidates.CopyTo(result);
                return result;
            }
            finally
            {
                warmStart.Dispose();
                candidates.Dispose();
            }
        }

        // Fraction of candidates (skipping candidate 0, which is the verbatim warm start) that
        // have at least one Window-length run of consecutively same-signed strafe values.
        private static float ConstantSignFraction(Control[] flat)
        {
            var hits = 0;
            for (var c = 1; c < Samples; c++)
            {
                var offset = c * Horizon;
                var found = false;
                for (var start = 0; start + Window <= Horizon && !found; start++)
                {
                    var allPos = true;
                    var allNeg = true;
                    for (var j = 0; j < Window; j++)
                    {
                        var s = flat[offset + start + j].strafe;
                        if (s <= 0f) allPos = false;
                        if (s >= 0f) allNeg = false;
                    }
                    found = allPos || allNeg;
                }
                if (found) hits++;
            }
            return hits / (float)(Samples - 1);
        }

        [Test]
        public void KnotNoise_HoldsStrafeSign_OverManyConsecutiveSteps()
        {
            // i.i.d. per-step Gaussian would hold a fixed 5-step window at ~2*(0.5)^5 = 0.0625.
            // Correlated 4-knot noise (segments span ~horizon/4 ≈ 4-6 steps) should keep sign
            // over some 5-step window in the vast majority of candidates.
            var frac = ConstantSignFraction(Generate(4));
            Assert.That(frac, Is.GreaterThan(0.6f),
                $"Correlated knot noise should hold strafe sign over a {Window}-step window " +
                $"far more often than the i.i.d. baseline; got {frac:P1}");
        }

        [Test]
        public void CorrelatedNoise_HoldsSign_MoreThanNearIidHighKnotCount()
        {
            // Differential: with knots ≈ horizon the interpolation degrades toward per-step
            // independence, so long constant-sign runs get rarer. Correlation is what buys the
            // coherent maneuvers, so few knots must dominate many knots.
            var correlated = ConstantSignFraction(Generate(4));
            var nearIid = ConstantSignFraction(Generate(16));
            Assert.That(correlated, Is.GreaterThan(nearIid + 0.15f),
                $"4-knot correlated noise ({correlated:P1}) should hold sign markedly more " +
                $"than near-i.i.d. 16-knot noise ({nearIid:P1})");
        }
    }
}
#endif
