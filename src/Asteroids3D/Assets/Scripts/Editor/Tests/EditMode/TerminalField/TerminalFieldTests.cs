#if UNITY_EDITOR
using System;
using AI.Navigation.MPC;
using AI.Navigation.MPC.TerminalField;
using AI.Scanning;
using Movement;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using FieldOwner = AI.Navigation.MPC.TerminalField.TerminalField;

namespace Tests.EditMode
{
    [Category("MPC")]
    public class TerminalFieldTests
    {
        private const float DistanceTolerance = 0.002f;

        [Test]
        public void EmptyGrid_HasOctileDistancesAndZeroExcess()
        {
            using var grid = new Grid(48, 4f);
            grid.Bake(new float2(71.3f, 29.7f));
            var view = grid.View;
            for (var i = 0; i < grid.Distances.Length; i++)
                Assert.That(view.Excess(i), Is.InRange(0f, DistanceTolerance), $"cell {i}");
            Assert.That(view.Sample(new float2(31.9f, 61.2f)), Is.LessThan(DistanceTolerance));
        }

        [Test]
        public void FreePointBesideRock_DoesNotInheritDisconnectedFallback()
        {
            using var grid = new Grid(13, 1f);
            grid.Obstacles[0] = new ObstacleData { position = new float2(6f, 6f), radius = .25f };
            grid.Bake(new float2(12f, 6f), 1);
            Assert.That(float.IsPositiveInfinity(grid.Distances[6 + 6 * 13]), Is.True);
            Assert.That(grid.View.Sample(new float2(5.5f, 6f)), Is.LessThan(3f));
        }
        [Test]
        public void Rasterization_UsesInflatedDiscCentersAndProjectsRockGoal()
        {
            using var grid = new Grid(8, 2f);
            grid.Obstacles[0] = new ObstacleData { position = new float2(6f, 6f), radius = 0.1f };
            grid.Bake(new float2(6f, 6f), 1);
            Assert.That(grid.Occupied[27], Is.EqualTo(1));
            Assert.That(grid.View.seedIndex, Is.EqualTo(19), "Equidistant free cells break ties by index.");
            Assert.That(grid.Distances[19], Is.EqualTo(2f).Within(DistanceTolerance));
            Assert.That(grid.View.Sample(new float2(6f, 6f)), Is.GreaterThan(0f));
            grid.Obstacles[0] = new ObstacleData { position = new float2(5f, 6f), radius = 0.1f };
            grid.Bake(float2.zero, 1);
            Assert.That(grid.Occupied[26], Is.Zero);
            Assert.That(grid.Occupied[27], Is.Zero);
            grid.Bake(float2.zero, 1, 1f);
            Assert.That(grid.Occupied[26], Is.EqualTo(1));
            Assert.That(grid.Occupied[27], Is.EqualTo(1));
        }

        [Test]
        public void DisconnectedAndFullyBlockedGrids_SampleFiniteAtAndBeyondEdges()
        {
            using var grid = new Grid(8, 2f);
            for (var y = 0; y < 8; y++)
                grid.Obstacles[y] = new ObstacleData { position = new float2(6f, y * 2f), radius = 0.1f };
            grid.Bake(float2.zero, 8);
            Assert.That(float.IsPositiveInfinity(grid.Distances[7]), Is.True);
            Assert.That(grid.View.Sample(new float2(14f, 0f)), Is.GreaterThan(0f));
            Assert.That(grid.View.Sample(new float2(17f, 0f)),
                Is.EqualTo(grid.View.Sample(new float2(14f, 0f)) + 3f).Within(DistanceTolerance));
            grid.Obstacles[0] = new ObstacleData { position = new float2(7f, 7f), radius = 100f };
            grid.Bake(float2.zero, 1);
            Assert.That(grid.View.seedIndex, Is.EqualTo(-1));
            Assert.That(grid.View.Sample(new float2(8f, 9f)), Is.EqualTo(14f * math.sqrt(2f)).Within(DistanceTolerance));
        }

        [Test]
        public void DiagonalCannotCrossTwoTouchingBlockedCells()
        {
            using var grid = new Grid(8, 2f);
            grid.Obstacles[0] = new ObstacleData { position = new float2(2f, 0f), radius = 0.1f };
            grid.Obstacles[1] = new ObstacleData { position = new float2(0f, 2f), radius = 0.1f };
            grid.Bake(float2.zero, 2);
            Assert.That(float.IsPositiveInfinity(grid.Distances[9]), Is.True);
        }

        [Test]
        public void RandomTerrain_MatchesIndependentQuadraticShortestPathOracle()
        {
            var random = new System.Random(8073);
            using var grid = new Grid(12, 2f);
            for (var trial = 0; trial < 200; trial++)
            {
                for (var i = 0; i < 24; i++)
                    grid.Obstacles[i] = new ObstacleData
                    {
                        position = new float2((float)random.NextDouble() * 24f, (float)random.NextDouble() * 24f),
                        radius = (float)random.NextDouble() * 1.5f,
                    };
                grid.Bake(new float2((float)random.NextDouble() * 24f, (float)random.NextDouble() * 24f), 24);
                var expected = Reference(grid);
                for (var i = 0; i < expected.Length; i++)
                    if (double.IsPositiveInfinity(expected[i])) Assert.That(float.IsPositiveInfinity(grid.Distances[i]), Is.True);
                    else Assert.That(grid.Distances[i], Is.EqualTo(expected[i]).Within(DistanceTolerance), $"trial {trial}, cell {i}");
            }
        }

        private static double[] Reference(Grid grid)
        {
            var view = grid.View;
            var distance = new double[grid.Distances.Length];
            var done = new bool[distance.Length];
            Array.Fill(distance, double.PositiveInfinity);
            var seed = -1;
            var nearest = double.PositiveInfinity;
            for (var i = 0; i < distance.Length; i++)
            {
                if (grid.Occupied[i] != 0) continue;
                var center = view.CellCenter(i);
                var dx = (double)center.x - view.goal.x;
                var dy = (double)center.y - view.goal.y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d >= nearest) continue;
                seed = i;
                nearest = d;
            }
            if (seed < 0) return distance;
            distance[seed] = nearest;
            for (var iteration = 0; iteration < distance.Length; iteration++)
            {
                var current = -1;
                var best = double.PositiveInfinity;
                for (var i = 0; i < distance.Length; i++)
                    if (!done[i] && distance[i] < best) { best = distance[i]; current = i; }
                if (current < 0) break;
                done[current] = true;
                for (var next = 0; next < distance.Length; next++)
                {
                    if (done[next] || grid.Occupied[next] != 0) continue;
                    var dx = next % view.resolution - current % view.resolution;
                    var dy = next / view.resolution - current / view.resolution;
                    if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || dx == 0 && dy == 0) continue;
                    if (dx != 0 && dy != 0 && (grid.Occupied[current + dx] != 0 || grid.Occupied[current + dy * view.resolution] != 0)) continue;
                    distance[next] = Math.Min(distance[next], best + view.spacing * Math.Sqrt(dx * dx + dy * dy));
                }
            }
            return distance;
        }

        [Test]
        public void Owner_BakesOnCadenceDomainExitAndRestoredGoal()
        {
            var settings = ScriptableObject.CreateInstance<MpcSettings>();
            try
            {
                using var owner = new FieldOwner(settings, default, new ObstacleScanner(null, 0f, 0f, 0f, new CountingField(0)));
                var cfg = settings.ToConfig();
                owner.Update(0.02f, float2.zero, true, new float2(0f, 90f), cfg);
                Assert.That(owner.BakeCount, Is.EqualTo(1));
                for (var i = 0; i < 19; i++) owner.Update(0.02f, float2.zero, true, new float2(1f, 90f), cfg);
                Assert.That(owner.BakeCount, Is.EqualTo(1));
                owner.Update(0.02f, float2.zero, true, new float2(1f, 90f), cfg);
                Assert.That(owner.BakeCount, Is.EqualTo(2));
                owner.Update(0.02f, float2.zero, true, new float2(1000f, 0f), cfg);
                Assert.That(owner.BakeCount, Is.EqualTo(3));
                Assert.That(owner.View.Contains(new float2(1000f, 0f)), Is.True);
                owner.Update(0.02f, float2.zero, false, default, cfg);
                Assert.That(owner.View.valid, Is.False);
                Assert.That(owner.View.distances.IsCreated, Is.True);
                owner.Update(0.02f, float2.zero, true, float2.zero, cfg);
                Assert.That(owner.BakeCount, Is.EqualTo(4));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void TerminalCost_UsesEndpointAndFieldAuthorityOnlyOnce()
        {
            using var grid = new Grid(8, 2f);
            grid.Obstacles[0] = new ObstacleData { position = new float2(4f, 4f), radius = 2f };
            grid.Bake(new float2(14f, 14f), 1);
            var input = new CostInput { terminalField = grid.View };
            var cfg = new Config { wTerminalField = 2f, terminalMultiplier = 100f };
            var endpoint = new State { pos = new float2(4f, 4f) };
            var expected = grid.View.Sample(endpoint.pos) * 2f;
            Assert.That(Cost.EvaluateTerminal(endpoint, input, cfg), Is.EqualTo(expected));
            input.sentence.field = new FieldSlot { armed = true, weight = 0.25f };
            Assert.That(Cost.EvaluateTerminal(endpoint, input, cfg), Is.EqualTo(expected * 0.25f));
            input.sentence.field.weight = 0f;
            Assert.That(Cost.EvaluateTerminal(endpoint, input, cfg), Is.Zero);
        }

        [Test]
        public void NoObstacleSource_HasNoTerminalContribution()
        {
            var settings = ScriptableObject.CreateInstance<MpcSettings>();
            try
            {
                using var owner = new FieldOwner(settings, default, null);
                owner.Update(1f, float2.zero, true, new float2(50f, 80f), settings.ToConfig());
                Assert.That(owner.View.valid, Is.False);
                Assert.That(owner.View.Sample(new float2(500f, 800f)), Is.Zero);
                Assert.That(owner.BakeCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void GoalResolution_UsesOffsetCentreEvenWithZeroPosWeight()
        {
            var input = new CostInput
            {
                enemyYaw = float.NaN,
                referent3 = new ReferentSnapshot { valid = true, pos = new float2(20f, 30f), yaw = 0f },
                sentence = new IntentSentence
                {
                    pos = new PosSlot { armed = true, referent = 3, offsetR = 5f, setpoint = 12f, weight = 0f },
                },
            };
            var resolved = Cost.EvalContext.Create(default, input, default, 0);
            Assert.That(resolved.posResolved, Is.True);
            Assert.That(resolved.posPoint, Is.EqualTo(new float2(20f, 35f)));
            input.referent3.valid = false;
            Assert.That(Cost.EvalContext.Create(default, input, default, 0).posResolved, Is.False);
        }

        [Test]
        public void TrajectoryBreakdown_AddsOnlyTheFinalStateFieldCost()
        {
            using var grid = new Grid(8, 2f);
            grid.Obstacles[0] = new ObstacleData { position = new float2(4f, 4f), radius = 2f };
            grid.Bake(new float2(14f, 14f), 1);
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var cfg = settings.ToConfig();
            cfg.ApplyDynamics(dynamics);
            var input = new CostInput { terminalField = grid.View, enemyYaw = float.NaN, velocityReference = new float2(float.NaN, 0f) };
            var sequence = new Control[cfg.horizon];
            for (var i = 0; i < sequence.Length; i++) sequence[i] = new Control { thrust = 0.5f, strafe = 0.2f };
            var start = new State { pos = new float2(4f, 4f) };
            var current = start;
            var previous = default(Control);
            var accumulated = 0f;
            for (var i = 0; i < sequence.Length; i++)
            {
                accumulated += Cost.Evaluate(current, sequence[i], previous, input, cfg, i);
                current = Model.Step(current, sequence[i], cfg, dynamics);
                previous = sequence[i];
            }
            var terminal = Cost.EvaluateTerminal(current, input, cfg);
            var breakdown = Cost.EvaluateTrajectoryBreakdown(start, sequence, input, cfg, dynamics, default);
            Assert.That(breakdown.terminalField, Is.EqualTo(terminal).Within(DistanceTolerance));
            Assert.That(breakdown.total, Is.EqualTo(accumulated + terminal).Within(DistanceTolerance));
        }

        [Test]
        public void RegionalQuery_GrowsWithoutChangingTheShipScan()
        {
            var origin = new GameObject("scanner-test");
            try
            {
                var source = new CountingField();
                var scanner = new ObstacleScanner(origin.transform, 1f, 0f, 1f, source, 64);
                scanner.Scan();
                var initialBuffer = scanner.DetectedBuffer;
                var first = initialBuffer[0].radius;
                var regional = new DetectedObstacle[8];
                var result = scanner.Query(Vector2.zero, 200f, ref regional);
                Assert.That(result.count, Is.EqualTo(129));
                Assert.That(regional.Length, Is.EqualTo(256));
                Assert.That(scanner.DetectedBuffer, Is.SameAs(initialBuffer));
                Assert.That(scanner.DetectedCount, Is.EqualTo(64));
                Assert.That(scanner.DetectedBuffer[0].radius, Is.EqualTo(first));
            }
            finally { UnityEngine.Object.DestroyImmediate(origin); }
        }

        private sealed class CountingField : IObstacleField
        {
            private readonly int available;
            public CountingField(int available = 129) => this.available = available;
            public int QueryObstacles(Vector2 center, float extent, DetectedObstacle[] buffer)
            {
                var count = Math.Min(buffer.Length, available);
                for (var i = 0; i < count; i++) buffer[i] = new DetectedObstacle(Vector3.zero, i + extent, null);
                return count;
            }
        }

        private sealed class Grid : IDisposable
        {
            internal readonly NativeArray<float> Distances;
            private readonly NativeArray<float> emptyDistances;
            internal readonly NativeArray<byte> Occupied;
            internal NativeArray<ObstacleData> Obstacles;
            private readonly NativeArray<int> heap;
            private readonly NativeArray<int> positions;
            private readonly NativeArray<TerminalFieldBakeJob.BakeResult> result;
            private readonly int size;
            private readonly float spacing;
            internal TerminalFieldView View;

            internal Grid(int size, float spacing)
            {
                this.size = size;
                this.spacing = spacing;
                Distances = new NativeArray<float>(size * size, Allocator.TempJob);
                emptyDistances = new NativeArray<float>(size * size, Allocator.TempJob);
                Occupied = new NativeArray<byte>(size * size, Allocator.TempJob);
                Obstacles = new NativeArray<ObstacleData>(64, Allocator.TempJob);
                heap = new NativeArray<int>(size * size, Allocator.TempJob);
                positions = new NativeArray<int>(size * size, Allocator.TempJob);
                result = new NativeArray<TerminalFieldBakeJob.BakeResult>(1, Allocator.TempJob);
            }

            internal void Bake(float2 goal, int count = 0, float clearance = 0f)
            {
                new TerminalFieldBakeJob
                {
                    obstacles = Obstacles, obstacleCount = count, resolution = size, spacing = spacing,
                    goal = goal, clearance = clearance, distances = Distances, emptyDistances = emptyDistances, occupied = Occupied, heap = heap,
                    heapPosition = positions, result = result,
                }.Schedule().Complete();
                View = new TerminalFieldView
                {
                    distances = Distances, emptyDistances = emptyDistances, occupied = Occupied, valid = true, resolution = size, spacing = spacing, goal = goal,
                    seedIndex = result[0].seedIndex, maxFiniteDistance = result[0].maxFiniteDistance,
                };
            }

            public void Dispose()
            {
                Distances.Dispose(); emptyDistances.Dispose(); Occupied.Dispose(); Obstacles.Dispose();
                heap.Dispose(); positions.Dispose(); result.Dispose();
            }
        }
    }
}
#endif
