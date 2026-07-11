using NUnit.Framework;
using UI;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("UI")]
    public class HangarPreviewFramingEditModeTests
    {
        private const float FovDeg = 30f;
        private static readonly Vector3 CameraDirection = new Vector3(0f, 0.55f, -1f).normalized;

        [Test]
        [Category("Smoke")]
        public void ElongatedHull_StaysInFrustum_AtAllSpinAngles()
        {
            AssertSweptBoundsInFrustum(
                new Bounds(Vector3.zero, new Vector3(8f, 1.2f, 3f)), Vector3.zero);
        }

        [Test]
        public void OffAxisHull_AtStageOffset_StaysInFrustum_AtAllSpinAngles()
        {
            var anchor = new Vector3(0f, -1000f, 0f);
            AssertSweptBoundsInFrustum(
                new Bounds(anchor + new Vector3(0.6f, 0.4f, -0.5f), new Vector3(6.4f, 1f, 2.4f)), anchor);
        }

        [Test]
        public void TinyHull_StaysInFrustum_AtAllSpinAngles()
        {
            AssertSweptBoundsInFrustum(
                new Bounds(Vector3.zero, Vector3.one * 0.2f), Vector3.zero);
        }

        [Test]
        public void PopInOvershoot_CoversTheEaseOutBackPeak()
        {
            var peak = 0f;
            for (var t = 0f; t <= 1f; t += 0.001f)
                peak = Mathf.Max(peak, HangarPreviewStage.EaseOutBack(t));
            Assert.LessOrEqual(peak, HangarPreviewStage.PopInOvershoot,
                "framing budgets PopInOvershoot; the easing curve must stay under it");
        }

        [Test]
        public void ElongatedHull_FillsAReasonableShareOfTheFrame()
        {
            var maxRatio = MaxProjectedFrustumRatio(
                new Bounds(Vector3.zero, new Vector3(8f, 1.2f, 3f)), Vector3.zero);
            Assert.GreaterOrEqual(maxRatio, 0.5f, "framing is far looser than the swept bounds require");
        }

        private static void AssertSweptBoundsInFrustum(Bounds bounds, Vector3 anchor)
        {
            ForEachSpunCorner(bounds, anchor, (local, limit, label) =>
            {
                Assert.Greater(local.z, 0f, label);
                Assert.LessOrEqual(Mathf.Abs(local.x), limit, label);
                Assert.LessOrEqual(Mathf.Abs(local.y), limit, label);
            });
        }

        private static float MaxProjectedFrustumRatio(Bounds bounds, Vector3 anchor)
        {
            var maxRatio = 0f;
            ForEachSpunCorner(bounds, anchor, (local, limit, _) =>
                maxRatio = Mathf.Max(maxRatio, Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y)) / limit));
            return maxRatio;
        }

        private delegate void CornerAssertion(Vector3 cameraLocal, float frustumHalfWidth, string label);

        private static void ForEachSpunCorner(Bounds bounds, Vector3 anchor, CornerAssertion assert)
        {
            var (focus, distance) = HangarPreviewStage.SolveFraming(bounds, anchor, FovDeg);
            var cameraPosition = focus + CameraDirection * distance;
            var worldToCamera = Quaternion.Inverse(Quaternion.LookRotation(focus - cameraPosition));
            var tanHalfFov = Mathf.Tan(FovDeg * 0.5f * Mathf.Deg2Rad);

            for (var angle = 0f; angle < 360f; angle += 5f)
            {
                var spin = Quaternion.AngleAxis(angle, Vector3.up);
                for (var i = 0; i < 8; i++)
                {
                    var sign = new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1);
                    var corner = bounds.center + Vector3.Scale(bounds.extents, sign);
                    var spun = anchor + spin * (corner - anchor);
                    var local = worldToCamera * (spun - cameraPosition);
                    assert(local, local.z * tanHalfFov, $"corner {i} at spin {angle:F0}°");
                }
            }
        }
    }
}
