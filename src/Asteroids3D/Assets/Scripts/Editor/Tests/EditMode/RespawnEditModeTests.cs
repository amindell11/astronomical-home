using System.Collections;
using Game;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using UnityEngine;
using World;

namespace Tests.EditMode
{
    /// <summary>Deterministic (radius 0) anchor math for <see cref="Respawn.Resolve"/> plus the <see cref="Respawn.Wire"/> guards; the full death→revive pipeline is covered in PlayMode.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class RespawnEditModeTests
    {
        private GameObject _follower;
        private GameObject _arenaHost;

        private class StubEnv : IEnvironmentService
        {
            public Transform follower;
            public WorldRoot World => null;
            public Transform WorldFollowerTransform => follower;
            public IEnumerator ApplyLocaleAsync(string localeSceneName) { yield break; }
            public IEnumerator RestoreBootEnvironmentAsync() { yield break; }
            public void SpawnWorld(WorldRoot prefab) { }
            public void AdoptWorld(WorldRoot existing) { }
            public void Clear() { }
        }

        private class StubServices : IGameServices
        {
            public IEnvironmentService Env;
            public ArenaContext ArenaCtx;
            public IUnitService UnitService => null;
            public IProjectileService Projectiles => null;
            public IEnvironmentService EnvironmentService => Env;
            public IObjectiveService ObjectiveService => null;
            public ICameraService CameraService => null;
            public IUIService UIService => null;
            public ArenaContext Arena => ArenaCtx;
        }

        [TearDown]
        public void TearDown()
        {
            if (_follower) Object.DestroyImmediate(_follower);
            _follower = null;
            if (_arenaHost) Object.DestroyImmediate(_arenaHost);
            _arenaHost = null;
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
                point = new Vector2(7, 3),
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
        public void Resolve_FollowerRelative_NoFollower_FallsBackToArenaOffset()
        {
            _arenaHost = new GameObject("Arena");
            var offset = new Vector2(1000f, -250f);
            var services = Services(follower: null);
            services.ArenaCtx = new ArenaContext(offset, new Tests.Common.StubShipRegistry());

            var policy = new RespawnPolicy
            {
                origin = RespawnPolicy.Origin.FollowerRelative,
                radius = 0f,
            };

            Assert.AreEqual(offset, Respawn.Resolve(policy, services),
                "With no world follower, FollowerRelative anchors to the arena origin, not the world origin.");
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
