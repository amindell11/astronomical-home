using Movement.MPC;
using Unity.Mathematics;

namespace Game.RLHarness
{
    /// <summary>One tactic-bingo row: a named scenario whose sentence is the tactic written in the grammar. Verdicts (composes / needs new frame or geometry / red flag) are human rulings recorded at #395, not code.</summary>
    public struct BingoRow
    {
        public string name;
        public bool movement; // runs the VEL-zeroed protocol variant (fork 1's falsifier)
        public RigScenario scenario;
    }

    /// <summary>The 14-row tactic-bingo catalog: the 13 Stage A rows (#485) plus Stage C's field-authority row. Hand-authored sentences over scripted stimuli; geometry values are the authoring, not tuned constants.</summary>
    public static class RigBingoCard
    {
        public static BingoRow[] Rows() => new[]
        {
            Orbit(), Kite(), CoverTake(), FireLaneDodge(), MissileDrag(), HerdTowardAsteroid(),
            MineRetreat(), ShootTheRock(), TwoHostileLanes(), WingmanHold(), MinefieldTransit(),
            DummyCloseout(), DriftHold(), FieldAuthority(),
        };

        /// <summary>Fork 1's falsifier arm: authority to zero, slot left armed — the sentence still says VEL, it just carries no weight.</summary>
        public static RigScenario VelZeroed(in RigScenario scenario)
        {
            var copy = scenario;
            copy.intent.vel.weight = 0f;
            return copy;
        }

        /// <summary>The differential-authority arm: FIELD authority to zero, slot left armed — turn-away shaping off, collision penalty untouched.</summary>
        public static RigScenario FieldZeroed(in RigScenario scenario)
        {
            var copy = scenario;
            copy.intent.field.weight = 0f;
            return copy;
        }

        private static BingoRow Orbit()
        {
            var s = Base();
            s.enemyLaw = RigLaw.Static(new float2(0f, 40f), FacingYaw(new float2(0f, 40f), float2.zero));
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 1f },
                vel = new VelSlot { armed = true, tangentialSpeed = 8f, weight = 1f },
            };
            return new BingoRow { name = "orbit", movement = true, scenario = s };
        }

        private static BingoRow Kite()
        {
            var s = Base();
            s.enemyLaw = RigLaw.Pursue(new float2(0f, 55f), speed: 8f);
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 1f },
                vel = new VelSlot { armed = true, radialSpeed = -6f, weight = 1f },
            };
            return new BingoRow { name = "kite", movement = true, scenario = s };
        }

        private static BingoRow CoverTake()
        {
            var s = Base();
            var enemyPos = new float2(0f, 60f);
            var rock = new float2(18f, 30f);
            s.enemyLaw = RigLaw.Static(enemyPos, FacingYaw(enemyPos, float2.zero));
            s.referent1Law = RigLaw.Static(rock);
            s.obstacles = new[] { new RigCircle(rock, 6f) };
            // Best available sentence: a point on the rock's far side from the enemy. The brief expects
            // this row may read "needs new geometry" (a proximity potential) — that is the card working.
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 0.5f },
                pos = new PosSlot
                {
                    armed = true, referent = 1, frame = ReferentFrame.Position,
                    offsetR = 14f, offsetThetaRad = ThetaFromDirection(math.normalize(rock - enemyPos)),
                    weight = 1f,
                },
                field = new FieldSlot { armed = true, weight = 1f },
            };
            // Asset posWidth (10) saturates at the ~31 m approach — no far-field gradient, the ship
            // parks. 20 buys reach at the price of settle tightness; the reach-vs-tightness tension
            // is recorded Stage B evidence for setpoint-relative normalization (brief §blindsiders).
            s.posWidthOverride = 20f;
            return new BingoRow { name = "cover-take", scenario = s };
        }

        private static BingoRow FireLaneDodge()
        {
            var s = Base();
            var enemyPos = new float2(0f, 50f);
            s.enemyLaw = RigLaw.Static(enemyPos, FacingYaw(enemyPos, float2.zero));
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 1f },
                lane = new LaneSlot { armed = true, weight = -1f },
            };
            return new BingoRow { name = "fire-lane-dodge", scenario = s };
        }

        private static BingoRow MissileDrag()
        {
            var s = Base();
            var enemyPos = new float2(0f, 80f);
            s.enemyLaw = RigLaw.Static(enemyPos, FacingYaw(enemyPos, float2.zero));
            s.referent1Law = RigLaw.Pursue(new float2(0f, -50f), speed: 14f);
            s.obstacles = new[]
            {
                new RigCircle(new float2(-12f, 18f), 5f),
                new RigCircle(new float2(10f, 26f), 6f),
                new RigCircle(new float2(-4f, 40f), 5f),
            };
            // Flee the missile with a tangential bias, hazard authority low: willing to thread rocks.
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 0.5f },
                vel = new VelSlot { armed = true, referent = 1, radialSpeed = -12f, tangentialSpeed = 4f, weight = 1f },
                field = new FieldSlot { armed = true, weight = 0.5f },
            };
            return new BingoRow { name = "missile-drag", movement = true, scenario = s };
        }

        private static BingoRow HerdTowardAsteroid()
        {
            var s = Base();
            var enemyStart = new float2(0f, 45f);
            var rock = new float2(0f, 75f);
            s.enemyLaw = RigLaw.ConstantVelocity(enemyStart, new float2(3f, 0f));
            s.obstacles = new[] { new RigCircle(rock, 8f) };
            // Stand on the enemy's far side from the rock, pushing the engagement toward it.
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 1f },
                pos = new PosSlot
                {
                    armed = true, referent = 0, frame = ReferentFrame.Position,
                    offsetR = 22f, offsetThetaRad = ThetaFromDirection(math.normalize(enemyStart - rock)),
                    weight = 1f,
                },
                field = new FieldSlot { armed = true, weight = 1f },
            };
            return new BingoRow { name = "herd-toward-asteroid", scenario = s };
        }

        private static BingoRow MineRetreat()
        {
            var s = Base();
            var enemyPos = new float2(0f, 70f);
            s.enemyLaw = RigLaw.Static(enemyPos, FacingYaw(enemyPos, float2.zero));
            s.referent1Law = RigLaw.Static(new float2(6f, 12f));
            // Negative POS weight repels: cost falls with distance from the mine point.
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 0.6f },
                pos = new PosSlot { armed = true, referent = 1, weight = -1f },
            };
            return new BingoRow { name = "mine-retreat", scenario = s };
        }

        private static BingoRow ShootTheRock()
        {
            var s = Base();
            var rock = new float2(25f, 25f);
            s.referent1Law = RigLaw.Static(rock);
            s.obstacles = new[] { new RigCircle(rock, 6f) };
            // No hostile: AIM binds a non-ship referent and POS stands off outside the hull ring.
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, referent = 1, weight = 1f },
                pos = new PosSlot { armed = true, referent = 1, setpoint = 18f, weight = 0.6f },
                field = new FieldSlot { armed = true, weight = 1f },
            };
            return new BingoRow { name = "shoot-the-rock", scenario = s };
        }

        private static BingoRow TwoHostileLanes()
        {
            var s = Base();
            var enemyPos = new float2(0f, 50f);
            s.enemyLaw = RigLaw.Static(enemyPos, FacingYaw(enemyPos, float2.zero));
            s.referent2Law = RigLaw.ConstantVelocity(new float2(45f, 15f), new float2(-6f, 0f));
            // Fight hostile 1 while staying out of hostile 2's facing lane (its facing = its velocity).
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 1f },
                pos = new PosSlot
                {
                    armed = true, referent = 2, frame = ReferentFrame.Facing,
                    offsetR = 20f, weight = -1f,
                },
                vel = new VelSlot { armed = true, tangentialSpeed = 5f, weight = 0.5f },
            };
            return new BingoRow { name = "two-hostile-lanes", scenario = s };
        }

        private static BingoRow WingmanHold()
        {
            var s = Base();
            s.referent1Law = RigLaw.ConstantVelocity(new float2(-15f, 5f), new float2(0f, 6f));
            // Hold the ally's right wing and match its velocity (vr = vt = 0 in its frame).
            s.intent = new IntentSentence
            {
                pos = new PosSlot
                {
                    armed = true, referent = 1, frame = ReferentFrame.Facing,
                    offsetR = 12f, offsetThetaRad = -math.PI / 2f, weight = 1f,
                },
                vel = new VelSlot { armed = true, referent = 1, weight = 0.5f },
            };
            // No posWidthOverride: error-relative width covers the reach; the asset posWidth is the settle floor.
            return new BingoRow { name = "wingman-hold", scenario = s };
        }

        private static BingoRow MinefieldTransit()
        {
            var s = Base();
            s.referent1Law = RigLaw.Static(new float2(0f, 90f)); // the far waypoint
            s.obstacles = new[]
            {
                new RigCircle(new float2(-14f, 25f), 2.5f),
                new RigCircle(new float2(6f, 30f), 2.5f),
                new RigCircle(new float2(-4f, 42f), 2.5f),
                new RigCircle(new float2(14f, 48f), 2.5f),
                new RigCircle(new float2(-12f, 55f), 2.5f),
                new RigCircle(new float2(4f, 63f), 2.5f),
                new RigCircle(new float2(-6f, 72f), 2.5f),
                new RigCircle(new float2(10f, 78f), 2.5f),
            };
            s.intent = new IntentSentence
            {
                pos = new PosSlot { armed = true, referent = 1, weight = 1f },
                field = new FieldSlot { armed = true, weight = 1f },
            };
            // No posWidthOverride: error-relative width produces the hand-tuned 60 from the
            // 90 m leg (2/3 × 90) — this leg is the slope's calibration source.
            s.durationSeconds = 25f;
            return new BingoRow { name = "minefield-transit", scenario = s };
        }

        private static BingoRow DummyCloseout()
        {
            var s = Base();
            var enemyPos = new float2(0f, 55f);
            s.enemyLaw = RigLaw.Static(enemyPos, FacingYaw(enemyPos, float2.zero));
            // The retune pass's live success criterion: close a stationary target to weapons range and hold.
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, weight = 1f },
                pos = new PosSlot { armed = true, setpoint = 12f, weight = 1f },
                vel = new VelSlot { armed = true, radialSpeed = 6f, weight = 0.5f },
            };
            return new BingoRow { name = "dummy-closeout", scenario = s };
        }

        private static BingoRow DriftHold()
        {
            var s = Base();
            s.enemyLaw = RigLaw.Static(new float2(0f, 40f), math.PI);
            s.obstacles = new[] { new RigCircle(new float2(15f, 20f), 5f) };
            // "Nothing matters": every slot armed at weight 0 — a sentence, not the absence of one.
            // FIELD 0 zeroes turn-away shaping; the character-axis collision penalty still holds.
            s.intent = new IntentSentence
            {
                aim = new AimSlot { armed = true },
                pos = new PosSlot { armed = true },
                vel = new VelSlot { armed = true },
                field = new FieldSlot { armed = true },
            };
            return new BingoRow { name = "drift-hold", scenario = s };
        }

        private static BingoRow FieldAuthority()
        {
            var s = Base();
            s.referent1Law = RigLaw.Static(new float2(0f, 80f)); // the far waypoint
            // Spawned already flying at a rock on the drive line, inside the band where the
            // strafe-only sidestep is deficient (threat at t=0 by construction: 2.5·(26/16)² ≈ 6.6 m
            // < the 7.7 needed) but brake+strafe still clears (~11 m by the rock plane) — forced
            // threat states on an escapable approach, regardless of solver mood. Warmup 0 keeps
            // those opening steps in the metric window. FIELD 0 vs 1 must measurably diverge (early
            // shaped swerve vs the bare collision fence) — the differential-authority proof
            // (§Stage C). VEL is identical in both arms, keeping the divergence attributable to the
            // turn-away term alone.
            s.startVel = new float2(0f, 16f);
            s.warmupSeconds = 0f;
            s.obstacles = new[] { new RigCircle(new float2(1f, 26f), 7f) };
            s.intent = new IntentSentence
            {
                pos = new PosSlot { armed = true, referent = 1, weight = 1f },
                vel = new VelSlot { armed = true, referent = 1, radialSpeed = 16f, weight = 0.5f },
                field = new FieldSlot { armed = true, weight = 1f },
            };
            return new BingoRow { name = "field-authority", scenario = s };
        }

        private static RigScenario Base() => new()
        {
            projectileSpeed = 60f,
            simDt = 0.02f,
            warmupSeconds = 2f,
            durationSeconds = 20f,
        };

        // Inverse of the MPC forward convention fwd = (-sin, cos).
        private static float ThetaFromDirection(float2 direction) => math.atan2(-direction.x, direction.y);

        private static float FacingYaw(float2 from, float2 to) => ThetaFromDirection(math.normalize(to - from));
    }
}
