using Movement.MPC;
using Ships;
using UnityEngine;

namespace AI
{
    /// <summary>The decision-varying slice of the MPC cost function: one move channel and one facing channel. Enemy-frame channels reach the solver only through <see cref="Anchored"/>, so an anchorless one cannot be authored. The anchor is identity, not kinematics — the host resolves it against the live ship each tick, so a decision held across its 5 Hz interval never steers at a stale enemy.</summary>
    public readonly struct NavObjective
    {
        internal readonly bool hasAnchor;
        internal readonly ShipId anchor;
        internal readonly bool hasPlanarVelocity;
        internal readonly Vector2 planarVelocity;
        internal readonly AnchoredIntent anchored;

        internal NavObjective(bool hasAnchor, ShipId anchor, bool hasPlanarVelocity,
            Vector2 planarVelocity, in AnchoredIntent anchored)
        {
            this.hasAnchor = hasAnchor;
            this.anchor = anchor;
            this.hasPlanarVelocity = hasPlanarVelocity;
            this.planarVelocity = planarVelocity;
            this.anchored = anchored;
        }

        /// <summary>No move channel, so the navigator idles and never solves.</summary>
        public static NavObjective Drift => default;

        /// <summary>A world-plane velocity reference; facing is left to the delegation priors.</summary>
        public static NavObjective Planar(Vector2 velocity) => new(false, default, true, velocity, default);

        public static AnchoredBuilder Anchored(ShipId anchor) => new(anchor);

        public bool TryGetAnchorId(out ShipId anchor)
        {
            anchor = this.anchor;
            return hasAnchor;
        }

        /// <summary>A zero reference is a commanded stop, so the armed channel — not its value — decides idleness.</summary>
        internal bool IsIdle => !hasPlanarVelocity && !anchored.hasVelocity;
    }

    /// <summary>Fluent assembly of an anchored objective. Allocation-free, and the two move channels overwrite each other so only one can survive.</summary>
    public readonly struct AnchoredBuilder
    {
        private readonly ShipId anchor;
        private readonly bool hasPlanarVelocity;
        private readonly Vector2 planarVelocity;
        private readonly AnchoredIntent anchored;

        internal AnchoredBuilder(ShipId anchor)
            : this(anchor, false, default, default) { }

        private AnchoredBuilder(ShipId anchor, bool hasPlanarVelocity, Vector2 planarVelocity,
            in AnchoredIntent anchored)
        {
            this.anchor = anchor;
            this.hasPlanarVelocity = hasPlanarVelocity;
            this.planarVelocity = planarVelocity;
            this.anchored = anchored;
        }

        /// <summary>Polar velocity in the enemy frame: radial > 0 closes along the LOS, tangential > 0 orbits CCW.</summary>
        public AnchoredBuilder Velocity(float radial, float tangential, float authority)
        {
            var next = anchored;
            next.hasVelocity = true;
            next.radialSpeed = radial;
            next.tangentialSpeed = tangential;
            next.velocityWeight = authority;
            return new AnchoredBuilder(anchor, false, default, next);
        }

        /// <summary>A world-plane velocity reference while the facing channel stays enemy-anchored.</summary>
        public AnchoredBuilder Planar(Vector2 velocity)
        {
            var next = anchored;
            next.hasVelocity = false;
            next.radialSpeed = 0f;
            next.tangentialSpeed = 0f;
            next.velocityWeight = 0f;
            return new AnchoredBuilder(anchor, true, velocity, next);
        }

        /// <summary>Nose offset from the intercept anchor, CCW positive; authority scales the settings asset's wFacing ceiling.</summary>
        public AnchoredBuilder Facing(float offsetRad, float authority)
        {
            var next = anchored;
            next.hasFacing = true;
            next.facingOffsetRad = offsetRad;
            next.facingWeight = authority;
            return new AnchoredBuilder(anchor, hasPlanarVelocity, planarVelocity, next);
        }

        public static implicit operator NavObjective(AnchoredBuilder builder) =>
            new(true, builder.anchor, builder.hasPlanarVelocity, builder.planarVelocity, builder.anchored);
    }
}
