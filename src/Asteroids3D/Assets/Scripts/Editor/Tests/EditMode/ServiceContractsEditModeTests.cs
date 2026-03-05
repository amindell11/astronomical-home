using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Services;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode tests validating the game service contracts and basic behavior.
    /// No scene loading required. MonoBehaviour services use a temp GameObject.
    /// </summary>
    [Category("Services")]
    public class ServiceContractsEditModeTests
    {
        private readonly List<GameObject> tempObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in tempObjects)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            tempObjects.Clear();
        }

        private T CreateMonoBehaviourService<T>() where T : MonoBehaviour
        {
            var go = new GameObject($"Test_{typeof(T).Name}");
            tempObjects.Add(go);
            return go.AddComponent<T>();
        }

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
            var unit = CreateMonoBehaviourService<UnitService>();
            var obj = CreateMonoBehaviourService<ObjectiveService>();
            var env = new EnvironmentService();
            var cam = new CameraService();

            Assert.Throws<ArgumentNullException>(() => new GameServices(null, env, obj, cam));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, null, obj, cam));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, env, null, cam));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, env, obj, null));
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
            var svc = CreateMonoBehaviourService<UnitService>();
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
        public void ObjectiveService_Implements_IObjectiveTrackerAdapter()
        {
            Assert.IsTrue(typeof(IObjectiveTrackerAdapter).IsAssignableFrom(typeof(ObjectiveService)));
        }

        [Test]
        public void ObjectiveService_SetObjective_CreatesTracker_AndForwardsEvents()
        {
            var svc = CreateMonoBehaviourService<ObjectiveService>();
            var key = new StubKeyTracker(false);
            var paramsAsset = ScriptableObject.CreateInstance<ObjectiveParams>();

            try
            {
                var builders = new Dictionary<string, Func<ObjectiveState>>
                {
                    ["explore"] = () => new ExploreState(key),
                    ["key"] = () => new KeyAcquiredState(),
                    ["extraction"] = () => new ExtractionChallengeState(
                        () => Vector3.zero,
                        () => Vector3.zero,
                        () => false,
                        paramsAsset.ExtractionRadius),
                    ["extracted"] = () => new ExtractedState(),
                    ["failed"] = () => new FailedState()
                };

                svc.SetObjective(MissionDefinition.CreateDefault(), builders, () => true);

                Assert.IsNotNull(svc.CurrentTracker);
                Assert.AreEqual(ObjectiveType.Explore, svc.CurrentState);

                var transitions = new List<(ObjectiveType from, ObjectiveType to)>();
                svc.OnStateChanged += (f, t) => transitions.Add((f, t));

                key.HasKey = true;
                svc.CurrentTracker.Tick(0.1f); // Explore → KeyAcquired
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
            var svc = CreateMonoBehaviourService<ObjectiveService>();
            Assert.IsNull(svc.CurrentTracker);
            Assert.IsNull(svc.CurrentState);

            var paramsAsset = ScriptableObject.CreateInstance<ObjectiveParams>();
            try
            {
                var builders = new Dictionary<string, Func<ObjectiveState>>
                {
                    ["explore"] = () => new ExploreState(new StubKeyTracker(false)),
                    ["key"] = () => new KeyAcquiredState(),
                    ["extraction"] = () => new ExtractionChallengeState(
                        () => Vector3.zero, () => Vector3.zero, () => false,
                        paramsAsset.ExtractionRadius),
                    ["extracted"] = () => new ExtractedState(),
                    ["failed"] = () => new FailedState()
                };

                svc.SetObjective(MissionDefinition.CreateDefault(), builders, () => true);
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
            var svc = CreateMonoBehaviourService<ObjectiveService>();
            var key = new StubKeyTracker(false);
            var alive = true;
            var paramsAsset = ScriptableObject.CreateInstance<ObjectiveParams>();

            try
            {
                var builders = new Dictionary<string, Func<ObjectiveState>>
                {
                    ["explore"] = () => new ExploreState(key),
                    ["key"] = () => new KeyAcquiredState(),
                    ["extraction"] = () => new ExtractionChallengeState(
                        () => Vector3.zero, () => Vector3.zero, () => false,
                        paramsAsset.ExtractionRadius),
                    ["extracted"] = () => new ExtractedState(),
                    ["failed"] = () => new FailedState()
                };

                svc.SetObjective(MissionDefinition.CreateDefault(), builders, () => alive);

                alive = false;
                svc.CurrentTracker.Tick(0.1f);
                Assert.AreEqual(ObjectiveType.Failed, svc.CurrentState);

                alive = true;
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
            Assert.IsNotNull(type.GetProperty("Cameras"), "Missing Cameras");
            Assert.IsNotNull(type.GetMethod("Initialize"), "Missing Initialize");
            Assert.IsNotNull(type.GetMethod("AddCamera"), "Missing AddCamera");
            Assert.IsNotNull(type.GetMethod("RemoveCamera"), "Missing RemoveCamera");
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
            Assert.IsNull(svc.Cameras);
        }

        // ── GameServices.ClearAll ────────────────────────────────────────────────

        [Test]
        public void GameServices_ClearAll_ClearsAllServices()
        {
            var unit = CreateMonoBehaviourService<UnitService>();
            var env = new EnvironmentService();
            var obj = CreateMonoBehaviourService<ObjectiveService>();
            var cam = new CameraService();
            var services = new GameServices(unit, env, obj, cam);

            Assert.DoesNotThrow(() => services.ClearAll());
        }

        // ── Test stubs ───────────────────────────────────────────────────────────

        private sealed class StubKeyTracker : IKeyTracker
        {
            public bool HasKey;
            public StubKeyTracker(bool hasKey) => HasKey = hasKey;
            public bool PlayerHasKey => HasKey;
        }
    }
}
