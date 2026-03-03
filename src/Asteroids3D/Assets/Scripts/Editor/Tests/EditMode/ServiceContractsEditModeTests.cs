using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Services;
using NUnit.Framework;
using Objectives;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode tests validating the game service contracts and basic behavior.
    /// No scene loading or MonoBehaviours required.
    /// </summary>
    [Category("Services")]
    public class ServiceContractsEditModeTests
    {
        // ── IGameServices shape ──────────────────────────────────────────────────

        [Test]
        public void IGameServices_Exposes_AllFourServices()
        {
            var type = typeof(IGameServices);
            Assert.IsNotNull(type.GetProperty("UnitService"), "Missing UnitService");
            Assert.IsNotNull(type.GetProperty("EnvironmentService"), "Missing EnvironmentService");
            Assert.IsNotNull(type.GetProperty("ObjectiveService"), "Missing ObjectiveService");
            Assert.IsNotNull(type.GetProperty("CameraService"), "Missing CameraService");
        }

        [Test]
        public void GameServices_Implements_IGameServices()
        {
            Assert.IsTrue(typeof(IGameServices).IsAssignableFrom(typeof(GameServices)));
        }

        [Test]
        public void GameServices_Constructor_ThrowsOnNullService()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GameServices(null, new EnvironmentService(), new ObjectiveService(), new CameraService()));
            Assert.Throws<ArgumentNullException>(() =>
                new GameServices(new UnitService(), null, new ObjectiveService(), new CameraService()));
            Assert.Throws<ArgumentNullException>(() =>
                new GameServices(new UnitService(), new EnvironmentService(), null, new CameraService()));
            Assert.Throws<ArgumentNullException>(() =>
                new GameServices(new UnitService(), new EnvironmentService(), new ObjectiveService(), null));
        }

        // ── IUnitService shape ───────────────────────────────────────────────────

        [Test]
        public void IUnitService_HasRequiredMembers()
        {
            var type = typeof(IUnitService);
            Assert.IsNotNull(type.GetProperty("Registry"), "Missing Registry");
            Assert.IsNotNull(type.GetProperty("ActiveRegistry"), "Missing ActiveRegistry");
            Assert.IsNotNull(type.GetMethod("SpawnShip"), "Missing SpawnShip");
            Assert.IsNotNull(type.GetMethod("Clear"), "Missing Clear");
            Assert.IsNotNull(type.GetEvent("OnShipSpawned"), "Missing OnShipSpawned");
        }

        [Test]
        public void UnitService_Implements_IUnitService()
        {
            Assert.IsTrue(typeof(IUnitService).IsAssignableFrom(typeof(UnitService)));
        }

        [Test]
        public void UnitService_RegistryIsNotNull_AfterConstruction()
        {
            var svc = new UnitService();
            Assert.IsNotNull(svc.Registry);
            Assert.IsNotNull(svc.ActiveRegistry);
        }

        // ── IEnvironmentService shape ────────────────────────────────────────────

        [Test]
        public void IEnvironmentService_HasRequiredMembers()
        {
            var type = typeof(IEnvironmentService);
            Assert.IsNotNull(type.GetProperty("World"), "Missing World");
            Assert.IsNotNull(type.GetProperty("WorldFollowerTransform"), "Missing WorldFollowerTransform");
            Assert.IsNotNull(type.GetMethod("LoadSceneAsync"), "Missing LoadSceneAsync");
            Assert.IsNotNull(type.GetMethod("UnloadSceneAsync"), "Missing UnloadSceneAsync");
            Assert.IsNotNull(type.GetMethod("SpawnWorld"), "Missing SpawnWorld");
            Assert.IsNotNull(type.GetMethod("Clear"), "Missing Clear");
        }

        [Test]
        public void EnvironmentService_Implements_IEnvironmentService()
        {
            Assert.IsTrue(typeof(IEnvironmentService).IsAssignableFrom(typeof(EnvironmentService)));
        }

        [Test]
        public void EnvironmentService_WorldIsNull_AfterConstruction()
        {
            var svc = new EnvironmentService();
            Assert.IsNull(svc.World);
            Assert.IsNull(svc.WorldFollowerTransform);
        }

        // ── IObjectiveService shape ──────────────────────────────────────────────

        [Test]
        public void IObjectiveService_HasRequiredMembers()
        {
            var type = typeof(IObjectiveService);
            Assert.IsNotNull(type.GetProperty("CurrentTracker"), "Missing CurrentTracker");
            Assert.IsNotNull(type.GetProperty("CurrentState"), "Missing CurrentState");
            Assert.IsNotNull(type.GetMethod("SetObjective"), "Missing SetObjective");
            Assert.IsNotNull(type.GetMethod("Tick"), "Missing Tick");
            Assert.IsNotNull(type.GetMethod("Restart"), "Missing Restart");
            Assert.IsNotNull(type.GetMethod("Clear"), "Missing Clear");
            Assert.IsNotNull(type.GetEvent("OnStateChanged"), "Missing OnStateChanged");
        }

        [Test]
        public void ObjectiveService_Implements_IObjectiveService()
        {
            Assert.IsTrue(typeof(IObjectiveService).IsAssignableFrom(typeof(ObjectiveService)));
        }

        [Test]
        public void ObjectiveService_SetObjective_CreatesTracker_AndForwardsEvents()
        {
            var svc = new ObjectiveService();
            var key = new StubKeyTracker(false);
            var alive = new StubPlayerAlive(true);
            var zone = new StubExtractionZone(Vector3.zero);
            var paramsAsset = ScriptableObject.CreateInstance<ObjectiveParams>();

            try
            {
                var factory = new ObjectiveStateFactory(key, new StubPlayerPosition(Vector3.zero), zone, null, null, paramsAsset);
                svc.SetObjective(MissionDefinition.CreateDefault(), factory, alive);

                Assert.IsNotNull(svc.CurrentTracker);
                Assert.AreEqual(ObjectiveType.Explore, svc.CurrentState);

                var transitions = new List<(ObjectiveType from, ObjectiveType to)>();
                svc.OnStateChanged += (f, t) => transitions.Add((f, t));

                key.HasKey = true;
                svc.Tick(0.1f); // Explore → KeyAcquired
                Assert.AreEqual(1, transitions.Count);
                Assert.AreEqual(ObjectiveType.KeyAcquired, svc.CurrentState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(paramsAsset);
            }
        }

        [Test]
        public void ObjectiveService_Clear_RemovesTracker()
        {
            var svc = new ObjectiveService();
            Assert.IsNull(svc.CurrentTracker);
            Assert.IsNull(svc.CurrentState);

            var paramsAsset = ScriptableObject.CreateInstance<ObjectiveParams>();
            try
            {
                var factory = new ObjectiveStateFactory(
                    new StubKeyTracker(false), new StubPlayerPosition(Vector3.zero),
                    new StubExtractionZone(Vector3.zero), null, null, paramsAsset);
                svc.SetObjective(MissionDefinition.CreateDefault(), factory, new StubPlayerAlive(true));
                Assert.IsNotNull(svc.CurrentTracker);

                svc.Clear();
                Assert.IsNull(svc.CurrentTracker);
                Assert.IsNull(svc.CurrentState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(paramsAsset);
            }
        }

        [Test]
        public void ObjectiveService_Restart_DelegatesToTracker()
        {
            var svc = new ObjectiveService();
            var key = new StubKeyTracker(false);
            var alive = new StubPlayerAlive(true);
            var paramsAsset = ScriptableObject.CreateInstance<ObjectiveParams>();

            try
            {
                var factory = new ObjectiveStateFactory(
                    key, new StubPlayerPosition(Vector3.zero),
                    new StubExtractionZone(Vector3.zero), null, null, paramsAsset);
                svc.SetObjective(MissionDefinition.CreateDefault(), factory, alive);

                // Kill player → Failed
                alive.Alive = false;
                svc.Tick(0.1f);
                Assert.AreEqual(ObjectiveType.Failed, svc.CurrentState);

                // Restart
                alive.Alive = true;
                svc.Restart();
                Assert.AreEqual(ObjectiveType.Explore, svc.CurrentState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(paramsAsset);
            }
        }

        // ── ICameraService shape ─────────────────────────────────────────────────

        [Test]
        public void ICameraService_HasRequiredMembers()
        {
            var type = typeof(ICameraService);
            Assert.IsNotNull(type.GetProperty("CameraRig"), "Missing CameraRig");
            Assert.IsNotNull(type.GetProperty("MainCamera"), "Missing MainCamera");
            Assert.IsNotNull(type.GetProperty("UICamera"), "Missing UICamera");
            Assert.IsNotNull(type.GetMethod("Initialize"), "Missing Initialize");
            Assert.IsNotNull(type.GetMethod("SetSubject"), "Missing SetSubject");
            Assert.IsNotNull(type.GetMethod("AddSecondarySubject"), "Missing AddSecondarySubject");
            Assert.IsNotNull(type.GetMethod("RemoveSecondarySubject"), "Missing RemoveSecondarySubject");
            Assert.IsNotNull(type.GetMethod("ConfigurePlayerInputProjection"), "Missing ConfigurePlayerInputProjection");
            Assert.IsNotNull(type.GetMethod("Clear"), "Missing Clear");
        }

        [Test]
        public void CameraService_Implements_ICameraService()
        {
            Assert.IsTrue(typeof(ICameraService).IsAssignableFrom(typeof(CameraService)));
        }

        [Test]
        public void CameraService_IsNull_AfterConstruction()
        {
            var svc = new CameraService();
            Assert.IsNull(svc.CameraRig);
            Assert.IsNull(svc.MainCamera);
            Assert.IsNull(svc.UICamera);
        }

        // ── GameServices.ClearAll ────────────────────────────────────────────────

        [Test]
        public void GameServices_ClearAll_ClearsAllServices()
        {
            var unit = new UnitService();
            var env = new EnvironmentService();
            var obj = new ObjectiveService();
            var cam = new CameraService();
            var services = new GameServices(unit, env, obj, cam);

            // Should not throw
            Assert.DoesNotThrow(() => services.ClearAll());
        }

        // ── Test stubs ───────────────────────────────────────────────────────────

        private sealed class StubKeyTracker : IKeyTracker
        {
            public bool HasKey;
            public StubKeyTracker(bool hasKey) => HasKey = hasKey;
            public bool PlayerHasKey => HasKey;
        }

        private sealed class StubPlayerAlive : IPlayerAlive
        {
            public bool Alive;
            public StubPlayerAlive(bool alive) => Alive = alive;
            public bool IsAlive => Alive;
        }

        private sealed class StubPlayerPosition : IPlayerPosition
        {
            public StubPlayerPosition(Vector3 pos) { }
            public Vector3 Position => Vector3.zero;
        }

        private sealed class StubExtractionZone : IExtractionZone
        {
            private readonly Vector3 pos;
            public StubExtractionZone(Vector3 pos) => this.pos = pos;
            public Vector3 Position => pos;
        }
    }
}
