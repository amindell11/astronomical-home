using Combat.Projectiles;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class ConcussionGrenadeEditModeTests
    {
        // ── Wave falloff ──

        [Test]
        public void Falloff_IsFullAtCenter_ZeroAtMaxRadius_LinearBetween()
        {
            Assert.AreEqual(1f, ConcussionWave.Falloff(0f, 12f), 0.0001f);
            Assert.AreEqual(0.5f, ConcussionWave.Falloff(6f, 12f), 0.0001f);
            Assert.AreEqual(0f, ConcussionWave.Falloff(12f, 12f), 0.0001f);
        }

        [Test]
        public void Falloff_ClampsBeyondMaxRadius_AndHandlesDegenerateRadius()
        {
            Assert.AreEqual(0f, ConcussionWave.Falloff(20f, 12f), 0.0001f);
            Assert.AreEqual(0f, ConcussionWave.Falloff(1f, 0f), 0.0001f);
        }

        // ── Charge braking ──

        // Mirrors Unity's step: velocity integrates first, then position moves by the new velocity.
        private static float RestDistance(float speed, float distance, float dt, bool resolveEachStep)
        {
            var travelled = 0f;
            var deceleration = Grenade.BrakingDeceleration(speed, distance, dt);
            for (var step = 0; step < 100000 && speed > 0f && travelled < distance; step++)
            {
                if (resolveEachStep)
                    deceleration = Grenade.BrakingDeceleration(speed, distance - travelled, dt);
                speed -= deceleration * dt;
                travelled += speed * dt;
            }
            return travelled;
        }

        [TestCase(40f, 15f, false)]
        [TestCase(40f, 15f, true)]
        [TestCase(40f, 25f, true)]
        [TestCase(40f, 0.7f, true)]
        [TestCase(10f, 25f, true)]
        public void BrakingDeceleration_ComesToRestOnTheDistance(float speed, float distance, bool resolveEachStep)
        {
            var dt = 0.02f;
            Assert.AreEqual(distance, RestDistance(speed, distance, dt, resolveEachStep), 0.05f);
        }

        [Test]
        public void BrakingDeceleration_ContinuousFormula_WouldStopShort()
        {
            var dt = 0.02f;
            var naive = 40f * 40f / (2f * 15f);
            var travelled = 0f;
            for (var speed = 40f; speed > 0f;)
            {
                speed -= naive * dt;
                travelled += Mathf.Max(0f, speed) * dt;
            }
            Assert.Greater(15f - travelled, 0.3f, "v²/2d under step integration lands about v·dt/2 short — the reason for the discrete form.");
        }

        [Test]
        public void BrakingDeceleration_AtZeroDistance_IsFinite()
        {
            Assert.IsFalse(float.IsInfinity(Grenade.BrakingDeceleration(40f, 0f, 0.02f)));
        }
    }
}
