using System.Linq;
using System.Reflection;
using Asteroids.Spawning;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.EditMode
{
    /// <summary>
    /// Structural tests for the PR1 attribute-provider extraction
    /// (doc/Feature_Plans/Deterministic_Asteroid_Field.md): the attribute
    /// decision now lives in <see cref="RandomAsteroidAttributeRoller"/> and
    /// must reproduce the old inline math exactly, and
    /// <see cref="AsteroidSpawner"/> exposes only the narrowed spawn/query
    /// surface.
    /// </summary>
    [Category("Asteroids")]
    public class AsteroidAttributeRollerEditModeTests
    {
        private AsteroidSpawnSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<AsteroidSpawnSettings>();
            settings.density = 2f;
            settings.massScaleRange = new Vector2(0.5f, 2f);
            settings.velocityRange = new Vector2(0.5f, 2f);
            settings.spinRange = new Vector2(-30f, 30f);
            settings.meshInfos = new[]
            {
                new AsteroidSpawnSettings.MeshInfo { cachedVolume = 1f },
                new AsteroidSpawnSettings.MeshInfo { cachedVolume = 3f },
                new AsteroidSpawnSettings.MeshInfo { cachedVolume = 8f }
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        // ── Roll: preserves the old inline math ─────────────────────────────────

        [Test]
        public void Roll_MassEqualsBaseMassTimesScaleCubed()
        {
            var roller = new RandomAsteroidAttributeRoller(settings);
            for (var i = 0; i < 50; i++)
            {
                var attrs = roller.Roll();
                var baseMass = attrs.MeshInfo.cachedVolume * settings.density;
                Assert.AreEqual(attrs.Mass, baseMass * attrs.Scale * attrs.Scale * attrs.Scale, attrs.Mass * 1e-4f,
                    "scale must remain the cube root of the mass factor");
            }
        }

        [Test]
        public void Roll_MassFactorStaysWithinMassScaleRange()
        {
            var roller = new RandomAsteroidAttributeRoller(settings);
            for (var i = 0; i < 50; i++)
            {
                var attrs = roller.Roll();
                var factor = attrs.Mass / (attrs.MeshInfo.cachedVolume * settings.density);
                Assert.That(factor, Is.InRange(settings.massScaleRange.x - 1e-4f, settings.massScaleRange.y + 1e-4f));
            }
        }

        [Test]
        public void Roll_VelocityMagnitudeMatchesMassScaledRange()
        {
            var roller = new RandomAsteroidAttributeRoller(settings);
            for (var i = 0; i < 50; i++)
            {
                var attrs = roller.Roll();
                var velocityScale = 1f / Mathf.Pow(attrs.Mass, 1f / 3f);
                var speed = attrs.Velocity.magnitude;
                Assert.That(speed, Is.InRange(
                        settings.velocityRange.x * velocityScale - 1e-4f,
                        settings.velocityRange.y * velocityScale + 1e-4f),
                    "velocity must keep the old inverse-cube-root mass scaling");
            }
        }

        [Test]
        public void Roll_MeshComesFromSettingsArray()
        {
            var roller = new RandomAsteroidAttributeRoller(settings);
            var volumes = settings.meshInfos.Select(m => m.cachedVolume).ToArray();
            for (var i = 0; i < 50; i++)
            {
                var attrs = roller.Roll();
                Assert.Contains(attrs.MeshInfo.cachedVolume, volumes);
            }
        }

        [Test]
        public void Roll_EmptyMeshInfos_ReturnsDefaultMeshInfo()
        {
            settings.meshInfos = new AsteroidSpawnSettings.MeshInfo[0];
            var roller = new RandomAsteroidAttributeRoller(settings);
            var attrs = roller.Roll();
            Assert.IsNull(attrs.MeshInfo.mesh);
            Assert.AreEqual(0f, attrs.MeshInfo.cachedVolume);
        }

        [Test]
        public void Roll_SameRandomState_ProducesIdenticalSequence()
        {
            var roller = new RandomAsteroidAttributeRoller(settings);

            Random.InitState(12345);
            var first = Enumerable.Range(0, 10).Select(_ => roller.Roll()).ToArray();
            Random.InitState(12345);
            var second = Enumerable.Range(0, 10).Select(_ => roller.Roll()).ToArray();

            for (var i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].MeshInfo.cachedVolume, second[i].MeshInfo.cachedVolume);
                Assert.AreEqual(first[i].Mass, second[i].Mass);
                Assert.AreEqual(first[i].Scale, second[i].Scale);
                Assert.AreEqual(first[i].Velocity, second[i].Velocity);
                Assert.AreEqual(first[i].AngularVelocity, second[i].AngularVelocity);
            }
        }

        // ── RollForMass: fragment path ───────────────────────────────────────────

        [Test]
        public void RollForMass_PreservesMassAndKinematics()
        {
            var roller = new RandomAsteroidAttributeRoller(settings);
            var velocity = new Vector3(1f, 2f, 3f);
            var spin = new Vector3(-4f, 5f, -6f);

            var attrs = roller.RollForMass(7.5f, velocity, spin);

            Assert.AreEqual(7.5f, attrs.Mass);
            Assert.AreEqual(velocity, attrs.Velocity);
            Assert.AreEqual(spin, attrs.AngularVelocity);
        }

        [Test]
        public void RollForMass_ScaleIsCubeRootOfMassOverBaseMass()
        {
            var roller = new RandomAsteroidAttributeRoller(settings);
            for (var i = 0; i < 50; i++)
            {
                var attrs = roller.RollForMass(7.5f, Vector3.zero, Vector3.zero);
                var baseMass = attrs.MeshInfo.cachedVolume * settings.density;
                var expectedScale = Mathf.Pow(attrs.Mass / baseMass, 1f / 3f);
                Assert.AreEqual(expectedScale, attrs.Scale, expectedScale * 1e-5f);
            }
        }

        // ── AsteroidSpawner: narrowed surface ────────────────────────────────────

        [Test]
        public void AsteroidSpawner_SpawnRandomIsGone_AttributeDecisionMovedOut()
        {
            Assert.IsNull(typeof(AsteroidSpawner).GetMethod("SpawnRandom"),
                "SpawnRandom must not come back: callers roll attributes and call Spawn(pose, attrs)");
        }

        [Test]
        public void AsteroidSpawner_RegistryIsNotPubliclyReachable()
        {
            Assert.IsNull(typeof(AsteroidSpawner).GetProperty("Registry", BindingFlags.Public | BindingFlags.Instance),
                "Registry reach-through must stay tightened to TotalVolume/ActiveCount");
        }

        [Test]
        public void AsteroidSpawner_ExposesMinimalSpawnAndQuerySurface()
        {
            var type = typeof(AsteroidSpawner);
            Assert.IsNotNull(type.GetMethod("Spawn"), "Missing Spawn(pose, attrs)");
            Assert.IsNotNull(type.GetMethod("SpawnFragment"), "Missing SpawnFragment (Fragger path stays)");
            Assert.IsNotNull(type.GetProperty("TotalVolume"), "Missing TotalVolume query");
            Assert.IsNotNull(type.GetProperty("ActiveCount"), "Missing ActiveCount query");
            Assert.IsNotNull(type.GetProperty("AttributeProvider"), "Missing AttributeProvider seam");
        }

        [Test]
        public void RandomRoller_ImplementsAttributeProviderSeam()
        {
            Assert.IsTrue(typeof(IAsteroidAttributeProvider).IsAssignableFrom(typeof(RandomAsteroidAttributeRoller)));
        }
    }
}
