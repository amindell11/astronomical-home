#if UNITY_EDITOR
using Asteroids;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the collision-lethality fold in AsteroidDamage: below the soft cap damage scales linearly with the injected lethality, and the default is an exact no-op (production paths stay byte-identical).</summary>
    [Category("Damage")]
    public class AsteroidDamageLethalityEditModeTests
    {
        // Below the soft-cap threshold, so lethality scaling stays linear.
        private const float AsteroidMass = 1000f;
        private const float ShipMass = 500f;
        private static readonly Vector3 AsteroidVel = new(-1f, 0f, 0f);
        private static readonly Vector3 ShipVel = new(1f, 0f, 0f);

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

        private float Damage() => damage.CalcDamage(AsteroidMass, AsteroidVel, ShipMass, ShipVel);

        [Test]
        public void CalcDamage_ScalesLinearlyWithLethalityBelowTheSoftCap()
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
    }
}
#endif
