using Cameras;
using Cameras.Shake;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Cameras
{
    [Category("Camera")]
    public class ShakeLayersEditModeTests
    {
        private const float HalfHeight = 10f;
        private const float Kick = .1f;

        private ShakeLayers layers;
        private CameraShakeProfile profile;

        [SetUp]
        public void SetUp()
        {
            layers = new ShakeLayers();
            profile = ScriptableObject.CreateInstance<CameraShakeProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            if (profile) Object.DestroyImmediate(profile);
        }

        [Test]
        public void Empty_SamplesZero()
        {
            layers.Sample(1f, HalfHeight, out var offset, out var roll);
            Assert.AreEqual(Vector2.zero, offset);
            Assert.AreEqual(0f, roll);
            Assert.AreEqual(0, layers.Count);
        }

        [Test]
        public void Jitter_StaysWithinItsReach_AndDropsWhenTheEnvelopeEnds()
        {
            const float jitter = .05f;
            profile.envelope = AnimationCurve.EaseInOut(0f, 1f, .3f, 0f);
            profile.sustainAt = -1f;
            profile.frequency = 12f;
            profile.jitter = jitter;
            profile.roll = 0f;
            profile.kick = 0f;
            layers.Start(profile, profile, Vector2.up, 0f);

            for (var step = 0; step <= 6; step++)
            {
                layers.Sample(step / 6f * profile.Length, HalfHeight, out var offset, out var roll);
                Assert.LessOrEqual(Mathf.Abs(offset.x), jitter * HalfHeight + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(offset.y), jitter * HalfHeight + 1e-4f);
                Assert.AreEqual(0f, roll);
                Assert.AreEqual(1, layers.Count);
            }

            layers.Sample(profile.Length + .01f, HalfHeight, out var done, out var doneRoll);
            Assert.AreEqual(Vector2.zero, done);
            Assert.AreEqual(0f, doneRoll);
            Assert.AreEqual(0, layers.Count);
        }

        [Test]
        public void Kick_MovesAlongTheGivenDirection()
        {
            const float kick = .1f;
            var direction = new Vector2(3f, 4f);
            profile.envelope = AnimationCurve.Constant(0f, .4f, 1f);
            profile.sustainAt = -1f;
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = kick;
            layers.Start(profile, profile, direction, 0f);

            layers.Sample(.1f, HalfHeight, out var offset, out var roll);
            var expected = direction.normalized * (kick * HalfHeight);
            Assert.That(Vector2.Distance(offset, expected), Is.LessThan(1e-4f));
            Assert.AreEqual(0f, roll);
        }

        [Test]
        public void Sustain_HoldsUntilRelease_ThenPlaysOut()
        {
            const float kick = .1f;
            const float sustainAt = .25f;
            const float releaseAt = .6f;
            profile.envelope = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            profile.sustainAt = sustainAt;
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = kick;
            layers.Start(profile, profile, Vector2.up, 0f);

            layers.Sample(sustainAt, HalfHeight, out var atSustain, out _);
            layers.Sample(releaseAt, HalfHeight, out var held, out _);
            var heldReach = Mathf.Clamp01(profile.envelope.Evaluate(sustainAt)) * kick * HalfHeight;
            Assert.AreEqual(heldReach, atSustain.y, 1e-4f);
            Assert.AreEqual(heldReach, held.y, 1e-4f, "the envelope holds at the sustain point");
            Assert.AreEqual(0f, held.x, 1e-4f);
            Assert.AreEqual(1, layers.Count);

            layers.Release(profile, releaseAt);
            var later = 1f;
            var playedTime = sustainAt + (later - releaseAt);
            layers.Sample(later, HalfHeight, out var played, out _);
            var playedReach = Mathf.Clamp01(profile.envelope.Evaluate(playedTime)) * kick * HalfHeight;
            Assert.AreEqual(playedReach, played.y, 1e-4f, "release plays the envelope on from the sustain point");
            Assert.AreEqual(1, layers.Count);

            layers.Sample(releaseAt + (profile.Length - sustainAt) + .01f, HalfHeight, out var done, out var doneRoll);
            Assert.AreEqual(Vector2.zero, done);
            Assert.AreEqual(0f, doneRoll);
            Assert.AreEqual(0, layers.Count);
        }

        [Test]
        public void RestartingASource_KeepsOneLayer()
        {
            profile.envelope = AnimationCurve.Constant(0f, .2f, 1f);
            profile.sustainAt = -1f;
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = .1f;
            layers.Start(profile, profile, Vector2.up, 0f);
            layers.Start(profile, profile, Vector2.right, 5f);

            layers.Sample(5f, HalfHeight, out var offset, out _);
            Assert.AreEqual(1, layers.Count);
            Assert.AreEqual(1f, offset.x, 1e-4f);
            Assert.AreEqual(0f, offset.y, 1e-4f);
        }

        [Test]
        public void Scale_HalvesAKickOnlyLayer()
        {
            const float kick = .1f;
            profile.envelope = AnimationCurve.Constant(0f, .4f, 1f);
            profile.sustainAt = -1f;
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = kick;
            layers.Start(profile, profile, Vector2.up, 0f);

            layers.Sample(.1f, HalfHeight, out var full, out _);
            layers.Scale(profile, .5f);
            layers.Sample(.1f, HalfHeight, out var halved, out _);
            Assert.AreEqual(full.y * .5f, halved.y, 1e-4f);
            Assert.AreEqual(0f, halved.x, 1e-4f);
        }

        [Test]
        public void Scale_ZeroesTheOffset_WhileTheLayerStays()
        {
            profile.envelope = AnimationCurve.Constant(0f, .4f, 1f);
            profile.sustainAt = -1f;
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = .1f;
            layers.Start(profile, profile, Vector2.up, 0f);

            layers.Scale(profile, 0f);
            layers.Sample(.1f, HalfHeight, out var offset, out _);
            Assert.AreEqual(Vector2.zero, offset);
            Assert.AreEqual(1, layers.Count);
        }

        [Test]
        public void Start_AfterScale_RestoresFullStrength()
        {
            const float kick = .1f;
            profile.envelope = AnimationCurve.Constant(0f, .4f, 1f);
            profile.sustainAt = -1f;
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = kick;
            layers.Start(profile, profile, Vector2.up, 0f);
            layers.Scale(profile, .5f);

            layers.Start(profile, profile, Vector2.up, 0f);
            layers.Sample(.1f, HalfHeight, out var offset, out _);
            Assert.AreEqual(kick * HalfHeight, offset.y, 1e-4f);
            Assert.AreEqual(0f, offset.x, 1e-4f);
        }

        [Test]
        public void CapShake_ClampsOffsetAndRoll()
        {
            var inside = new Vector2(.2f, 0f);
            var insideRoll = 1f;
            CameraUtils.CapShake(ref inside, ref insideRoll, 2f, 4f);
            Assert.AreEqual(.2f, inside.x, 1e-4f);
            Assert.AreEqual(1f, insideRoll);

            var offset = new Vector2(3f, 4f);
            var roll = 9f;
            CameraUtils.CapShake(ref offset, ref roll, 2f, 4f);
            Assert.AreEqual(2f, offset.magnitude, 1e-4f);
            Assert.AreEqual(4f, roll);

            roll = -9f;
            CameraUtils.CapShake(ref offset, ref roll, 2f, 4f);
            Assert.AreEqual(-4f, roll);
        }

        [Test]
        public void StartStronger_DropsAWeakerBlast()
        {
            KickOnly(.4f);
            layers.StartStronger(profile, profile, Vector2.up, .8f, 0f);

            layers.StartStronger(profile, profile, Vector2.right, .3f, .1f);
            layers.Sample(.1f, HalfHeight, out var offset, out _);
            Assert.AreEqual(1, layers.Count);
            Assert.AreEqual(.8f * Kick * HalfHeight, offset.y, 1e-4f);
            Assert.AreEqual(0f, offset.x, 1e-4f);
        }

        [Test]
        public void StartStronger_RestartsAnEqualOrStrongerBlast()
        {
            KickOnly(.4f);
            layers.StartStronger(profile, profile, Vector2.up, .5f, 0f);

            layers.StartStronger(profile, profile, Vector2.right, .5f, .2f);
            layers.Sample(.2f, HalfHeight, out var equal, out _);
            Assert.AreEqual(1, layers.Count);
            Assert.AreEqual(.5f * Kick * HalfHeight, equal.x, 1e-4f);
            Assert.AreEqual(0f, equal.y, 1e-4f);

            layers.StartStronger(profile, profile, Vector2.up, .9f, .2f);
            layers.Sample(.2f, HalfHeight, out var stronger, out _);
            Assert.AreEqual(1, layers.Count);
            Assert.AreEqual(.9f * Kick * HalfHeight, stronger.y, 1e-4f);
            Assert.AreEqual(0f, stronger.x, 1e-4f);
        }

        [Test]
        public void StartStronger_AfterTheLayerEnds_StartsAnyStrength()
        {
            const float length = .2f;
            KickOnly(length);
            layers.StartStronger(profile, profile, Vector2.up, 1f, 0f);

            var at = length + .1f;
            layers.StartStronger(profile, profile, Vector2.right, .2f, at);
            layers.Sample(at, HalfHeight, out var offset, out _);
            Assert.AreEqual(1, layers.Count);
            Assert.AreEqual(.2f * Kick * HalfHeight, offset.x, 1e-4f);
            Assert.AreEqual(0f, offset.y, 1e-4f);
        }

        private void KickOnly(float length)
        {
            profile.envelope = AnimationCurve.Constant(0f, length, 1f);
            profile.sustainAt = -1f;
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = Kick;
        }
    }
}
