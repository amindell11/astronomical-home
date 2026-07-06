#if UNITY_EDITOR
using AI.Scanning;
using Game;
using Movement.MPC;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Unit tests for the analytic <see cref="GapDetector"/> — pure geometry over an
    /// <see cref="ObstacleScan"/>. Obstacles are placed in the game plane (PlaneAxis.Y maps a
    /// world (x, 0, y) to plane (x, y)).
    /// </summary>
    [Category("MPC")]
    public class MpcGapDetectorEditModeTests
    {
        private const float ShipRadius = 1.4f;
        private const float MaxBank = 35f * Mathf.Deg2Rad;
        private const float SafetyMargin = 0.1f;
        private const float WorkingRange = 40f;

        private bool configuredHere;

        [SetUp]
        public void SetUp()
        {
            if (!GamePlane.IsConfigured) { GamePlane.Configure(PlaneAxis.Y); configuredHere = true; }
        }

        [TearDown]
        public void TearDown()
        {
            if (configuredHere) { GamePlane.Reset(); configuredHere = false; }
        }

        private static ObstacleScan Scan(params (float x, float y, float r)[] discs)
        {
            var buf = new DetectedObstacle[discs.Length];
            for (var i = 0; i < discs.Length; i++)
                buf[i] = new DetectedObstacle(new Vector3(discs[i].x, 0f, discs[i].y), discs[i].r, null);
            return new ObstacleScan(buf, discs.Length);
        }

        private static int Detect(ObstacleScan scan, Gap[] gaps) =>
            new GapDetector().Detect(float2.zero, new float2(0f, 1f), scan,
                ShipRadius, MaxBank, SafetyMargin, WorkingRange, gaps, gaps.Length);

        [Test]
        public void TwoDiscs_SingleGapTowardGoal_CorrectAxisAndLinearWidth()
        {
            var gaps = new Gap[3];
            var n = Detect(Scan((-3f, 8f, 1f), (3f, 8f, 1f)), gaps);

            Assert.That(n, Is.GreaterThanOrEqualTo(1));
            var top = gaps[0];
            // Gap axis points straight toward the goal (+Y => yaw 0 in MPC convention).
            Assert.That(top.dirRad, Is.EqualTo(0f).Within(0.15f), "gap axis should bisect toward the goal");
            // Linear width = centre distance (6) − both radii (2) = 4.
            Assert.That(top.linearWidth, Is.EqualTo(4f).Within(0.5f));
            Assert.That(top.classification, Is.EqualTo(GapClass.Open));
        }

        [Test]
        public void OccludedObstacle_BehindAWall_DoesNotSplitTheGap()
        {
            var gapsA = new Gap[3];
            var withoutBehind = Detect(Scan((-3f, 8f, 1f), (3f, 8f, 1f)), gapsA);

            var gapsB = new Gap[3];
            // Third disc directly behind the left wall (same bearing, farther) — fully occluded.
            var withBehind = Detect(Scan((-3f, 8f, 1f), (3f, 8f, 1f), (-6f, 16f, 1f)), gapsB);

            Assert.That(withBehind, Is.EqualTo(withoutBehind),
                "an occluded obstacle must not create a spurious extra gap");
            Assert.That(gapsB[0].dirRad, Is.EqualTo(gapsA[0].dirRad).Within(1e-3f));
            Assert.That(gapsB[0].linearWidth, Is.EqualTo(gapsA[0].linearWidth).Within(1e-3f),
                "the occluded obstacle must not change the gap's walls");
        }

        [Test]
        public void GapBetweenBankAndOpenWidth_ClassifiesBankOnly()
        {
            // Centre distance 4.7, radii 1 each => linear width 2.7. Ship diameter 2.8, bank
            // diameter 2*1.4*cos(35°)=2.29 => 2.7 is passable only while banked.
            var gaps = new Gap[3];
            var n = Detect(Scan((-2.35f, 8f, 1f), (2.35f, 8f, 1f)), gaps);

            Assert.That(n, Is.GreaterThanOrEqualTo(1));
            Assert.That(gaps[0].linearWidth, Is.EqualTo(2.7f).Within(0.4f));
            Assert.That(gaps[0].classification, Is.EqualTo(GapClass.BankOnly));
        }

        [Test]
        public void NoObstacles_SingleOpenGapTowardGoal()
        {
            var gaps = new Gap[3];
            var n = Detect(Scan(), gaps);
            Assert.That(n, Is.EqualTo(1));
            Assert.That(gaps[0].classification, Is.EqualTo(GapClass.Open));
            Assert.That(gaps[0].dirRad, Is.EqualTo(0f).Within(1e-3f));
        }
    }
}
#endif
