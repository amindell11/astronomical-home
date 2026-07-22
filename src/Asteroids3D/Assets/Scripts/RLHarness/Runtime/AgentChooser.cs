using AI;
using AI.Context;
using AI.States;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The policy end of the intent seam: holds the decision-boundary action (world-plane velocity + fire gate + one-shot boost) and rebuilds the intent every Decide with a fresh aim/target snapshot from the INJECTED opponent (Ranger precedent — Scout's 30 m radius is blind past the 25–60 m spawn band). Boost emits on exactly one tick and only if it was available as observed at the boundary (spend-now-if-ready — a cooldown expiring mid-interval must not fire a boost the policy saw as unavailable).</summary>
    public sealed class AgentChooser : IIntentChooser
    {
        private Ship opponent;
        private float projectileSpeed;

        private bool hasAction;
        private Vector2 worldVelocity;
        private bool fire;
        private bool boostPending;

        public void Configure(Ship opponent, float projectileSpeed)
        {
            this.opponent = opponent;
            this.projectileSpeed = projectileSpeed;
            Reset();
        }

        public void SetAction(Vector2 worldVelocity, bool fire, bool boost, bool boostAvailable)
        {
            this.worldVelocity = worldVelocity;
            this.fire = fire;
            boostPending = boost && boostAvailable;
            hasAction = true;
        }

        public void Reset()
        {
            hasAction = false;
            worldVelocity = default;
            fire = false;
            boostPending = false;
        }

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (!hasAction || !opponent || !opponent.gameObject.activeInHierarchy)
                return NavigationIntent.None;

            var boost = boostPending;
            boostPending = false;

            return new NavigationIntent
            {
                isValid = true,
                velocityReference = worldVelocity,
                boost = boost,
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = opponent.Kinematics,
                    dynamics = opponent.Dynamics,
                    source = opponent.transform,
                },
                aimAtTarget = true,
                projectileSpeed = projectileSpeed,
                enableFiring = fire,
            };
        }
    }
}
