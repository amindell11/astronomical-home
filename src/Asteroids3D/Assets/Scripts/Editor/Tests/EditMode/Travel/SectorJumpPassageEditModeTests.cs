using Game.Travel;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Travel
{
    [Category("Sectors")]
    public sealed class SectorJumpPassageEditModeTests
    {
        private SectorJumpProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<SectorJumpProfile>();
            profile.turnDuration = 0.5f;
            profile.chargeDuration = 0.25f;
            profile.travelDuration = 2f;
            profile.settleDuration = 0.1f;
            profile.hudFadeIn = 0.4f;
            profile.speed = new AnimationCurve(new Keyframe(0f, 0f, 4f, 4f),
                new Keyframe(0.5f, 2f, 4f, -4f), new Keyframe(1f, 0f, -4f, -4f));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void HoldEdges_KeepSpeedAndDistanceContinuous()
        {
            var passage = new SectorJumpPassage(profile, 40f, 90f);
            const float hold = 3f;
            const float step = 0.001f;
            foreach (var edge in new[] { passage.HoldStartsAt, passage.HoldStartsAt + hold })
            {
                var before = passage.StateAt(edge - step, hold);
                var at = passage.StateAt(edge, hold);
                var after = passage.StateAt(edge + step, hold);
                Assert.That(before.Speed, Is.EqualTo(at.Speed).Within(0.003f));
                Assert.That(after.Speed, Is.EqualTo(at.Speed).Within(0.011f));
                Assert.That(at.Distance - before.Distance, Is.EqualTo(at.Speed * step).Within(1e-5f));
                Assert.That(after.Distance - at.Distance, Is.EqualTo(at.Speed * step).Within(1e-5f));
                Assert.That(at.TravelFraction, Is.GreaterThan(before.TravelFraction));
                Assert.That(after.TravelFraction, Is.GreaterThan(at.TravelFraction));
            }
        }

        [Test]
        public void GrowingHold_ContinuesMovingAndBlendsIntoTheDestination()
        {
            var passage = new SectorJumpPassage(profile, 0f, -135f);
            var entry = passage.StateAt(passage.HoldStartsAt, 0f);
            foreach (var held in new[] { 1f, 5f, 45f, 300f })
            {
                var state = passage.StateAt(passage.HoldStartsAt + held, held);
                Assert.That(state.TravelProgress, Is.EqualTo(0.5f));
                Assert.That(state.Speed, Is.EqualTo(entry.Speed));
                Assert.That(state.Distance, Is.EqualTo(entry.Distance + entry.Speed * held).Within(1e-4f));
                Assert.That(state.TravelFraction, Is.GreaterThan(entry.TravelFraction));
                Assert.That(state.LocaleBlend, Is.EqualTo(1f));
                Assert.That(state.HudAlpha, Is.Zero);
                Assert.That(state.ShipBlurStrength, Is.EqualTo(entry.ShipBlurStrength));
                Assert.That(state.ShipBlurLength, Is.EqualTo(entry.ShipBlurLength));
            }
        }

        [Test]
        public void Continue_GoesDirectlyIntoTheExitWithContinuousSpeed()
        {
            var passage = new SectorJumpPassage(profile, 0f, 90f);
            const float hold = 4f;
            var resume = passage.HoldStartsAt + hold;
            var waiting = passage.StateAt(resume, hold);
            var resumed = passage.StateAt(resume + profile.worldBlendDuration * 0.5f, hold);
            var end = passage.StateAt(passage.DurationWithHold(hold), hold);
            Assert.That(waiting.LocaleBlend, Is.EqualTo(1f));
            Assert.That(resumed.TravelProgress, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(resumed.LocaleBlend, Is.EqualTo(1f));
            Assert.That(resumed.Speed, Is.LessThan(waiting.Speed));
            Assert.That(end.Distance, Is.GreaterThan(resumed.Distance));
            Assert.That(end.LocaleBlend, Is.EqualTo(1f));
            Assert.That(end.Speed, Is.Zero);
            Assert.That(end.HudAlpha, Is.EqualTo(1f).Within(1e-6f));
        }

        [TestCase(350f, 10f, 360f)]
        [TestCase(10f, 350f, 0f)]
        public void Turn_TakesTheShortestPathFromTheActualHeading(float start, float target, float halfway)
        {
            var passage = new SectorJumpPassage(profile, start, target);
            Assert.That(passage.StateAt(0f, 0f).HeadingDegrees, Is.EqualTo(start));
            Assert.That(passage.StateAt(profile.turnDuration * 0.5f, 0f).HeadingDegrees,
                Is.EqualTo(halfway).Within(1e-5f));
            var turned = passage.StateAt(profile.turnDuration, 0f);
            Assert.That(Mathf.DeltaAngle(turned.HeadingDegrees, target), Is.Zero.Within(1e-5f));
            var direction = target * Mathf.Deg2Rad;
            Assert.That(turned.JumpDirection.x, Is.EqualTo(-Mathf.Sin(direction)).Within(1e-6f));
            Assert.That(turned.JumpDirection.y, Is.EqualTo(Mathf.Cos(direction)).Within(1e-6f));
        }

        [Test]
        public void LongHudFade_ExtendsBothFiniteAndHeldDurationsToFullOpacity()
        {
            profile.hudFadeIn = 1.5f;
            var sequence = new SectorJumpSequence(profile);
            var passage = new SectorJumpPassage(profile, 0f, 90f);
            var arrival = profile.turnDuration + profile.chargeDuration + profile.travelDuration;
            const float hold = 5f;
            Assert.That(profile.Duration, Is.EqualTo(arrival + profile.hudFadeIn));
            Assert.That(sequence.Duration, Is.EqualTo(profile.Duration));
            Assert.That(passage.DurationWithHold(hold),
                Is.EqualTo(passage.HoldStartsAt + hold + profile.worldBlendDuration + profile.hudFadeIn));
            Assert.That(sequence.StateAt(arrival + profile.settleDuration, 90f).HudAlpha, Is.LessThan(1f));
            Assert.That(sequence.StateAt(sequence.Duration, 90f).HudAlpha, Is.EqualTo(1f));
            Assert.That(passage.StateAt(passage.DurationWithHold(hold), hold).HudAlpha, Is.EqualTo(1f));
        }
    }
}
