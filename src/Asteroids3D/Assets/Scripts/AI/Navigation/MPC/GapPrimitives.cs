using Movement;
using Unity.Mathematics;

namespace Movement.MPC
{
    /// <summary>
    /// Synthesizes scripted "thread the gap" control sequences (Biased-MPPI / CEM seeding pattern).
    /// Each primitive forward-simulates the ship model: steer yaw toward the gap axis (PD), ramp
    /// strafe to bank (narrowing the hull) through the traversal window, then unwind. Variants
    /// differ in bank magnitude/sign and thrust level. The results are injected into the CEM
    /// candidate set; CEM refits, so they need only be good seeds, not exact.
    /// </summary>
    public static class GapPrimitives
    {
        // (bankMagnitude, thrustLevel, bankSign)
        private static readonly float3[] Variants =
        {
            new float3(1.0f, 1.0f, 1f),
            new float3(1.0f, 1.0f, -1f),
            new float3(1.0f, 0.6f, 1f),
            new float3(0.7f, 1.0f, 1f),
            new float3(0.85f, 0.85f, -1f),
        };

        public const int MaxVariants = 5;

        private const float YawKp = 2.5f;
        private const float YawKd = 0.4f;
        private const float AlignTol = 0.35f;   // rad; bank once roughly pointing through the gap
        private const float MinSpeedForTiming = 4f;

        /// <summary>
        /// Writes up to <paramref name="maxPrimitives"/> primitive sequences into
        /// <paramref name="outFlat"/> (flat, indexed [p*horizon + step]) and returns the count.
        /// The bank is a tight pulse timed (closed-loop, from the forward sim) to the mouth crossing:
        /// because banking is coupled to lateral strafe translation, holding it wide drifts the hull
        /// into a wall — a short pulse narrows the hull just as it crosses the mouth.
        /// </summary>
        public static int Synthesize(in State initial, in Gap gap, in Config cfg, in Dynamics dyn,
            Control[] outFlat, int maxPrimitives, int horizon)
        {
            var count = math.min(maxPrimitives, Variants.Length);
            if (count <= 0 || horizon <= 0) return 0;

            var dirVec = new float2(-math.sin(gap.dirRad), math.cos(gap.dirRad));
            var mouth = initial.pos + dirVec * gap.mouthDist;
            var speed = math.max(math.length(initial.vel), MinSpeedForTiming);
            // Bank only within a tight longitudinal band around the mouth (≈1-2 steps at speed).
            var bankBand = math.max(1.5f * dyn.shipRadius, speed * cfg.dt * 1.5f);
            var bankOnly = gap.classification == GapClass.BankOnly;

            for (var p = 0; p < count; p++)
            {
                var bankMag = Variants[p].x;
                var thrust = Variants[p].y;
                var sign = Variants[p].z;
                var offset = p * horizon;
                var s = initial;

                for (var j = 0; j < horizon; j++)
                {
                    var yawErr = WrapPi(gap.dirRad - s.yaw);
                    var yawCmd = math.clamp(YawKp * yawErr - YawKd * s.yawRate, -1f, 1f);
                    var aligned = math.abs(yawErr) < AlignTol;

                    // Signed along-axis distance still to travel to the mouth (from the sim state).
                    var alongRemaining = math.dot(mouth - s.pos, dirVec);
                    var atMouth = math.abs(alongRemaining) < bankBand;

                    // Only bank-only gaps need the hull narrowed; open gaps just steer through.
                    var strafe = (bankOnly && aligned && atMouth) ? bankMag * sign : 0f;

                    var u = new Control { thrust = thrust, strafe = strafe, yawTorque = yawCmd, boost = 0f };
                    outFlat[offset + j] = u;
                    s = Model.Step(s, u, cfg, dyn);
                }
            }

            return count;
        }

        private static float WrapPi(float a)
        {
            const float twoPi = 2f * math.PI;
            while (a > math.PI) a -= twoPi;
            while (a < -math.PI) a += twoPi;
            return a;
        }
    }
}
