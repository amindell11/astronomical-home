#if UNITY_EDITOR
using Movement.MPC;
using Ships;
using UnityEngine;

namespace Tests.PlayMode.ChaseBenchmark
{
    /// <summary>
    /// Attaches to a ship root (the Rigidbody GameObject, so it receives compound-collider
    /// collision callbacks) and accumulates that ship's per-run metrics: mean speed, control
    /// chatter (Σ|Δu| — the thrash detector), asteroid collisions + impact impulse, and mean
    /// MPC solve time. The pursuer probe additionally tracks the relational chase context
    /// (min / mean distance-behind, time-to-intercept) against its target.
    /// </summary>
    public sealed class ShipBenchmarkProbe : MonoBehaviour
    {
        private Ship self;
        private Ship target;
        private Navigator navigator;
        private string role;
        private float interceptRadius;
        private int asteroidLayer;

        private int ticks;
        private double sumSpeed;
        private double sumChatter;
        private double sumSolveMs;

        private bool hasPrev;
        private float prevThrust, prevStrafe, prevYaw;

        public int Collisions { get; private set; }
        public double ImpactImpulse { get; private set; }

        // Relational (meaningful on the pursuer probe). -1 sentinels keep the JSONL valid
        // (JsonUtility would emit NaN/Infinity as invalid JSON).
        public float MinDistance { get; private set; } = float.PositiveInfinity;
        private double sumDistance;
        public float TimeToInterceptSec { get; private set; } = -1f;

        public void Configure(Ship self, Ship target, Navigator navigator, string role, float interceptRadius)
        {
            this.self = self;
            this.target = target;
            this.navigator = navigator;
            this.role = role;
            this.interceptRadius = interceptRadius;
            asteroidLayer = Utils.LayerIds.Asteroid;
        }

        private void FixedUpdate()
        {
            if (!self) return;

            var kin = self.Kinematics;
            sumSpeed += kin.vel.magnitude;

            var cmd = self.Movement ? self.Movement.CurrentCommand : default;
            if (hasPrev)
                sumChatter += Mathf.Abs(cmd.thrust - prevThrust)
                            + Mathf.Abs(cmd.strafe - prevStrafe)
                            + Mathf.Abs(cmd.yawTorque - prevYaw);
            prevThrust = cmd.thrust;
            prevStrafe = cmd.strafe;
            prevYaw = cmd.yawTorque;
            hasPrev = true;

#if UNITY_EDITOR
            if (navigator) sumSolveMs += navigator.lastSolveTimeMs;
#endif

            if (target)
            {
                var dist = (target.Kinematics.pos - kin.pos).magnitude;
                sumDistance += dist;
                if (dist < MinDistance) MinDistance = dist;
                if (TimeToInterceptSec < 0f && dist <= interceptRadius)
                    TimeToInterceptSec = ticks * Time.fixedDeltaTime;
            }

            ticks++;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var go = collision.gameObject;
            var isAsteroid = (asteroidLayer >= 0 && go.layer == asteroidLayer)
                             || collision.collider.GetComponentInParent<Asteroids.AsteroidController>() != null;
            if (!isAsteroid) return;
            Collisions++;
            ImpactImpulse += collision.impulse.magnitude;
        }

        public ShipRunMetrics Result()
        {
            var n = Mathf.Max(1, ticks);
            var simSeconds = Mathf.Max(1e-4f, ticks * Time.fixedDeltaTime);
            return new ShipRunMetrics
            {
                role = role,
                collisions = Collisions,
                impactImpulse = (float)ImpactImpulse,
                meanSpeed = (float)(sumSpeed / n),
                chatterPerSec = (float)(sumChatter / simSeconds),
                meanSolveMs = (float)(sumSolveMs / n),
            };
        }

        public float MeanDistanceBehind => ticks > 0 ? (float)(sumDistance / ticks) : -1f;
    }
}
#endif
