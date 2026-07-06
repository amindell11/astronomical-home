using Movement.MPC;
using Ships;
using Ships.Damage;
using UnityEngine;

namespace Game.Benchmark
{
    /// <summary>
    /// Per-ship metric collector for the chase benchmark. Attached to a ship root by
    /// <see cref="ChaseBenchmarkModule"/> at sector setup; all references are cached once in
    /// <see cref="Initialize"/> (the runtime-attach equivalent of Awake caching — no per-frame
    /// lookups). Samples speed, control chatter and solver time each FixedUpdate, counts
    /// physical collisions via OnCollisionEnter, and (optionally) keeps the ship alive by
    /// resetting its damage state every tick so a benchmark episode is never cut short by a
    /// lethal impact — the impacts themselves are still fully recorded.
    /// </summary>
    public class ChaseMetricsProbe : MonoBehaviour
    {
        private Ship ship;
        private Navigator navigator;
        private DamageController damage;
        private bool initialized;
        private bool keepAlive;

        private int collisionCount;
        private float impactImpulseSum;
        private float speedSum;
        private int sampleCount;
        private bool hasPrevCommand;
        private Ships.Command.PilotCommand prevCommand;
        private float dThrustSum, dStrafeSum, dYawSum;
        private float solveMsSum, solveMsMax;
        private float simTime;

        public void Initialize(Ship ship, Navigator navigator, DamageController damage, bool keepAlive)
        {
            this.ship = ship;
            this.navigator = navigator;
            this.damage = damage;
            this.keepAlive = keepAlive;
            initialized = true;
        }

        private void FixedUpdate()
        {
            if (!initialized) return;

            simTime += Time.fixedDeltaTime;
            speedSum += ship.Kinematics.Speed;
            sampleCount++;

            var cmd = navigator.CurrentCommand;
            if (hasPrevCommand)
            {
                dThrustSum += Mathf.Abs(cmd.thrust - prevCommand.thrust);
                dStrafeSum += Mathf.Abs(cmd.strafe - prevCommand.strafe);
                dYawSum += Mathf.Abs(cmd.yawTorque - prevCommand.yawTorque);
            }
            prevCommand = cmd;
            hasPrevCommand = true;

#if UNITY_EDITOR
            var ms = navigator.lastSolveTimeMs;
            solveMsSum += ms;
            if (ms > solveMsMax) solveMsMax = ms;
#endif

            if (keepAlive && damage) damage.ResetDamageState();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!initialized) return;
            collisionCount++;
            impactImpulseSum += collision.impulse.magnitude;
        }

        public ChaseShipMetrics Snapshot()
        {
            var invSamples = sampleCount > 0 ? 1f / sampleCount : 0f;
            var invTime = simTime > 1e-4f ? 1f / simTime : 0f;
            return new ChaseShipMetrics
            {
                collisions = collisionCount,
                impactImpulse = impactImpulseSum,
                meanSpeed = speedSum * invSamples,
                chatterThrust = dThrustSum * invTime,
                chatterStrafe = dStrafeSum * invTime,
                chatterYaw = dYawSum * invTime,
                meanSolveMs = solveMsSum * invSamples,
                maxSolveMs = solveMsMax,
            };
        }
    }
}
