using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AI.Navigation.MPC
{
    // Synchronous: the Editor's managed fallback is a second float path that flips marginal plans.
    [BurstCompile(CompileSynchronously = true)]
    public struct GenerateCandidatesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Control> warmStart;
        [NativeDisableParallelForRestriction]
        public NativeArray<Control> candidates;

        public int horizon;
        public float noiseStd;
        public int noiseKnots;
        public uint rngSeed;

        public void Execute(int candidateIndex)
        {
            var offset = candidateIndex * horizon;

            if (candidateIndex == 0)
            {
                for (var j = 0; j < horizon; j++)
                    candidates[offset + j] = warmStart[j];
                return;
            }

            var rng = new Unity.Mathematics.Random(CandidateSeed(rngSeed, (uint)candidateIndex));

            // Time-correlated noise: Gaussian draws at a few evenly spaced knots, interpolated between, so one draw can express a sustained maneuver instead of self-averaging per-step jitter.
            var knots = math.max(2, noiseKnots);
            var knotScale = (knots - 1) / (float)math.max(1, horizon - 1);
            var prevKnot = NextGaussian3(ref rng);
            var nextKnot = NextGaussian3(ref rng);
            var segment = 0;

            for (var j = 0; j < horizon; j++)
            {
                var knotPos = j * knotScale;
                var seg = math.min((int)knotPos, knots - 2);
                while (segment < seg)
                {
                    prevKnot = nextKnot;
                    nextKnot = NextGaussian3(ref rng);
                    segment++;
                }

                var noise = math.lerp(prevKnot, nextKnot, knotPos - segment) * noiseStd;
                var warm = warmStart[j];
                candidates[offset + j] = new Control
                {
                    thrust = math.clamp(warm.thrust + noise.x, -1f, 1f),
                    strafe = math.clamp(warm.strafe + noise.y, -1f, 1f),
                    yawTorque = math.clamp(warm.yawTorque + noise.z, -1f, 1f),
                };
            }
        }

        // Hashes the solve seed with the candidate index into a scattered nonzero seed (Unity.Mathematics.Random rejects a zero seed). Inline, not SeedScope, to keep the Burst solver decoupled from the command layer.
        private static uint CandidateSeed(uint solveSeed, uint candidateIndex)
        {
            var h = solveSeed * 2654435761u;
            h = (h ^ candidateIndex) * 2654435761u;
            h ^= h >> 15;
            return h == 0u ? 1u : h;
        }

        private static float3 NextGaussian3(ref Unity.Mathematics.Random rng)
        {
            return new float3(NextGaussian(ref rng), NextGaussian(ref rng), NextGaussian(ref rng));
        }

        private static float NextGaussian(ref Unity.Mathematics.Random rng)
        {
            var u1 = 1f - rng.NextFloat();
            var u2 = 1f - rng.NextFloat();
            return math.sqrt(-2f * math.log(u1)) * math.sin(2f * math.PI * u2);
        }
    }
}
