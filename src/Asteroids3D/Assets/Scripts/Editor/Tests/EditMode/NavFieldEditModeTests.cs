using AI.Planning;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Planning")]
    public class NavFieldEditModeTests
    {
        private static NavField BuildField(int size = 10, float cellSize = 1f, Vector2? origin = null)
        {
            var f = new NavField(size, cellSize);
            f.Recenter(origin ?? Vector2.zero);
            return f;
        }

        [Test]
        public void Solve_Source_Has_Zero_Cost()
        {
            var f = BuildField();
            f.SetSource(new Vector2(5f, 5f));
            f.Solve();

            Assert.IsTrue(f.HasSolution);
            Assert.AreEqual(0f, f.CostToGo(new Vector2(5f, 5f)));
        }

        [Test]
        public void Solve_OpenField_GradientHeadsToSource()
        {
            var f = BuildField();
            f.SetSource(new Vector2(2.5f, 2.5f)); // cell (2,2)
            f.Solve();

            // Ship at (8,8). Routed cell should walk toward source.
            var routed = f.RoutedCell(new Vector2(8.5f, 8.5f), 5, RoutingMode.Chase);

            Assert.Less(routed.x, 8.5f, "Routed cell should be closer to source in x");
            Assert.Less(routed.y, 8.5f, "Routed cell should be closer to source in y");
        }

        [Test]
        public void Solve_OpenField_EvadeHeadsAwayFromSource()
        {
            var f = BuildField();
            f.SetSource(new Vector2(2.5f, 2.5f));
            f.Solve();

            // Ship at (5, 5) — gradient ascent should head toward grid corner away from source
            var routed = f.RoutedCell(new Vector2(5.5f, 5.5f), 4, RoutingMode.Evade);

            Assert.Greater(routed.x, 5.5f, "Evade should head away from source in x");
            Assert.Greater(routed.y, 5.5f, "Evade should head away from source in y");
        }

        [Test]
        public void StampObstacle_BlocksCellsWithinRadius()
        {
            var f = BuildField();
            f.StampObstacle(new Vector2(5f, 5f), 1.5f);

            // Cell containing (5,5) should be blocked
            Assert.IsTrue(f.IsBlocked(new Vector2(5f, 5f)));
            // Cell far away should not be
            Assert.IsFalse(f.IsBlocked(new Vector2(0f, 0f)));
        }

        [Test]
        public void Solve_SingleAsteroid_GradientRoutesAround()
        {
            // 20x20 grid, 1m cells. Asteroid centered at (10,10) blocking ~3 cells radius.
            // Source at (1,1), ship at (18,18). Routing should not go through (10,10).
            var f = BuildField(20);
            f.StampObstacle(new Vector2(10f, 10f), 2.5f);
            f.SetSource(new Vector2(1.5f, 1.5f));
            f.Solve();

            // Walk a long route from far ship — every step should be in a free cell.
            var pos = new Vector2(18.5f, 18.5f);
            for (var i = 0; i < 30; i++)
            {
                Assert.IsFalse(f.IsBlocked(pos), $"Step {i} landed on blocked cell at {pos}");
                var next = f.RoutedCell(pos, 1, RoutingMode.Chase);
                if ((next - pos).sqrMagnitude < 1e-6f) break;
                pos = next;
            }
            // Should arrive near source.
            Assert.Less((pos - new Vector2(1.5f, 1.5f)).magnitude, 2f, "Route should reach source vicinity");
        }

        [Test]
        public void Solve_WallWithGap_RouteFindsGap()
        {
            // 20x20 grid. Wall along y=10 except for gap at x=15.
            var f = BuildField(20);
            for (var x = 0; x < 20; x++)
            {
                if (x == 15) continue; // gap
                f.StampObstacle(new Vector2(x + 0.5f, 10.5f), 0.4f);
            }
            f.SetSource(new Vector2(2.5f, 2.5f));
            f.Solve();

            // Ship on the far side — y=18. Routed cell after several steps should pass through x≈15.
            var pos = new Vector2(5.5f, 18.5f);
            var passedGap = false;
            for (var i = 0; i < 40; i++)
            {
                if (Mathf.Abs(pos.y - 10.5f) < 1f)
                {
                    // Inside the wall row; we should be near the gap
                    if (Mathf.Abs(pos.x - 15.5f) < 1.5f) passedGap = true;
                }
                var next = f.RoutedCell(pos, 1, RoutingMode.Chase);
                if ((next - pos).sqrMagnitude < 1e-6f) break;
                pos = next;
            }
            Assert.IsTrue(passedGap, "Route should pass through the gap at x=15");
        }

        [Test]
        public void Solve_SourceInBlockedCell_FallsBackToNearestFree()
        {
            var f = BuildField(10);
            // Center on cell (5,5) (center=5.5,5.5), radius 0.6 blocks just that cell.
            f.StampObstacle(new Vector2(5.5f, 5.5f), 0.6f);
            Assert.IsTrue(f.IsBlocked(new Vector2(5.2f, 5.2f)), "Test setup: source cell should be blocked");

            f.SetSource(new Vector2(5.2f, 5.2f));
            f.Solve();
            Assert.IsTrue(f.HasSolution);

            // Some unblocked neighbor of the source cell should be the effective source (cost 0).
            var foundEffectiveSource = false;
            for (var dx = -1; dx <= 1 && !foundEffectiveSource; dx++)
            {
                for (var dy = -1; dy <= 1 && !foundEffectiveSource; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (f.CostToGo(new Vector2(5.5f + dx, 5.5f + dy)) == 0f)
                        foundEffectiveSource = true;
                }
            }
            Assert.IsTrue(foundEffectiveSource, "A neighbor of the blocked source cell should be the effective source");
        }

        [Test]
        public void RoutedCell_ShipInBlockedCell_FindsFreeCell()
        {
            var f = BuildField(10);
            f.StampObstacle(new Vector2(8f, 8f), 0.4f); // Block cell (8,8)
            f.SetSource(new Vector2(2.5f, 2.5f));
            f.Solve();

            // Ship in the blocked cell — should still produce some routed point in a free cell
            var routed = f.RoutedCell(new Vector2(8.2f, 8.2f), 3, RoutingMode.Chase);
            Assert.IsFalse(f.IsBlocked(routed), "Routed cell must be in a free cell");
        }

        [Test]
        public void Solve_FullyEnclosedSource_LeavesUnreachableCellsAtInfinity()
        {
            // 20x20 grid. Wall completely encloses the source region (cells 0..2).
            var f = BuildField(20);
            for (var x = 0; x < 20; x++) f.StampObstacle(new Vector2(x + 0.5f, 3.5f), 0.4f);
            for (var y = 0; y < 20; y++) f.StampObstacle(new Vector2(3.5f, y + 0.5f), 0.4f);
            f.SetSource(new Vector2(1.5f, 1.5f));
            f.Solve();

            // Source area reachable
            Assert.AreEqual(0f, f.CostToGo(new Vector2(1.5f, 1.5f)));
            // Far side of wall unreachable
            Assert.IsTrue(float.IsPositiveInfinity(f.CostToGo(new Vector2(15.5f, 15.5f))));
        }

        [Test]
        public void RoutedCell_NoSolution_ReturnsShipPos()
        {
            var f = BuildField();
            // Don't call Solve — HasSolution is false.
            var ship = new Vector2(5f, 5f);
            var routed = f.RoutedCell(ship, 5, RoutingMode.Chase);
            Assert.AreEqual(ship, routed);
        }

        [Test]
        public void RoutedCell_ShipOutsideGrid_ReturnsShipPos()
        {
            var f = BuildField();
            f.SetSource(new Vector2(5f, 5f));
            f.Solve();
            var ship = new Vector2(100f, 100f); // off-grid
            var routed = f.RoutedCell(ship, 5, RoutingMode.Chase);
            Assert.AreEqual(ship, routed);
        }

        [Test]
        public void Recenter_ChangesOriginAndInvalidatesSolution()
        {
            var f = BuildField();
            f.SetSource(new Vector2(5f, 5f));
            f.Solve();
            Assert.IsTrue(f.HasSolution);

            f.Recenter(new Vector2(100f, 100f));
            Assert.AreEqual(new Vector2(100f, 100f), f.Origin);
            Assert.IsFalse(f.HasSolution);
        }

        [Test]
        public void ClearObstacles_RemovesBlocking()
        {
            var f = BuildField();
            f.StampObstacle(new Vector2(5f, 5f), 1f);
            Assert.IsTrue(f.IsBlocked(new Vector2(5f, 5f)));
            f.ClearObstacles();
            Assert.IsFalse(f.IsBlocked(new Vector2(5f, 5f)));
        }

        [Test]
        public void Solve_DiagonalCostExceedsCardinal()
        {
            // Source at (0,0). Verify diagonal step has higher cost than cardinal.
            var f = BuildField(20);
            f.SetSource(new Vector2(0.5f, 0.5f));
            f.Solve();

            var diagonal = f.CostToGo(new Vector2(5.5f, 5.5f));   // ~5*sqrt(2) ≈ 7.07
            var cardinal = f.CostToGo(new Vector2(7.5f, 0.5f));   // 7
            Assert.Greater(diagonal, 6.9f);
            Assert.Less(diagonal, 7.2f);
            Assert.AreEqual(7f, cardinal, 0.01f);
        }

        [Test]
        public void Solve_BoundaryActsAsWall()
        {
            // Source pressed against the corner. Costs propagate inward only — verify off-grid query is infinity.
            var f = BuildField(10);
            f.SetSource(new Vector2(0.5f, 0.5f));
            f.Solve();

            // Off-grid position
            Assert.IsTrue(float.IsPositiveInfinity(f.CostToGo(new Vector2(-5f, -5f))));
            // Far corner is reachable through the grid
            Assert.IsFalse(float.IsPositiveInfinity(f.CostToGo(new Vector2(9.5f, 9.5f))));
        }
    }
}
