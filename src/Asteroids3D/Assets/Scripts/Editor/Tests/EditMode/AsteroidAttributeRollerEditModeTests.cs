using System.Reflection;
using Asteroids.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Tests for the fragment-path attribute roller and the narrowed
    /// <see cref="AsteroidSpawner"/> surface. The baseline field no longer
    /// rolls random attributes at all — it draws from seeded streams
    /// (see AsteroidFieldCoreEditModeTests); only the mass-constrained
    /// fragment roll keeps UnityEngine.Random, and its outcome is persisted.
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

        [Test]
        public void RollForMass_EmptyMeshInfos_ReturnsDefaultMeshInfo()
        {
            settings.meshInfos = new AsteroidSpawnSettings.MeshInfo[0];
            var roller = new RandomAsteroidAttributeRoller(settings);
            var attrs = roller.RollForMass(5f, Vector3.zero, Vector3.zero);
            Assert.IsNull(attrs.MeshInfo.mesh);
            Assert.AreEqual(0f, attrs.MeshInfo.cachedVolume);
        }

        // ── AsteroidSpawner: narrowed surface ────────────────────────────────────

        [Test]
        public void AsteroidSpawner_SpawnRandomIsGone_AttributeDecisionMovedOut()
        {
            Assert.IsNull(typeof(AsteroidSpawner).GetMethod("SpawnRandom"),
                "SpawnRandom must not come back: the deterministic field builds attributes and calls Spawn(pose, attrs)");
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
            Assert.IsNotNull(type.GetEvent("OnFragmentSpawned"), "Missing fragment hook for the overlay");
        }
    }
}
