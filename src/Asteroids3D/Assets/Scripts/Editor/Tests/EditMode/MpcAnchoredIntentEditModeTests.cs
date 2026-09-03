#if UNITY_EDITOR
using AI;
using AI.Context;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the anchored-intent semantics — the enemy-bound AIM+VEL degenerate sentence (doc/Glossary.md → anchored intent): the enemy-polar frame is relative to the enemy's motion, vr &gt; 0 closes along +losHat, vt &gt; 0 and positive facing offsets are CCW, references re-resolve per rollout step from the rolled ship pos, weight-0 falls back to the delegation priors, and the default (nothing-armed) sentence leaves the legacy world-frame path unchanged.</summary>
    [Category("MPC")]
    public class MpcAnchoredIntentEditModeTests
    {
        private static Config BareConfig() => new()
        {
            dt = 0.1f, horizon = 17,
            wFacing = 1f, facingWidth = 0.5f, facingTarget = float.NaN,
            maxSpeedSq = 100f,
            wVelTrack = 5f,
        };

        private static IntentSentence Velocity(float vr, float vt, float weight = 1f) =>
            new() { vel = new VelSlot { armed = true, radialSpeed = vr, tangentialSpeed = vt, weight = weight } };

        // ---- Anchored velocity reference (closed-form) ----

        [Test]
        public void VelocityRef_PositiveRadial_ClosesAlongLos()
        {
            // Enemy dead ahead on +Y, stationary: losHat = (0,1), so vr > 0 must point AT the enemy.
            var vRef = Cost.AnchoredVelocityRef(float2.zero, new float2(0f, 10f), float2.zero, radialSpeed: 3f, tangentialSpeed: 0f);
            Assert.That(vRef.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(vRef.y, Is.EqualTo(3f).Within(1e-5f), "vr > 0 closes along +losHat");
        }

        [Test]
        public void VelocityRef_PositiveTangential_OrbitsCcw()
        {
            // Ship due south of the enemy: a CCW orbit (viewed on the XY plane) moves +X there.
            var vRef = Cost.AnchoredVelocityRef(float2.zero, new float2(0f, 10f), float2.zero, radialSpeed: 0f, tangentialSpeed: 4f);
            Assert.That(vRef.x, Is.EqualTo(4f).Within(1e-5f), "vt > 0 is CCW around the enemy");
            Assert.That(vRef.y, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void VelocityRef_IsRelativeToEnemyMotion()
        {
            // Zero polar command against a moving enemy = match its velocity (an orbit is an orbit because the frame moves).
            var vRef = Cost.AnchoredVelocityRef(float2.zero, new float2(0f, 10f), new float2(3f, -1f), radialSpeed: 0f, tangentialSpeed: 0f);
            Assert.That(vRef.x, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(vRef.y, Is.EqualTo(-1f).Within(1e-5f));
        }

        [Test]
        public void VelocityRef_IsDeliberatelyUnclamped()
        {
            // Closing on a fleeing enemy can command more than maxSpeed — best-effort tracking is the honest semantics.
            var vRef = Cost.AnchoredVelocityRef(float2.zero, new float2(0f, 10f), new float2(0f, 10f), radialSpeed: 10f, tangentialSpeed: 0f);
            Assert.That(math.length(vRef), Is.EqualTo(20f).Within(1e-4f));
        }

        [Test]
        public void VelocityRef_ZeroRange_CollapsesToVelocityMatch()
        {
            // Polar directions are undefined at zero range: both components drop, reference = enemy velocity.
            var vRef = Cost.AnchoredVelocityRef(new float2(5f, 5f), new float2(5f, 5f), new float2(2f, 1f), radialSpeed: 9f, tangentialSpeed: 9f);
            Assert.That(vRef.x, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(vRef.y, Is.EqualTo(1f).Within(1e-5f));
        }

        // ---- Anchored facing target ----

        [Test]
        public void AnchorYaw_WithProjectileSpeed_IsTheInterceptYaw()
        {
            var shipPos = new float2(1f, -2f);
            var enemyPos = new float2(20f, 6f);
            var enemyVel = new float2(-3f, 2f);
            Assert.That(Cost.AnchorYaw(shipPos, enemyPos, enemyVel, 50f),
                Is.EqualTo(Cost.InterceptYaw(shipPos, enemyPos, enemyVel, 50f)),
                "offset 0 at full authority must reproduce the legacy intercept aim exactly");
        }

        [Test]
        public void AnchorYaw_Hitscan_IsThePureBearing()
        {
            // Enemy on +X from the ship: bearing yaw = −π/2 (MPC convention fwd = (−sin, cos)). Enemy velocity must not lead a hitscan aim.
            Assert.That(Cost.AnchorYaw(float2.zero, new float2(10f, 0f), new float2(0f, 99f), 0f),
                Is.EqualTo(-0.5f * math.PI).Within(1e-4f));
        }

        [Test]
        public void FacingTarget_PositiveOffset_RotatesCcwFromIntercept()
        {
            // Enemy dead ahead (+Y, bearing yaw 0), hitscan: offset +π/2 must land at yaw +π/2 (CCW, toward −X).
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyYaw = 0f,
                sentence = new IntentSentence { aim = new AimSlot { armed = true, offsetRad = 0.5f * math.PI, weight = 1f } },
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), step: 0);
            Assert.That(ctx.facingTarget, Is.EqualTo(0.5f * math.PI).Within(1e-4f));
            Assert.That(ctx.facingWeightScale, Is.EqualTo(1f));
        }

        [Test]
        public void FacingTarget_OffsetPi_FacesAway_WrappedIntoRange()
        {
            // "Face away while evading" = constant offset π; the composed target must come back wrapped to [−π, π].
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyYaw = 0f,
                sentence = new IntentSentence { aim = new AimSlot { armed = true, offsetRad = math.PI, weight = 1f } },
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), step: 0);
            Assert.That(math.abs(ctx.facingTarget), Is.EqualTo(math.PI).Within(1e-4f));
            Assert.That(ctx.facingTarget, Is.InRange(-math.PI, math.PI));
        }

        // ---- Per-step re-resolution ----

        [Test]
        public void Resolution_TracksTheEnemyRolloutPerStep()
        {
            var cfg = BareConfig();
            var enemyStates = new NativeArray<State>(2, Allocator.Temp);
            try
            {
                enemyStates[0] = new State { pos = new float2(0f, 10f) };
                enemyStates[1] = new State { pos = new float2(10f, 0f) };
                var input = new CostInput
                {
                    enemyPos = new float2(0f, 10f),
                    enemyYaw = 0f,
                    enemyStates = enemyStates,
                    enemyStateCount = 2,
                    sentence = new IntentSentence
                    {
                        aim = new AimSlot { armed = true, weight = 1f },
                        vel = new VelSlot { armed = true, radialSpeed = 3f, weight = 1f },
                    },
                };

                var step0 = Cost.EvalContext.Create(default, input, cfg, step: 0);
                var step1 = Cost.EvalContext.Create(default, input, cfg, step: 1);

                Assert.That(step0.velocityRef.y, Is.EqualTo(3f).Within(1e-5f), "step 0: enemy on +Y → close along +Y");
                Assert.That(step1.velocityRef.x, Is.EqualTo(3f).Within(1e-5f), "step 1: enemy on +X → close along +X");
                Assert.That(step0.facingTarget, Is.Not.EqualTo(step1.facingTarget).Within(1e-3f),
                    "the facing anchor must re-resolve against the rolled enemy, not the decision-time snapshot");
            }
            finally
            {
                enemyStates.Dispose();
            }
        }

        [Test]
        public void Resolution_UsesTheRolledShipPos()
        {
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyYaw = 0f,
                sentence = Velocity(vr: 3f, vt: 0f),
            };
            var cfg = BareConfig();
            var fromOrigin = Cost.EvalContext.Create(new State { pos = float2.zero }, input, cfg, 0);
            var fromEast = Cost.EvalContext.Create(new State { pos = new float2(10f, 10f) }, input, cfg, 0);
            Assert.That(fromOrigin.velocityRef.y, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(fromEast.velocityRef.x, Is.EqualTo(-3f).Within(1e-5f),
                "a candidate rolled east of the enemy closes westward — the reference follows the rolled position");
        }

        // ---- Authority weights and the delegation priors ----

        [Test]
        public void Weights_ScaleTheirTermsAgainstTheConfigCeiling()
        {
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyYaw = 0f,
                sentence = new IntentSentence
                {
                    aim = new AimSlot { armed = true, offsetRad = 1f, weight = 0.25f },
                    vel = new VelSlot { armed = true, radialSpeed = 3f, weight = 0.5f },
                },
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.facingWeightScale, Is.EqualTo(0.25f));
            Assert.That(ctx.velTrackScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void FacingPrior_AlignsTheNoseWithTravel_SpeedGated()
        {
            var cfg = BareConfig();
            cfg.wFacingPrior = 0.2f;

            // Moving along +Y with the nose on +Y: aligned, zero cost.
            Assert.That(Cost.FacingPriorCost(yaw: 0f, vel: new float2(0f, 5f), cfg), Is.EqualTo(0f).Within(1e-5f));
            // Same speed, nose 90° off: the prior pulls.
            Assert.That(Cost.FacingPriorCost(yaw: 0.5f * math.PI, vel: new float2(0f, 5f), cfg), Is.GreaterThan(0f));
            // Near rest the direction of travel is meaningless — the nose is free.
            Assert.That(Cost.FacingPriorCost(yaw: 0.5f * math.PI, vel: new float2(0.001f, 0f), cfg), Is.EqualTo(0f));
            // Weight 0 disables outright (the production default).
            cfg.wFacingPrior = 0f;
            Assert.That(Cost.FacingPriorCost(yaw: 0.5f * math.PI, vel: new float2(0f, 5f), cfg), Is.EqualTo(0f));
        }

        [Test]
        public void WeightZero_FacingFallsToThePriorAlone()
        {
            var cfg = BareConfig();
            cfg.wFacingPrior = 0.2f;
            var input = new CostInput
            {
                enemyPos = new float2(10f, 0f),
                enemyYaw = 0f,
                sentence = new IntentSentence { aim = new AimSlot { armed = true, weight = 0f } },
            };
            var s = new State { vel = new float2(0f, 5f), yaw = 0.5f * math.PI };
            var ctx = Cost.EvalContext.Create(s, input, cfg, 0);

            Assert.That(Cost.Aim(s, ctx, cfg), Is.EqualTo(0f), "zero authority silences the AIM slot");
            Assert.That(Cost.FacingPriorCost(s.yaw, s.vel, cfg), Is.GreaterThan(0f), "the prior still steers the nose");
        }

        [Test]
        public void TargetlessAnchored_CollapsesToThePriors()
        {
            // No enemy (yaw NaN) with sentence slots armed — the slots drop instead of throwing or tracking garbage.
            var input = new CostInput
            {
                enemyYaw = float.NaN,
                sentence = new IntentSentence
                {
                    aim = new AimSlot { armed = true, weight = 1f },
                    vel = new VelSlot { armed = true, radialSpeed = 5f, weight = 1f },
                },
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(float.IsNaN(ctx.facingTarget), Is.True, "no referent → no facing target (FacingCost reads NaN as 0)");
            Assert.That(ctx.velTrackScale, Is.EqualTo(0f), "no referent → the velocity term drops; the momentum prior carries delegation");
        }

        // ---- Dormancy: the legacy path is bit-unchanged ----

        [Test]
        public void DefaultSentence_LeavesTheLegacyPathUntouched()
        {
            var cfg = BareConfig();
            cfg.facingTarget = 0.7f;
            var input = new CostInput
            {
                velocityReference = new float2(2f, -6f),
                enemyPos = new float2(25f, 10f),
                enemyVel = new float2(-2f, 1f),
                enemyYaw = 1.0f,
                projectileSpeed = 50f,
            };
            var ctx = Cost.EvalContext.Create(default, input, cfg, 0);

            Assert.That(ctx.facingTarget, Is.EqualTo(0.7f),
                "projectileSpeed alone no longer arms intercept-facing — the old silent precedence is gone; intercept aim now arrives as an AIM slot at offset 0");
            Assert.That(ctx.facingWeightScale, Is.EqualTo(1f));
            Assert.That(ctx.velocityRef.x, Is.EqualTo(2f));
            Assert.That(ctx.velocityRef.y, Is.EqualTo(-6f));
            Assert.That(ctx.velTrackScale, Is.EqualTo(1f));
            Assert.That(ctx.posWeightScale, Is.EqualTo(0f), "POS unarmed → no position term");
            Assert.That(ctx.fieldScale, Is.EqualTo(1f), "FIELD unarmed → the turn-away branch runs at the character ceiling");
        }

        // ---- Navigator seam: mapping, contradiction, collapse, arming ----

        private sealed class StubStatus : IShipStatus
        {
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
        }

        private Navigator nav;
        private MpcSettings createdSettings;

        [SetUp]
        public void SetUp()
        {
            var host = new GameObject("AnchoredIntentNav");
            var scout = host.AddComponent<Scout>();
            nav = host.AddComponent<Navigator>();
            nav.Initialize(new StubStatus(), default, scout, new SeedScope(1), primaryProjectileSpeed: 40f);
            createdSettings = nav.mpcSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (nav) Object.DestroyImmediate(nav.gameObject);
            if (createdSettings) Object.DestroyImmediate(createdSettings);
        }

        // The host resolves the anchor, so any valid id serves here.
        private static readonly ShipId AnchorId = new(1);

        private static EnemyTarget Anchor() => new()
        {
            kinematics = new Kinematics(new Vector2(0f, 10f), Vector2.zero, 0f, 0f, 0f),
        };

        [Test]
        public void ApplyObjective_FacingAtZeroFullAuthority_IsTheOldAimAtTarget()
        {
            nav.ApplyObjective(NavObjective.Anchored(AnchorId).Planar(Vector2.zero).Facing(0f, 1f), Anchor());

            Assert.That(nav.sentence.aim.armed, Is.True);
            Assert.That(nav.sentence.aim.offsetRad, Is.EqualTo(0f));
            Assert.That(nav.sentence.aim.weight, Is.EqualTo(1f));
        }

        [Test]
        public void AnchoredBuilder_MoveChannelsAreExclusive()
        {
            var planarWins = (NavObjective)NavObjective.Anchored(AnchorId)
                .Velocity(3f, 0f, 1f)
                .Planar(new Vector2(1f, 2f));
            Assert.That(planarWins.sentence.vel.armed, Is.False,
                "a world reference replaces the polar channel rather than stacking with it");

            var polarWins = (NavObjective)NavObjective.Anchored(AnchorId)
                .Planar(new Vector2(1f, 2f))
                .Velocity(3f, 0f, 1f);
            Assert.That(polarWins.hasPlanarVelocity, Is.False);
            Assert.That(polarWins.sentence.vel.radialSpeed, Is.EqualTo(3f));
        }

        [Test]
        public void ApplyObjective_AnchoredVelocity_FeedsEnemyStateWithoutAFacingChannel()
        {
            nav.ApplyObjective(NavObjective.Anchored(AnchorId).Velocity(3f, 0f, 1f), Anchor());

            Assert.That(nav.ShouldIdle(), Is.False,
                "an enemy-polar move channel arms the navigator on its own");
            Assert.That(float.IsNaN(nav.enemyYaw), Is.False,
                "anchored slots need the enemy in the solver even with no facing command");
        }

        [Test]
        public void ApplyObjective_Planar_ClearsEnemyStateSoTheCostModelCollapsesToPriors()
        {
            nav.ApplyObjective(NavObjective.Anchored(AnchorId).Velocity(3f, 0f, 1f), Anchor());
            nav.ApplyObjective(NavObjective.Planar(new Vector2(3f, 0f)), Anchor());

            Assert.That(nav.sentence.vel.armed, Is.False,
                "an unanchored objective must not leak the prior anchored command");
            Assert.That(float.IsNaN(nav.enemyYaw), Is.True,
                "no instance slot → no enemy state → the cost model collapses to the priors");
        }

        [Test]
        public void ApplyObjective_Drift_ClearsTheSentence()
        {
            nav.ApplyObjective(NavObjective.Anchored(AnchorId).Velocity(3f, 0f, 1f), Anchor());
            nav.ApplyObjective(NavObjective.Drift, Anchor());

            Assert.That(nav.sentence.vel.armed, Is.False, "reset must not leak a stale anchored command");
        }

        [Test]
        public void ApplyObjective_SameObjectiveMovedAnchor_TracksTheLiveEnemy()
        {
            // The objective names a ship; the pose comes from the host's per-tick resolution. Re-applying
            // an unchanged decision against a moved anchor is precisely what a held 5 Hz decision does.
            var objective = (NavObjective)NavObjective.Anchored(AnchorId).Velocity(3f, 0f, 1f);

            nav.ApplyObjective(objective, Anchor());
            var atDecisionTime = nav.enemyPos;

            var moved = Anchor();
            moved.kinematics = new Kinematics(new Vector2(25f, -8f), Vector2.zero, 0f, 0f, 0f);
            nav.ApplyObjective(objective, moved);

            Assert.That(nav.enemyPos.x, Is.EqualTo(25f).Within(1e-4f));
            Assert.That(nav.enemyPos.y, Is.EqualTo(-8f).Within(1e-4f));
            Assert.That(nav.enemyPos, Is.Not.EqualTo(atDecisionTime),
                "a snapshot anchor would pin the enemy frame to the decision-time pose");
        }
    }
}
#endif
