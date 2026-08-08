using AI;
using AI.Context;
using Movement.MPC;
using Ships;

namespace Game.RLHarness
{
    /// <summary>The policy end of the decision seam: holds the decision-boundary action (enemy-anchored facing offset + polar velocity + trigger + one-shot boost) and rebuilds the decision every Decide with a fresh anchor snapshot from the INJECTED opponent (Ranger precedent — Scout's 30 m radius is blind past the 25–60 m spawn band). The policy owns aim and trigger: it commands the primary directly and holds the secondary, so the MPC re-resolves both anchored channels per rollout step. Boost emits on exactly one tick and only if it was available as observed at the boundary (spend-now-if-ready — a cooldown expiring mid-interval must not fire a boost the policy saw as unavailable).</summary>
    public sealed class AgentChooser : IIntentChooser, IPolicyReadout
    {
        private const int RingCapacity = 16;

        private Ship opponent;

        private bool hasAction;
        private AgentAction action;
        private bool boostPending;

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

        public void SetAction(in AgentAction action, bool boostAvailable)
        {
            this.action = action;
            boostPending = action.boost && boostAvailable;
            hasAction = true;

            ring[ringHead] = new PolicyAction(action.facingOffsetRad, action.facingWeight,
                action.radialSpeed, action.tangentialSpeed, action.velocityWeight);
            ringHead = (ringHead + 1) % RingCapacity;
            if (Count < RingCapacity) Count++;
            TotalDecisions++;
        }

        public PolicyAction ActionFromNewest(int index) => ring[(ringHead - 1 - index + RingCapacity) % RingCapacity];

        public void Reset()
        {
            hasAction = false;
            action = default;
            boostPending = false;

            ringHead = 0;
            Count = 0;
            TotalDecisions = 0;
        }

        public BrainDecision? Decide(AIContext ctx, float dt)
        {
            if (!hasAction || !opponent || !opponent.gameObject.activeInHierarchy)
                return null;

            var boost = boostPending;
            boostPending = false;

            var nav = NavObjective
                .Anchored(new EnemyTarget { kinematics = opponent.Kinematics, dynamics = opponent.Dynamics })
                .Velocity(action.radialSpeed, action.tangentialSpeed, action.velocityWeight)
                // At scale 1 the settings asset's wFacing stays the authority ceiling.
                .Facing(action.facingOffsetRad, action.facingWeight * FacingAuthorityScale);

            return new BrainDecision(nav, FireControl.Commanded(action.fire), FireControl.Hold, boost);
        }
    }
}
