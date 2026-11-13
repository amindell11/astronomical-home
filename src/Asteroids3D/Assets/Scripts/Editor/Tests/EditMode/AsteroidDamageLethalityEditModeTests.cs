#if UNITY_EDITOR
using Asteroids;
using Asteroids.Fields;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the delta-v damage law: linear in the injected lethality, lethality 1 leaves slope times excess, knocks at or under the grace are free, and the field-settings defaults price both sides of the floor.</summary>
    [Category("Damage")]
    public class AsteroidDamageLethalityEditModeTests
    {
        private const float SolidKnockDeltaV = 10f;

        private GameObject host;
        private AsteroidDamage damage;
        private AsteroidFieldSettings settings;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("[LethalityHost]");
            damage = host.AddComponent<AsteroidDamage>();
            settings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settings);
        }

        private float Damage() => damage.CalcDamage(SolidKnockDeltaV);

        [Test]
        public void CalcDamage_ScalesLinearlyWithLethality()
        {
            damage.Initialize(volume: 8f, Law(1f));
            var baseline = Damage();
            Assert.Greater(baseline, 0f);

            damage.Initialize(volume: 8f, Law(0.25f));
            Assert.AreEqual(0.25f * baseline, Damage(), 1e-4f);

            damage.Initialize(volume: 8f, Law(2f));
            Assert.AreEqual(2f * baseline, Damage(), 1e-4f);
        }

        [Test]
        public void CalcDamage_LethalityOne_LeavesSlopeTimesExcess()
        {
            damage.Initialize(volume: 8f, Law(1f));
            Assert.AreEqual(3f * (SolidKnockDeltaV - 2f), Damage());
        }

        [Test]
        public void CalcDamage_AtOrBelowGraceDeltaV_IsFree()
        {
            damage.Initialize(volume: 8f, Law(1f));
            Assert.AreEqual(0f, damage.CalcDamage(0f));
            Assert.AreEqual(0f, damage.CalcDamage(1.9f));
            Assert.AreEqual(0f, damage.CalcDamage(2f));
        }

        [Test]
        public void CalcDamage_FieldSettingsDefaults_PricesBothSidesOfTheFloor()
        {
            damage.Initialize(volume: 8f,
                new CollisionDamageLaw(settings.graceDeltaV, settings.damagePerDeltaV, lethality: 1f));
            Assert.AreEqual(5f, settings.graceDeltaV);
            Assert.AreEqual(3f, settings.damagePerDeltaV);
            Assert.AreEqual(0f, damage.CalcDamage(5f));
            Assert.AreEqual(15f, damage.CalcDamage(10f));
        }

        private static CollisionDamageLaw Law(float lethality) =>
            new(graceDeltaV: 2f, damagePerDeltaV: 3f, lethality: lethality);
    }
}
#endif
