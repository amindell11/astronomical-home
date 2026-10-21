#if UNITY_EDITOR
using Asteroids;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the delta-v damage law in AsteroidDamage: linear in the injected lethality, default lethality is an exact no-op, and knocks at or under the grace delta-v are free.</summary>
    [Category("Damage")]
    public class AsteroidDamageLethalityEditModeTests
    {
        private const float SolidKnockDeltaV = 10f;

        private GameObject host;
        private AsteroidDamage damage;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("[LethalityHost]");
            damage = host.AddComponent<AsteroidDamage>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        private float Damage() => damage.CalcDamage(SolidKnockDeltaV);

        [Test]
        public void CalcDamage_ScalesLinearlyWithLethality()
        {
            damage.Initialize(volume: 8f, lethality: 1f);
            var baseline = Damage();
            Assert.Greater(baseline, 0f);

            damage.Initialize(volume: 8f, lethality: 0.25f);
            Assert.AreEqual(0.25f * baseline, Damage(), 1e-4f);

            damage.Initialize(volume: 8f, lethality: 2f);
            Assert.AreEqual(2f * baseline, Damage(), 1e-4f);
        }

        [Test]
        public void CalcDamage_DefaultLethality_IsAnExactNoOp()
        {
            damage.Initialize(volume: 8f, lethality: 1f);
            var explicitOne = Damage();

            damage.Initialize(volume: 8f);
            Assert.AreEqual(explicitOne, Damage());
        }

        [Test]
        public void CalcDamage_AtOrBelowGraceDeltaV_IsFree()
        {
            damage.Initialize(volume: 8f, lethality: 1f);
            Assert.AreEqual(0f, damage.CalcDamage(0f));
            Assert.AreEqual(0f, damage.CalcDamage(1.9f));
        }
    }
}
#endif
