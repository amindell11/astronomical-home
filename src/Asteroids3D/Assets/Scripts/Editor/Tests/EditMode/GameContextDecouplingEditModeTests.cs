using System.IO;
using System.Reflection;
using AI;
using Combat.Targeting;
using Game;
using NUnit.Framework;
using Player;
using Ships;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Regression")]
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
        public void TargetingComputer_RegistryFlagReflectsInjection()
        {
            var go = new GameObject("Targeting");
            try
            {
                var targeting = go.AddComponent<TargetingComputer>();
                Assert.IsFalse(targeting.HasRegistry);

                targeting.SetRegistry(new StubRegistry());
                Assert.IsTrue(targeting.HasRegistry);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TargetingComputer_SourceStopsScanWhenRegistryIsCleared()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Combat", "Targeting", "TargetingComputer.cs"));
            StringAssert.Contains("StopScanRoutine();", source);
            StringAssert.Contains("StartScanRoutineIfNeeded();", source);
        }

        [Test]
        public void ShipRespawnRunner_SourceSupportsReinitializeAcrossSessions()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "ShipRespawnRunner.cs"));
            StringAssert.Contains("UnbindCurrentRegistry();", source);
            StringAssert.Contains("public void ResetRunner()", source);
            StringAssert.Contains("StopAllCoroutines();", source);
            StringAssert.DoesNotContain("if (isInitialized)", source);
        }

        [Test]
        public void Spawner_SourceKeepsRespawnPositionInWorldSpace()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Ships", "Spawner.cs"));
            StringAssert.Contains("GamePlane.ProjectOntoPlane(pos) + GamePlane.Origin", source);
            StringAssert.DoesNotContain("GamePlane.WorldPointToPlane(pos)", source);
        }

        [Test]
        public void MainGameManager_ResetsRespawnRunnerOnRestart()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Game", "Bootstrap", "MainGameManager.cs"));
            StringAssert.Contains("respawnRunner.ResetRunner();", source);
        }

        [Test]
        public void MainGameManager_UsesSerializedReferencePlaneAndRespawnRunner()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Game", "Bootstrap", "MainGameManager.cs"));
            StringAssert.Contains("[SerializeField] private Transform referencePlane;", source);
            StringAssert.Contains("[SerializeField] private ShipRespawnRunner respawnRunner;", source);
            StringAssert.Contains("GamePlane.SetReferencePlane(referencePlane);", source);
            StringAssert.DoesNotContain("GameContext.Instance", source);
        }

        [Test]
        public void PlaySector_OwnsOverlayLifecycleViaUIService()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Game", "Sectors", "PlaySector.cs"));
            StringAssert.Contains("Services.UIService.Show(overlay, observer.Cam);", source);
            StringAssert.Contains("overlay.Initialize(player);", source);
            StringAssert.Contains("Services.UIService.Clear();", source);
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
                Path.Combine(assetsPath, "Scripts", "Combat", "Targeting", "TargetingComputer.cs"),
                Path.Combine(assetsPath, "Scripts", "Player", "PlayerCommander.cs"),
                Path.Combine(assetsPath, "Scripts", "Asteroids", "AsteroidController.cs"),
                Path.Combine(assetsPath, "Scripts", "Ships", "Spawner.cs")
            };

            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                StringAssert.DoesNotContain("GameContext.Instance", source, file);
                StringAssert.DoesNotContain("GameContext.SingletonOrNull", source, file);
            }
        }

        private sealed class StubRegistry : IShipRegistry
        {
            public bool TryGetShipId(Collider collider, out ShipId id)
            {
                id = ShipId.Invalid;
                return false;
            }

            public bool TryGetShip(ShipId id, out Ship ship)
            {
                ship = null;
                return false;
            }

            public bool TryGetShip(Collider collider, out Ship ship, ShipId? excludeId = null)
            {
                ship = null;
                return false;
            }

            public bool IsFriendly(ShipId a, ShipId b) => false;
            public bool IsHostile(ShipId a, ShipId b) => false;
            public int GetTeam(ShipId id) => -1;
        }
    }
}
