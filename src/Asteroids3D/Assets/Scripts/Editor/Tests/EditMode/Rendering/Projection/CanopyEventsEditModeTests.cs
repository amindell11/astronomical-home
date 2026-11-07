using System.Collections.Generic;
using NUnit.Framework;
using UI.Projection;

namespace Tests.EditMode.Rendering.Projection
{
    [Category("UI")]
    public sealed class CanopyEventsEditModeTests
    {
        [Test]
        public void LowHullSchedule_RepeatsTheReferenceOffsetsWithoutAnAlarmPeriod()
        {
            var offsets = new[] { 0f, 1.23f, 3.78f, 7.1f, 8.64f, 13.27f, 17.9f };
            foreach (var offset in offsets)
            {
                Assert.That(CanopyPersonality.LowHullHitAge(offset + .01f), Is.EqualTo(.01f).Within(.0001f));
                Assert.That(CanopyPersonality.LowHullHitAge(19f + offset + .01f), Is.EqualTo(.01f).Within(.0001f));
            }
            Assert.That(CanopyPersonality.LowHullHitAge(2.5f), Is.GreaterThan(.7f));
            Assert.That(CanopyPersonality.LowHullHitAge(18.99f), Is.GreaterThan(.7f));
        }

        [Test]
        public void Appearance_HasTwoBoundedDips_AndRapidVisibilityEdgesCannotExceedThreePerSecond()
        {
            var envelope = new AppearanceEnvelope();
            Assert.That(envelope.Sample(0f, true, 1f), Is.EqualTo(1f));
            foreach (var age in new[] { .1f, .18f, .4f, .48f })
                Assert.That(envelope.Sample(age, true, 1f), Is.EqualTo(.83f).Within(.0001f));
            Assert.That(envelope.Sample(.5f, true, 1f), Is.EqualTo(1f));
            envelope.Sample(1f, false, 1f);
            envelope.Sample(1.1f, true, 1f);
            Assert.That(envelope.Sample(1.2f, true, .5f), Is.EqualTo(.915f).Within(.0001f));

            foreach (var period in new[] { .04f, .15f, .3f, .47f, .6f, .8f, 1.01f })
            {
                envelope = new AppearanceEnvelope();
                var dips = new Queue<float>();
                var dipping = false;
                for (var tick = 0; tick < 10000; tick++)
                {
                    var now = tick * .001f;
                    var visible = now % period < period * .8f;
                    var alpha = envelope.Sample(now, visible, 1f);
                    Assert.That(alpha, Is.InRange(.83f, 1f));
                    var nextDipping = alpha < 1f;
                    if (nextDipping && !dipping) dips.Enqueue(now);
                    dipping = nextDipping;
                    while (dips.Count > 0 && now - dips.Peek() >= 1f) dips.Dequeue();
                    Assert.That(dips.Count, Is.LessThanOrEqualTo(3), $"Visibility period {period}, time {now}");
                }
            }
        }
    }
}
