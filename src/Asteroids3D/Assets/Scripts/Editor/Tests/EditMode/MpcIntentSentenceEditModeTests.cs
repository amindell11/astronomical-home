#if UNITY_EDITOR
using AI;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the intent-sentence additions (doc/Feature_Plans/Intent_Grammar.md): the POS and LANE terms' normalization contracts, frame/referent resolution, the error-relative POS width law, FIELD's turn-away-only authority, synthetic-referent extrapolation, the AIM (FacingCost) normalization contract, and the sentence-slot generalizations of idleness and the world velocity reference.</summary>
    [Category("MPC")]
    public class MpcIntentSentenceEditModeTests
    {
        private const float PosWidth = 10f;
        private const float LaneWidth = 8f;
        private const float LaneRange = 60f;

        private static Config BareConfig() => new()
        {
            dt = 0.1f, horizon = 17,
            wFacing = 1f, facingWidth = 0.5f, facingTarget = float.NaN,
            wPos = 2f, posWidth = PosWidth,
            wLane = 2f, laneRange = LaneRange, laneWidth = LaneWidth,
            maxSpeedSq = 100f,
            wVelTrack = 5f,
        };

        private static IntentSentence PosSentence(float offsetR, float offsetThetaRad, float setpoint,
            float weight, ReferentFrame frame = ReferentFrame.Position, int referent = 0) =>
            new()
            {
                pos = new PosSlot
                {
                    armed = true,
                    offsetR = offsetR,
                    offsetThetaRad = offsetThetaRad,
                    setpoint = setpoint,
                    weight = weight,
                    referent = referent,
                    frame = frame,
                },
            };

        // ---- POS normalization contract (rule 6: 0-1-ish per step, tested not asserted) ----

        [Test]
        public void RingCost_ZeroAtThePoint_AndOnTheRing()
        {
            var point = new float2(3f, -4f);
            Assert.That(Cost.RingCost(point, point, setpoint: 0f, PosWidth), Is.EqualTo(0f),
                "setpoint 0 = be-at-point: standing on it costs nothing");
            Assert.That(Cost.RingCost(new float2(3f, 8f), point, setpoint: 12f, PosWidth), Is.EqualTo(0f).Within(1e-6f),
                "setpoint r₀ = hold-ring: standing anywhere on the ring costs nothing");
        }

        [Test]
        public void RingCost_HalfAtPosWidth_SaturatesBelowOne()
        {
            Assert.That(Cost.RingCost(new float2(PosWidth, 0f), float2.zero, 0f, PosWidth),
                Is.EqualTo(0.5f).Within(1e-5f), "posWidth is the half-cost error by construction");
            var far = Cost.RingCost(new float2(100f * PosWidth, 0f), float2.zero, 0f, PosWidth);
            Assert.That(far, Is.LessThan(1f), "the contract is a bounded 0-1 envelope");
            Assert.That(far, Is.GreaterThan(0.99f), "…that saturates toward 1, not a hard clip");
        }

        [Test]
        public void RingCost_SymmetricAboutTheRing()
        {
            // 5 m inside and 5 m outside a 20 m ring cost the same — the ring, not the point, is the geometry.
            var inside = Cost.RingCost(new float2(0f, 15f), float2.zero, 20f, PosWidth);
            var outside = Cost.RingCost(new float2(0f, 25f), float2.zero, 20f, PosWidth);
            Assert.That(inside, Is.EqualTo(outside).Within(1e-6f));
            Assert.That(inside, Is.GreaterThan(0f));
        }

        [Test]
        public void FacingCost_NormalizedZeroToOne()
        {
            Assert.That(Cost.FacingCost(0.3f, 0.3f, width: 0.5f), Is.EqualTo(0f), "on target costs nothing");
            Assert.That(Cost.FacingCost(math.PI, 0f, width: 0.5f), Is.EqualTo(1f).Within(1e-5f),
                "the worst possible error (π) is the 1.0 ceiling");
            Assert.That(Cost.FacingCost(0.5f, 0f, width: 0.5f),
                Is.LessThan(Cost.FacingCost(1.5f, 0f, width: 0.5f)), "monotone in error");
        }

        // ---- POS resolution: frames, referents, invalidation ----

        [Test]
        public void PosPoint_PositionFrame_OffsetsInWorldAxes()
        {
            // θ = 0 points world +Y (the yaw-0 forward); the offset rides the referent's position, not its pose.
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyYaw = 2f,   // pose must not matter in the position frame
                sentence = PosSentence(offsetR: 5f, offsetThetaRad: 0f, setpoint: 0f, weight: 1f),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.posPoint.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(ctx.posPoint.y, Is.EqualTo(15f).Within(1e-5f));
            Assert.That(ctx.posWeightScale, Is.EqualTo(1f));
        }

        [Test]
        public void PosPoint_FacingFrame_RotatesWithTheReferentNose()
        {
            // Referent yaw π/2 (CCW, nose toward −X): a forward offset lands 5 m to its −X.
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyYaw = 0.5f * math.PI,
                sentence = PosSentence(5f, 0f, 0f, 1f, ReferentFrame.Facing),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.posPoint.x, Is.EqualTo(-5f).Within(1e-5f));
            Assert.That(ctx.posPoint.y, Is.EqualTo(10f).Within(1e-5f));
        }

        [Test]
        public void PosPoint_VelocityFrame_FollowsTheReferentVelocity_WorldFallbackAtRest()
        {
            var moving = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyVel = new float2(1f, 0f),
                enemyYaw = 0f,
                sentence = PosSentence(5f, 0f, 0f, 1f, ReferentFrame.Velocity),
            };
            // Step 0: forward-of-velocity = +X, so the point sits 5 m down-track of the extrapolated referent.
            var ctx = Cost.EvalContext.Create(default, moving, BareConfig(), 0);
            Assert.That(ctx.posPoint.x, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(ctx.posPoint.y, Is.EqualTo(10f).Within(1e-4f));

            var atRest = moving;
            atRest.enemyVel = default;
            var restCtx = Cost.EvalContext.Create(default, atRest, BareConfig(), 0);
            Assert.That(restCtx.posPoint.y, Is.EqualTo(15f).Within(1e-4f),
                "near rest the velocity direction is meaningless — the frame falls back to world axes");
        }

        [Test]
        public void Pos_NoReferent_DropsToWeightZero()
        {
            var input = new CostInput
            {
                enemyYaw = float.NaN,
                sentence = PosSentence(5f, 0f, 0f, 1f),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.posWeightScale, Is.EqualTo(0f), "referent invalidation is defined behavior, not an error");
            Assert.That(Cost.Pos(default, ctx, BareConfig()), Is.EqualTo(0f));
        }

        [Test]
        public void SyntheticReferent_ExtrapolatesLinearly_PerStep()
        {
            // dt 0.1 × step 5 = 0.5 s: the snapshot at (0,10) moving +X at 2 m/s resolves to (1,10).
            var input = new CostInput
            {
                enemyYaw = float.NaN,   // no enemy at all — the slot lives on the synthetic referent alone
                referent1 = new ReferentSnapshot { valid = true, pos = new float2(0f, 10f), vel = new float2(2f, 0f) },
                sentence = PosSentence(0f, 0f, 0f, 1f, referent: 1),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), step: 5);
            Assert.That(ctx.posPoint.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ctx.posPoint.y, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(ctx.posWeightScale, Is.EqualTo(1f));
        }

        [Test]
        public void SyntheticReferent_Invalid_DropsItsSlot()
        {
            var input = new CostInput
            {
                enemyYaw = 0f,
                enemyPos = new float2(0f, 10f),
                referent2 = new ReferentSnapshot { valid = false },
                sentence = PosSentence(0f, 0f, 0f, 1f, referent: 2),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.posWeightScale, Is.EqualTo(0f),
                "a despawned referent silences its slot; the live enemy must not stand in for it");
        }

        [Test]
        public void SyntheticReferent_ThirdSeat_ResolvesLikeTheFirstTwo()
        {
            // Seat 3 exists because AIM/POS/VEL can each bind a distinct rock — one seat per slot.
            var input = new CostInput
            {
                enemyYaw = float.NaN,
                referent3 = new ReferentSnapshot { valid = true, pos = new float2(0f, 10f), vel = new float2(2f, 0f) },
                sentence = PosSentence(0f, 0f, 0f, 1f, referent: 3),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), step: 5);
            Assert.That(ctx.posPoint.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ctx.posPoint.y, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(ctx.posWeightScale, Is.EqualTo(1f));
        }

        [Test]
        public void SyntheticReferent_ThirdSeatInvalid_DropsItsSlot()
        {
            var input = new CostInput
            {
                enemyYaw = 0f,
                enemyPos = new float2(0f, 10f),
                referent3 = new ReferentSnapshot { valid = false },
                sentence = PosSentence(0f, 0f, 0f, 1f, referent: 3),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.posWeightScale, Is.EqualTo(0f),
                "a despawned referent silences its slot; the live enemy must not stand in for it");
        }

        [Test]
        public void Aim_CanBindASyntheticReferent()
        {
            // AIM on referent 1 with no enemy: instance slots generalize past the bound enemy (rig rows depend on this).
            var input = new CostInput
            {
                enemyYaw = float.NaN,
                referent1 = new ReferentSnapshot { valid = true, pos = new float2(0f, 10f) },
                sentence = new IntentSentence { aim = new AimSlot { armed = true, weight = 1f, referent = 1 } },
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.facingTarget, Is.EqualTo(0f).Within(1e-4f), "bearing to the snapshot dead ahead on +Y");
        }

        // ---- VEL frames: the basis forward swaps per frame, the LOS basis stays the Position default ----

        private static IntentSentence VelSentence(float radial, float tangential, float weight,
            ReferentFrame frame = ReferentFrame.Position, int referent = 0) =>
            new()
            {
                vel = new VelSlot
                {
                    armed = true,
                    radialSpeed = radial,
                    tangentialSpeed = tangential,
                    weight = weight,
                    referent = referent,
                    frame = frame,
                },
            };

        [Test]
        public void VelocityRef_PositionFrame_IsTheLiveLosBasis()
        {
            // Ship at origin, referent at (0,10) moving +X: losHat (0,1), tangentHat (1,0) — vr closes, vt orbits CCW.
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyVel = new float2(1f, 0f),
                enemyYaw = 0f,
                sentence = VelSentence(2f, 3f, 1f),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.velocityRef.x, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(ctx.velocityRef.y, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(ctx.velTrackScale, Is.EqualTo(1f));
        }

        [Test]
        public void VelocityRef_FacingFrame_RidesTheReferentNose_NotTheLos()
        {
            // Referent yaw π/2 (nose toward −X): radial rides the nose, tangential keeps the LOS handedness.
            var input = new CostInput
            {
                enemyPos = new float2(0f, 10f),
                enemyVel = new float2(1f, 0f),
                enemyYaw = 0.5f * math.PI,
                sentence = VelSentence(2f, 3f, 1f, ReferentFrame.Facing),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.velocityRef.x, Is.EqualTo(-1f).Within(1e-4f), "refVel (1,0) + 2·forward (−1,0) + 3·side (0,1)");
            Assert.That(ctx.velocityRef.y, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void VelocityRef_VelocityFrame_FollowsTheReferentMotion_WorldFallbackAtRest()
        {
            var moving = new CostInput
            {
                enemyPos = new float2(10f, 0f),   // LOS deliberately ⊥ the motion: the frame, not the LOS, must win
                enemyVel = new float2(0f, 2f),
                enemyYaw = 0f,
                sentence = VelSentence(4f, 0f, 1f, ReferentFrame.Velocity),
            };
            var ctx = Cost.EvalContext.Create(default, moving, BareConfig(), 0);
            Assert.That(ctx.velocityRef.x, Is.EqualTo(0f).Within(1e-4f), "radial rides down-track of the motion");
            Assert.That(ctx.velocityRef.y, Is.EqualTo(6f).Within(1e-4f), "refVel (0,2) + 4·forward (0,1)");

            var atRest = moving;
            atRest.enemyVel = default;
            var restCtx = Cost.EvalContext.Create(default, atRest, BareConfig(), 0);
            Assert.That(restCtx.velocityRef.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(restCtx.velocityRef.y, Is.EqualTo(4f).Within(1e-4f),
                "near rest the velocity direction is meaningless — the frame falls back to world axes");
        }

        // ---- POS in the evaluated cost: signed weight, terminal ramp ----

        [Test]
        public void Pos_SignedWeight_FlipsToRepulsion()
        {
            var cfg = BareConfig();
            var attract = new CostInput { enemyPos = new float2(0f, 10f), enemyYaw = 0f, sentence = PosSentence(0f, 0f, 0f, 1f) };
            var repel = attract;
            repel.sentence = PosSentence(0f, 0f, 0f, -1f);

            var s = new State { pos = new float2(0f, 30f) };
            var attractCost = Cost.Pos(s, Cost.EvalContext.Create(s, attract, cfg, 0), cfg);
            var repelCost = Cost.Pos(s, Cost.EvalContext.Create(s, repel, cfg, 0), cfg);

            Assert.That(attractCost, Is.GreaterThan(0f));
            Assert.That(repelCost, Is.EqualTo(-attractCost).Within(1e-6f),
                "attract/repel is the weight's sign, not a discrete branch");
        }

        [Test]
        public void Pos_RidesTheTerminalRamp()
        {
            var cfg = BareConfig();
            cfg.terminalMultiplier = 10f;
            cfg.terminalCurve = 1f;
            var input = new CostInput
            {
                velocityReference = new float2(float.NaN, float.NaN),   // no tracker, no priors: POS is the only live term
                enemyPos = new float2(0f, 10f),
                enemyYaw = 0f,
                sentence = PosSentence(0f, 0f, 0f, 1f),
            };
            var s = new State { pos = new float2(0f, 40f) };

            var early = Cost.Evaluate(s, default, default, input, cfg, step: 0);
            var late = Cost.Evaluate(s, default, default, input, cfg, step: cfg.horizon - 1);
            Assert.That(early, Is.GreaterThan(0f));
            Assert.That(late, Is.EqualTo(early * (1f + cfg.terminalMultiplier)).Within(1e-3f).Percent,
                "POS is a state cost: it must scale with the terminal ramp like facing does");
        }

        // ---- LANE normalization contract (rule 6) and resolution ----

        private static IntentSentence LaneSentence(float weight) =>
            new() { lane = new LaneSlot { armed = true, weight = weight } };

        // Enemy at (0,10), yaw 0 (fwd = +Y): the lane runs (0,10) → (0,10+LaneRange).
        private static CostInput LaneInput(float weight) => new()
        {
            enemyPos = new float2(0f, 10f),
            enemyYaw = 0f,
            sentence = LaneSentence(weight),
        };

        [Test]
        public void LaneCost_ZeroOnTheSegment()
        {
            var start = new float2(0f, 10f);
            var end = new float2(0f, 70f);
            Assert.That(Cost.LaneCost(start, start, end, LaneWidth), Is.EqualTo(0f));
            Assert.That(Cost.LaneCost(new float2(0f, 40f), start, end, LaneWidth), Is.EqualTo(0f));
            Assert.That(Cost.LaneCost(end, start, end, LaneWidth), Is.EqualTo(0f));
        }

        [Test]
        public void LaneCost_HalfAtLaneWidth_SaturatesBelowOne()
        {
            var start = new float2(0f, 10f);
            var end = new float2(0f, 70f);
            Assert.That(Cost.LaneCost(new float2(LaneWidth, 40f), start, end, LaneWidth),
                Is.EqualTo(0.5f).Within(1e-5f), "laneWidth is the half-cost lateral error by construction");
            var far = Cost.LaneCost(new float2(100f * LaneWidth, 40f), start, end, LaneWidth);
            Assert.That(far, Is.LessThan(1f), "the contract is a bounded 0-1 envelope");
            Assert.That(far, Is.GreaterThan(0.99f), "…that saturates toward 1, not a hard clip");
        }

        [Test]
        public void LaneCost_BeyondTheEnds_MeasuresToTheEndpoint()
        {
            // 10 m past the far end and 10 m lateral cost the same — the lane is a segment, not a line;
            // behind the enemy is off-lane too.
            var start = new float2(0f, 10f);
            var end = new float2(0f, 70f);
            var past = Cost.LaneCost(new float2(0f, 80f), start, end, LaneWidth);
            var lateral = Cost.LaneCost(new float2(10f, 40f), start, end, LaneWidth);
            var behind = Cost.LaneCost(new float2(0f, 0f), start, end, LaneWidth);
            Assert.That(past, Is.EqualTo(lateral).Within(1e-6f));
            Assert.That(behind, Is.EqualTo(lateral).Within(1e-6f));
            Assert.That(past, Is.GreaterThan(0f));
        }

        [Test]
        public void Lane_SegmentRidesTheEnemyFacing()
        {
            // Enemy yaw π/2 (CCW, nose toward −X): the lane runs down −X from the enemy.
            var input = LaneInput(1f);
            input.enemyYaw = 0.5f * math.PI;
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.laneStart.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(ctx.laneStart.y, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(ctx.laneEnd.x, Is.EqualTo(-LaneRange).Within(1e-4f));
            Assert.That(ctx.laneEnd.y, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(ctx.laneWeightScale, Is.EqualTo(1f));
        }

        [Test]
        public void Lane_PerStep_FollowsTheMovingEnemy()
        {
            // dt 0.1 × step 5 = 0.5 s: the enemy at (0,10) moving +X at 2 m/s carries its lane to (1,10).
            var input = LaneInput(1f);
            input.enemyVel = new float2(2f, 0f);
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), step: 5);
            Assert.That(ctx.laneStart.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ctx.laneStart.y, Is.EqualTo(10f).Within(1e-5f));
        }

        [Test]
        public void Lane_SignedWeight_FlipsToRepulsion()
        {
            var cfg = BareConfig();
            var s = new State { pos = new float2(10f, 40f) };
            var attractCost = Cost.Lane(s, Cost.EvalContext.Create(s, LaneInput(1f), cfg, 0), cfg);
            var repelCost = Cost.Lane(s, Cost.EvalContext.Create(s, LaneInput(-1f), cfg, 0), cfg);
            Assert.That(attractCost, Is.GreaterThan(0f));
            Assert.That(repelCost, Is.EqualTo(-attractCost).Within(1e-6f),
                "hold/dodge is the weight's sign, not a discrete branch");
        }

        [Test]
        public void Lane_NoEnemy_DropsToWeightZero()
        {
            var input = LaneInput(1f);
            input.enemyYaw = float.NaN;
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.laneWeightScale, Is.EqualTo(0f), "referent invalidation is defined behavior, not an error");
            Assert.That(Cost.Lane(default, ctx, BareConfig()), Is.EqualTo(0f));
        }

        [Test]
        public void Lane_RidesTheTerminalRamp()
        {
            var cfg = BareConfig();
            cfg.terminalMultiplier = 10f;
            cfg.terminalCurve = 1f;
            var input = LaneInput(1f);
            input.velocityReference = new float2(float.NaN, float.NaN);   // no tracker, no priors: LANE is the only live term
            var s = new State { pos = new float2(20f, 40f) };

            var early = Cost.Evaluate(s, default, default, input, cfg, step: 0);
            var late = Cost.Evaluate(s, default, default, input, cfg, step: cfg.horizon - 1);
            Assert.That(early, Is.GreaterThan(0f));
            Assert.That(late, Is.EqualTo(early * (1f + cfg.terminalMultiplier)).Within(1e-3f).Percent,
                "LANE is a state cost: it must scale with the terminal ramp like POS does");
        }

        // ---- Error-relative POS width (per-solve law; the floor is the asset posWidth) ----

        [Test]
        public void EffectivePosWidth_FloorsAtPosWidth_NearThePoint()
        {
            var input = new CostInput { enemyPos = new float2(0f, 10f), enemyYaw = 0f, sentence = PosSentence(0f, 0f, 0f, 1f) };
            var initial = new State { pos = new float2(0f, 8f) };   // err₀ = 2 → slope·err₀ under the floor
            Assert.That(Cost.EffectivePosWidth(initial, input, BareConfig(), slope: 0.65f), Is.EqualTo(PosWidth));
        }

        [Test]
        public void EffectivePosWidth_ScalesWithInitialError()
        {
            var input = new CostInput { enemyPos = new float2(0f, 90f), enemyYaw = 0f, sentence = PosSentence(0f, 0f, 0f, 1f) };
            Assert.That(Cost.EffectivePosWidth(default, input, BareConfig(), slope: 2f / 3f),
                Is.EqualTo(60f).Within(1e-4f), "the 90 m minefield leg gets the hand-tuned 60 — the ratio that set the slope");
        }

        [Test]
        public void EffectivePosWidth_ErrorIsSetpointRelative()
        {
            // 52 m from the point with a 12 m hold-ring: the ring error 40, not the raw distance, drives the width.
            var input = new CostInput { enemyPos = new float2(0f, 52f), enemyYaw = 0f, sentence = PosSentence(0f, 0f, 12f, 1f) };
            Assert.That(Cost.EffectivePosWidth(default, input, BareConfig(), slope: 0.65f),
                Is.EqualTo(26f).Within(1e-4f));
        }

        [Test]
        public void EffectivePosWidth_UnarmedUnresolvedOrDisabled_KeepsTheFloor()
        {
            var cfg = BareConfig();
            var far = new CostInput { enemyPos = new float2(0f, 100f), enemyYaw = 0f, sentence = PosSentence(0f, 0f, 0f, 1f) };

            Assert.That(Cost.EffectivePosWidth(default, new CostInput { enemyYaw = 0f }, cfg, 0.65f),
                Is.EqualTo(PosWidth), "POS unarmed → the width law never runs");

            var unresolved = far;
            unresolved.enemyYaw = float.NaN;
            Assert.That(Cost.EffectivePosWidth(default, unresolved, cfg, 0.65f),
                Is.EqualTo(PosWidth), "no referent → the slot is silent, the width stays the floor");

            Assert.That(Cost.EffectivePosWidth(default, far, cfg, slope: 0f),
                Is.EqualTo(PosWidth), "slope 0 disables — fixed width is still expressible");
        }

        // ---- FIELD: turn-away authority, un-zeroable collision penalty ----

        private static Config ObstacleConfig() => new()
        {
            dt = 0.1f, horizon = 17,
            facingTarget = float.NaN,
            wObstacle = 5f, collisionPenalty = 10000f, collisionSafetyMargin = 0.3f,
            maxBankAngleRad = 35f * Mathf.Deg2Rad, maxSpeedSq = 900f,
            shipRadius = 1.4f, maxLatAccel = 6f,
        };

        private static void WithObstacle(float2 obstaclePos, IntentSentence sentence,
            System.Action<CostInput> body)
        {
            var obstacles = new NativeArray<ObstacleData>(1, Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = obstaclePos, radius = 2f, weight = 1f };
                body(new CostInput
                {
                    obstacles = obstacles,
                    obstacleCount = 1,
                    enemyYaw = float.NaN,
                    sentence = sentence,
                });
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        private static IntentSentence Field(float weight) =>
            new() { field = new FieldSlot { armed = true, weight = weight } };

        // Moving straight at an obstacle 8 m ahead → the turn-away branch is live, the hull still clear.
        private static readonly State OnCollisionCourse = new() { pos = float2.zero, vel = new float2(0f, 20f) };

        [Test]
        public void Field_Weight_ScalesTheTurnAwayBranchLinearly()
        {
            var cfg = ObstacleConfig();
            WithObstacle(new float2(0f, 8f), default, unarmedInput =>
            {
                var unarmed = Cost.EvaluateBreakdown(OnCollisionCourse, default, default, unarmedInput, cfg).obstacle;
                Assert.That(unarmed, Is.GreaterThan(0f), "fixture must exercise turn-away");

                WithObstacle(new float2(0f, 8f), Field(0.5f), halfInput =>
                {
                    var half = Cost.EvaluateBreakdown(OnCollisionCourse, default, default, halfInput, cfg).obstacle;
                    Assert.That(half, Is.EqualTo(0.5f * unarmed).Within(1e-4f).Percent,
                        "FIELD authority multiplies the character ceiling, exactly");
                });

                WithObstacle(new float2(0f, 8f), Field(0f), zeroInput =>
                {
                    var zeroed = Cost.EvaluateBreakdown(OnCollisionCourse, default, default, zeroInput, cfg);
                    Assert.That(zeroed.obstacle, Is.EqualTo(0f), "drift-hold: FIELD 0 = no hazard shaping");
                });
            });
        }

        [Test]
        public void Field_Zero_CannotZeroTheCollisionPenalty()
        {
            var cfg = ObstacleConfig();
            // Overlapping hull: the penalty branch, which no sentence may weaken (no suicide channel).
            WithObstacle(new float2(0f, 1f), Field(0f), input =>
            {
                var breakdown = Cost.EvaluateBreakdown(new State { pos = float2.zero }, default, default, input, cfg);
                Assert.That(breakdown.collision, Is.EqualTo(cfg.collisionPenalty));
            });
        }

        // ---- World-reference NaN sentinel ----

        [Test]
        public void NaNVelocityReference_DropsTheTrackerInsteadOfCommandingAStop()
        {
            var input = new CostInput
            {
                velocityReference = new float2(float.NaN, float.NaN),
                enemyPos = new float2(0f, 10f),
                enemyYaw = 0f,
                sentence = PosSentence(0f, 0f, 0f, 1f),
            };
            var ctx = Cost.EvalContext.Create(default, input, BareConfig(), 0);
            Assert.That(ctx.velTrackScale, Is.EqualTo(0f),
                "a sentence-only objective has no velocity command; tracking to zero would fight POS");
            Assert.That(math.any(math.isnan(ctx.velocityRef)), Is.False, "no NaN may reach the tracker math");
        }

        // ---- NavObjective: sentence authoring and the generalized idle gate ----

        private static readonly ShipId AnchorId = new(1);

        [Test]
        public void Builder_Position_ArmsThePosSlot()
        {
            var objective = (NavObjective)NavObjective.Anchored(AnchorId)
                .Position(offsetR: 3f, offsetThetaRad: 0.5f, setpoint: 12f, authority: 0.7f, ReferentFrame.Facing);

            Assert.That(objective.sentence.pos.armed, Is.True);
            Assert.That(objective.sentence.pos.offsetR, Is.EqualTo(3f));
            Assert.That(objective.sentence.pos.offsetThetaRad, Is.EqualTo(0.5f));
            Assert.That(objective.sentence.pos.setpoint, Is.EqualTo(12f));
            Assert.That(objective.sentence.pos.weight, Is.EqualTo(0.7f));
            Assert.That(objective.sentence.pos.frame, Is.EqualTo(ReferentFrame.Facing));
            Assert.That(objective.IsIdle, Is.False, "a POS-only objective must solve, not reset");
        }

        [Test]
        public void Builder_Field_ArmsTheFieldSlot()
        {
            var objective = (NavObjective)NavObjective.Anchored(AnchorId).Field(0.3f);
            Assert.That(objective.sentence.field.armed, Is.True);
            Assert.That(objective.sentence.field.weight, Is.EqualTo(0.3f));
            Assert.That(objective.IsIdle, Is.False, "a FIELD-only objective must solve, not reset");
        }

        [Test]
        public void IsIdle_ArmedZeroWeightSentence_IsNotIdle()
        {
            // The drift-hold distinction: an explicit all-weights-0 sentence solves on the priors; only an absent one idles.
            var driftHold = (NavObjective)NavObjective.Anchored(AnchorId)
                .Facing(0f, 0f).Velocity(0f, 0f, 0f).Field(0f);
            Assert.That(driftHold.IsIdle, Is.False);
            Assert.That(NavObjective.Drift.IsIdle, Is.True);
        }
    }
}
#endif
