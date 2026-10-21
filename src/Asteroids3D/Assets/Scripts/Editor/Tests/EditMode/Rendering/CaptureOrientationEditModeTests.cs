#if UNITY_EDITOR
using Capture;
using NUnit.Framework;
using Substrate;
using UnityEngine;

namespace Tests.EditMode.Rendering
{
    [Category("Camera")]
    public sealed class CaptureOrientationEditModeTests
    {
        [Test]
        public void Framing_ViewsShipTopWithUnmirroredPlaneDirections()
        {
            var root = new GameObject("Capture orientation", typeof(Camera));
            try
            {
                var camera = root.GetComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 16f / 9;
                CaptureFraming.Apply(camera, new CaptureConfig(), new[] { Vector2.zero });
                var center = camera.WorldToViewportPoint(GamePlane.Origin);
                var right = camera.WorldToViewportPoint(GamePlane.Origin + GamePlane.Right);
                var forward = camera.WorldToViewportPoint(GamePlane.Origin + GamePlane.Forward);
                Assert.That(Vector3.Dot(camera.transform.position - GamePlane.Origin, GamePlane.Normal), Is.LessThan(0),
                    "The ship's top faces the negative-normal side of the game plane.");
                Assert.That(right.x, Is.GreaterThan(center.x), "Plane-right must appear screen-right.");
                Assert.That(forward.y, Is.GreaterThan(center.y), "Plane-forward must appear screen-up.");
                Assert.That(center.z, Is.GreaterThan(0), "The game plane must be in front of the camera.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
