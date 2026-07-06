using System.Collections;
using Game;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using UnityEngine;
using World;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the producer-owned <see cref="RespawnPolicy"/> position resolution and the
    /// <see cref="Respawn.Wire"/> guards. The full death→revive pipeline (real ship + UnitService
    /// tick) is covered in PlayMode; here we verify the deterministic anchor math (radius 0) and that
    /// a disabled/invalid policy wires nothing.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class RespawnEditModeTests
    {
        private GameObject _follower;

        // ── Minimal service stubs (Resolve only touches EnvironmentService.WorldFollowerTransform) ──
        private class StubEnv : IEnvironmentService
        {
            public Transform follower;
            public WorldRoot World => null;
            public Transform WorldFollowerTransform => follower;
            public IEnumerator LoadSceneAsync(string sceneName) { yield break; }
            public IEnumerator UnloadSceneAsync(string sceneName) { yield break; }
            public void SpawnWorld(WorldRoot prefab) { }
            public void AdoptWorld(WorldRoot existing) { }
            public void Clear() { }
            public AI.Scanning.IObstacleField ObstacleField { get; private set; }
            public void RegisterObstacleField(AI.Scanning.IObstacleField field) => ObstacleField = field;
        }

        private class StubServices : IGameServices
        {
            public IEnvironmentService Env;
            public IUnitService UnitService => null;
            public IEnvironmentService EnvironmentService => Env;
            public IObjectiveService ObjectiveService => null;
            public ICameraService CameraService => null;
            public IUIService UIService => null;
        }

        [SetUp]
        public void SetUp()
        {
            // GamePlane is static/process-wide; another fixture may have configured it already.
            if (!GamePlane.IsConfigured) GamePlane.Configure(PlaneAxis.Y);
        }

        [TearDown]
        public void TearDown()
        {
            if (_follower) Object.DestroyImmediate(_follower);
            _follower = null;
        }

        private StubServices Services(Transform follower = null) =>
            new StubServices { Env = new StubEnv { follower = follower } };

        [Test]
        public void Resolve_FixedPoint_ReturnsPoint_IgnoringFollower()
        {
            _follower = new GameObject("Follower");
            _follower.transform.position = new Vector3(100, 0, 100);

            var policy = new RespawnPolicy
            {
                origin = RespawnPolicy.Origin.FixedPoint,
                point = new Vector2(7, 3),
                radius = 0f,
            };

            var resolved = Respawn.Resolve(policy, Services(_follower.transform));
            Assert.AreEqual(new Vector2(7, 3), resolved,
                "FixedPoint must revive at 'point' (follower ignored) when radius is 0.");
        }

        [Test]
        public void Resolve_FixedPoint_IsProducerRelative()
        {
            var policy = new RespawnPolicy
            {
                origin = RespawnPolicy.Origin.FixedPoint,
                point = new Vector2(2, -3),
                radius = 0f,
            };

            var producerBase = new Vector2(10, 5);
            Assert.AreEqual(new Vector2(12, 2), Respawn.Resolve(policy, Services(), producerBase),
                "FixedPoint must resolve to the producer's base position plus 'point' (producer-relative offset).");
        }

        [Test]
        public void Resolve_FollowerRelative_ReturnsFollowerPlanePosition()
        {
            _follower = new GameObject("Follower");
            _follower.transform.position = new Vector3(42, 5, -13);

            var policy = new RespawnPolicy
            {
                origin = RespawnPolicy.Origin.FollowerRelative,
                point = new Vector2(7, 3), // must be ignored
                radius = 0f,
            };

            var expected = GamePlane.WorldPointToPlane(_follower.transform.position);
            var resolved = Respawn.Resolve(policy, Services(_follower.transform));

            Assert.AreEqual(expected, resolved,
                "FollowerRelative must anchor to the world follower's plane position, not 'point'.");
            Assert.AreNotEqual(new Vector2(7, 3), resolved);
        }

        [Test]
        public void Resolve_FollowerRelative_NoFollower_FallsBackToZero()
        {
            var policy = new RespawnPolicy
            {
                origin = RespawnPolicy.Origin.FollowerRelative,
                radius = 0f,
            };

            Assert.AreEqual(Vector2.zero, Respawn.Resolve(policy, Services(follower: null)),
                "With no world follower, FollowerRelative anchors to zero.");
        }

        [Test]
        public void Wire_OriginNone_WiresNothing_ReturnsFalse()
        {
            var policy = new RespawnPolicy { origin = RespawnPolicy.Origin.None };
            Assert.IsFalse(Respawn.Wire(null, policy, Services()),
                "A None policy must wire nothing and report false (even with a null ship).");
        }

        [Test]
        public void Wire_MissingShipOrServices_ReturnsFalse()
        {
            var policy = new RespawnPolicy { origin = RespawnPolicy.Origin.FixedPoint };
            Assert.IsFalse(Respawn.Wire(null, policy, Services()), "Null ship must not wire.");
            Assert.IsFalse(Respawn.Wire(null, policy, null), "Null services must not wire.");
        }
    }
}
