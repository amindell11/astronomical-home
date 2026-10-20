using System;
using AI;
using AI.Context;
using Combat.Targeting;
using Ships;
using Ships.Command;
using UnityEngine;
using AI.Strategy;
using AI.Research;

namespace RL.Opponents
{
    /// <summary>The one scripted opponent component, switched on <see cref="OpponentArchetype"/>: target validity, the 5 Hz recompute cache, the missile evasion blended over the law, and the border tangent-steer world-velocity pack. The roster Configures a pinned target per episode draw; an authored pilot serializes the same fields and can instead track the context's enemy.</summary>
    public sealed class ArchetypeBrain : Brain
    {
        private const int RecomputeIntervalTicks = 10;

        [Tooltip("Which archetype to fly. Aggressor and Kiter hold a range and fire, Orbiter circles and fires, Evader flees and never fires, Dummy sits still.")]
        [SerializeField] private OpponentArchetype archetype = OpponentArchetype.Aggressor;

        [SerializeField] private OpponentDraw shape = new()
        {
            speedFraction = 0.85f,
            jukePeriod = 1.2f,
            orbitRadius = 16f,
            orbitDirection = 1,
            desiredRange = 10f,
        };

        [Tooltip("Evader: the seed its juke flip sequence runs on.")]
        [SerializeField] private int jukeSeed = 1;

        [Tooltip("Radius of the border circle the archetype tangent-steers off, centered where the brain first decides. Author a huge value for no border.")]
        [SerializeField] private float borderRadius = 500f;

        [Tooltip("Track the context's enemy, resetting brain state whenever it changes; off flies the Configure-pinned target — the roster/trained behavior.")]
        [SerializeField] private bool liveTargeting;

        [Header("Missile evasion")]
        [Tooltip("Lean away from and boost clear of missiles tracking this ship. Off flies the archetype law alone. The Dummy never evades.")]
        [SerializeField] private bool evadeMissiles;

        [Tooltip("Seconds from the first missile tracking this ship until the pilot reacts. A second missile doesn't restart it.")]
        [SerializeField] private float reactionDelay = 0.4f;

        [Tooltip("Floor (u/s) on a missile's closing speed when timing its impact, so a missile in its slow start doesn't read as far away.")]
        [SerializeField] private float closingSpeedFloor = 15f;

        [Tooltip("Time to impact (s) at which evasion urgency starts rising from 0.")]
        [SerializeField] private float urgencyFarTime = 2f;

        [Tooltip("Time to impact (s) at which evasion urgency reaches 1.")]
        [SerializeField] private float urgencyNearTime = 0.5f;

        [Tooltip("Evasion urgency at or above which the pilot boosts, once its nose is aligned with the escape heading.")]
        [SerializeField, Range(0f, 1f)] private float boostUrgency = 0.5f;

        [Tooltip("Largest angle (degrees) between the nose and the escape heading at which the pilot boosts.")]
        [SerializeField, Range(0f, 180f)] private float boostAlignment = 30f;

        private Ship target;
        private Vector2 arenaCenter;
        private bool borderAnchored;

        private int tickCounter;
        private BrainDecision? cachedDecision;

        private System.Random rng;
        private int jukeSign = 1;
        private int recomputes;
        private int jukeEveryRecomputes = 1;

        private int threatSeenTick = -1;
        private ITrackingMissile evadedMissile;
        private int escapeSide;
        private GunDoctrine doctrine;
        private bool doctrineResolved;

        private GunDoctrine Doctrine
        {
            get
            {
                if (doctrineResolved) return doctrine;
                doctrine = GunDoctrine.Of(this);
                doctrineResolved = true;
                return doctrine;
            }
        }

        public void Configure(Ship target, OpponentArchetype archetype, in OpponentDraw shape, int jukeSeed,
            Vector2 arenaCenter, float borderRadius)
        {
            this.target = target;
            this.archetype = archetype;
            this.shape = shape;
            this.jukeSeed = jukeSeed;
            this.arenaCenter = arenaCenter;
            this.borderRadius = borderRadius;
            borderAnchored = true;
            ResetState();
        }

        public override void ResetState()
        {
            tickCounter = 0;
            cachedDecision = null;
            rng = new System.Random(jukeSeed);
            jukeSign = 1;
            recomputes = 0;
            jukeEveryRecomputes = Mathf.Max(1, Mathf.RoundToInt(
                shape.jukePeriod / (RecomputeIntervalTicks * Time.fixedDeltaTime)));
            threatSeenTick = -1;
            evadedMissile = null;
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (ctx?.Self == null) return null;

            if (!borderAnchored)
            {
                // No Configure call on authored pilots: the border centers at first Decide, target or not.
                arenaCenter = ctx.Self.Kinematics.pos;
                borderAnchored = true;
            }

            if (liveTargeting && ctx.Combat.Enemy != target)
            {
                target = ctx.Combat.Enemy;
                ResetState();
            }

            if (archetype != OpponentArchetype.Dummy
                && (!target || !target.gameObject.activeInHierarchy))
                return null;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedDecision = BuildDecision(ctx);
            tickCounter++;
            return cachedDecision;
        }

        private BrainDecision BuildDecision(AIContext ctx)
        {
            if (archetype == OpponentArchetype.Dummy)
                return new BrainDecision(NavObjective.Planar(Vector2.zero));

            var self = ctx.Self.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;
            switch (archetype)
            {
                case OpponentArchetype.Aggressor:
                case OpponentArchetype.Kiter:
                {
                    var range = Doctrine && Doctrine.HoldRange > 0f ? Doctrine.HoldRange : shape.desiredRange;
                    var law = RangerBrain.HoldRangeVelocity(in self, target.Kinematics, range, shape.speedFraction * maxSpeed);
                    if (Doctrine)
                        law = Doctrine.ShapeVelocity(law, in self, target.Kinematics, shape.speedFraction * maxSpeed,
                            tickCounter * Time.fixedDeltaTime);
                    return Pack(ctx.Self, law, engages: true);
                }
                case OpponentArchetype.Evader:
                    if (recomputes++ % jukeEveryRecomputes == 0)
                        jukeSign = rng.Next(2) == 0 ? -1 : 1;
                    return Pack(ctx.Self, ArchetypeLaws.FleeVelocity(self.pos, target.Kinematics.pos,
                        jukeSign, shape.speedFraction * maxSpeed), engages: false);
                case OpponentArchetype.Orbiter:
                    return Pack(ctx.Self, ArchetypeLaws.OrbitVelocity(in self, target.Kinematics,
                        shape.orbitRadius, shape.orbitDirection >= 0 ? 1 : -1, shape.speedFraction, maxSpeed),
                        engages: true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, null);
            }
        }

        private BrainDecision Pack(IShipStatus status, Vector2 lawVelocity, bool engages)
        {
            var self = status.Kinematics;
            var urgency = Evade(status, out var escapeHeading);
            if (Doctrine) Doctrine.evadeUrgency = urgency;
            var velocity = ArchetypeLaws.EvasionVelocity(lawVelocity, escapeHeading, status.Dynamics.maxSpeed, urgency);
            var worldVelocity = ArchetypeSteering.BorderTangentSteer(self.pos, velocity, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
            var builder = NavObjective.Anchored(target.Id).Planar(worldVelocity);
            if (engages) builder = builder.Facing(0f, (1f - urgency) * (Doctrine ? Doctrine.aimAuthority : 1f));
            var boost = urgency > 0f && urgency >= boostUrgency
                && Vector2.Angle(self.Forward, escapeHeading) <= boostAlignment;
            return new BrainDecision(builder, engages, engages, boost);
        }

        private float Evade(IShipStatus status, out Vector2 escapeHeading)
        {
            escapeHeading = Vector2.zero;
            var trackers = status.TrackingMissiles;
            if (!evadeMissiles || trackers.Count == 0)
            {
                threatSeenTick = -1;
                evadedMissile = null;
                return 0f;
            }

            if (threatSeenTick < 0) threatSeenTick = tickCounter;
            if ((tickCounter - threatSeenTick) * Time.fixedDeltaTime < reactionDelay) return 0f;

            var self = status.Kinematics;
            var missile = ArchetypeLaws.MostUrgentTracker(in self, trackers, closingSpeedFloor, out var timeToImpact);
            var missilePos = missile.Kinematics.pos;
            if (missile != evadedMissile)
            {
                evadedMissile = missile;
                escapeSide = ArchetypeLaws.EscapeSide(in self, missilePos);
            }

            escapeHeading = ArchetypeLaws.EscapeHeading(self.pos, missilePos, escapeSide);
            return ArchetypeLaws.EvasionUrgency(timeToImpact, urgencyFarTime, urgencyNearTime);
        }
    }
}
