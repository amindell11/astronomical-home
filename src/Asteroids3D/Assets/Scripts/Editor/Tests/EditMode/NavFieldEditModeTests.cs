using System.Collections.Generic;
using Movement.MPC.Field;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests.EditMode
{
    /// <summary>
    /// Track B3 — cost-to-go NavField core (resurrected pre-#43 Dijkstra) and its terminal
    /// sampling view. Headless: the solve runs as a synchronous Burst job (SolveImmediate).
    /// </summary>
    [Category("AI")]
    public class NavFieldEditModeTests
    {
        private static NavField Solve(int grid, float cs, float2 goal, IReadOnlyList<float3> obs)
        {
            var field = new NavField(grid, cs);
            var count = obs?.Count ?? 0;
            var arr = new NativeArray<float3>(math.max(1, count), Allocator.TempJob);
            for (var i = 0; i < count; i++) arr[i] = obs[i];
            field.SolveImmediate(goal, arr, count);
            arr.Dispose();
            return field;
        }

        private static int Cell(NavField f, float2 p)
        {
            var gx = (int)math.floor((p.x - f.Origin.x) / f.CellSize);
            var gy = (int)math.floor((p.y - f.Origin.y) / f.CellSize);
            return gy * f.GridSize + gx;
        }

        private static float CostAt(NavField f, float2 p) => f.CostToGo[Cell(f, p)];

        // Vertical wall of unit-blocking obstacles at plane-x = wallX, rows yLo..yHi inclusive.
        private static List<float3> Wall(float wallX, int yLo, int yHi, float radius = 0.6f)
        {
            var list = new List<float3>();
            for (var y = yLo; y <= yHi; y++)
                list.Add(new float3(wallX + 0.5f, y + 0.5f, radius));
            return list;
        }

        [Test]
        public void EmptyGrid_SourceIsZero_AndCostGrowsWithDistance()
        {
            var goal = new float2(30, 24);
            using var f = Solve(48, 1f, goal, null);

            Assert.IsTrue(f.HasSolution);
            Assert.AreEqual(0f, CostAt(f, goal), 1e-4f, "source cell cost-to-go is 0");

            var c1 = CostAt(f, goal + new float2(5, 0));
            var c2 = CostAt(f, goal + new float2(10, 0));
            Assert.AreEqual(5f, c1, 1e-3f, "5 cells away along a cardinal ray ≈ 5 steps");
            Assert.Greater(c2, c1, "cost increases with distance");
        }

        [Test]
        public void EmptyGrid_DiagonalUsesOctileDistance()
        {
            var goal = new float2(30, 24);
            using var f = Solve(48, 1f, goal, null);
            // 8-connected Dijkstra ⇒ pure diagonal costs sqrt(2) per step.
            Assert.AreEqual(4f * 1.41421356f, CostAt(f, goal + new float2(4, 4)), 1e-2f);
        }

        [Test]
        public void Obstacle_StampsBlockedCell_WithInfiniteCost()
        {
            var goal = new float2(30, 24);
            var obs = new List<float3> { new(20.5f, 24.5f, 0.6f) };
            using var f = Solve(48, 1f, goal, obs);

            var idx = Cell(f, new float2(20.5f, 24.5f));
            Assert.AreEqual(1, f.Blocked[idx], "cell under the obstacle is blocked");
            Assert.IsTrue(float.IsInfinity(f.CostToGo[idx]), "blocked cell keeps infinite cost");
        }

        [Test]
        public void Wall_ForcesDetour_RaisesCostToGoAboveStraightLine()
        {
            var goal = new float2(30, 24);
            var point = new float2(10, 24); // 20 units left of the goal, same row
            var wall = Wall(18, 0, 40);       // blocks the straight line; gap only near the top

            using var open = Solve(48, 1f, goal, null);
            using var walled = Solve(48, 1f, goal, wall);

            Assert.AreEqual(20f, CostAt(open, point), 1e-2f, "open field: straight-line 20 steps");
            Assert.Greater(CostAt(walled, point), 32f,
                "wall forces a detour to the top gap — cost-to-go well above the 20-step straight line");
        }

        [Test]
        public void Sample_OffGrid_ReturnsFiniteFallback()
        {
            var goal = new float2(30, 24);
            using var f = Solve(48, 1f, goal, null);
            var data = f.Data(1f); // secondsPerStep = 1

            var farOutside = new float2(500, 500);
            var s = TerminalFieldData.Sample(farOutside, data);
            Assert.IsTrue(math.isfinite(s), "off-grid sample is finite (never infinity)");
            Assert.Greater(s, 0f);
        }

        [Test]
        public void Sample_RoutedTerminalCostsMoreThanOpen_AtEqualEuclideanDistance()
        {
            var goal = new float2(30, 24);
            var wall = Wall(18, 0, 40);
            using var f = Solve(48, 1f, goal, wall);
            var data = f.Data(1f); // secondsPerStep = 1 ⇒ Sample ≈ cost-to-go in steps

            var behindWall = new float2(10, 24); // 20 left of goal — must detour
            var openSameDist = new float2(30, 44); // 20 above goal — open corridor

            var routed = TerminalFieldData.Sample(behindWall, data);
            var open = TerminalFieldData.Sample(openSameDist, data);

            Assert.Greater(routed, open,
                "a terminal state walled off from the goal costs more than an equidistant open one — " +
                "the terminal hook reorders elites toward the around-route");
            Assert.AreEqual(20f, open, 0.5f, "open corridor sample ≈ 20 steps");
        }

        [Test]
        public void Data_BeforeSolve_IsInvalid()
        {
            var f = new NavField(16, 1f);
            try
            {
                Assert.AreEqual(0, f.Data(1f).isValid, "unsolved field reports invalid (hook stays off)");
            }
            finally
            {
                f.Dispose();
            }
        }
    }
}
