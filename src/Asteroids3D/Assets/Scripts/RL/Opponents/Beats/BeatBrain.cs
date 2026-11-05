using AI;
using AI.Context;
using AI.Strategy;
using Combat.Targeting;
using Movement;
using Ships;
using Ships.Command;
using Ships.Registry;
using UnityEngine;

namespace RL.Opponents.Beats
{
    /// <summary>
    /// The game's enemy brain. Its <see cref="BeatSelector"/> picks a committed beat from the pilot's
    /// <see cref="Personality"/>, and each beat flies one planar velocity law under the same post-step as
    /// the archetypes: missile evasion, the border tangent-steer and facing. It publishes its target and
    /// whether it is attacking, so allies on this brain that read it through the registry hold back from a
    /// crowded target; no sector object reaches it. With no live target, or while its scout is unaware (a
    /// hunt patrol before the alarm), it searches around the wreck with its triggers cold. Its draws come
    /// from one seed taken from the engine's random state at its first decision, so each enemy differs,
    /// and differs run to run.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1349
    /// </summary>
    public sealed class BeatBrain : Brain, IBeatReadout
    {
        private const int RecomputeIntervalTicks = 10;
        private const float FlankArrivalRadius = 6f;
        private const float JukePeriod = 1.2f;
        // The orbit swings about its ring; a tenth of the fire distance stays in hand.
        private const float HoldRingInsideFire = 0.9f;

        [Tooltip("The weight set this pilot picks beats by; each enemy flies its own draw from it.")]
        [SerializeField] private Personality personality;

        [Tooltip("Distance (u) an attack run closes to when the primary weapon declares no hold range.")]
        [SerializeField, Min(0f)] private float attackRange = 10f;

        [Tooltip("Fraction of max speed the beats fly at.")]
        [SerializeField, Range(0f, 1f)] private float speedFraction = 0.85f;

        [Tooltip("Radius of the border circle the pilot tangent-steers off, centred where it first decides.")]
        [SerializeField] private float borderRadius = 500f;

        [Tooltip("Radius (u) of the disc around the wreck, or the pilot's first position, that search waypoints are drawn in.")]
        [SerializeField, Min(0f)] private float searchRadius = 120f;

        [Header("Missile evasion")]
        [Tooltip("Lean away from and boost clear of missiles tracking this ship.")]
        [SerializeField] private bool evadeMissiles = true;

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

        private BeatSelector selector;
        private System.Random rng;
        private int phase;
        private int tick;
        private int lastRecomputeTick;
        private int flownPick = -1;
        private BrainDecision? cachedDecision;

        private Ship target;
        private Ship droppedTarget;
        private bool targetChanged;
        private Vector2? wreck;
        private Vector2 arenaCenter;
        private bool borderAnchored;

        private int orbitSign = 1;
        private int flankSide = 1;
        private int jukeOffset;
        private float shotOffset;
        private Crowd crowd;

        private bool searching;
        private Vector2 searchCenter;
        private Vector2 searchWaypoint;

        private int threatSeenTick = -1;
        private ITrackingMissile evadedMissile;
        private int escapeSide;

        internal Beat Beat => selector?.Beat ?? Beat.Search;
        internal ShipId TargetId => Live(target) ? target.Id : ShipId.Invalid;
        internal bool Attacking => Live(target) && Beat is Beat.AttackRun or Beat.Flank;

        string IBeatReadout.Beat => Beat.ToString();
        float IBeatReadout.BeatSecondsLeft => selector?.SecondsLeft ?? 0f;
        int IBeatReadout.ScoredCount => selector is { Picks: > 0 } ? BeatSelector.ScoredBeats.Length : 0;
        (string beat, float score) IBeatReadout.Scored(int index) =>
            (BeatSelector.ScoredBeats[index].ToString(), selector.LastScore(index));

        private void Seed(int seed)
        {
            rng = new System.Random(seed);
            selector = new BeatSelector(personality, rng);
            phase = rng.Next(RecomputeIntervalTicks);
            orbitSign = rng.Next(2) == 0 ? -1 : 1;
        }

        public override void ResetState()
        {
            selector = null;
            tick = 0;
            lastRecomputeTick = 0;
            flownPick = -1;
            cachedDecision = null;
            target = null;
            droppedTarget = null;
            targetChanged = false;
            wreck = null;
            searching = false;
            threatSeenTick = -1;
            evadedMissile = null;
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (ctx?.Self == null) return null;
            if (selector == null) Seed(UnityEngine.Random.Range(int.MinValue, int.MaxValue));

            if (!borderAnchored)
            {
                arenaCenter = ctx.Self.Kinematics.pos;
                borderAnchored = true;
            }

            // An unaware scout reports no contacts; a target kept from before must not outlive that either.
            var unaware = ctx.Scout.Unaware;
            var enemy = unaware ? null : ctx.Combat.Enemy;
            if (enemy != target)
            {
                droppedTarget = target;
                target = enemy;
                targetChanged = true;
            }
            if (Live(target)) wreck = target.Kinematics.pos;

            // Null on a concealed target lets the commander hunt its last-seen point, engaging nothing.
            if (!unaware && !Live(target) && Concealed(droppedTarget)) return null;

            var due = !cachedDecision.HasValue || targetChanged || tick % RecomputeIntervalTicks == phase;
            tick++;
            if (!due) return cachedDecision;

            var situation = Live(target) ? Read(ctx) : BeatSituation.NoTarget;
            selector.Advance((tick - lastRecomputeTick) * Time.fixedDeltaTime, in situation, targetChanged);
            lastRecomputeTick = tick;
            targetChanged = false;
            if (selector.Picks != flownPick) BeginBeat(ctx.Self.Kinematics);
            cachedDecision = Fly(ctx);
            return cachedDecision;
        }

        private static bool Live(Ship ship) => ship && ship.gameObject.activeInHierarchy;

        private static bool Concealed(Ship ship) => Live(ship) && ship.Lock.IsConcealed;

        private BeatSituation Read(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;
            crowd = Crowd.Read(ctx.Scout, ctx.Self.Id, target);
            var toSelf = self.pos - enemy.pos;
            var aimedAt = toSelf.sqrMagnitude > 1e-6f ? Mathf.Max(0f, Vector2.Dot(enemy.Forward, toSelf.normalized)) : 0f;
            var reached = selector.Beat == Beat.Flank
                && Vector2.Distance(self.pos, FlankLaw.BlindPoint(in enemy, flankSide, personality.flankDistance))
                <= FlankArrivalRadius;
            return new BeatSituation(crowd.attackers, aimedAt, ctx.Self.ShieldPct,
                ctx.SecondsSinceHit <= personality.hitMemorySeconds, reached);
        }

        private void BeginBeat(in Kinematics self)
        {
            flownPick = selector.Picks;
            if (selector.Beat != Beat.Search) searching = false;
            if (!Live(target)) return;
            var enemy = target.Kinematics;
            flankSide = FlankLaw.Side(self.pos, in enemy, in crowd);
            jukeOffset = rng.Next(2);
            shotOffset = (float)rng.NextDouble() * personality.longShotInterval;
        }

        private BrainDecision Fly(AIContext ctx)
        {
            var status = ctx.Self;
            if (selector.Beat == Beat.Search) return Search(status);

            var self = status.Kinematics;
            var enemy = target.Kinematics;
            var maxSpeed = status.Dynamics.maxSpeed;
            var speed = speedFraction * maxSpeed;
            var range = ctx.HoldRange ?? attackRange;
            return selector.Beat switch
            {
                Beat.AttackRun => Pack(status, RangerBrain.HoldRangeVelocity(in self, in enemy, range, speed),
                    primary: true, secondary: true, facing: true),
                Beat.Flank => Pack(status, FlankLaw.Velocity(self.pos, in enemy,
                        FlankLaw.BlindPoint(in enemy, flankSide, personality.flankDistance), speed),
                    primary: true, secondary: true, facing: true),
                Beat.Hold => Pack(status, ArchetypeLaws.OrbitVelocity(in self, in enemy,
                        HoldRing(range, ctx.FireRange, personality.holdRangeScale), orbitSign, speedFraction,
                        maxSpeed),
                    primary: LongShot(), secondary: false, facing: true),
                _ => Pack(status, ArchetypeLaws.FleeVelocity(self.pos, enemy.pos, Juke(), speed),
                    primary: false, secondary: false, facing: false),
            };
        }

        /// <summary>Pulled inside the primary's fire distance, so a hold can always take its long shots.</summary>
        internal static float HoldRing(float attackRange, float? fireRange, float holdRangeScale)
        {
            var ring = holdRangeScale * attackRange;
            return fireRange.HasValue ? Mathf.Min(ring, HoldRingInsideFire * fireRange.Value) : ring;
        }

        private bool LongShot() =>
            (selector.Elapsed + shotOffset) % personality.longShotInterval < personality.longShotSeconds;

        private int Juke() => ((int)(selector.Elapsed / JukePeriod) + jukeOffset) % 2 == 0 ? 1 : -1;

        private BrainDecision Search(IShipStatus status)
        {
            var self = status.Kinematics;
            if (!searching) searchCenter = wreck ?? self.pos;
            if (!searching || Vector2.Distance(self.pos, searchWaypoint) <= ArchetypeRoam.ArrivalRadius)
            {
                searchWaypoint = ArchetypeRoam.WanderWaypoint(searchCenter, searchRadius, rng);
                searching = true;
            }
            var seek = ArchetypeRoam.SeekVelocity(self.pos, searchWaypoint, speedFraction * status.Dynamics.maxSpeed);
            return new BrainDecision(NavObjective.Planar(ArchetypeSteering.BorderTangentSteer(self.pos, seek,
                arenaCenter, borderRadius, ArchetypeSteering.BorderMargin)));
        }

        private BrainDecision Pack(IShipStatus status, Vector2 lawVelocity, bool primary, bool secondary, bool facing)
        {
            var self = status.Kinematics;
            var urgency = Evade(status, out var escapeHeading);
            var velocity = ArchetypeLaws.EvasionVelocity(lawVelocity, escapeHeading, status.Dynamics.maxSpeed, urgency);
            var worldVelocity = ArchetypeSteering.BorderTangentSteer(self.pos, velocity, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
            var builder = NavObjective.Anchored(target.Id).Planar(worldVelocity);
            if (facing) builder = builder.Facing(0f, 1f - urgency);
            var boost = urgency > 0f && urgency >= boostUrgency
                && Vector2.Angle(self.Forward, escapeHeading) <= boostAlignment;
            return new BrainDecision(builder, primary, secondary, boost);
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

            if (threatSeenTick < 0) threatSeenTick = tick;
            if ((tick - threatSeenTick) * Time.fixedDeltaTime < reactionDelay) return 0f;

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
