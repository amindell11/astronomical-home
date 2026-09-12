#if UNITY_EDITOR
using System.Collections;
using Audio;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Substrate.Presentation;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// The unit service governs ship presentation the way the projectile service governs transients:
    /// what it wires is darkened or lit whole, weapon mounts included. Headless leaves no renderer,
    /// audio source, particle or part live under the ship, and no death one-shot reaches the pool —
    /// paired with the presenting case, without which a ship that simply never died would pass.
    /// </summary>
    [Category("Presentation")]
    public class ShipSpawnPresentationPlayModeTests : PlayModeWorldFixture
    {
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";
        private const string RailgunPath = "Assets/Prefabs/Weapons/Railgun.prefab";
        private const string MissilesPath = "Assets/Prefabs/Weapons/Missiles.prefab";

        private GameObject servicesHost;
        private UnitService units;

        public override void TearDown()
        {
            if (units) units.Clear();
            units = null;
            DestroyTestObject(servicesHost);
            servicesHost = null;
            DestroyPooledAudio();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator HeadlessSpawn_DarkensShipAndMounts()
        {
            var ship = SpawnShip(presentationEnabled: false);
            var beam = ReequipToRailgunAndMissiles(ship);
            yield return null;

            var rig = ship.GetComponentInChildren<ShipVisualRig>(true);
            Assert.IsNotNull(rig, "test premise: Ship_1 embeds a visual rig");
            Assert.IsFalse(rig.gameObject.activeInHierarchy, "the rig subtree stays deactivated");

            foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(renderer.enabled, $"renderer '{renderer.name}' still enabled");
            foreach (var source in ship.GetComponentsInChildren<AudioSource>(true))
                Assert.IsFalse(source.enabled, $"audio source '{source.name}' still enabled");
            foreach (var ps in ship.GetComponentsInChildren<ParticleSystem>(true))
                Assert.IsFalse(ps.isPlaying, $"particle system '{ps.name}' still simulating");
            foreach (var part in ship.GetComponentsInChildren<IPresentationPart>(true))
                Assert.IsFalse(((Behaviour)part).isActiveAndEnabled,
                    $"part '{part.GetType().Name}' still live");

            Assert.IsFalse(beam.enabled, "the mounted beam visual is darkened through the ship's seam");
            Assert.IsFalse(beam.GetComponent<LineRenderer>().enabled, "the beam's own line stays off");
        }

        [UnityTest]
        public IEnumerator PresentingSpawn_LightsTheShip_AndLeavesTheBeamUnlit()
        {
            var ship = SpawnShip(presentationEnabled: true);
            var beam = ReequipToRailgunAndMissiles(ship);
            yield return null;

            var rig = ship.GetComponentInChildren<ShipVisualRig>(true);
            Assert.IsTrue(rig.gameObject.activeInHierarchy, "test premise: a presenting rig stays live");
            Assert.IsTrue(beam.enabled, "the mounted beam visual listens for shots");
            Assert.IsFalse(beam.GetComponent<LineRenderer>().enabled,
                "the beam owns its line against the sweep: only a shot lights it");
        }

        [UnityTest]
        public IEnumerator HeadlessShipDeath_ChecksOutNoPooledAudio()
        {
            var ship = SpawnShip(presentationEnabled: false);
            yield return null;

            var before = ActivePooledAudioCount();
            TestDamage.Kill(ship);
            yield return null;

            Assert.AreEqual(before, ActivePooledAudioCount(),
                "a headless ship's death must pool no one-shot");
        }

        [UnityTest]
        public IEnumerator PresentingShipDeath_ChecksOutPooledAudio()
        {
            var ship = SpawnShip(presentationEnabled: true);
            yield return null;

            var before = ActivePooledAudioCount();
            TestDamage.Kill(ship);
            yield return null;

            Assert.Greater(ActivePooledAudioCount(), before,
                "test premise: a presenting ship's death plays its pooled death clip");
        }

        // Re-equip mid-life is the moment the seam must reach a ship's swapped-in mounts.
        private RailBeamVisual ReequipToRailgunAndMissiles(Ship ship)
        {
            var railgun = AssetDatabase.LoadAssetAtPath<WeaponComponent>(RailgunPath);
            var missiles = AssetDatabase.LoadAssetAtPath<WeaponComponent>(MissilesPath);
            Assert.IsTrue(railgun && missiles, "test premise: both mount prefabs load");

            ship.Reequip(null, null, railgun, missiles);
            units.WireShipDependencies(ship, Field);

            var beam = ship.GetComponentInChildren<RailBeamVisual>(true);
            Assert.IsNotNull(beam, "test premise: the railgun mount carries the beam visual");
            return beam;
        }

        private Ship SpawnShip(bool presentationEnabled)
        {
            servicesHost = new GameObject("[TestServices]");
            units = servicesHost.AddComponent<UnitService>();
            ShipServices.Compose(units, servicesHost.transform, presentationEnabled);

            var prefab = AssetDatabase.LoadAssetAtPath<Ship>(Ship1Path);
            Assert.IsNotNull(prefab, $"Ship_1 prefab loads from {Ship1Path}");
            var ship = units.SpawnShip(prefab, null, team: 0, Vector3.zero, Quaternion.identity, Field);
            Assert.IsNotNull(ship, "the unit service spawned the ship");
            return ship;
        }

        private static int ActivePooledAudioCount()
        {
            var count = 0;
            foreach (var source in Object.FindObjectsByType<PooledAudioSource>(FindObjectsSortMode.None))
                if (source.gameObject.activeInHierarchy) count++;
            return count;
        }

        // The pool's Clear destroys only stacked instances; a checked-out one-shot would outlive this fixture.
        private static void DestroyPooledAudio()
        {
            foreach (var source in Object.FindObjectsByType<PooledAudioSource>(FindObjectsSortMode.None))
                if (source.gameObject.activeInHierarchy) Object.DestroyImmediate(source.gameObject);
            SimplePool<PooledAudioSource>.Clear();
        }
    }
}
#endif
