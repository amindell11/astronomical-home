using System.Collections.Generic;
using System.Linq;
using Asteroids.Fields.FarField;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Asteroids")]
    public class FarFieldEditModeTests
    {
        private static readonly Vector3 ToCamera = new(0f, 0f, -1f);
        private static readonly Vector3 ScreenRight = Vector3.right;
        private static readonly Vector3 ScreenUp = Vector3.up;

        [Test]
        public void Views_AreTwelveUnitIcosahedronDirectionsWithOrthonormalFrames()
        {
            Assert.That(FarFieldViews.Count, Is.EqualTo(12));
            for (var a = 0; a < FarFieldViews.Count; a++)
            {
                var dir = FarFieldViews.Direction(a);
                var up = FarFieldViews.Up(a);
                var right = FarFieldViews.Right(a);
                Assert.That(dir.magnitude, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(up.magnitude, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(right.magnitude, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(Vector3.Dot(up, dir), Is.EqualTo(0f).Within(1e-5f));
                Assert.That(Vector3.Dot(right, dir), Is.EqualTo(0f).Within(1e-5f));
                Assert.That(Vector3.Dot(right, up), Is.EqualTo(0f).Within(1e-5f));
                for (var b = a + 1; b < FarFieldViews.Count; b++)
                    Assert.That(Vector3.Angle(dir, FarFieldViews.Direction(b)), Is.GreaterThanOrEqualTo(63f),
                        $"views {a} and {b}");
            }
        }

        [Test]
        public void Pick_RecoversViewAndSpin_ForExactBakePoses()
        {
            var spins = new[] { 0f, 0.7f, -1.9f, 3.0f };
            for (var k = 0; k < FarFieldViews.Count; k++)
            foreach (var theta in spins)
            {
                var rotation = PoseShowing(k, theta);
                var pick = FarFieldViews.Pick(rotation, ToCamera, ScreenRight, ScreenUp);
                Assert.That(pick.View, Is.EqualTo(k), $"view {k}, spin {theta}");
                var error = Mathf.Abs(Mathf.DeltaAngle(pick.Spin * Mathf.Rad2Deg, theta * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                Assert.That(error, Is.LessThan(1e-3f), $"view {k}, spin {theta}");
            }
        }

        [Test]
        public void Pick_ChoosesNearestView_ForRandomRotations()
        {
            var rng = new System.Random(4242);
            for (var n = 0; n < 500; n++)
            {
                var rotation = UniformRotation(rng);
                var local = Quaternion.Inverse(rotation) * ToCamera;
                var pick = FarFieldViews.Pick(rotation, ToCamera, ScreenRight, ScreenUp);
                var picked = Vector3.Dot(local, FarFieldViews.Direction(pick.View));
                for (var k = 0; k < FarFieldViews.Count; k++)
                    Assert.That(picked, Is.GreaterThanOrEqualTo(Vector3.Dot(local, FarFieldViews.Direction(k))));
                Assert.That(Vector3.Angle(local, FarFieldViews.Direction(pick.View)), Is.LessThanOrEqualTo(38f));
            }
        }

        private const float CellSize = 4.7f, LoadRadius = 87.5f, UnloadRadius = 131f, Pad = 6f;
        private static readonly Vector2 Anchor = new(37.3f, -11.9f);
        private static readonly Rect StraddlingView = Rect.MinMaxRect(-301.3f, -177.7f, 402.9f, 203.1f);

        [Test]
        public void Cells_ViewInsideLoadRadius_RejectsWithoutQueryingChunks()
        {
            var calls = 0;
            var wholeFar = new List<Vector2Int> { new(9, 9) };
            var boundary = new List<Vector2Int> { new(9, 9) };
            var farCells = new List<Vector2Int> { new(9, 9) };
            var expanded = FarFieldCells.Expand(Rect.MinMaxRect(20f, -30f, 60f, 5f), Pad);
            var any = FarFieldCells.Select(expanded, Anchor, LoadRadius, UnloadRadius, CellSize,
                _ => { calls++; return false; }, wholeFar, boundary, farCells);
            Assert.That(any, Is.False);
            Assert.That(wholeFar, Is.Empty);
            Assert.That(boundary, Is.Empty);
            Assert.That(farCells, Is.Empty);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Cells_BlocksOverlappingTheView_SplitIntoWholeFarAndBoundaryWithNoGaps()
        {
            var expanded = FarFieldCells.Expand(StraddlingView, Pad);
            var wholeFar = new List<Vector2Int>();
            var boundary = new List<Vector2Int>();
            FarFieldCells.Select(expanded, Anchor, LoadRadius, UnloadRadius, CellSize, IsReal,
                wholeFar, boundary, new List<Vector2Int>());

            var blockSize = CellSize * FarFieldCells.BlockCells;
            var expected = new List<Vector2Int>();
            for (var by = Mathf.FloorToInt(expanded.yMin / blockSize); by <= Mathf.FloorToInt(expanded.yMax / blockSize); by++)
            for (var bx = Mathf.FloorToInt(expanded.xMin / blockSize); bx <= Mathf.FloorToInt(expanded.xMax / blockSize); bx++)
                expected.Add(new Vector2Int(bx, by));

            Assert.That(wholeFar, Is.Not.Empty);
            Assert.That(boundary, Is.Not.Empty);
            CollectionAssert.IsEmpty(wholeFar.Intersect(boundary));
            CollectionAssert.AreEquivalent(expected, wholeFar.Concat(boundary));
            foreach (var block in wholeFar)
                foreach (var cell in CellsOf(block))
                    Assert.That(Vector2.Distance(Centre(cell), Anchor), Is.GreaterThan(UnloadRadius),
                        $"whole-far block {block} holds loadable cell {cell}");
        }

        [Test]
        public void Cells_DrawnCellsOverlappingTheView_MatchBruteForceFarCells()
        {
            var expanded = FarFieldCells.Expand(StraddlingView, Pad);
            var wholeFar = new List<Vector2Int>();
            var farCells = new List<Vector2Int>();
            var any = FarFieldCells.Select(expanded, Anchor, LoadRadius, UnloadRadius, CellSize, IsReal,
                wholeFar, new List<Vector2Int>(), farCells);

            var drawn = new HashSet<Vector2Int>(farCells);
            foreach (var block in wholeFar) drawn.UnionWith(CellsOf(block));

            var expected = new HashSet<Vector2Int>();
            var minX = Mathf.FloorToInt(expanded.xMin / CellSize) - 2;
            var maxX = Mathf.FloorToInt(expanded.xMax / CellSize) + 2;
            var minY = Mathf.FloorToInt(expanded.yMin / CellSize) - 2;
            var maxY = Mathf.FloorToInt(expanded.yMax / CellSize) + 2;
            for (var cy = minY; cy <= maxY; cy++)
            for (var cx = minX; cx <= maxX; cx++)
            {
                var cell = new Vector2Int(cx, cy);
                if (OverlapsClosed(expanded, cell) && !IsReal(cell)) expected.Add(cell);
            }

            Assert.That(any, Is.True);
            Assert.That(farCells.All(c => OverlapsClosed(expanded, c)), "boundary far cells must overlap the view");
            CollectionAssert.AreEquivalent(expected, drawn.Where(c => OverlapsClosed(expanded, c)));
            Assert.That(drawn.Any(IsReal), Is.False, "a real cell must never draw as far");
        }

        private static bool IsReal(Vector2Int cell) => Vector2.Distance(Centre(cell), Anchor) <= LoadRadius;

        private static bool OverlapsClosed(Rect rect, Vector2Int cell) =>
            (cell.x + 1) * CellSize >= rect.xMin && cell.x * CellSize <= rect.xMax
            && (cell.y + 1) * CellSize >= rect.yMin && cell.y * CellSize <= rect.yMax;

        private static IEnumerable<Vector2Int> CellsOf(Vector2Int block)
        {
            for (var y = 0; y < FarFieldCells.BlockCells; y++)
            for (var x = 0; x < FarFieldCells.BlockCells; x++)
                yield return new Vector2Int(block.x * FarFieldCells.BlockCells + x, block.y * FarFieldCells.BlockCells + y);
        }

        private static Vector2 Centre(Vector2Int cell) =>
            new((cell.x + 0.5f) * CellSize, (cell.y + 0.5f) * CellSize);

        private static Quaternion PoseShowing(int view, float spin)
        {
            var dir = FarFieldViews.Direction(view);
            var bakePose = Quaternion.LookRotation(-dir, FarFieldViews.Up(view));
            var projectedUp = Mathf.Cos(spin) * ScreenUp - Mathf.Sin(spin) * ScreenRight;
            var screenPose = Quaternion.LookRotation(-ToCamera, projectedUp);
            return screenPose * Quaternion.Inverse(bakePose);
        }

        private static Quaternion UniformRotation(System.Random rng)
        {
            var u1 = (float)rng.NextDouble();
            var u2 = (float)rng.NextDouble() * 2f * Mathf.PI;
            var u3 = (float)rng.NextDouble() * 2f * Mathf.PI;
            var a = Mathf.Sqrt(1f - u1);
            var b = Mathf.Sqrt(u1);
            return new Quaternion(a * Mathf.Sin(u2), a * Mathf.Cos(u2), b * Mathf.Sin(u3), b * Mathf.Cos(u3));
        }
    }
}
