using Movement.MPC;
using Ships;
using UnityEngine;

namespace AI
{
    /// <summary>The decision-varying slice of the MPC cost: an intent sentence plus the legacy world-plane move channel. Referent-frame slots exist only through <see cref="Anchored"/> and all bind the one anchor — which is identity, not kinematics: the host resolves it each tick, so a held decision never steers at a stale enemy.</summary>
    public readonly struct NavObjective
    {
        internal readonly bool hasAnchor;
        internal readonly ShipId anchor;
        internal readonly bool hasPlanarVelocity;
        internal readonly Vector2 planarVelocity;
        internal readonly IntentSentence sentence;

        internal NavObjective(bool hasAnchor, ShipId anchor, bool hasPlanarVelocity,
            Vector2 planarVelocity, in IntentSentence sentence)
        {
            this.hasAnchor = hasAnchor;
            this.anchor = anchor;
            this.hasPlanarVelocity = hasPlanarVelocity;
            this.planarVelocity = planarVelocity;
            this.sentence = sentence;
        }

        /// <summary>No armed channel at all, so the navigator idles and never solves. Distinct from an armed all-weights-0 sentence, which solves on the priors.</summary>
        public static NavObjective Drift => default;

        /// <summary>A world-plane velocity reference; facing is left to the delegation priors.</summary>
        public static NavObjective Planar(Vector2 velocity) => new(false, default, true, velocity, default);

        public static AnchoredBuilder Anchored(ShipId anchor) => new(anchor);

        public bool TryGetAnchorId(out ShipId anchor)
        {
            anchor = this.anchor;
            return hasAnchor;
        }

        /// <summary>A zero reference is a commanded stop and an armed weight-0 slot is a live "nothing matters", so the armed channels — not their values — decide idleness.</summary>
        internal bool IsIdle => !hasPlanarVelocity && !sentence.AnyArmed;
    }

    /// <summary>Fluent assembly of an anchored objective's sentence. Allocation-free, and the two move channels (world-plane vs VEL slot) overwrite each other so only one can survive.</summary>
    public readonly struct AnchoredBuilder
    {
        private readonly ShipId anchor;
        private readonly bool hasPlanarVelocity;
        private readonly Vector2 planarVelocity;
        private readonly IntentSentence sentence;

        internal AnchoredBuilder(ShipId anchor)
            : this(anchor, false, default, default) { }

        private AnchoredBuilder(ShipId anchor, bool hasPlanarVelocity, Vector2 planarVelocity,
            in IntentSentence sentence)
        {
            this.anchor = anchor;
            this.hasPlanarVelocity = hasPlanarVelocity;
            this.planarVelocity = planarVelocity;
            this.sentence = sentence;
        }

        /// <summary>Polar velocity in the anchor frame: radial > 0 closes along the LOS, tangential > 0 orbits CCW.</summary>
        public AnchoredBuilder Velocity(float radial, float tangential, float authority)
        {
            var next = sentence;
            next.vel = new VelSlot
            {
                armed = true,
                radialSpeed = radial,
                tangentialSpeed = tangential,
                weight = authority,
            };
            return new AnchoredBuilder(anchor, false, default, next);
        }

        /// <summary>A world-plane velocity reference while the other slots stay anchored.</summary>
        public AnchoredBuilder Planar(Vector2 velocity)
        {
            var next = sentence;
            next.vel = default;
            return new AnchoredBuilder(anchor, true, velocity, next);
        }

        /// <summary>Nose offset from the intercept anchor, CCW positive; authority scales the settings asset's wFacing ceiling.</summary>
        public AnchoredBuilder Facing(float offsetRad, float authority)
        {
            var next = sentence;
            next.aim = new AimSlot
            {
                armed = true,
                offsetRad = offsetRad,
                weight = authority,
            };
            return new AnchoredBuilder(anchor, hasPlanarVelocity, planarVelocity, next);
        }

        /// <summary>A point at polar offset (r, θ) in the anchor's chosen frame; the setpoint makes it a hold-ring (0 = at the point).</summary>
        public AnchoredBuilder Position(float offsetR, float offsetThetaRad, float setpoint, float authority,
            ReferentFrame frame = ReferentFrame.Position)
        {
            var next = sentence;
            next.pos = new PosSlot
            {
                armed = true,
                offsetR = offsetR,
                offsetThetaRad = offsetThetaRad,
                setpoint = setpoint,
                weight = authority,
                frame = frame,
            };
            return new AnchoredBuilder(anchor, hasPlanarVelocity, planarVelocity, next);
        }

        /// <summary>Hazard-repulsion authority over the turn-away branch (0 = no hazard shaping; the collision penalty stays).</summary>
        public AnchoredBuilder Field(float authority)
        {
            var next = sentence;
            next.field = new FieldSlot { armed = true, weight = authority };
            return new AnchoredBuilder(anchor, hasPlanarVelocity, planarVelocity, next);
        }

        public static implicit operator NavObjective(AnchoredBuilder builder) =>
            new(true, builder.anchor, builder.hasPlanarVelocity, builder.planarVelocity, builder.sentence);
    }
}
