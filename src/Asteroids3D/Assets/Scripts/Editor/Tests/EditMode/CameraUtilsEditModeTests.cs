// Renamed from CameraUtilsEditMode.cs to follow *Tests naming convention.
using Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Camera")]
    public class CameraUtilsEditModeTests
    {
        [Test]
        [Category("Smoke")]
        public void ZoomToFitBounds_WideBounds_RespectsAspectRatio()
        {
            var center = new Vector2(0, 0);
            var min = new Vector2(-20, -5);
            var max = new Vector2(20, 5);
            
            var zoom = CameraUtils.ZoomToFitBounds(center, min, max, padding: 2f, aspect: 16f/9f);
            
            // With 16:9 aspect, horizontal extent (22) / aspect ≈ 12.4
            // Vertical extent is 7, so horizontal wins
            Assert.That(zoom, Is.EqualTo(22f / (16f/9f)).Within(0.01f));
        }

        [Test]
        public void ShiftToKeepPointInView_TargetOutsideBounds_ShiftsCenter()
        {
            var center = Vector2.zero;
            var targetOffset = new Vector2(15, 0); // target is 15 units right of center
            
            var shifted = CameraUtils.ShiftToKeepPointInView(
                center, targetOffset, zoomSize: 10f, aspect: 1f, padding: 2f);
            
            // View extent is 10, with padding 2, so max allowed offset is 8
            // Target at 15 requires shift of 7
            Assert.That(shifted.x, Is.EqualTo(7f).Within(0.01f));
        }
    }
}
