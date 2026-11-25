using System.Reflection;
using Game.Travel;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Travel
{
    [Category("Sectors")]
    public sealed class SectorJumpSequenceEditModeTests
    {
        private SectorJumpProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SectorJumpProfile>(
                "Assets/Settings/Game/SectorJumpTuning.asset"));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void Endpoints_RestoreHudAndStopAtDestination()
        {
            var sequence = new SectorJumpSequence(profile);
            var start = sequence.StateAt(-1f, -135f);
            Assert.That(start.Seconds, Is.Zero);
            Assert.That(start.TravelFraction, Is.Zero);
            Assert.That(start.Speed, Is.Zero);
            Assert.That(start.ShipBlurStrength, Is.Zero);
            Assert.That(start.ShipBlurLength, Is.Zero);
            Assert.That(start.HeadingDegrees, Is.Zero);
            Assert.That(start.LocaleBlend, Is.Zero);
            Assert.That(start.HudAlpha, Is.EqualTo(1f));

            var arrivalTime = profile.turnDuration + profile.chargeDuration + profile.travelDuration;
            var arrival = sequence.StateAt(arrivalTime, -135f);
            var end = sequence.StateAt(sequence.Duration + 1f, -135f);
            Assert.That(arrival.Speed, Is.Zero);
            Assert.That(arrival.HudAlpha, Is.Zero);
            Assert.That(end.TravelFraction, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(end.Distance, Is.EqualTo(arrival.Distance));
            Assert.That(end.Speed, Is.Zero);
            Assert.That(end.ShipBlurStrength, Is.Zero);
            Assert.That(end.ShipBlurLength, Is.Zero);
            Assert.That(end.HeadingDegrees, Is.EqualTo(-135f));
            Assert.That(end.JumpDirection.x, Is.EqualTo(Mathf.Sqrt(0.5f)).Within(1e-6f));
            Assert.That(end.JumpDirection.y, Is.EqualTo(-Mathf.Sqrt(0.5f)).Within(1e-6f));
            Assert.That(end.LocaleBlend, Is.EqualTo(1f));
            Assert.That(end.HudAlpha, Is.EqualTo(1f));
        }

        [Test]
        public void BlendWindow_MatchesTheEaseInOutCurveOverTheSameWindow()
        {
            profile.localeBlendStart = 0.2f;
            profile.localeBlendEnd = 0.85f;
            var sequence = new SectorJumpSequence(profile);
            var replaced = AnimationCurve.EaseInOut(0.2f, 0f, 0.85f, 1f);
            for (var sample = 0; sample <= 200; sample++)
            {
                var progress = sample / 200f;
                Assert.That(sequence.LocaleBlendAt(progress),
                    Is.EqualTo(Mathf.Clamp01(replaced.Evaluate(progress))).Within(1e-5f), $"travel progress {progress}");
            }
        }

        [Test]
        public void EveryAuthorableWindow_FinishesTheBlendByFullTravel()
        {
            foreach (var knob in new[] { nameof(SectorJumpProfile.localeBlendStart), nameof(SectorJumpProfile.localeBlendEnd) })
            {
                var range = typeof(SectorJumpProfile).GetField(knob).GetCustomAttribute<UnityEngine.RangeAttribute>();
                Assert.That(range.min, Is.Zero, knob);
                Assert.That(range.max, Is.EqualTo(1f), knob);
            }
            for (var start = 0; start <= 20; start++)
            for (var end = 0; end <= 20; end++)
            {
                profile.localeBlendStart = start / 20f;
                profile.localeBlendEnd = end / 20f;
                var sequence = new SectorJumpSequence(profile);
                var window = $"window ({profile.localeBlendStart}, {profile.localeBlendEnd})";
                Assert.That(sequence.LocaleBlendAt(profile.localeBlendEnd), Is.EqualTo(1f), window);
                Assert.That(sequence.StateAt(sequence.Duration, 0f).LocaleBlend, Is.EqualTo(1f), window);
            }
        }

        [Test]
        public void HudFadeLongerThanSettle_FinishesBeforeTheSequenceEnds()
        {
            profile.settleDuration = 0.1f;
            profile.hudFadeIn = 1.5f;
            var sequence = new SectorJumpSequence(profile);
            var arrival = profile.turnDuration + profile.chargeDuration + profile.travelDuration;
            Assert.That(profile.Duration, Is.EqualTo(arrival + profile.hudFadeIn));
            Assert.That(sequence.Duration, Is.EqualTo(profile.Duration));
            Assert.That(sequence.StateAt(arrival + profile.settleDuration, 0f).HudAlpha, Is.LessThan(1f));
            Assert.That(sequence.StateAt(sequence.Duration, 0f).HudAlpha, Is.EqualTo(1f));
        }

        [Test]
        public void Scrubbing_RevisitsTheSameStateRegardlessOfQueryOrder()
        {
            var sequence = new SectorJumpSequence(profile);
            var expected = sequence.StateAt(3.123f, 67f);
            sequence.StateAt(sequence.Duration, -45f);
            sequence.StateAt(0f, 160f);
            sequence.StateAt(1.9f, 67f);
            Assert.That(sequence.StateAt(3.123f, 67f), Is.EqualTo(expected));
            Assert.That(new SectorJumpSequence(profile).StateAt(3.123f, 67f), Is.EqualTo(expected));
        }

        [Test]
        public void SavedSpeedOvershoot_DrivesBothBlurStrengthAndLength()
        {
            var sequence = new SectorJumpSequence(profile);
            var departure = profile.turnDuration + profile.chargeDuration;
            var cruise = sequence.StateAt(departure + profile.travelDuration * 0.4f, 0f);
            var slowing = sequence.StateAt(departure + profile.travelDuration * 0.9f, 0f);
            Assert.That(cruise.Speed, Is.GreaterThan(1f));
            Assert.That(slowing.Speed, Is.LessThan(cruise.Speed));
            foreach (var state in new[] { cruise, slowing })
            {
                Assert.That(state.ShipBlurStrength,
                    Is.EqualTo(state.Speed * profile.shipBlurStrength).Within(1e-6f));
                Assert.That(state.ShipBlurLength,
                    Is.EqualTo(state.Speed * profile.shipBlurLength).Within(1e-6f));
            }
            Assert.That(slowing.ShipBlurStrength, Is.LessThan(cruise.ShipBlurStrength));
            Assert.That(slowing.ShipBlurLength, Is.LessThan(cruise.ShipBlurLength));
        }

        [Test]
        public void LinearSpeed_DistanceMatchesItsAnalyticIntegral()
        {
            profile.speed = AnimationCurve.Linear(0f, 0f, 1f, 2f);
            var sequence = new SectorJumpSequence(profile);
            var departure = profile.turnDuration + profile.chargeDuration;
            foreach (var progress in new[] { 0.05f, 0.37f, 0.8f, 1f })
            {
                var state = sequence.StateAt(departure + progress * profile.travelDuration, 0f);
                Assert.That(state.Distance,
                    Is.EqualTo(progress * progress * profile.travelDuration).Within(2e-5f));
                Assert.That(state.TravelFraction, Is.EqualTo(progress * progress).Within(2e-5f));
            }
        }

        [Test]
        public void SavedCurve_DistanceDerivativeMatchesTheReportedSpeed()
        {
            var sequence = new SectorJumpSequence(profile);
            var departure = profile.turnDuration + profile.chargeDuration;
            const float step = 0.002f;
            foreach (var progress in new[] { 0.04f, 0.25f, 0.6f, 0.92f })
            {
                var time = departure + progress * profile.travelDuration;
                var rate = (sequence.StateAt(time + step, 0f).Distance -
                    sequence.StateAt(time - step, 0f).Distance) / (2f * step);
                Assert.That(rate, Is.EqualTo(sequence.StateAt(time, 0f).Speed).Within(0.001f));
            }
        }
    }
}
