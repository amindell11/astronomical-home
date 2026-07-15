using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Services;
using Movement.MPC.Field;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Game service contract-shape and basic-behavior tests; no scene loading required.</summary>
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

        [Test]
        public void IGameServices_Exposes_AllServices()
        {
            var type = typeof(IGameServices);
            Assert.IsNotNull(type.GetProperty("UnitService"), "Missing UnitService");
            Assert.IsNotNull(type.GetProperty("Projectiles"), "Missing Projectiles");
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

            var ui = new UIService();
            var proj = new ProjectileService();
            var arena = TestArena.On(CreateMonoBehaviourService<NavFieldService>().gameObject);

            Assert.Throws<ArgumentNullException>(() => new GameServices(null, proj, env, obj, cam, ui, arena));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, null, env, obj, cam, ui, arena));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, proj, null, obj, cam, ui, arena));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, proj, env, null, cam, ui, arena));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, proj, env, obj, null, ui, arena));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, proj, env, obj, cam, null, arena));
            Assert.Throws<ArgumentNullException>(() => new GameServices(unit, proj, env, obj, cam, ui, null));
        }

        [Test]
        public void IUnitService_HasRequiredMembers()
        {
            var type = typeof(IUnitService);
            Assert.IsNotNull(type.GetProperty("Registry"), "Missing Registry");
            Assert.IsNotNull(type.GetProperty("ActiveRegistry"), "Missing ActiveRegistry");
            Assert.IsNotNull(type.GetMethod("SpawnShip"), "Missing SpawnShip");
            Assert.IsNotNull(type.GetMethod("SetProjectiles"), "Missing SetProjectiles");
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

        [Test]
        public void IEnvironmentService_HasRequiredMembers()
        {
            var type = typeof(IEnvironmentService);
            Assert.IsNotNull(type.GetProperty("World"), "Missing World");
            Assert.IsNotNull(type.GetProperty("WorldFollowerTransform"), "Missing WorldFollowerTransform");
            Assert.IsNotNull(type.GetMethod("ApplyLocaleAsync"), "Missing ApplyLocaleAsync");
            Assert.IsNotNull(type.GetMethod("RestoreBootEnvironmentAsync"), "Missing RestoreBootEnvironmentAsync");
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

        [Test]
        public void IObjectiveService_HasRequiredMembers()
        {
            var type = typeof(IObjectiveService);
            Assert.IsNotNull(type.GetProperty("SpineTracker"), "Missing SpineTracker");
            Assert.IsNotNull(type.GetProperty("SpineState"), "Missing SpineState");
            Assert.IsNotNull(type.GetProperty("SpineStep"), "Missing SpineStep");
            Assert.IsNotNull(type.GetProperty("SpineTarget"), "Missing SpineTarget");
            Assert.IsNotNull(type.GetMethod("SetSpineObjective"), "Missing SetSpineObjective");
            Assert.AreEqual(typeof(SpineObjectiveHandle), type.GetMethod("SetSpineObjective").ReturnType,
                "Spine mutation must flow through the ownership handle.");
            Assert.IsNull(type.GetMethod("SetSpineTarget"), "SetSpineTarget must not be ambient on the service.");
            Assert.IsNull(type.GetMethod("FailSpine"), "FailSpine must not be ambient on the service.");
            Assert.IsNull(type.GetMethod("RestartSpine"), "RestartSpine must not be ambient on the service.");
            Assert.IsNull(type.GetMethod("ClearSpine"), "ClearSpine must not be ambient on the service.");
            Assert.IsNotNull(type.GetEvent("OnSpineStateChanged"), "Missing OnSpineStateChanged");
            Assert.IsNotNull(type.GetEvent("OnSpineStepChanged"), "Missing OnSpineStepChanged");
            Assert.IsNotNull(type.GetEvent("OnSpineTargetChanged"), "Missing OnSpineTargetChanged");
            Assert.IsNotNull(type.GetMethod("OpenLocal"), "Missing OpenLocal");
            Assert.IsNotNull(type.GetProperty("Locals"), "Missing Locals");
            Assert.IsNotNull(type.GetEvent("OnLocalsChanged"), "Missing OnLocalsChanged");
            Assert.IsNotNull(type.GetMethod("ClearAll"), "Missing ClearAll");
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

        private static Dictionary<string, Func<ObjectiveState>> BuildDefaultBuilders(
            StubKeyTracker key = null, StubExtractionZone zone = null)
        {
            return new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(key ?? new StubKeyTracker(false)),
                ["key"] = () => new KeyAcquiredState(),
                ["extraction"] = () => new ExtractionChallengeState(zone ?? new StubExtractionZone(false)),
                ["extracted"] = () => new ExtractedState(),
                ["failed"] = () => new FailedState()
            };
        }

        [Test]
        public void ObjectiveService_SetSpineObjective_CreatesTracker_AndForwardsEvents()
        {
            var svc = CreateMonoBehaviourService<ObjectiveService>();
            var key = new StubKeyTracker(false);

            svc.SetSpineObjective(MissionDefinition.CreateDefault(), BuildDefaultBuilders(key));

            Assert.IsNotNull(svc.SpineTracker);
            Assert.AreEqual(ObjectiveType.Explore, svc.SpineState);

            var transitions = new List<(ObjectiveType from, ObjectiveType to)>();
            svc.OnSpineStateChanged += (f, t) => transitions.Add((f, t));

            key.HasKey = true;
            svc.SpineTracker.Tick(0.1f);
            Assert.AreEqual(1, transitions.Count);
            Assert.AreEqual(ObjectiveType.KeyAcquired, svc.SpineState);
        }

        [Test]
        public void ObjectiveService_HandleClose_RemovesTracker()
        {
            var svc = CreateMonoBehaviourService<ObjectiveService>();
            Assert.IsNull(svc.SpineTracker);
            Assert.IsNull(svc.SpineState);

            var handle = svc.SetSpineObjective(MissionDefinition.CreateDefault(), BuildDefaultBuilders());
            Assert.IsNotNull(svc.SpineTracker);
            Assert.AreSame(svc.SpineTracker, handle.Tracker);

            handle.Close();
            Assert.IsNull(svc.SpineTracker);
            Assert.IsNull(svc.SpineState);
        }

        [Test]
        public void ObjectiveService_HandleFailAndRestart_DelegateToTracker()
        {
            var svc = CreateMonoBehaviourService<ObjectiveService>();

            var handle = svc.SetSpineObjective(MissionDefinition.CreateDefault(), BuildDefaultBuilders());

            handle.Fail();
            Assert.AreEqual(ObjectiveType.Failed, svc.SpineState);

            handle.Restart();
            Assert.AreEqual(ObjectiveType.Explore, svc.SpineState);
        }

        [Test]
        public void ObjectiveService_AdapterEvents_FollowSpine_AcrossSpineCycles()
        {
            var svc = CreateMonoBehaviourService<ObjectiveService>();
            var adapter = (IObjectiveTrackerAdapter)svc;

            var transitions = 0;
            adapter.OnStateChanged += (f, t) => transitions++;

            var key = new StubKeyTracker(false);
            var handle = svc.SetSpineObjective(MissionDefinition.CreateDefault(), BuildDefaultBuilders(key));
            key.HasKey = true;
            svc.SpineTracker.Tick(0.1f);
            Assert.AreEqual(1, transitions);
            Assert.AreEqual(ObjectiveType.KeyAcquired, adapter.CurrentState);

            handle.Close();
            Assert.AreEqual(ObjectiveType.Explore, adapter.CurrentState);

            var nextKey = new StubKeyTracker(false);
            svc.SetSpineObjective(MissionDefinition.CreateDefault(), BuildDefaultBuilders(nextKey));
            nextKey.HasKey = true;
            svc.SpineTracker.Tick(0.1f);
            Assert.AreEqual(2, transitions,
                "Adapter subscription must survive a handle-close/SetSpineObjective cycle.");
        }

        [Test]
        public void ObjectiveService_HandleClose_DetachesForwarder_FromOldTracker()
        {
            var svc = CreateMonoBehaviourService<ObjectiveService>();
            var key = new StubKeyTracker(false);
            var handle = svc.SetSpineObjective(MissionDefinition.CreateDefault(), BuildDefaultBuilders(key));
            var oldTracker = svc.SpineTracker;

            var raised = 0;
            svc.OnSpineStateChanged += (f, t) => raised++;

            handle.Close();
            key.HasKey = true;
            oldTracker.Tick(0.1f);

            Assert.AreEqual(0, raised,
                "A cleared spine tracker must be detached from the service forwarder.");
        }

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
        public void CameraService_IsEmpty_AfterConstruction()
        {
            var svc = new CameraService();
            Assert.IsEmpty(svc.Cameras);
        }

        [Test]
        public void GameServices_ClearAll_ClearsAllServices()
        {
            var unit = CreateMonoBehaviourService<UnitService>();
            var env = new EnvironmentService();
            var obj = CreateMonoBehaviourService<ObjectiveService>();
            var cam = new CameraService();
            var arena = TestArena.On(CreateMonoBehaviourService<NavFieldService>().gameObject);
            var services = new GameServices(unit, new ProjectileService(), env, obj, cam, new UIService(), arena);

            Assert.DoesNotThrow(() => services.ClearAll());
        }

        private sealed class StubKeyTracker : IKeyTracker
        {
            public bool HasKey;
            public StubKeyTracker(bool hasKey) => HasKey = hasKey;
            public bool PlayerHasKey => HasKey;
        }

        private sealed class StubExtractionZone : IExtractionZone
        {
            public bool InZone;
            public StubExtractionZone(bool inZone) => InZone = inZone;
            public bool IsPlayerInZone => InZone;
        }
    }
}
