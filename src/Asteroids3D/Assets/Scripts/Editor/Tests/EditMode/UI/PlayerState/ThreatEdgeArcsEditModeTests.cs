using NUnit.Framework;
using UI.PlayerState.Alerts;
using UnityEngine;

namespace Tests.EditMode.UI.PlayerState
{
    [Category("UI")]
    public sealed class ThreatEdgeArcsEditModeTests
    {
        private static readonly Vector2 Half = new(960f, 540f);
        private const float Inset = 30f;

        [TestCase(2000f, 0f, 930f, 0f, 0f)]
        [TestCase(-2000f, 0f, -930f, 0f, 180f)]
        [TestCase(0f, 1000f, 0f, 510f, 90f)]
        [TestCase(0f, -1000f, 0f, -510f, -90f)]
        [TestCase(1920f, 540f, 930f, 261.5625f, 15.70864f)]
        [TestCase(1000f, 1000f, 510f, 510f, 45f)]
        public void OffScreenPoint_SitsInsideTheEdgeItsBearingCrosses(float x, float y,
            float edgeX, float edgeY, float degrees)
        {
            Assert.IsTrue(ThreatEdgeArcs.TryPlace(new Vector2(x, y), Half, Inset, out var point, out var bearing));
            Assert.AreEqual(edgeX, point.x, .01f);
            Assert.AreEqual(edgeY, point.y, .01f);
            Assert.AreEqual(degrees, bearing, .01f);
        }

        [Test]
        public void ArcSlidesAlongTheEdgeWithTheBearing()
        {
            ThreatEdgeArcs.TryPlace(new Vector2(2000f, -200f), Half, Inset, out var low, out _);
            ThreatEdgeArcs.TryPlace(new Vector2(2000f, 200f), Half, Inset, out var high, out _);
            Assert.AreEqual(930f, low.x, .01f);
            Assert.AreEqual(930f, high.x, .01f);
            Assert.Less(low.y, 0f);
            Assert.Greater(high.y, 0f);
        }

        [TestCase(0f, 0f)]
        [TestCase(959f, 539f)]
        [TestCase(-960f, -540f)]
        public void OnScreenPoint_DrawsNoArc(float x, float y) =>
            Assert.IsFalse(ThreatEdgeArcs.TryPlace(new Vector2(x, y), Half, Inset, out _, out _));
    }
}
