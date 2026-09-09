#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AI.Navigation.MPC.TerminalField;
using AI.Scanning;
using Game.RLHarness;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Tests.EditMode
{
    /// <summary>Baker and sampler proofs on direct arrays, the owner's bake cadence, and the warmed Burst microbench (env-gated).</summary>
    [Category("MPC")]
    public class TerminalFieldEditModeTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset";
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_1.prefab";
        private const float Tolerance = 1e-3f;

        private MpcSettings settings;
        private Dynamics dynamics;

        [SetUp]
        public void SetUp()
        {
            settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath));
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(settings, Is.Not.Null);
            Assert.That(ship, Is.Not.Null);
            dynamics = ship.ResolveStats().Dynamics;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);

        /// <summary>Direct-array bake: one job over hand-placed discs, returning the view plus the arrays it borrows.</summary>
        private sealed class Baked : IDisposable
        {
            public TerminalFieldView view;
            public NativeArray<byte> occupied;
            private readonly List<IDisposable> owned = new();

            public Baked(int n, float spacing, float2 origin, float2 goal, params float3[] discArray)
            {
                var cells = n * n;
                var discs = Keep(new NativeArray<float3>(math.max(1, discArray.Length), Allocator.TempJob));
                for (var i = 0; i < discArray.Length; i++) discs[i] = discArray[i];
                var distances = Keep(new NativeArray<float>(cells, Allocator.TempJob));
                occupied = Keep(new NativeArray<byte>(cells, Allocator.TempJob));
                var seedOut = Keep(new NativeArray<int>(1, Allocator.TempJob));
                var maxOut = Keep(new NativeArray<float>(1, Allocator.TempJob));
                new TerminalFieldBakeJob
                {
                    discs = discs,
                    discCount = discArray.Length,
                    resolution = n,
                    spacing = spacing,
                    origin = origin,
                    goal = goal,
                    distances = distances,
                    occupied = occupied,
                    heap = Keep(new NativeArray<int>(cells, Allocator.TempJob)),
                    heapSlot = Keep(new NativeArray<int>(cells, Allocator.TempJob)),
                    seedIndexOut = seedOut,
                    maxFiniteOut = maxOut,
                }.Run();
                var seed = seedOut[0];
                view = new TerminalFieldView
                {
                    distances = distances,
                    valid = 1,
                    resolution = n,
                    spacing = spacing,
                    origin = origin,
                    goal = goal,
                    seedIndex = seed,
                    seedToGoal = math.distance(origin + new float2(seed % n, seed / n) * spacing, goal),
                    maxFiniteDistance = maxOut[0],
                };
            }

            private NativeArray<T> Keep<T>(NativeArray<T> array) where T : struct
            {
                owned.Add(array);
                return array;
            }

            public void Dispose()
            {
                foreach (var d in owned) d.Dispose();
            }
        }

        [Test]
        public void EmptyGrid_BakesOctileDistances_AndZeroExcessEverywhere()
        {
            const int n = 16;
            const float h = 3f;
            using var baked = new Baked(n, h, new float2(-10f, -10f), new float2(12.5f, 7.25f));
            var v = baked.view;
            Assert.That(v.CellDistance(v.seedIndex % n, v.seedIndex / n), Is.EqualTo(v.seedToGoal).Within(Tolerance));

            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                Assert.That(v.CellDistance(x, y), Is.EqualTo(v.EmptyDistance(x, y)).Within(Tolerance),
                    $"cell ({x},{y}) must bake the closed-form octile distance on an empty grid");
                Assert.That(v.CellExcess(x, y), Is.LessThanOrEqualTo(Tolerance));
            }

            var rng = new Unity.Mathematics.Random(7u);
            for (var i = 0; i < 200; i++)
            {
                var p = v.origin + rng.NextFloat2(0f, (n - 1) * h);
                Assert.That(v.DetourExcess(p), Is.LessThanOrEqualTo(Tolerance),
                    $"an empty grid must sample zero excess at {p}");
            }
        }

        [Test]
        public void Wall_RoutesAround_AndChargesTheDetour()
        {
            const int n = 24;
            const float h = 2f;
            var origin = new float2(-23f, -23f);
            var goal = new float2(0f, 20f);
            // A wall across y = 0 from x = −12 … 12; both ends stay open.
            var discs = new List<float3>();
            for (var x = -12f; x <= 12f; x += 2f) discs.Add(new float3(x, 0f, 1.5f));
            using var baked = new Baked(n, h, origin, goal, discs.ToArray());
            var v = baked.view;

            var behindWall = v.DetourExcess(new float2(0f, -10f));
            var pastTheEnd = v.DetourExcess(new float2(20f, -10f));
            Assert.That(behindWall, Is.GreaterThan(4f), $"a point shadowed by the wall pays the detour; measured {behindWall:F2} m");
            Assert.That(pastTheEnd, Is.LessThan(behindWall), "a point with a clear line pays less than the shadowed one");
            Assert.That(v.DetourExcess(goal), Is.LessThanOrEqualTo(Tolerance), "the goal itself costs nothing");
        }

        [Test]
        public void EnclosedGoal_LeavesTheOutsideUnreachable_YetEveryThingSampledIsFinite()
        {
            const int n = 20;
            const float h = 2f;
            var origin = new float2(-19f, -19f);
            var goal = float2.zero;
            // A closed ring of discs around the goal, sealed against corner-cutting.
            var discs = new List<float3>();
            for (var a = 0f; a < 360f; a += 12f)
                discs.Add(new float3(8f * math.cos(math.radians(a)), 8f * math.sin(math.radians(a)), 1.6f));
            using var baked = new Baked(n, h, origin, goal, discs.ToArray());
            var v = baked.view;

            Assert.That(math.isfinite(v.CellDistance(v.seedIndex % n, v.seedIndex / n)), "the seed inside the ring is reachable");
            Assert.That(math.isfinite(v.CellDistance(0, 0)), Is.False, "a corner outside the ring is disconnected");
            Assert.That(v.maxFiniteDistance, Is.GreaterThan(0f).And.LessThan(v.GridDiagonal));

            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                Assert.That(math.isfinite(v.CellExcess(x, y)), $"cell ({x},{y}) excess must be finite");
            var far = v.DetourExcess(new float2(-15f, -15f));
            Assert.That(math.isfinite(far) && far > 0f, $"a disconnected sample takes the finite bound; measured {far}");
            Assert.That(far, Is.LessThanOrEqualTo(v.UnreachableBound));
        }

        [Test]
        public void DiagonalStep_NeverCutsACorner()
        {
            const int n = 8;
            const float h = 1f;
            var origin = float2.zero;
            // Occupy (1,0) and (0,1) exactly; (0,0) and (1,1) stay free, so the corner between them is sealed.
            using var baked = new Baked(n, h, origin, new float2(1f, 1f),
                new float3(1f, 0f, 0.4f), new float3(0f, 1f, 0.4f));
            var v = baked.view;
            Assert.That(baked.occupied[1], Is.EqualTo(1));
            Assert.That(baked.occupied[n], Is.EqualTo(1));
            Assert.That(v.seedIndex, Is.EqualTo(1 + n), "the goal sits on the free cell (1,1)");
            Assert.That(math.isfinite(v.CellDistance(0, 0)), Is.False,
                "cell (0,0) has only the diagonal neighbour left and cutting between two occupied cells is forbidden");
            Assert.That(v.CellDistance(2, 2), Is.EqualTo(math.SQRT2).Within(Tolerance), "an open diagonal still costs √2·h");
        }

        [Test]
        public void GoalInsideARock_SeedsTheNearestFreeCell_WithItsGoalDistance()
        {
            const int n = 12;
            const float h = 2f;
            var origin = new float2(-11f, -11f);
            var goal = new float2(0.5f, 0.5f);
            using var baked = new Baked(n, h, origin, goal, new float3(0.5f, 0.5f, 4f));
            var v = baked.view;
            var sx = v.seedIndex % n;
            var sy = v.seedIndex / n;
            Assert.That(baked.occupied[v.seedIndex], Is.EqualTo(0), "the seed is a free cell");
            Assert.That(v.seedToGoal, Is.GreaterThan(4f - h).And.LessThan(4f + h * math.SQRT2),
                $"the seed sits just outside the rock; seed→goal {v.seedToGoal:F2}");
            Assert.That(v.CellDistance(sx, sy), Is.EqualTo(v.seedToGoal).Within(Tolerance));
            Assert.That(v.CellExcess(sx, sy), Is.LessThanOrEqualTo(Tolerance));
            Assert.That(math.isfinite(v.CellDistance(0, 0)), "the far corner routes to the seed around the rock");
        }

        [Test]
        public void OutsideTheDomain_ClampsAndAddsTheDistanceBackIn()
        {
            const int n = 10;
            const float h = 2f;
            var origin = float2.zero;
            using var baked = new Baked(n, h, origin, new float2(9f, 9f));
            var v = baked.view;
            var edge = new float2(18f, 9f);
            var beyond = new float2(25f, 9f);
            Assert.That(v.DetourExcess(beyond), Is.EqualTo(v.DetourExcess(edge) + 7f).Within(Tolerance));
            var corner = new float2(-3f, -4f);
            Assert.That(v.DetourExcess(corner), Is.EqualTo(v.DetourExcess(float2.zero) + 5f).Within(Tolerance));
        }

        [Test]
        public void Owner_BakesOnFirstGoal_ThenOnCadence_EarlyOnExit_AndInvalidatesWithoutGoal()
        {
            var scanner = new ObstacleScanner(null, 0f, 0f, 0f, new RigObstacleField(new[] { new RigCircle(new float2(0f, 30f), 3f) }));
            using var field = new TerminalField(settings, dynamics, scanner);
            var interval = settings.terminalFieldBakeInterval;

            field.Update(float2.zero, hasGoal: false, float2.zero, 0.02f);
            Assert.That(field.View.IsValid, Is.False);
            Assert.That(field.BakeCount, Is.Zero, "no goal, no bake");

            var goal = new float2(0f, 60f);
            field.Update(float2.zero, true, goal, 0.02f);
            Assert.That(field.BakeCount, Is.EqualTo(1), "the first valid POS bakes at once");
            Assert.That(field.View.IsValid);
            Assert.That(field.View.spacing, Is.GreaterThanOrEqualTo(settings.terminalFieldMinSpacing));
            Assert.That(field.LastDiscCount, Is.EqualTo(1), "the rock inside the grid is gathered through the scanner");

            var steps = 0;
            while (field.BakeCount == 1 && steps < 1000)
            {
                field.Update(float2.zero, true, goal, 0.02f);
                steps++;
            }
            Assert.That(steps * 0.02f, Is.EqualTo(interval).Within(0.021f), $"the next bake lands one interval later; took {steps} steps");

            var farAway = new float2(5000f, 5000f);
            var before = field.BakeCount;
            field.Update(farAway, true, goal, 0.02f);
            Assert.That(field.BakeCount, Is.EqualTo(before + 1), "the ship leaving the grid rebakes early");

            field.Update(farAway, false, goal, 0.02f);
            Assert.That(field.View.IsValid, Is.False, "a solve without a valid POS invalidates the view");
            field.Update(farAway, true, goal, 0.02f);
            Assert.That(field.BakeCount, Is.EqualTo(before + 2), "the goal's return bakes at once");
        }

        [Test]
        public void GrowableQuery_ReturnsEveryRockPastTheInitialBuffer()
        {
            var circles = new RigCircle[150];
            for (var i = 0; i < circles.Length; i++)
                circles[i] = new RigCircle(new float2(i % 15 * 6f - 42f, i / 15 * 6f - 27f), 1f);
            var scanner = new ObstacleScanner(null, 0f, 0f, 0f, new RigObstacleField(circles));
            var buffer = new DetectedObstacle[16];
            var count = scanner.QueryAround(Vector2.zero, 100f, ref buffer);
            Assert.That(count, Is.EqualTo(150));
            Assert.That(buffer.Length, Is.GreaterThan(150), "the caller's buffer grew past the last full query");
            Assert.That(scanner.DetectedCount, Is.Zero, "the ship-centred scan buffer is untouched");
        }

        [Test]
        public void TerminalCost_MatchesTheTrajectoryBreakdown_AndFieldAuthority()
        {
            var scanner = new ObstacleScanner(null, 0f, 0f, 0f, new RigObstacleField(new[]
            {
                new RigCircle(new float2(-5f, 20f), 5.5f), new RigCircle(new float2(5f, 20f), 5.5f),
            }));
            using var field = new TerminalField(settings, dynamics, scanner);
            var goal = new float2(0f, 45f);
            field.Bake(float2.zero, goal);

            var cfg = settings.ToConfig();
            cfg.ApplyDynamics(in dynamics);
            var sentence = new IntentSentence
            {
                pos = new PosSlot { armed = true, referent = 1, weight = 1f },
                field = new FieldSlot { armed = true, weight = 0.5f },
            };
            var input = new CostInput
            {
                velocityReference = new float2(float.NaN, 0f),
                enemyYaw = float.NaN,
                obstacles = new NativeArray<ObstacleData>(0, Allocator.Temp),
                enemyStates = new NativeArray<State>(0, Allocator.Temp),
                sentence = sentence,
                referent1 = new ReferentSnapshot { valid = true, pos = goal },
                terminalField = field.View,
            };
            var sequence = new Control[cfg.horizon];
            for (var i = 0; i < sequence.Length; i++) sequence[i] = new Control { thrust = 1f };
            var start = new State { pos = new float2(0f, 8f) };

            var end = start;
            foreach (var u in sequence) end = Model.Step(end, u, cfg, dynamics);
            var direct = Cost.EvaluateTerminal(end, input, cfg);
            var breakdown = Cost.EvaluateTrajectoryBreakdown(start, sequence, input, cfg, dynamics, default);

            Assert.That(direct, Is.GreaterThan(0f), "a rollout ending under the rock pair pays detour excess");
            Assert.That(breakdown.terminalField, Is.EqualTo(direct).Within(1e-5f));
            Assert.That(direct, Is.EqualTo(cfg.wTerminalField * 0.5f * field.View.DetourExcess(end.pos)).Within(1e-5f),
                "FIELD authority scales the terminal term like turn-away");

            cfg.wTerminalField = 0f;
            Assert.That(Cost.EvaluateTerminal(end, input, cfg), Is.Zero);
            input.obstacles.Dispose();
            input.enemyStates.Dispose();
        }

        // The deliverable is the number: warmed Burst bake at 32/48/64 cells across the training densities,
        // gathering and allocation timed apart. Rock density follows the scan-box occupancy probe (mean 78
        // rocks in the ship's 2 s box at density 2.5).
        [Test]
        public void Microbench_WarmedBake_ByResolutionAndDensity()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1")
                Assert.Ignore("Set MPC_RIG_EMIT=1 to run the terminal-field microbench.");

            var maxAccel = Mathf.Sqrt(dynamics.forwardAcc * dynamics.forwardAcc + dynamics.maxStrafeAcc * dynamics.maxStrafeAcc) / dynamics.mass;
            var scanHalfExtent = dynamics.maxSpeed * 2f + 0.5f * maxAccel * 4f;
            var rocksPerSqm25 = 78f / (4f * scanHalfExtent * scanHalfExtent);
            const int repeats = 50;
            var report = "resolution,density,rocks,spacing,alloc_ms,gather_us,bake_us\n";

            foreach (var n in new[] { 32, 48, 64 })
            foreach (var density in new[] { 2.0f, 2.5f })
            {
                settings.terminalFieldResolution = n;
                var side = (n - 1) * settings.terminalFieldMinSpacing + 40f;
                var rocks = Mathf.RoundToInt(rocksPerSqm25 * density / 2.5f * side * side);
                var rng = new Unity.Mathematics.Random(99u + (uint)n);
                var circles = new RigCircle[rocks];
                for (var i = 0; i < rocks; i++)
                    circles[i] = new RigCircle(rng.NextFloat2(-side / 2f, side / 2f), rng.NextFloat(1.5f, 4.17f));
                var scanner = new ObstacleScanner(null, 0f, 0f, 0f, new RigObstacleField(circles));

                var sw = Stopwatch.StartNew();
                var field = new TerminalField(settings, dynamics, scanner);
                var allocMs = sw.Elapsed.TotalMilliseconds;

                var ship = new float2(-side / 4f, -side / 4f);
                var goal = new float2(side / 4f, side / 4f);
                field.Bake(ship, goal);
                var spacing = field.View.spacing;
                var halfSpan = (n - 1) * spacing * 0.5f;
                var halfExtent = halfSpan + 0.5f * spacing + dynamics.shipRadius + settings.collisionSafetyMargin;
                var origin = 0.5f * (ship + goal) - halfSpan;

                sw.Restart();
                var discCount = 0;
                for (var r = 0; r < repeats; r++) discCount = field.Gather(halfExtent);
                var gatherUs = sw.Elapsed.TotalMilliseconds * 1000.0 / repeats;

                sw.Restart();
                for (var r = 0; r < repeats; r++) field.RunBake(discCount, spacing, origin, goal);
                var bakeUs = sw.Elapsed.TotalMilliseconds * 1000.0 / repeats;

                field.Dispose();
                report += $"{n},{density:F1},{rocks},{spacing:F2},{allocMs:F3},{gatherUs:F1},{bakeUs:F1}\n";
            }
            Debug.Log("[TerminalFieldBench]\n" + report);
        }
    }
}
#endif
