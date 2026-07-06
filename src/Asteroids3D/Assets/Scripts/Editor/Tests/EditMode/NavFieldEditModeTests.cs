#if UNITY_EDITOR
using AI.Navigation.Field;
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("MPC")]
    public class NavFieldEditModeTests
    {
        [Test]
        public void Solve_RoutesAroundStampedObstacleWall()
        {
            var field = new NavField(9, 1f);
            field.Recenter(Vector2.zero);

            for (var y = 0; y < 9; y++)
            {
                if (y == 4) continue;
                field.StampObstacle(new Vector2(4.5f, y + 0.5f), 0.51f);
            }

            field.SetSource(new Vector2(8.5f, 4.5f));
            field.Solve();

            Assert.That(field.HasSolution, Is.True);
            Assert.That(field.CostToGo(new Vector2(0.5f, 4.5f)), Is.EqualTo(8f).Within(0.001f),
                "The gap cell should preserve the straight route across the wall.");
            Assert.That(field.CostToGo(new Vector2(0.5f, 0.5f)), Is.GreaterThan(8f),
                "Cells away from the gap should pay the detour cost around the stamped wall.");
            Assert.That(field.IsBlocked(new Vector2(4.5f, 0.5f)), Is.True);
        }

        [Test]
        public void TerminalTimeToGoCost_UsesFieldAndFallsBackOffGrid()
        {
            var costs = new NativeArray<float>(4, Allocator.Temp);
            try
            {
                costs[0] = 0f;
                costs[1] = 1f;
                costs[2] = 1f;
                costs[3] = 2f;

                var input = new CostInput
                {
                    terminalCosts = costs,
                    terminalGridSize = 2,
                    terminalCellSize = 10f,
                    terminalOrigin = float2.zero,
                    terminalSource = float2.zero,
                    terminalNominalSpeed = 5f,
                    terminalHasSolution = 1,
                };

                var onGrid = Cost.TerminalTimeToGoCost(new float2(5f, 5f), input, default);
                var offGrid = Cost.TerminalTimeToGoCost(new float2(30f, 40f), input, default);

                Assert.That(onGrid, Is.EqualTo(0f).Within(0.001f));
                Assert.That(offGrid, Is.EqualTo(10f).Within(0.001f));
            }
            finally
            {
                costs.Dispose();
            }
        }
    }
}
#endif
