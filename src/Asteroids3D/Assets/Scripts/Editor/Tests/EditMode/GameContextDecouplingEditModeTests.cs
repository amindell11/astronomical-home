using System.IO;
using System.Reflection;
using AI;
using Combat.Targeting;
using Game;
using NUnit.Framework;
using Player;
using Ships;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Core")]
    public class GameContextDecouplingEditModeTests
    {
        [Test]
        public void PlayerInputReader_ScreenProjectorCanBeReconfigured()
        {
            var reader = new PlayerInputReader(_ => new Vector3(1f, 2f, 3f));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), reader.GetMouseWorldPosition());

            reader.SetScreenToGamePlane(_ => new Vector3(9f, 8f, 7f));
            Assert.AreEqual(new Vector3(9f, 8f, 7f), reader.GetMouseWorldPosition());
        }
        
        [Test]
        public void AiCommander_ExposesRegistryInjectionApi()
        {
            var method = typeof(AICommander).GetMethod("SetRegistry", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method);
            var parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(1));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(IShipRegistry)));
        }

        [Test]
        public void LockOnSensor_RegistryFlagReflectsInjection()
        {
            var go = new GameObject("Targeting");
            try
            {
                var targeting = go.AddComponent<LockOnSensor>();
                Assert.IsFalse(targeting.HasRegistry);

                targeting.SetRegistry(new StubShipRegistry());
                Assert.IsTrue(targeting.HasRegistry);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LockOnSensor_SourceStopsScanWhenRegistryIsCleared()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Combat", "Targeting", "LockOnSensor.cs"));
            StringAssert.Contains("StopScanRoutine();", source);
            StringAssert.Contains("StartScanRoutineIfNeeded();", source);
        }

        [Test]
        public void MainGameManager_UsesSerializedPlaneAxis()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Game", "Bootstrap", "MainGameManager.cs"));
            StringAssert.Contains("[SerializeField] private PlaneAxis planeAxis", source);
            StringAssert.Contains("GamePlane.Configure(planeAxis, planeOrigin);", source);
            StringAssert.DoesNotContain("GameContext.Instance", source);
        }

        [Test]
        public void PlayerRig_OwnsOverlayLifecycleViaUIService()
        {
            // The overlay lifecycle moved UP to the session-tier rig (Stage 3): the rig builds it via
            // UIService and hands it the player's narrow read surfaces (HudBinding), never the Ship.
            // Teardown is via services.ClearAll() on session exit.
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Player", "PlayerRig.cs"));
            StringAssert.Contains("services.UIService.Show(overlay, uiCam);", source);
            StringAssert.Contains("overlay.Initialize(new HudBinding(", source);
        }

        [Test]
        public void TurboLaserPrefab_DoesNotReferenceEditorOnlyDiagnosticsScript()
        {
            var prefab = File.ReadAllText(Path.Combine(Application.dataPath, "Prefabs", "Weapons", "TurboLaser.prefab"));
            StringAssert.DoesNotContain("5c7f1d5a3e1e486abec74ce5bb7c4d16", prefab);
        }

        [Test]
        public void RuntimeGameScripts_DoNotUseGameContextSingleton()
        {
            var assetsPath = Application.dataPath;
            var files = new[]
            {
                Path.Combine(assetsPath, "Scripts", "Game", "Bootstrap", "MainGameManager.cs"),
                Path.Combine(assetsPath, "Scripts", "AI", "AICommander.cs"),
                Path.Combine(assetsPath, "Scripts", "Combat", "Targeting", "LockOnSensor.cs"),
                Path.Combine(assetsPath, "Scripts", "Player", "PlayerCommander.cs"),
                Path.Combine(assetsPath, "Scripts", "Asteroids", "AsteroidController.cs")
            };

            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                StringAssert.DoesNotContain("GameContext.Instance", source, file);
                StringAssert.DoesNotContain("GameContext.SingletonOrNull", source, file);
            }
        }

    }
}
