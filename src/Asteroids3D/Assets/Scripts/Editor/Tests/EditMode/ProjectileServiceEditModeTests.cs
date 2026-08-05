using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Combat.Projectile;
using Damage;
using Game.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    [Category("Services")]
    public class ProjectileServiceEditModeTests
    {
        private readonly List<GameObject> tempObjects = new();
        private GameObject liveRoot;
        private ProjectileService service;

        [SetUp]
        public void SetUp()
        {
            liveRoot = new GameObject("LiveRoot");
            tempObjects.Add(liveRoot);
            service = new ProjectileService(liveRoot.transform);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in tempObjects)
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            tempObjects.Clear();
        }

        /// <summary>Pool-free ProjectileBase: the domain return path raises the event without touching SimplePool statics.</summary>
        private class TestProjectile : ProjectileBase
        {
            protected override DamageKind Kind => DamageKind.Laser;

            public int Returns { get; private set; }
            public Action OnReturning;

            protected override void ReturnToPool()
            {
                Returns++;
                OnReturning?.Invoke();
                gameObject.SetActive(false);
                RaiseReturnedToPool();
            }
        }

        private sealed class TestSpawnerProjectile : TestProjectile, ITransientSpawner
        {
            public event Action<MonoBehaviour, Action> Spawned;
            public void Announce(MonoBehaviour child, Action returnToPool) => Spawned?.Invoke(child, returnToPool);
        }

        private T Create<T>(string name = null) where T : MonoBehaviour
        {
            var go = new GameObject(name ?? typeof(T).Name);
            tempObjects.Add(go);
            return go.AddComponent<T>();
        }

        [Test]
        public void RegisteredProjectiles_AreLive_ParentedUnderTheRoot_AndFlushReturnsThemAll()
        {
            var a = Create<TestProjectile>("A");
            var b = Create<TestProjectile>("B");
            service.Register(a, a.ReturnToPoolImmediate);
            service.Register(b, b.ReturnToPoolImmediate);
            Assert.AreEqual(2, service.ActiveCount);
            Assert.AreSame(liveRoot.transform, a.transform.parent,
                "live transients ride the context root so they die with it");

            service.ReturnAllToPool();

            Assert.AreEqual(0, service.ActiveCount);
            Assert.AreEqual(1, a.Returns);
            Assert.AreEqual(1, b.Returns);
            Assert.IsFalse(a.gameObject.activeSelf);
        }

        [Test]
        public void NaturalPoolReturn_Deregisters_WithoutAFlush()
        {
            var projectile = Create<TestProjectile>();
            service.Register(projectile, projectile.ReturnToPoolImmediate);

            projectile.ReturnToPoolImmediate();

            Assert.AreEqual(0, service.ActiveCount);
            service.ReturnAllToPool();
            Assert.AreEqual(1, projectile.Returns, "a returned projectile must not be flushed again");
        }

        [Test]
        public void ReRegisteringALiveInstance_IsSurfacedAsPoolCorruption_AndReplacesTheFlushAction()
        {
            var projectile = Create<TestProjectile>();
            var replacementCalls = 0;
            service.Register(projectile, projectile.ReturnToPoolImmediate);
            LogAssert.Expect(LogType.Error, new Regex("registered while already live"));
            service.Register(projectile, () => { replacementCalls++; projectile.ReturnToPoolImmediate(); });
            Assert.AreEqual(1, service.ActiveCount);

            service.ReturnAllToPool();

            Assert.AreEqual(1, replacementCalls);
            Assert.AreEqual(1, projectile.Returns);
        }

        [Test]
        public void DestroyingTheContextRoot_TakesLiveTransientsWithIt()
        {
            var projectile = Create<TestProjectile>();
            service.Register(projectile, projectile.ReturnToPoolImmediate);

            UnityEngine.Object.DestroyImmediate(liveRoot);

            Assert.IsTrue(projectile == null, "a live transient must not outlive its context root");
            Assert.AreEqual(0, service.ActiveCount);
        }

        [Test]
        public void DestroyedInstances_AreSkippedAndPruned()
        {
            var corpse = Create<TestProjectile>("Corpse");
            var survivor = Create<TestProjectile>("Survivor");
            service.Register(corpse, corpse.ReturnToPoolImmediate);
            service.Register(survivor, survivor.ReturnToPoolImmediate);

            UnityEngine.Object.DestroyImmediate(corpse.gameObject);

            Assert.AreEqual(1, service.ActiveCount);
            Assert.DoesNotThrow(() => service.ReturnAllToPool());
            Assert.AreEqual(0, service.ActiveCount);
            Assert.AreEqual(1, survivor.Returns);
        }

        [Test]
        public void FlushSurvivesAReturnEvent_MutatingTheSetMidFlush()
        {
            var a = Create<TestProjectile>("A");
            var b = Create<TestProjectile>("B");
            // A's return drags B back too, so the flush snapshot meets an already-deregistered B.
            a.OnReturning = () =>
            {
                if (b.gameObject.activeSelf) b.ReturnToPoolImmediate();
            };
            service.Register(a, a.ReturnToPoolImmediate);
            service.Register(b, b.ReturnToPoolImmediate);

            Assert.DoesNotThrow(() => service.ReturnAllToPool());

            Assert.AreEqual(0, service.ActiveCount);
            Assert.AreEqual(1, a.Returns);
            Assert.AreEqual(1, b.Returns, "the mid-flush return must count once, never twice");
        }

        [Test]
        public void SpawnerCascade_AutoRegistersAnnouncedChildren_Recursively()
        {
            var spawner = Create<TestSpawnerProjectile>("Spawner");
            var child = Create<TestSpawnerProjectile>("Child");
            var grandchild = Create<TestProjectile>("Grandchild");
            service.Register(spawner, spawner.ReturnToPoolImmediate);

            spawner.Announce(child, child.ReturnToPoolImmediate);
            child.Announce(grandchild, grandchild.ReturnToPoolImmediate);
            Assert.AreEqual(3, service.ActiveCount, "announced children register under the same rule, recursively");

            service.ReturnAllToPool();
            Assert.AreEqual(0, service.ActiveCount);
            Assert.AreEqual(1, grandchild.Returns);
        }

        [Test]
        public void SpawnerCascade_StopsListening_AfterTheSpawnerDeregisters()
        {
            var spawner = Create<TestSpawnerProjectile>("Spawner");
            var orphan = Create<TestProjectile>("Orphan");
            service.Register(spawner, spawner.ReturnToPoolImmediate);

            spawner.ReturnToPoolImmediate();
            spawner.Announce(orphan, orphan.ReturnToPoolImmediate);

            Assert.AreEqual(0, service.ActiveCount, "a deregistered spawner's announcements must not register");
        }

        [Test]
        public void ConcussionWaveRelease_Deregisters()
        {
            var wave = Create<ConcussionWave>();
            service.Register(wave, wave.ReturnToPoolImmediate);
            Assert.AreEqual(1, service.ActiveCount);

            wave.ReturnToPoolImmediate();

            Assert.AreEqual(0, service.ActiveCount);
            Assert.IsFalse(wave.gameObject.activeSelf);
        }

        [Test]
        public void ForEachLive_VisitsOnlyLiveInstances()
        {
            var live = Create<TestProjectile>("Live");
            var returned = Create<TestProjectile>("Returned");
            service.Register(live, live.ReturnToPoolImmediate);
            service.Register(returned, returned.ReturnToPoolImmediate);
            returned.ReturnToPoolImmediate();

            var visited = new List<MonoBehaviour>();
            service.ForEachLive(visited.Add);

            Assert.AreEqual(new MonoBehaviour[] { live }, visited);
        }

        [Test]
        public void GameServicesClearAll_FlushesLiveTransients()
        {
            var unitGo = new GameObject("Unit");
            tempObjects.Add(unitGo);
            var services = new GameServices(
                unitGo.AddComponent<UnitService>(), service, new EnvironmentService(),
                unitGo.AddComponent<ObjectiveService>(), new CameraService(), new UIService(),
                Tests.Common.TestArena.On(unitGo));
            var projectile = Create<TestProjectile>();
            service.Register(projectile, projectile.ReturnToPoolImmediate);

            services.ClearAll();

            Assert.AreEqual(1, projectile.Returns, "sector transitions must not leak live projectiles");
            Assert.AreEqual(0, service.ActiveCount);
        }
    }
}
