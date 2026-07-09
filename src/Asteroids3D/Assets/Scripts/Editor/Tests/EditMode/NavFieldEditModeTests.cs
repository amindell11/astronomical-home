using Movement.MPC;
using Movement.MPC.Field;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests.EditMode
{
    /// <summary>
    /// Track B3: the resurrected Dijkstra cost-to-go core (Burst job form) and its
    /// terminal-cost sampling semantics. Adapted from the pre-#43 NavFieldEditModeTests;
    /// gradient-walk/RoutedCell tests are deliberately dropped — that consumption pattern
    /// is not resurrected.
    /// </summary>
    [Category("Planning")]
    public class NavFieldEditModeTests
    {
        private static NativeArray<float3> Obstacles(params float3[] obs) =>
            new NativeArray<float3>(obs.Length == 0 ? new float3[1] : obs, Allocator.TempJob);

        private static NavField Solve(int size, float cellSize, float2 goal, params float3[] obs)
        {
            var f = new NavField(size, cellSize);
            var arr = Obstacles(obs);
            f.SolveImmediate(goal, arr, obs.Length);
            arr.Dispose();
            return f;
        }

        // secondsPerStep = 1 when nominalSpeed == cellSize: samples read in grid-step units.
        private static float SampleSteps(NavField f, float2 pos) =>
            TerminalFieldData.Sample(pos, f.Data(f.CellSize));

        [Test]
        public void Solve_Source_Has_ZeroCost()
        {
            using var f = Solve(10, 1f, new float2(5f, 5f));
            Assert.IsTrue(f.HasSolution);
            Assert.Less(SampleSteps(f, new float2(5f, 5f)), 1.5f); // bilinear around the 0 cell
        }

        [Test]
        public void Solve_DiagonalCostExceedsCardinal()
        {
            using var f = Solve(20, 1f, new float2(0.5f, 0.5f));
            var diagonal = SampleSteps(f, new float2(5.5f, 5.5f)); // ~5*sqrt2
            var cardinal = SampleSteps(f, new float2(7.5f, 0.5f)); // ~7
            Assert.AreEqual(7.07f, diagonal, 0.3f);
            Assert.AreEqual(7f, cardinal, 0.3f);
        }

        [Test]
        public void StampObstacle_BlockedCells_SampleFallback_NotRouted()
        {
            using var f = Solve(10, 1f, new float2(1.5f, 1.5f), new float3(5.5f, 5.5f, 1.5f));
            // A blocked cell must sample the finite fallback, which is strictly worse than a
            // free cell at comparable range.
            var blockedSample = SampleSteps(f, new float2(5.5f, 5.5f));
            var freeSample = SampleSteps(f, new float2(5.5f, 1.5f)); // same range, free
            Assert.IsTrue(float.IsFinite(blockedSample), "Blocked sample must be finite");
            Assert.Greater(blockedSample, freeSample);
        }

        [Test]
        public void Solve_WallWithGap_CostRoutesThroughGap()
        {
            // Wall along y=10 except a gap at x=15: cost on the far side must reflect the
            // detour through the gap, not the straight line.
            // Goal-centered grid: goal (2.5,2.5) on a 20x1 grid spans roughly [-8, 12].
            // Wall along y = 6.5 from x = -7..11 with a gap at x = 8.
            var wall = new System.Collections.Generic.List<float3>();
            for (var x = -7; x < 12; x++)
                if (x != 8)
                    wall.Add(new float3(x + 0.5f, 6.5f, 0.45f));
            using var f = Solve(20, 1f, new float2(2.5f, 2.5f), wall.ToArray());

            var behindWall = SampleSteps(f, new float2(2.5f, 10.5f)); // straight-line ~8
            var nearGapSide = SampleSteps(f, new float2(8.5f, 10.5f));
            Assert.IsTrue(float.IsFinite(behindWall));
            // Detour via the gap at x=8: ~7 across + back ≈ 13+; must exceed straight line.
            Assert.Greater(behindWall, 11f, "Cost behind the wall must include the gap detour");
            Assert.Less(nearGapSide, behindWall, "Cells near the gap must be cheaper");
        }

        [Test]
        public void Solve_FullyEnclosedRegion_SamplesLargeButFiniteFallback()
        {
            // Box the source in completely; cells outside the box are unreachable.
            var walls = new System.Collections.Generic.List<float3>();
            for (var x = 0; x < 20; x++) walls.Add(new float3(x + 0.5f, 3.5f, 0.45f));
            for (var y = 0; y < 20; y++) walls.Add(new float3(3.5f, y + 0.5f, 0.45f));
            using var f = Solve(20, 1f, new float2(1.5f, 1.5f), walls.ToArray());

            var unreachable = SampleSteps(f, new float2(15.5f, 15.5f));
            Assert.IsTrue(float.IsFinite(unreachable),
                "Unreachable terminal states must sample a finite fallback, never infinity");
            var straightSteps = math.distance(new float2(15.5f, 15.5f), new float2(1.5f, 1.5f));
            Assert.GreaterOrEqual(unreachable, straightSteps,
                "Fallback must be pessimistic relative to the straight-line time");
        }

        [Test]
        public void Sample_OffGrid_ReturnsFiniteDistanceShapedFallback()
        {
            using var f = Solve(10, 1f, new float2(5f, 5f));
            var near = SampleSteps(f, new float2(20f, 5f));
            var far = SampleSteps(f, new float2(40f, 5f));
            Assert.IsTrue(float.IsFinite(near) && float.IsFinite(far));
            Assert.Greater(far, near, "Off-grid fallback must still slope toward the goal");
        }

        [Test]
        public void Sample_Bilinear_InterpolatesBetweenCells()
        {
            // Grid is goal-centered; keep the samples in the interior (the outer half-cell
            // ring intentionally samples the off-grid fallback).
            using var f = Solve(16, 1f, new float2(0.5f, 0.5f));
            var a = SampleSteps(f, new float2(3.5f, 0.5f)); // cell center, ~3
            var b = SampleSteps(f, new float2(4.5f, 0.5f)); // cell center, ~4
            var mid = SampleSteps(f, new float2(4.0f, 0.5f));
            Assert.AreEqual((a + b) * 0.5f, mid, 0.05f, "Midpoint sample must be the average of the two cells");
        }

        [Test]
        public void Sample_InvalidField_GuardIsCallerSide_DataFlagsInvalid()
        {
            using var f = new NavField(10, 1f);
            var data = f.Data(1f);
            Assert.AreEqual(0, data.isValid, "Unsolved field must flag itself invalid (hook contributes 0)");
        }

        [Test]
        public void Sample_TimeUnits_ScaleWithNominalSpeed()
        {
            using var f = Solve(20, 2f, new float2(1f, 1f));
            var at5 = TerminalFieldData.Sample(new float2(21f, 1f), f.Data(5f));
            var at10 = TerminalFieldData.Sample(new float2(21f, 1f), f.Data(10f));
            Assert.AreEqual(at5, at10 * 2f, at5 * 0.05f, "Time-to-go must scale inversely with nominal speed");
        }

        // ── BorderEscape seeding (flee escape field) ──

        private static NavField SolveEscape(int size, float cellSize, float2 self, float2 threat,
            float threatBias, params float3[] obs)
        {
            var f = new NavField(size, cellSize);
            var arr = Obstacles(obs);
            f.SolveImmediate(SeedSpec.ForBorderEscape(self, threat, threatBias), arr, obs.Length);
            arr.Dispose();
            return f;
        }

        private static float EscapePessimisticSteps(int size, float threatBias) =>
            threatBias * (size - 1) * 1.41421356f + (size - 1) * 1.41421356f;

        [Test]
        public void BorderEscape_ExitOppositeThreat_IsCheapest()
        {
            // Open 16x1 grid centered on the ship, threat to the east: fleeing west must be
            // far cheaper than fleeing east (which pays the threat-bearing seed bias).
            using var f = SolveEscape(16, 1f, float2.zero, new float2(4f, 0f), 1f);
            var west = SampleSteps(f, new float2(-6f, 0f));
            var east = SampleSteps(f, new float2(6f, 0f));
            Assert.IsTrue(float.IsFinite(west) && float.IsFinite(east));
            Assert.Greater(east, west + 5f, "Exits toward the threat must be substantially pricier");
        }

        [Test]
        public void BorderEscape_GradientDescends_TowardAwayBorder()
        {
            // Along the away-from-threat axis, cost must fall monotonically toward the exit.
            using var f = SolveEscape(16, 1f, float2.zero, new float2(4f, 0f), 1f);
            var near = SampleSteps(f, new float2(-1.5f, 0.5f));
            var mid = SampleSteps(f, new float2(-3.5f, 0.5f));
            var far = SampleSteps(f, new float2(-5.5f, 0.5f));
            Assert.Greater(near, mid);
            Assert.Greater(mid, far);
        }

        [Test]
        public void BorderEscape_NoThreat_SeedsWholeBorderAtZero()
        {
            // Degenerate threat (== self) seeds the border uniformly: center cost is the
            // plain distance-to-border, with no directional preference.
            using var f = SolveEscape(16, 1f, float2.zero, float2.zero, 1f);
            var center = SampleSteps(f, new float2(0.5f, 0.5f));
            Assert.AreEqual(7f, center, 1.5f, "Uniform border: center ≈ steps to nearest edge");
            var west = SampleSteps(f, new float2(-5.5f, 0.5f));
            var east = SampleSteps(f, new float2(5.5f, 0.5f));
            Assert.AreEqual(west, east, 1f, "No threat ⇒ no directional bias");
        }

        [Test]
        public void BorderEscape_BlockedAndPocketCells_BakeFinitePessimistic()
        {
            // A stamped cell and an enclosed (unreachable) pocket must both sample large but
            // finite — NOT the goal-distance fallback, whose anchor here is the ship itself
            // (a blocked cell near the ship would otherwise read as near-zero cost).
            var pocket = new System.Collections.Generic.List<float3>
            {
                new float3(2.5f, 0.5f, 0.45f), // lone rock east of the ship (on a cell center)
            };
            // Ring enclosing (5,5) — interior cell unreachable.
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    pocket.Add(new float3(5.5f + dx, 5.5f + dy, 0.45f));

            using var f = SolveEscape(16, 1f, float2.zero, float2.zero, 1f, pocket.ToArray());
            var pessimistic = EscapePessimisticSteps(16, 1f);

            var onRock = SampleSteps(f, new float2(2.5f, 0.5f));
            var freeMirror = SampleSteps(f, new float2(-2.5f, 0.5f));
            Assert.IsTrue(float.IsFinite(onRock));
            Assert.Greater(onRock, freeMirror + 3f,
                "Blocked cell near the ship must sample pessimistic, not near-zero center distance");

            var inPocket = SampleSteps(f, new float2(5.5f, 5.5f));
            Assert.IsTrue(float.IsFinite(inPocket), "Pocket must be finite (elite ranking survives)");
            Assert.AreEqual(pessimistic, inPocket, pessimistic * 0.35f,
                "Pocket interior bakes the flat pessimistic value (bilinear blends its ring)");
        }

        [Test]
        public void BorderEscape_OnlyExitTowardThreat_StillRouted()
        {
            // Border fully walled except a gap on the threat side: the expensive exit must
            // still be used (priced like a full-grid detour, never forbidden) — center cost
            // stays below the baked pessimistic ceiling.
            var walls = new System.Collections.Generic.List<float3>();
            for (var i = 0; i < 16; i++)
            {
                var c = i - 8 + 0.5f; // cell centers -7.5 .. 7.5
                walls.Add(new float3(c, -7.5f, 0.45f));
                walls.Add(new float3(c, 7.5f, 0.45f));
                walls.Add(new float3(-7.5f, c, 0.45f));
                if (math.abs(c) > 1.6f) walls.Add(new float3(7.5f, c, 0.45f)); // east gap at |y|<1.6
            }

            using var f = SolveEscape(16, 1f, float2.zero, new float2(4f, 0f), 1f, walls.ToArray());
            var pessimistic = EscapePessimisticSteps(16, 1f);
            var center = SampleSteps(f, new float2(0.5f, 0.5f));

            Assert.IsTrue(float.IsFinite(center));
            Assert.Less(center, pessimistic * 0.95f,
                "The toward-threat gap must still route (finite, below the pessimistic ceiling)");
            Assert.Greater(center, EscapePessimisticSteps(16, 1f) * 0.3f,
                "…but it pays the threat-bearing seed bias");
        }

        [Test]
        public void SolverHook_TerminalField_ShiftsBestCostByTerminalSample()
        {
            // With a single candidate (the zero warm start), Solve's best cost must differ by
            // exactly wTerminal * Sample(terminal state) when a solved field is attached.
            var settings = UnityEngine.ScriptableObject.CreateInstance<MpcSettings>();
            settings.samples = 1;
            settings.horizonSeconds = 0.5f;
            settings.rolloutDt = 0.1f;
            settings.wTerminal = 0f;

            var dynamics = new Movement.Dynamics(
                mass: 1f, forwardAcc: 5f, reverseAcc: 5f, maxStrafeAcc: 5f, minStrafeAcc: 5f,
                maxSpeed: 10f, maxYawRate: 180f, yawTorque: 1f, angularDrag: 0.1f,
                linearDrag: 0.1f, yawInertia: 1f, maxBankAngleRad: 0.5f);

            using var field = Solve(16, 2f, new float2(10f, 10f));

            var seq = new Control[settings.Horizon];
            var solver = new SolverBuffers(0);
            try
            {
                var cfgOff = settings.ToConfig();
                cfgOff.maxSpeedSq = 100f;
                cfgOff.maxYawRateSq = 9.87f;
                var state = new State { pos = new float2(0f, 0f) };

                var costOff = solver.Solve(state, seq, cfgOff, dynamics,
                    default, false, false, new float2(5f, 0f), float2.zero,
                    float2.zero, float2.zero, float.NaN, 0f, default, 0f,
                    1, 0f, 2, default);

                System.Array.Clear(seq, 0, seq.Length);
                settings.wTerminal = 2.5f;
                var cfgOn = settings.ToConfig();
                cfgOn.maxSpeedSq = 100f;
                cfgOn.maxYawRateSq = 9.87f;

                var costOn = solver.Solve(state, seq, cfgOn, dynamics,
                    default, false, false, new float2(5f, 0f), float2.zero,
                    float2.zero, float2.zero, float.NaN, 0f, default, 0f,
                    1, 0f, 2, default,
                    terminalField: field.Data(10f));

                // Terminal state of the zero-control rollout from rest = start position.
                var expected = 2.5f * TerminalFieldData.Sample(state.pos, field.Data(10f));
                Assert.AreEqual(expected, costOn - costOff, math.max(0.01f, expected * 0.02f),
                    "Terminal hook must add exactly wTerminal * Sample(x_terminal)");
                Assert.Greater(costOn, costOff);
            }
            finally
            {
                solver.Dispose();
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
