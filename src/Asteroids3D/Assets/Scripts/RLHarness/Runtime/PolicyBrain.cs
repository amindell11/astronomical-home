using AI;
using AI.Context;
using Ships;

namespace Game.RLHarness
{
    /// <summary>The policy end of the decision seam: holds the decision-boundary intent sentence and rebuilds the decision every Decide, naming the INJECTED opponent as its anchor (Ranger precedent — Scout's 30 m radius is blind past the 25–60 m spawn band). Rock-bound slots carry the <see cref="AgentActions"/> boundary capture; the MPC re-resolves every armed slot per rollout step. The policy owns aim and the engage gates (the Gunner times the trigger); the secondary branch is action-masked to disengage until marksmanship (#409). Boost emits on exactly one tick and only if it was available as observed at the boundary (spend-now-if-ready — a cooldown expiring mid-interval must not fire a boost the policy saw as unavailable).</summary>
    public class PolicyBrain : Brain, IPolicyReadout
    {
        private const int RingCapacity = 16;

        private Ship opponent;

        private bool hasAction;
        private AgentAction action;
        private bool boostPending;

        // Debug-gizmo/probe readout only — never consulted by Decide. Fixed-size, no allocation past construction.
        private readonly PolicyAction[] ring = new PolicyAction[RingCapacity];
        private int ringHead;

        public int Count { get; private set; }
        public int TotalDecisions { get; private set; }

        // Facing-authority sweep seam: the owning probe re-applies each Begin and restores 1 on Dispose.
        internal float FacingAuthorityScale { get; set; } = 1f;

        public void Configure(Ship opponent)
        {
            this.opponent = opponent;
            ResetMailbox();
        }

        public void SetAction(in AgentAction action, bool boostAvailable)
        {
            this.action = action;
            boostPending = action.boost && boostAvailable;
            hasAction = true;

            ring[ringHead] = new PolicyAction(action.aimOffsetRad, action.aimWeight,
                action.velRadialSpeed, action.velTangentialSpeed, action.velWeight,
                action.posOffsetR, action.posOffsetThetaRad, action.posSetpoint, action.posWeight,
                action.laneWeight, action.fieldWeight,
                action.aimReferent.choice, action.posReferent.choice, action.velReferent.choice,
                (int)action.posFrame, (int)action.velFrame,
                action.firePrimary, action.fireSecondary, action.boost);
            ringHead = (ringHead + 1) % RingCapacity;
            if (Count < RingCapacity) Count++;
            TotalDecisions++;
        }

        public PolicyAction ActionFromNewest(int index) => ring[(ringHead - 1 - index + RingCapacity) % RingCapacity];

        /// <summary>The held sentence's rock bindings, for the roster's never-evict rule; returns the count written (≤ 3, dedup left to the roster's identity compare).</summary>
        public int GetBoundRocks(AsteroidRef[] dest)
        {
            if (!hasAction) return 0;
            var n = 0;
            if (action.aimReferent.rock.IsBound) dest[n++] = action.aimReferent.rock;
            if (action.posReferent.rock.IsBound) dest[n++] = action.posReferent.rock;
            if (action.velReferent.rock.IsBound) dest[n++] = action.velReferent.rock;
            return n;
        }

        public override void ResetState() => ResetMailbox();

        /// <summary>Clears the held action and the readout ring without disturbing anything a subclass layers on top — retargeting resets the mailbox alone.</summary>
        protected void ResetMailbox()
        {
            hasAction = false;
            action = default;
            boostPending = false;

            ringHead = 0;
            Count = 0;
            TotalDecisions = 0;
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (!hasAction || !opponent || !opponent.gameObject.activeInHierarchy)
                return null;

            var boost = boostPending;
            boostPending = false;

            var builder = NavObjective.Anchored(opponent.Id);

            builder = action.velReferent.rock.IsBound
                ? builder.Velocity(action.velReferent.rock, action.velRadialSpeed,
                    action.velTangentialSpeed, action.velWeight, action.velFrame)
                : builder.Velocity(action.velRadialSpeed, action.velTangentialSpeed,
                    action.velWeight, action.velFrame);

            // At scale 1 the settings asset's wFacing stays the authority ceiling.
            var aimWeight = action.aimWeight * FacingAuthorityScale;
            builder = action.aimReferent.rock.IsBound
                ? builder.Facing(action.aimReferent.rock, action.aimOffsetRad, aimWeight)
                : builder.Facing(action.aimOffsetRad, aimWeight);

            builder = action.posReferent.rock.IsBound
                ? builder.Position(action.posReferent.rock, action.posOffsetR,
                    action.posOffsetThetaRad, action.posSetpoint, action.posWeight, action.posFrame)
                : builder.Position(action.posOffsetR, action.posOffsetThetaRad,
                    action.posSetpoint, action.posWeight, action.posFrame);

            var nav = builder.Lane(action.laneWeight).Field(action.fieldWeight);

            return new BrainDecision(nav, engagePrimary: action.firePrimary,
                engageSecondary: action.fireSecondary, boost: boost);
        }
    }
}
