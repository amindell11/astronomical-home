#if UNITY_EDITOR
using AI.Tactics;
using NUnit.Framework;
using UnityEngine;
using Utils;

namespace Tests.EditMode.Tactics
{
    [Category("AI")]
    public class WoundedDrawEditModeTests
    {
        private TacticsSettings settings;

        [SetUp]
        public void SetUp() => settings = ScriptableObject.CreateInstance<TacticsSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [Test]
        public void EveryValue_LandsInsideItsRange()
        {
            settings.woundedBelow = new Spread(10f, 40f);
            settings.shieldBack = new Spread(50f, 90f);
            settings.woundedSeconds = new Spread(1f, 8f);
            settings.retreatsMin = 2;
            settings.retreatsMax = 6;

            for (var seed = 1; seed <= 12; seed++)
            {
                var draw = WoundedDraw.From(settings, new SeedScope(seed));
                Assert.That(draw.below, Is.InRange(10f, 40f));
                Assert.That(draw.shieldBack, Is.InRange(50f, 90f));
                Assert.That(draw.seconds, Is.InRange(1f, 8f));
                Assert.That(draw.retreats, Is.InRange(2, 6));
            }
        }

        [Test]
        public void SameSeed_SameDraw()
        {
            settings.woundedBelow = new Spread(10f, 90f);
            settings.shieldBack = new Spread(40f, 95f);
            settings.woundedSeconds = new Spread(1f, 12f);
            settings.retreatsMin = 1;
            settings.retreatsMax = 9;

            var a = WoundedDraw.From(settings, new SeedScope(4));
            var b = WoundedDraw.From(settings, new SeedScope(4));

            Assert.AreEqual(a.below, b.below);
            Assert.AreEqual(a.shieldBack, b.shieldBack);
            Assert.AreEqual(a.seconds, b.seconds);
            Assert.AreEqual(a.retreats, b.retreats);
        }

        [Test]
        public void TwoSeeds_DifferOnAWideRange()
        {
            settings.woundedBelow = new Spread(0f, 100f);
            settings.shieldBack = new Spread(0f, 100f);
            settings.woundedSeconds = new Spread(0f, 30f);
            settings.retreatsMin = 1;
            settings.retreatsMax = 1000;

            var a = WoundedDraw.From(settings, new SeedScope(1));
            var b = WoundedDraw.From(settings, new SeedScope(2));

            Assert.IsTrue(a.below != b.below || a.shieldBack != b.shieldBack || a.seconds != b.seconds
                || a.retreats != b.retreats);
        }

        [Test]
        public void MinEqualsMax_GivesExactlyThatValue()
        {
            settings.woundedBelow = new Spread(25f, 25f);
            settings.shieldBack = new Spread(80f, 80f);
            settings.woundedSeconds = new Spread(5f, 5f);
            settings.retreatsMin = 3;
            settings.retreatsMax = 3;

            var draw = WoundedDraw.From(settings, new SeedScope(99));

            Assert.AreEqual(25f, draw.below);
            Assert.AreEqual(80f, draw.shieldBack);
            Assert.AreEqual(5f, draw.seconds);
            Assert.AreEqual(3, draw.retreats);
        }

        [Test]
        public void RetreatCount_IsInclusiveOfBothEnds()
        {
            settings.woundedBelow = new Spread(0f, 0f);
            settings.shieldBack = new Spread(0f, 0f);
            settings.woundedSeconds = new Spread(0f, 0f);
            settings.retreatsMin = 2;
            settings.retreatsMax = 3;

            var seenLow = false;
            var seenHigh = false;
            for (var seed = 1; seed <= 80; seed++)
            {
                var retreats = WoundedDraw.From(settings, new SeedScope(seed)).retreats;
                Assert.That(retreats, Is.InRange(2, 3));
                seenLow |= retreats == 2;
                seenHigh |= retreats == 3;
            }

            Assert.IsTrue(seenLow, "the draw includes the min");
            Assert.IsTrue(seenHigh, "the draw includes the max");
        }
    }
}
#endif
