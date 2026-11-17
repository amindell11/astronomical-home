using System;
using NUnit.Framework;
using Ships.Keystones;
using UnityEngine;

namespace Tests.EditMode.Ships
{
    [TestFixture]
    [Category("Ships")]
    public sealed class HullOutlineEditModeTests
    {
        private static readonly Vector2[] Square =
        {
            new(1f, 1f), new(0f, 0f), new(0f, 1f), new(1f, 0f), new(0f, 0f)
        };

        [Test]
        public void Square_IsCounterClockwise_WithTheRequestedSampleCount()
        {
            var hull = HullOutline.FromPoints(Square, 8);

            Assert.That(hull, Has.Length.EqualTo(8));
            Assert.That(SignedArea(hull), Is.GreaterThan(0f));
            AssertPoint(hull[0], new Vector2(0f, 0f));
            AssertPoint(hull[1], new Vector2(0.5f, 0f));
            AssertPoint(hull[2], new Vector2(1f, 0f));
            AssertPoint(hull[3], new Vector2(1f, 0.5f));
            AssertPoint(hull[4], new Vector2(1f, 1f));
            AssertPoint(hull[5], new Vector2(0.5f, 1f));
            AssertPoint(hull[6], new Vector2(0f, 1f));
            AssertPoint(hull[7], new Vector2(0f, 0.5f));
        }

        [Test]
        public void InteriorPoints_AreDropped()
        {
            var interior = new Vector2(0.3f, 0.7f);
            var hull = HullOutline.FromPoints(new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                interior, new Vector2(0.5f, 0.5f)
            }, 4);

            Assert.That(hull, Has.Length.EqualTo(4));
            AssertPoint(hull[0], new Vector2(0f, 0f));
            AssertPoint(hull[1], new Vector2(1f, 0f));
            AssertPoint(hull[2], new Vector2(1f, 1f));
            AssertPoint(hull[3], new Vector2(0f, 1f));
        }

        [Test]
        public void Normals_PointOutward()
        {
            var hull = HullOutline.FromPoints(Square, 8);
            var centroid = Vector2.zero;
            foreach (var point in hull) centroid += point;
            centroid /= hull.Length;

            for (var i = 0; i < hull.Length; i++)
            {
                var normal = HullOutline.Normal(hull, i);
                Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-4f), $"point {i}");
                Assert.That(Vector2.Dot(normal, hull[i] - centroid), Is.GreaterThan(0f), $"point {i}");
            }

            Assert.That(Vector2.Dot(HullOutline.Normal(hull, 1), Vector2.down), Is.GreaterThan(0.99f));
        }

        [Test]
        public void Extent_IsMinAndMaxY()
        {
            var hull = HullOutline.FromPoints(new[]
            {
                new Vector2(-2f, 5f), new Vector2(2f, 1f), new Vector2(-2f, 1f), new Vector2(2f, 5f)
            }, 4);

            var extent = HullOutline.Extent(hull);
            Assert.That(extent.min, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(extent.max, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void FewerThanThreePoints_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => HullOutline.FromPoints(null, 4));
            Assert.Throws<ArgumentException>(() => HullOutline.FromPoints(Array.Empty<Vector2>(), 4));
            Assert.Throws<ArgumentException>(() => HullOutline.FromPoints(new[] { Vector2.zero }, 4));
            Assert.Throws<ArgumentException>(() => HullOutline.FromPoints(new[] { Vector2.zero, Vector2.up }, 4));
            Assert.Throws<ArgumentException>(() => HullOutline.FromPoints(new[]
            {
                Vector2.zero, Vector2.zero, Vector2.up, Vector2.up
            }, 4));
            Assert.Throws<ArgumentException>(() => HullOutline.FromPoints(new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(2f, 2f), new Vector2(3f, 3f)
            }, 4));
            Assert.Throws<ArgumentException>(() => HullOutline.FromPoints(Square, 2));
        }

        private static float SignedArea(Vector2[] outline)
        {
            var sum = 0f;
            for (var i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                sum += a.x * b.y - b.x * a.y;
            }

            return sum * 0.5f;
        }

        private static void AssertPoint(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f));
        }
    }
}
