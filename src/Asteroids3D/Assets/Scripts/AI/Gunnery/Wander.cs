using UnityEngine;
using Utils;

namespace AI.Gunnery
{
    /// <summary>A unit-RMS signal that drifts over its correlation time (an Ornstein-Uhlenbeck process), replayable from its seed scope.</summary>
    internal struct Wander
    {
        private Unity.Mathematics.Random rng;
        private float value;

        public float Value => value;

        public Wander(SeedScope scope)
        {
            rng = new Unity.Mathematics.Random(scope.ToUint());
            value = 0f;
            value = Gaussian();
        }

        // Exact discretization: an Euler step diverges once dt exceeds twice the correlation time.
        public void Step(float dt, float correlationTime)
        {
            if (dt <= 0f || correlationTime <= 0f) return;
            var decay = Mathf.Exp(-dt / correlationTime);
            value = value * decay + Mathf.Sqrt(1f - decay * decay) * Gaussian();
        }

        private float Gaussian()
        {
            var u1 = 1f - rng.NextFloat();
            var u2 = rng.NextFloat();
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
