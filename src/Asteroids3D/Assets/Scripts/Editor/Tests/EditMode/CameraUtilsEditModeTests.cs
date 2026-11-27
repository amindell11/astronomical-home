// Renamed from CameraUtilsEditMode.cs to follow *Tests naming convention.
using Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Camera")]
    public class CameraUtilsEditModeTests
    {
        private const float LeadDistance = 6f;
        private const float DeadZone = .2f;
        private const float FitZoom = 10f;
        private const float FitPadding = 2f;

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

        [Test]
        public void ShiftToFitBounds_BoundsInsideTheView_LeavesTheCenter()
        {
            var center = new Vector2(1f, 2f);
            var shifted = CameraUtils.ShiftToFitBounds(center, new Vector2(-3f, -4f), new Vector2(5f, 6f), FitZoom, 1f, FitPadding);
            Assert.That(shifted, Is.EqualTo(center));
        }

        [Test]
        public void ShiftToFitBounds_BoundsPokingOutAbove_ShiftsUpJustEnough()
        {
            var max = new Vector2(1f, 12f);
            var shifted = CameraUtils.ShiftToFitBounds(Vector2.zero, new Vector2(-1f, -2f), max, FitZoom, 1f, FitPadding);
            Assert.That(shifted.y + FitZoom, Is.EqualTo(max.y + FitPadding).Within(1e-4f));
            Assert.That(shifted.x, Is.EqualTo(0f));
        }

        [Test]
        public void ShiftToFitBounds_BoundsPokingOutLeft_ReachesAsFarAsTheAspectWidens()
        {
            const float aspect = 16f / 9f;
            var min = new Vector2(-20f, -1f);
            var shifted = CameraUtils.ShiftToFitBounds(Vector2.zero, min, new Vector2(0f, 1f), FitZoom, aspect, FitPadding);
            Assert.That(shifted.x, Is.EqualTo(min.x + FitZoom * aspect - FitPadding).Within(1e-4f));
            Assert.That(shifted.y, Is.EqualTo(0f));
        }

        [Test]
        public void ShiftToFitBounds_BoundsTallerThanTheView_CentersOnTheirVerticalMidpoint()
        {
            var min = new Vector2(-1f, -20f);
            var max = new Vector2(1f, 30f);
            var shifted = CameraUtils.ShiftToFitBounds(new Vector2(10f, 0f), min, max, FitZoom, 1f, FitPadding);
            Assert.That(shifted.y, Is.EqualTo((min.y + max.y) / 2f).Within(1e-4f));
            Assert.That(shifted.x, Is.EqualTo(min.x + FitZoom - FitPadding).Within(1e-4f));
        }

        [Test]
        public void SmoothZoom_TargetDropsBelowWhileWidening_NeverMovesAwayFromIt()
        {
            const float widenTime = .4f, narrowTime = 1.2f, deltaTime = .02f, settled = 18.7f;
            var current = 15f;
            var velocity = 0f;
            for (var step = 0; step < 20; step++)
                current = CameraUtils.SmoothZoom(current, 22f, ref velocity, widenTime, narrowTime, deltaTime);

            var targetBelow = false;
            for (var step = 1; step <= 300; step++)
            {
                var target = Mathf.Max(settled, 22f - .1f * step);
                targetBelow |= target < current;
                var previous = current;
                current = CameraUtils.SmoothZoom(current, target, ref velocity, widenTime, narrowTime, deltaTime);
                if (targetBelow)
                    Assert.That(current, Is.LessThanOrEqualTo(previous), $"the zoom widened past its target at step {step}");
            }

            Assert.That(current, Is.EqualTo(settled).Within(0.01f));
        }

        [Test]
        public void CursorLead_AtTheCenter_IsZero() =>
            AssertLead(Vector2.zero, CameraUtils.CursorLead(new Vector2(.5f, .5f), DeadZone, LeadDistance));

        [Test]
        public void CursorLead_InsideTheDeadZone_IsZero() =>
            AssertLead(Vector2.zero, CameraUtils.CursorLead(new Vector2(.55f, .45f), DeadZone, LeadDistance));

        [Test]
        public void CursorLead_AtAScreenEdge_LeadsTheFullDistanceAlongThatAxis()
        {
            AssertLead(new Vector2(LeadDistance, 0f), CameraUtils.CursorLead(new Vector2(1f, .5f), DeadZone, LeadDistance));
            AssertLead(new Vector2(0f, LeadDistance), CameraUtils.CursorLead(new Vector2(.5f, 1f), DeadZone, LeadDistance));
        }

        [Test]
        public void CursorLead_HalfwayThroughTheRamp_LeadsHalfTheDistance()
        {
            var halfway = (1f + (DeadZone + 1f) / 2f) / 2f;
            AssertLead(new Vector2(LeadDistance / 2f, 0f), CameraUtils.CursorLead(new Vector2(halfway, .5f), DeadZone, LeadDistance));
        }

        [Test]
        public void CursorLead_AtACorner_ClampsToTheFullDistanceDiagonally()
        {
            var diagonal = Vector2.one.normalized * LeadDistance;
            AssertLead(diagonal, CameraUtils.CursorLead(Vector2.one, DeadZone, LeadDistance));
        }

        [Test]
        public void CursorLead_OutsideTheView_IsZero()
        {
            AssertLead(Vector2.zero, CameraUtils.CursorLead(new Vector2(1.1f, .5f), DeadZone, LeadDistance));
            AssertLead(Vector2.zero, CameraUtils.CursorLead(new Vector2(-.1f, .5f), DeadZone, LeadDistance));
        }

        [Test]
        public void CursorLead_WithNoDeadZone_RampsLinearlyFromTheCenter()
        {
            AssertLead(new Vector2(LeadDistance * .2f, 0f), CameraUtils.CursorLead(new Vector2(.6f, .5f), 0f, LeadDistance));
            AssertLead(new Vector2(0f, -LeadDistance / 2f), CameraUtils.CursorLead(new Vector2(.5f, .25f), 0f, LeadDistance));
        }

        private static void AssertLead(Vector2 expected, Vector2 actual) =>
            Assert.That(Vector2.Distance(expected, actual), Is.LessThan(1e-4f), $"lead {actual}, expected {expected}");

        [Test]
        public void ShakeNoise_StaysInRange_AndRepeatsForTheSameInputs()
        {
            const float frequency = 10f;
            for (var channel = 0; channel < 3; channel++)
            {
                for (var step = 0; step <= 40; step++)
                {
                    var time = step * .25f;
                    var sample = CameraUtils.ShakeNoise(channel, time, frequency);
                    Assert.That(sample, Is.InRange(-1f, 1f));
                    Assert.That(CameraUtils.ShakeNoise(channel, time, frequency), Is.EqualTo(sample));
                }
            }
        }

        [Test]
        public void ShakeNoise_ChannelsDifferAtTheSameTime()
        {
            const float frequency = 10f;
            var differed = false;
            for (var step = 0; step <= 40; step++)
            {
                var time = step * .25f;
                var first = CameraUtils.ShakeNoise(0, time, frequency);
                var second = CameraUtils.ShakeNoise(1, time, frequency);
                var third = CameraUtils.ShakeNoise(2, time, frequency);
                differed |= first != second || second != third;
            }
            Assert.That(differed, Is.True);
        }

        [Test]
        public void ShakenView_ZeroOffsetAndRoll_ReturnsTheInput()
        {
            var view = Matrix4x4.TRS(new Vector3(3f, -2f, 5f), Quaternion.Euler(10f, 20f, 30f), Vector3.one);
            var shaken = CameraUtils.ShakenView(view, Vector2.zero, 0f);
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 4; column++)
                    Assert.That(shaken[row, column], Is.EqualTo(view[row, column]).Within(1e-5f));
        }

        [Test]
        public void ShakenView_Offset_MapsAPointByThatShift()
        {
            var point = new Vector3(4f, 7f, -2f);
            var mapped = CameraUtils.ShakenView(Matrix4x4.identity, new Vector2(1f, 2f), 0f).MultiplyPoint(point);
            Assert.That(Vector3.Distance(mapped, point - new Vector3(1f, 2f, 0f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void BlastStrength_InsideTheInnerRadius_IsFull()
        {
            Assert.That(CameraUtils.BlastStrength(0f, 4f, 20f, 0f, 0f), Is.EqualTo(1f));
            Assert.That(CameraUtils.BlastStrength(4f, 4f, 20f, 3f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void BlastStrength_AtOrBeyondTheOuterRadius_IsZero()
        {
            Assert.That(CameraUtils.BlastStrength(20f, 4f, 20f, 0f, 0f), Is.EqualTo(0f));
            Assert.That(CameraUtils.BlastStrength(21f, 4f, 20f, 0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void BlastStrength_HalfwayBetweenTheRadii_IsHalf() =>
            Assert.That(CameraUtils.BlastStrength(12f, 4f, 20f, 0f, 0f), Is.EqualTo(.5f).Within(1e-4f));

        [Test]
        public void BlastStrength_HalfOfFullSize_HalvesStrengthAndBothRadii()
        {
            const float inner = 4f, outer = 20f;
            Assert.That(CameraUtils.BlastStrength(0f, inner, outer, 5f, 10f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(CameraUtils.BlastStrength(inner * .5f, inner, outer, 5f, 10f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(CameraUtils.BlastStrength(outer * .5f, inner, outer, 5f, 10f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(CameraUtils.BlastStrength((inner + outer) * .25f, inner, outer, 5f, 10f), Is.EqualTo(.25f).Within(1e-4f));
        }

        [Test]
        public void BlastStrength_ZeroFullSize_IgnoresSize()
        {
            Assert.That(CameraUtils.BlastStrength(4f, 4f, 20f, 100f, 0f), Is.EqualTo(1f));
            Assert.That(CameraUtils.BlastStrength(12f, 4f, 20f, 100f, 0f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(CameraUtils.BlastStrength(20f, 4f, 20f, 100f, 0f), Is.EqualTo(0f));
        }
    }
}
