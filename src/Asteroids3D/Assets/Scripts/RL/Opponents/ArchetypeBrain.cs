using System;
using AI;
using AI.Context;
using AI.Tactics;
using Combat.Targeting;
using Movement;
using Ships;
using Ships.Command;
using UnityEngine;
using AI.Strategy;

namespace RL.Opponents
{
    /// <summary>The one scripted opponent component: target validity, the 5 Hz recompute cache, the missile evasion blended over the law, and the border tangent-steer world-velocity pack. The roster Configures a pinned target per episode draw and flies <see cref="OpponentArchetype"/>; an authored pilot serializes the same fields and can instead track the context's enemy.
    /// With that enemy gone, a live-targeting pilot roams around its last live position (the wreck) through held waypoints. A temper that circles the wreck does so for a few laps, then wanders. Otherwise the ship wanders from the start.
    /// A pilot with tactics settings never reads <c>archetype</c>. Its temper names its home tactic and how it roams. <see cref="TacticRules"/> picks the tactic from the ship's situation, and the ship flies one of the same laws through the same evasion and border pack.</summary>
    public sealed class ArchetypeBrain : Brain
    {
        private const int RecomputeIntervalTicks = 10;

        [Tooltip("Which archetype to fly. A pilot with tactics settings never reads this. Its temper names its home tactic and how it roams. Aggressor and Kiter hold a range and fire, Orbiter circles and fires, Evader flees and never fires, Dummy sits still.")]
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

        [Tooltip("This pilot's temper: its home tactic and how it roams. A pilot with these settings never reads its archetype. Empty flies the archetype alone, the roster and trained behavior.")]
        [SerializeField] private TacticsSettings tactics;

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

        [Header("Roam")]
        [Tooltip("With the target gone: the nearest distance (u) from the wreck at which circling waypoints are drawn.")]
        [SerializeField, Min(0f)] private float roamRingMin = 35f;

        [Tooltip("With the target gone: the farthest distance (u) from the wreck at which circling waypoints are drawn.")]
        [SerializeField, Min(0f)] private float roamRingMax = 60f;

        [Tooltip("Laps a ship whose temper circles the wreck flies before it switches to wandering.")]
        [SerializeField, Min(0f)] private float roamLaps = 2f;

        [Tooltip("Radius (u) of the disc around the wreck that wander waypoints are drawn in.")]
        [SerializeField, Min(0f)] private float roamWanderRadius = 120f;

        private Ship target;
        private Ship droppedTarget;
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

        private int tacticTick;
        private int flankSign;

        // Outlives ResetState: the live-target change that resets the brain is when roaming begins.
        private Vector2? wreck;

        private bool roaming;
        private bool roamCircling;
        private Vector2 roamCenter;
        private Vector2 roamWaypoint;
        private float roamAngle;
        private float roamDegrees;
        private int roamLane;
        private System.Random roamRng;

        internal Vector2 RoamCenter => roamCenter;
        internal Vector2 RoamWaypoint => roamWaypoint;
        internal bool RoamCircling => roamCircling;

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
            roaming = false;
            CurrentTactic = null;
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
                droppedTarget = target;
                target = ctx.Combat.Enemy;
                ResetState();
            }

            if (liveTargeting && !IsGone(target)) wreck = target.Kinematics.pos;

            // Null on a concealed target lets the commander hunt its last-seen point.
            if (archetype != OpponentArchetype.Dummy && IsGone(target))
                return liveTargeting && !IsConcealed(droppedTarget) ? Roam(ctx.Self) : null;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedDecision = BuildDecision(ctx);
            tickCounter++;
            return cachedDecision;
        }

        private static bool IsGone(Ship ship) => !ship || !ship.gameObject.activeInHierarchy;

        private static bool IsConcealed(Ship ship) => !IsGone(ship) && ship.Lock.IsConcealed;

        private BrainDecision Roam(IShipStatus status)
        {
            var self = status.Kinematics;
            if (!roaming) BeginRoam(self.pos, self.vel);
            else if (Vector2.Distance(self.pos, roamWaypoint) <= ArchetypeRoam.ArrivalRadius) PickRoamWaypoint();

            var seek = ArchetypeRoam.SeekVelocity(self.pos, roamWaypoint,
                shape.speedFraction * status.Dynamics.maxSpeed);
            return new BrainDecision(NavObjective.Planar(ArchetypeSteering.BorderTangentSteer(self.pos, seek,
                arenaCenter, borderRadius, ArchetypeSteering.BorderMargin)));
        }

        private void BeginRoam(Vector2 pos, Vector2 vel)
        {
            roaming = true;
            roamCenter = wreck ?? pos;
            roamCircling = tactics.circlesWreck;
            var radial = pos - roamCenter;
            roamLane = radial.x * vel.y - radial.y * vel.x >= 0f ? 1 : -1;
            roamAngle = Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg;
            roamDegrees = 0f;
            // Pilots authored with the same jukeSeed must still pick their own waypoints.
            roamRng = new System.Random(unchecked(jukeSeed * 397) ^ GetInstanceID());
            PickRoamWaypoint();
        }

        private void PickRoamWaypoint()
        {
            if (roamCircling && roamDegrees >= roamLaps * 360f) roamCircling = false;
            if (!roamCircling)
            {
                roamWaypoint = ArchetypeRoam.WanderWaypoint(roamCenter, roamWanderRadius, roamRng);
                return;
            }

            roamWaypoint = ArchetypeRoam.RingWaypoint(roamCenter, ref roamAngle, roamLane, roamRingMin, roamRingMax,
                roamRng, out var step);
            roamDegrees += step;
        }

        private BrainDecision BuildDecision(AIContext ctx)
        {
            if (archetype == OpponentArchetype.Dummy)
                return new BrainDecision(NavObjective.Planar(Vector2.zero));

            var self = ctx.Self.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;
            if (tactics) return FlyTactic(ctx, in self, maxSpeed);

            switch (archetype)
            {
                case OpponentArchetype.Aggressor:
                case OpponentArchetype.Kiter:
                    return Pack(ctx.Self, RangerBrain.HoldRangeVelocity(in self, target.Kinematics,
                        ctx.HoldRange ?? shape.desiredRange, shape.speedFraction * maxSpeed), engages: true);
                case OpponentArchetype.Evader:
                    return Pack(ctx.Self, FleeVelocity(self.pos, shape.speedFraction * maxSpeed), engages: false);
                case OpponentArchetype.Orbiter:
                    return Pack(ctx.Self, ArchetypeLaws.OrbitVelocity(in self, target.Kinematics,
                        shape.orbitRadius, shape.orbitDirection >= 0 ? 1 : -1, shape.speedFraction, maxSpeed),
                        engages: true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, null);
            }
        }

        private BrainDecision FlyTactic(AIContext ctx, in Kinematics self, float maxSpeed)
        {
            var situation = Situation.Read(ctx, tactics);
            var held = CurrentTactic.HasValue ? (tickCounter - tacticTick) * Time.fixedDeltaTime : float.PositiveInfinity;
            var tactic = TacticRules.Next(CurrentTactic ?? tactics.home, held, in situation, tactics);
            if (tactic != CurrentTactic)
            {
                CurrentTactic = tactic;
                tacticTick = tickCounter;
            }

            var them = target.Kinematics;
            if (situation.openSide != Vector2.zero)
                flankSign = Vector2.Dot(Vector2.Perpendicular(them.pos - self.pos), situation.openSide) >= 0f ? 1 : -1;

            var speed = shape.speedFraction * maxSpeed;
            var gunRange = ctx.HoldRange ?? shape.desiredRange;
            var flankSide = flankSign != 0 ? flankSign : (shape.orbitDirection >= 0 ? 1 : -1);
            return tactic switch
            {
                Tactic.AttackRun => Pack(ctx.Self, RangerBrain.HoldRangeVelocity(in self, them,
                    gunRange + tactics.attackRunMargin, speed), engages: true),
                Tactic.Flank => Pack(ctx.Self, ArchetypeLaws.OrbitVelocity(in self, them, gunRange, flankSide,
                    shape.speedFraction, maxSpeed), engages: situation.alliesInLane == 0),
                Tactic.StandOff => Pack(ctx.Self, RangerBrain.HoldRangeVelocity(in self, them,
                    situation.targetRange + tactics.standOffMargin, speed),
                    engages: !situation.inTargetsCone && situation.alliesInLane == 0),
                Tactic.BreakOff => Pack(ctx.Self, FleeVelocity(self.pos, speed), engages: false),
                _ => throw new ArgumentOutOfRangeException(nameof(tactic), tactic, null),
            };
        }

        private Vector2 FleeVelocity(Vector2 selfPos, float speed)
        {
            if (recomputes++ % jukeEveryRecomputes == 0)
                jukeSign = rng.Next(2) == 0 ? -1 : 1;
            return ArchetypeLaws.FleeVelocity(selfPos, target.Kinematics.pos, jukeSign, speed);
        }

        private BrainDecision Pack(IShipStatus status, Vector2 lawVelocity, bool engages)
        {
            var self = status.Kinematics;
            var urgency = Evade(status, out var escapeHeading);
            var velocity = ArchetypeLaws.EvasionVelocity(lawVelocity, escapeHeading, status.Dynamics.maxSpeed, urgency);
            var worldVelocity = ArchetypeSteering.BorderTangentSteer(self.pos, velocity, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
            var builder = NavObjective.Anchored(target.Id).Planar(worldVelocity);
            if (engages) builder = builder.Facing(0f, 1f - urgency);
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

        // Awake, not OnValidate: the base pilot prefab is authored without a temper so no variant inherits one.
        private void Awake()
        {
            if (!liveTargeting || tactics) return;
            throw new InvalidOperationException($"{name}: a live-targeting pilot must name its temper");
        }
    }
}
