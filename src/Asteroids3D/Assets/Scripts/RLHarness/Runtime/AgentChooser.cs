using AI;
using AI.Context;
using AI.States;
using Movement.MPC;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The policy end of the intent seam: holds the decision-boundary action (world-plane velocity + facing + trigger + one-shot boost) and rebuilds the intent every Decide with a fresh target snapshot from the INJECTED opponent (Ranger precedent — Scout's 30 m radius is blind past the 25–60 m spawn band). The policy owns aim and trigger: the intent carries a facing override and manual fire, never aimAtTarget/projectileSpeed, so the MPC intercept override stays dormant. Boost emits on exactly one tick and only if it was available as observed at the boundary (spend-now-if-ready — a cooldown expiring mid-interval must not fire a boost the policy saw as unavailable).</summary>
    public sealed class AgentChooser : IIntentChooser, IPolicyReadout
    {
        private const int RingCapacity = 16;

        private Ship opponent;

        private bool hasAction;
        private Vector2 worldVelocity;
        private float facingRad;
        private float facingWeight;
        private bool fire;
        private bool boostPending;
        // Reused across decisions — the intent seam runs per fixed step, a fresh array would allocate per decision.
        private readonly WeightOverride[] facingOverride = { new() { weight = MpcWeight.Facing } };

        // Debug-gizmo readout only — never consulted by Decide. Fixed-size, no allocation past construction.
        private readonly PolicyAction[] ring = new PolicyAction[RingCapacity];
        private int ringHead;

        public int Count { get; private set; }
        public int TotalDecisions { get; private set; }

        // Facing-authority sweep seam: the owning probe re-applies each Begin and restores 1 on Dispose.
        internal float FacingAuthorityScale { get; set; } = 1f;

        public void Configure(Ship opponent)
        {
            this.opponent = opponent;
            Reset();
        }

        public void SetAction(Vector2 worldVelocity, float facingRad, float facingWeight, bool fire, bool boost, bool boostAvailable)
        {
            this.worldVelocity = worldVelocity;
            this.facingRad = facingRad;
            this.facingWeight = facingWeight;
            this.fire = fire;
            boostPending = boost && boostAvailable;
            hasAction = true;

            ring[ringHead] = new PolicyAction(worldVelocity, facingRad, facingWeight);
            ringHead = (ringHead + 1) % RingCapacity;
            if (Count < RingCapacity) Count++;
            TotalDecisions++;
        }

        public PolicyAction ActionFromNewest(int index) => ring[(ringHead - 1 - index + RingCapacity) % RingCapacity];

        public void Reset()
        {
            hasAction = false;
            worldVelocity = default;
            facingRad = 0f;
            facingWeight = 0f;
            facingOverride[0].multiplier = 0f;
            fire = false;
            boostPending = false;

            ringHead = 0;
            Count = 0;
            TotalDecisions = 0;
        }

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (!hasAction || !opponent || !opponent.gameObject.activeInHierarchy)
                return NavigationIntent.None;

            var boost = boostPending;
            boostPending = false;

            var intent = new NavigationIntent
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
            };

            intent.hasFacing = true;
            intent.facingRad = facingRad;
            // At scale 1 the settings asset's wFacing stays the authority ceiling.
            facingOverride[0].multiplier = facingWeight * FacingAuthorityScale;
            intent.weightOverrides = facingOverride;
            intent.manualFire = true;
            intent.primaryHeld = fire;

            return intent;
        }
    }
}
