using System;
using Movement.MPC;
using Ships;
using UnityEngine;

namespace AI
{
    /// <summary>The decision-varying slice of the MPC cost: an intent sentence plus the legacy world-plane move channel. Referent-frame slots exist only through <see cref="Anchored"/>; each binds the one ship anchor (referent 0) or a rock seat (referent 1–3). Bindings are identity, not kinematics: the host resolves them each tick, so a held decision never steers at a stale referent.</summary>
    public readonly struct NavObjective
    {
        internal readonly bool hasAnchor;
        internal readonly ShipId anchor;
        internal readonly bool hasPlanarVelocity;
        internal readonly Vector2 planarVelocity;
        internal readonly IntentSentence sentence;
        // Ships only ever ride referent 0 via the anchor; seats 1–3 carry rocks alone.
        internal readonly AsteroidRef rockSeat1;
        internal readonly AsteroidRef rockSeat2;
        internal readonly AsteroidRef rockSeat3;

        internal NavObjective(bool hasAnchor, ShipId anchor, bool hasPlanarVelocity,
            Vector2 planarVelocity, in IntentSentence sentence,
            in AsteroidRef rockSeat1 = default, in AsteroidRef rockSeat2 = default,
            in AsteroidRef rockSeat3 = default)
        {
            this.hasAnchor = hasAnchor;
            this.anchor = anchor;
            this.hasPlanarVelocity = hasPlanarVelocity;
            this.planarVelocity = planarVelocity;
            this.sentence = sentence;
            this.rockSeat1 = rockSeat1;
            this.rockSeat2 = rockSeat2;
            this.rockSeat3 = rockSeat3;
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

        /// <summary>The identity behind a slot's synthetic referent index; default (unbound) outside 1–3.</summary>
        internal AsteroidRef RockSeat(int referent) => referent switch
        {
            1 => rockSeat1,
            2 => rockSeat2,
            3 => rockSeat3,
            _ => default,
        };

        /// <summary>A zero reference is a commanded stop and an armed weight-0 slot is a live "nothing matters", so the armed channels — not their values — decide idleness.</summary>
        internal bool IsIdle => !hasPlanarVelocity && !sentence.AnyArmed;
    }

    /// <summary>Fluent assembly of an anchored objective's sentence. Allocation-free, and the two move channels (world-plane vs VEL slot) overwrite each other so only one can survive. Rock overloads bind a slot to an asteroid instead of the anchor: distinct rocks claim seats 1–3 (dedup by identity), which is total by construction — only three slots can bind one rock each.</summary>
    public readonly struct AnchoredBuilder
    {
        private readonly ShipId anchor;
        private readonly bool hasPlanarVelocity;
        private readonly Vector2 planarVelocity;
        private readonly IntentSentence sentence;
        private readonly AsteroidRef rockSeat1;
        private readonly AsteroidRef rockSeat2;
        private readonly AsteroidRef rockSeat3;

        internal AnchoredBuilder(ShipId anchor)
            : this(anchor, false, default, default, default, default, default) { }

        private AnchoredBuilder(ShipId anchor, bool hasPlanarVelocity, Vector2 planarVelocity,
            in IntentSentence sentence, in AsteroidRef rockSeat1, in AsteroidRef rockSeat2,
            in AsteroidRef rockSeat3)
        {
            this.anchor = anchor;
            this.hasPlanarVelocity = hasPlanarVelocity;
            this.planarVelocity = planarVelocity;
            this.sentence = sentence;
            this.rockSeat1 = rockSeat1;
            this.rockSeat2 = rockSeat2;
            this.rockSeat3 = rockSeat3;
        }

        private AnchoredBuilder With(in IntentSentence next) =>
            new(anchor, hasPlanarVelocity, planarVelocity, next, rockSeat1, rockSeat2, rockSeat3);

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
            return new AnchoredBuilder(anchor, false, default, next, rockSeat1, rockSeat2, rockSeat3);
        }

        /// <summary>The VEL slot bound to a rock: polar velocity relative to the rock's motion.</summary>
        public AnchoredBuilder Velocity(in AsteroidRef rock, float radial, float tangential, float authority)
        {
            var bound = Bind(rock, out var referent);
            var next = bound.sentence;
            next.vel = new VelSlot
            {
                armed = true,
                radialSpeed = radial,
                tangentialSpeed = tangential,
                weight = authority,
                referent = referent,
            };
            return new AnchoredBuilder(bound.anchor, false, default, next,
                bound.rockSeat1, bound.rockSeat2, bound.rockSeat3);
        }

        /// <summary>A world-plane velocity reference while the other slots stay anchored.</summary>
        public AnchoredBuilder Planar(Vector2 velocity)
        {
            var next = sentence;
            next.vel = default;
            return new AnchoredBuilder(anchor, true, velocity, next, rockSeat1, rockSeat2, rockSeat3);
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
            return With(next);
        }

        /// <summary>The AIM slot bound to a rock: nose offset around the rock's intercept point.</summary>
        public AnchoredBuilder Facing(in AsteroidRef rock, float offsetRad, float authority)
        {
            var bound = Bind(rock, out var referent);
            var next = bound.sentence;
            next.aim = new AimSlot
            {
                armed = true,
                offsetRad = offsetRad,
                weight = authority,
                referent = referent,
            };
            return bound.With(next);
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
            return With(next);
        }

        /// <summary>The POS slot bound to a rock. Rocks have no facing: the Facing frame degrades to world axes (rock referents resolve with yaw 0).</summary>
        public AnchoredBuilder Position(in AsteroidRef rock, float offsetR, float offsetThetaRad,
            float setpoint, float authority, ReferentFrame frame = ReferentFrame.Position)
        {
            var bound = Bind(rock, out var referent);
            var next = bound.sentence;
            next.pos = new PosSlot
            {
                armed = true,
                offsetR = offsetR,
                offsetThetaRad = offsetThetaRad,
                setpoint = setpoint,
                weight = authority,
                referent = referent,
                frame = frame,
            };
            return bound.With(next);
        }

        /// <summary>Hazard-repulsion authority over the turn-away branch (0 = no hazard shaping; the collision penalty stays).</summary>
        public AnchoredBuilder Field(float authority)
        {
            var next = sentence;
            next.field = new FieldSlot { armed = true, weight = authority };
            return With(next);
        }

        /// <summary>Finds the rock's existing seat or claims the next free one. Re-binding a slot to a new rock does not free the old seat, so an authoring sequence can exhaust all three — that misuse fails here, not silently downstream.</summary>
        private AnchoredBuilder Bind(in AsteroidRef rock, out int referent)
        {
            if (!rock.IsBound)
                throw new ArgumentException("A rock overload needs an actual rock — an unbound AsteroidRef would alias an empty seat.", nameof(rock));
            if (rockSeat1.Equals(rock)) { referent = 1; return this; }
            if (rockSeat2.Equals(rock)) { referent = 2; return this; }
            if (rockSeat3.Equals(rock)) { referent = 3; return this; }

            if (!rockSeat1.IsBound)
            {
                referent = 1;
                return new AnchoredBuilder(anchor, hasPlanarVelocity, planarVelocity, sentence, rock, rockSeat2, rockSeat3);
            }
            if (!rockSeat2.IsBound)
            {
                referent = 2;
                return new AnchoredBuilder(anchor, hasPlanarVelocity, planarVelocity, sentence, rockSeat1, rock, rockSeat3);
            }
            if (!rockSeat3.IsBound)
            {
                referent = 3;
                return new AnchoredBuilder(anchor, hasPlanarVelocity, planarVelocity, sentence, rockSeat1, rockSeat2, rock);
            }
            throw new InvalidOperationException("Sentence already binds three distinct rocks — re-binding a slot never frees its seat.");
        }

        public static implicit operator NavObjective(AnchoredBuilder builder) =>
            new(true, builder.anchor, builder.hasPlanarVelocity, builder.planarVelocity, builder.sentence,
                builder.rockSeat1, builder.rockSeat2, builder.rockSeat3);
    }
}
