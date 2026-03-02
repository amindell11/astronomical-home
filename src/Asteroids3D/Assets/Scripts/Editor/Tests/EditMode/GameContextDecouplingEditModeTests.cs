using System.IO;
using Combat.Targeting;
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
        public void Spawner_GetRandomOffscreenPosition_UsesWorldCenterProvider()
        {
            var settings = ScriptableObject.CreateInstance<ShipSpawnerSettings>();
            settings.offscreenDistance = 0f;

            var anchorGo = new GameObject("Anchor");
            var called = 0;
            try
            {
                var spawner = new Spawner(settings, () =>
                {
                    called++;
                    return anchorGo.transform;
                });

                _ = spawner.GetRandomOffscreenPosition();
                Assert.That(called, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(anchorGo);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void AiCommander_SourceUsesInjectedRegistryInsteadOfGameContextLookup()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "AI", "AICommander.cs"));
            StringAssert.Contains("SetRegistry", source);
            StringAssert.DoesNotContain("GameContext.SingletonOrNull", source);
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
        public void RuntimeGameScripts_DoNotUseGameContextSingleton()
        {
            var assetsPath = Application.dataPath;
            var files = new[]
            {
                Path.Combine(assetsPath, "Scripts", "Game", "GameInitiator.cs"),
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
