#if UNITY_EDITOR
using System;
using AI.Navigation.MPC;
using AI.Navigation.MPC.TerminalField;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Tests.EditMode.TerminalField
{
    [Category("MPC")]
    public class TerminalFieldGoalRegionTests
    {
        private const float Tolerance = 0.002f;

        [TestCase(0.1f)]
        [TestCase(1.7f)]
        [TestCase(4.5f)]
        [TestCase(20f)]
        public void EmptyRegionGrid_HasZeroExcess(float radius)
        {
            using var grid = new Grid(12);
            grid.Bake(new float2(5.3f, 4.8f), radius);
            for (var i = 0; i < grid.Distances.Length; i++)
                Assert.That(grid.View.Excess(i), Is.EqualTo(0f).Within(Tolerance), $"cell {i}");
            Assert.That(grid.View.Sample(new float2(3.25f, 7.65f)), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void RegionEnclosingRock_HasIndependentApproachesFromEverySide()
        {
            using var grid = new Grid(13);
            grid.Obstacles[0] = new ObstacleData { position = new float2(6f, 6f), radius = 2.1f };
            grid.Bake(new float2(6f, 6f), 3.1f, 1);
            foreach (var index in new[] { 6, 6 * 13, 6 * 13 + 12, 12 * 13 + 6 })
            {
                Assert.That(grid.Distances[index], Is.EqualTo(3f).Within(Tolerance));
                Assert.That(grid.View.Excess(index), Is.EqualTo(0f).Within(Tolerance));
            }
            Assert.That(float.IsPositiveInfinity(grid.Distances[6 * 13 + 6]), Is.True);
            Assert.That(grid.EmptyDistances[6 * 13 + 6], Is.EqualTo(1f + math.sqrt(2f)).Within(Tolerance),
                "The empty reference must use the free region sources, excluding the occupied center.");
        }

        [Test]
        public void RegionWithNoFreeSource_UsesNearestFreeCellAndRemainingRadiusOffset()
        {
            using var grid = new Grid(9);
            grid.Obstacles[0] = new ObstacleData { position = new float2(4f, 4f), radius = 1.1f };
            grid.Bake(new float2(4f, 4f), 0.5f, 1);
            Assert.That(grid.View.seedIndex, Is.EqualTo(3 + 3 * 9));
            var offset = math.sqrt(2f) - 0.5f;
            Assert.That(grid.Distances[grid.View.seedIndex], Is.EqualTo(offset).Within(Tolerance));
            Assert.That(grid.EmptyDistances[grid.View.seedIndex], Is.EqualTo(offset).Within(Tolerance));
            Assert.That(grid.View.Excess(grid.View.seedIndex), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(float.IsPositiveInfinity(grid.Distances[4 + 4 * 9]), Is.True);
            Assert.That(math.isfinite(grid.View.Sample(new float2(4f, 4f))), Is.True);
        }

        [Test]
        public void SeparatedRegion_KeepsUnreachableDistancesInfiniteAndSamplingFinite()
        {
            using var grid = new Grid(9);
            for (var y = 0; y < 9; y++)
                grid.Obstacles[y] = new ObstacleData { position = new float2(4f, y), radius = 0.1f };
            grid.Bake(new float2(1f, 4f), 1.1f, 9);
            Assert.That(float.IsPositiveInfinity(grid.Distances[8 + 4 * 9]), Is.True);
            Assert.That(math.isfinite(grid.EmptyDistances[8 + 4 * 9]), Is.True);
            Assert.That(grid.View.Sample(new float2(8f, 4f)), Is.GreaterThan(0f));
            Assert.That(math.isfinite(grid.View.Sample(new float2(10f, 4f))), Is.True);
            grid.Obstacles[0] = new ObstacleData { position = new float2(4f, 4f), radius = 100f };
            grid.Bake(new float2(4f, 4f), 2f, 1);
            Assert.That(grid.View.seedIndex, Is.EqualTo(-1));
            for (var i = 0; i < grid.Distances.Length; i++)
                Assert.That(float.IsPositiveInfinity(grid.Distances[i]), Is.True);
            Assert.That(math.isfinite(grid.View.Sample(new float2(3.4f, 5.6f))), Is.True);
        }

        [Test]
        public void RandomRegions_MatchIndependentMultiSourceShortestPaths()
        {
            var random = new System.Random(15973);
            using var grid = new Grid(9);
            for (var trial = 0; trial < 24; trial++)
            {
                var blocked = new bool[81];
                var count = 0;
                for (var i = 0; i < blocked.Length; i++)
                {
                    blocked[i] = random.NextDouble() < 0.3;
                    if (blocked[i]) grid.Obstacles[count++] = new ObstacleData
                    {
                        position = new float2(i % 9, i / 9), radius = 0.1f,
                    };
                }
                var goal = new float2(2.2f + (float)random.NextDouble() * 4f, 2.2f + (float)random.NextDouble() * 4f);
                var radius = 1.2f + (float)random.NextDouble() * 2f;
                grid.Bake(goal, radius, count);
                var sources = Sources(blocked, goal, radius, 9);
                Compare(Reference(blocked, sources, 9), grid.Distances, trial, "blocked");
                Compare(Reference(new bool[81], sources, 9), grid.EmptyDistances, trial, "empty");
            }
        }

        private static double[] Sources(bool[] blocked, float2 goal, float radius, int size)
        {
            var sources = new double[blocked.Length];
            Array.Fill(sources, double.PositiveInfinity);
            var nearest = double.PositiveInfinity;
            var nearestIndex = -1;
            var count = 0;
            for (var i = 0; i < blocked.Length; i++)
            {
                if (blocked[i]) continue;
                var dx = i % size - (double)goal.x;
                var dy = i / size - (double)goal.y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < nearest) { nearest = distance; nearestIndex = i; }
                if (distance > radius) continue;
                sources[i] = 0;
                count++;
            }
            if (count == 0 && nearestIndex >= 0) sources[nearestIndex] = Math.Max(0, nearest - radius);
            return sources;
        }

        private static double[] Reference(bool[] blocked, double[] sources, int size)
        {
            var distances = (double[])sources.Clone();
            var done = new bool[distances.Length];
            for (var iteration = 0; iteration < distances.Length; iteration++)
            {
                var current = -1;
                var best = double.PositiveInfinity;
                for (var i = 0; i < distances.Length; i++)
                    if (!done[i] && distances[i] < best) { best = distances[i]; current = i; }
                if (current < 0) break;
                done[current] = true;
                for (var next = 0; next < distances.Length; next++)
                {
                    if (done[next] || blocked[next]) continue;
                    var dx = next % size - current % size;
                    var dy = next / size - current / size;
                    if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || dx == 0 && dy == 0) continue;
                    if (dx != 0 && dy != 0 && (blocked[current + dx] || blocked[current + dy * size])) continue;
                    distances[next] = Math.Min(distances[next], best + Math.Sqrt(dx * dx + dy * dy));
                }
            }
            return distances;
        }

        private static void Compare(double[] expected, NativeArray<float> actual, int trial, string field)
        {
            for (var i = 0; i < expected.Length; i++)
            {
                var context = $"{field}, trial {trial}, cell {i}";
                if (double.IsPositiveInfinity(expected[i])) Assert.That(float.IsPositiveInfinity(actual[i]), Is.True, context);
                else Assert.That(actual[i], Is.EqualTo(expected[i]).Within(Tolerance), context);
            }
        }

        private sealed class Grid : IDisposable
        {
            internal readonly NativeArray<float> Distances;
            internal readonly NativeArray<float> EmptyDistances;
            internal NativeArray<ObstacleData> Obstacles;
            internal TerminalFieldView View;
            private readonly NativeArray<byte> occupied;
            private readonly NativeArray<int> heap;
            private readonly NativeArray<int> positions;
            private readonly NativeArray<TerminalFieldBakeJob.BakeResult> result;
            private readonly int size;

            internal Grid(int size)
            {
                this.size = size;
                Distances = new NativeArray<float>(size * size, Allocator.TempJob);
                EmptyDistances = new NativeArray<float>(size * size, Allocator.TempJob);
                Obstacles = new NativeArray<ObstacleData>(size * size, Allocator.TempJob);
                occupied = new NativeArray<byte>(size * size, Allocator.TempJob);
                heap = new NativeArray<int>(size * size, Allocator.TempJob);
                positions = new NativeArray<int>(size * size, Allocator.TempJob);
                result = new NativeArray<TerminalFieldBakeJob.BakeResult>(1, Allocator.TempJob);
            }

            internal void Bake(float2 goal, float radius, int count = 0)
            {
                new TerminalFieldBakeJob
                {
                    obstacles = Obstacles, obstacleCount = count, resolution = size, spacing = 1f,
                    goal = goal, goalRadius = radius, distances = Distances, emptyDistances = EmptyDistances,
                    occupied = occupied, heap = heap, heapPosition = positions, result = result,
                }.Schedule().Complete();
                View = new TerminalFieldView
                {
                    distances = Distances, emptyDistances = EmptyDistances, occupied = occupied, valid = true,
                    resolution = size, spacing = 1f, goal = goal, goalRadius = radius,
                    seedIndex = result[0].seedIndex, maxFiniteDistance = result[0].maxFiniteDistance,
                };
            }

            public void Dispose()
            {
                Distances.Dispose(); EmptyDistances.Dispose(); Obstacles.Dispose(); occupied.Dispose();
                heap.Dispose(); positions.Dispose(); result.Dispose();
            }
        }
    }
}
#endif
