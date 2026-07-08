#if UNITY_EDITOR
using System.Collections.Generic;
using AI.Scanning;
using Game;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Regression tests for <see cref="ObstacleSelection.KeepNearest"/> — the fixed-capacity
    /// obstacle cull. The bug it guards against: when more asteroids overlap the query box than
    /// the buffer can hold, the field query used to keep the first N in chunk-scan order, so in
    /// a dense field it silently dropped NEAR rocks (scanned later) in favour of FAR ones
    /// (scanned first), blinding the MPC to the geometry that actually matters.
    /// </summary>
    [Category("AI")]
    public class ObstacleSelectionEditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            if (GamePlane.IsConfigured) GamePlane.Reset();
            GamePlane.Configure(PlaneAxis.Z); // XY plane: world (x, y, 0) → plane (x, y)
        }

        [TearDown]
        public void TearDown() => GamePlane.Reset();

        private static DetectedObstacle At(float x, float y) =>
            new DetectedObstacle(new Vector3(x, y, 0f), 1f, null);

        private static float Dist(DetectedObstacle o, Vector2 c) => (o.position - c).magnitude;

        [Test]
        public void KeepNearest_OverCapacity_KeepsNearest_DropsFar_RegardlessOfScanOrder()
        {
            var center = new Vector2(0f, 0f);

            // FAR rocks added FIRST (scan order), NEAR rocks added LAST. The old first-N
            // behaviour would have returned the far rocks; KeepNearest must return the near ones.
            var candidates = new List<DetectedObstacle>
            {
                At(100f, 0f), At(0f, 120f), At(-140f, 0f), At(0f, -110f), At(90f, 90f), At(-80f, 80f),
                At(3f, 0f), At(0f, 2f), At(-1f, 0f), At(0f, -4f),
            };

            var dst = new DetectedObstacle[4];
            var n = ObstacleSelection.KeepNearest(candidates, center, dst);

            Assert.AreEqual(4, n, "should fill the buffer");
            for (var i = 0; i < n; i++)
                Assert.Less(Dist(dst[i], center), 10f,
                    "every kept obstacle must be one of the near cluster, not a far rock scanned earlier");

            // The near rock added dead LAST must survive — the exact case first-N dropped.
            var keptLast = false;
            for (var i = 0; i < n; i++)
                if (Mathf.Approximately(dst[i].position.x, 0f) && Mathf.Approximately(dst[i].position.y, -4f))
                    keptLast = true;
            Assert.IsTrue(keptLast, "the last-scanned near rock must not be dropped for an earlier far one");
        }

        [Test]
        public void KeepNearest_RanksNearestFirst()
        {
            var center = new Vector2(5f, 5f);
            var candidates = new List<DetectedObstacle> { At(30f, 5f), At(6f, 5f), At(5f, 12f), At(20f, 20f), At(5f, 6f) };
            var dst = new DetectedObstacle[3];

            var n = ObstacleSelection.KeepNearest(candidates, center, dst);

            Assert.AreEqual(3, n);
            for (var i = 1; i < n; i++)
                Assert.LessOrEqual(Dist(dst[i - 1], center), Dist(dst[i], center),
                    "results must be ordered nearest-first");
        }

        [Test]
        public void KeepNearest_UnderCapacity_CopiesAll()
        {
            var candidates = new List<DetectedObstacle> { At(1f, 0f), At(0f, 2f) };
            var dst = new DetectedObstacle[8];
            Assert.AreEqual(2, ObstacleSelection.KeepNearest(candidates, Vector2.zero, dst));
        }

        [Test]
        public void KeepNearest_EmptyOrNoBuffer_ReturnsZero()
        {
            Assert.AreEqual(0, ObstacleSelection.KeepNearest(new List<DetectedObstacle>(), Vector2.zero, new DetectedObstacle[4]));
            Assert.AreEqual(0, ObstacleSelection.KeepNearest(new List<DetectedObstacle> { At(1f, 1f) }, Vector2.zero, new DetectedObstacle[0]));
        }
    }
}
#endif
