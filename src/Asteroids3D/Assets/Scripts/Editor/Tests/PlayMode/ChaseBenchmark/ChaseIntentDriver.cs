#if UNITY_EDITOR
using AI.Context;
using AI.States;
using Movement.MPC;
using Ships;
using UnityEngine;

namespace Tests.PlayMode.ChaseBenchmark
{
    /// <summary>
    /// Drives one ship's navigator directly, bypassing the utility state machine (the
    /// commander's <c>Brain</c> is disabled by the scenario). Each fixed step it snapshots
    /// the target ship and applies a <see cref="NavigationIntent"/> — <c>MaintainRange</c>
    /// for the pursuer, <c>Flee</c> for the evader — so the pairing is fixed and
    /// deterministic regardless of what the scanner would have picked. Runs before
    /// <see cref="AI.AICommander"/> (order -40) so the intent is set before the commander
    /// pulls <c>Navigator.ComputeCommand()</c>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ChaseIntentDriver : MonoBehaviour
    {
        private Ship target;
        private Navigator navigator;
        private GoalMode goalMode;
        private float desiredRange;
        private float rangeTolerance;

        public void Configure(Ship target, Navigator navigator, GoalMode goalMode,
            float desiredRange, float rangeTolerance)
        {
            this.target = target;
            this.navigator = navigator;
            this.goalMode = goalMode;
            this.desiredRange = desiredRange;
            this.rangeTolerance = rangeTolerance;
        }

        private void FixedUpdate()
        {
            if (!navigator || !target) return;

            var k = target.Kinematics;
            var enemy = new EnemyTarget
            {
                kinematics = k,
                dynamics = target.Dynamics,
                source = target.transform,
            };

            navigator.ApplyIntent(new NavigationIntent
            {
                isValid = true,
                goalMode = goalMode,
                goalPosition = k.pos,
                goalVelocity = k.vel,
                desiredRange = desiredRange,
                rangeTolerance = rangeTolerance,
                hasTarget = true,
                target = enemy,
                applyTacticalCosts = true,
                projectileSpeed = 0f,
                weightOverrides = null,
                enableFiring = false,
            });
        }
    }
}
#endif
