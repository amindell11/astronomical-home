#if UNITY_EDITOR
using System.Collections;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Audio;
using Ships.Presentation;
using Substrate.Presentation;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    /// <summary>
    /// The unit service governs ship presentation the way the projectile service governs transients:
    /// what it wires is darkened or lit whole, weapon mounts included. Headless leaves no renderer,
    /// audio, particle or part live under the ship, so its damage and death play no one-shot —
    /// paired with the presenting case, without which a rig whose audio never went live would pass.
    /// </summary>
    [Category("Presentation")]
    public class ShipSpawnPresentationPlayModeTests : PlayModeWorldFixture
    {
        private const string VanguardPath = "Assets/Prefabs/Ships/Vanguard.prefab";
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
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator HeadlessSpawn_DarkensShipAndMounts()
        {
            var ship = SpawnShip(presentationEnabled: false);
            var beam = ReequipToRailgunAndMissiles(ship);
            yield return null;

            var rig = ship.GetComponentInChildren<ShipVisualRig>(true);
            Assert.IsNotNull(rig, "test premise: Vanguard embeds a visual rig");
            Assert.IsFalse(rig.gameObject.activeInHierarchy, "the rig subtree stays deactivated");

            foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(renderer.enabled, $"renderer '{renderer.name}' still enabled");
            foreach (var audio in ship.GetComponentsInChildren<EngineAudio>(true))
                Assert.IsFalse(audio.isActiveAndEnabled, $"engine audio '{audio.name}' still live");
            foreach (var audio in ship.GetComponentsInChildren<ShipDamageAudio>(true))
                Assert.IsFalse(audio.isActiveAndEnabled, $"damage audio '{audio.name}' still live");
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
            var engineAudio = ship.GetComponentInChildren<EngineAudio>(true);
            var damageAudio = ship.GetComponentInChildren<ShipDamageAudio>(true);
            Assert.IsTrue(engineAudio && damageAudio, "test premise: Vanguard embeds engine and damage audio");
            Assert.IsTrue(engineAudio.isActiveAndEnabled, "a presenting ship's engine audio is live");
            Assert.IsTrue(damageAudio.isActiveAndEnabled, "a presenting ship's damage audio hears its hits and death");
            Assert.IsTrue(beam.enabled, "the mounted beam visual listens for shots");
            Assert.IsFalse(beam.GetComponent<LineRenderer>().enabled,
                "the beam owns its line against the sweep: only a charge or a shot lights it");
        }

        // Re-equip mid-life is the moment the seam must reach a ship's swapped-in mounts.
        private RailBeamVisual ReequipToRailgunAndMissiles(Ship ship)
        {
            var railgun = AssetDatabase.LoadAssetAtPath<WeaponComponent>(RailgunPath);
            var missiles = AssetDatabase.LoadAssetAtPath<WeaponComponent>(MissilesPath);
            Assert.IsTrue(railgun && missiles, "test premise: both mount prefabs load");

            ship.Reequip(railgun, missiles);
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

            var prefab = AssetDatabase.LoadAssetAtPath<Ship>(VanguardPath);
            Assert.IsNotNull(prefab, $"Vanguard prefab loads from {VanguardPath}");
            var ship = units.SpawnShip(prefab, null, team: 0, Vector3.zero, Quaternion.identity, Field);
            Assert.IsNotNull(ship, "the unit service spawned the ship");
            return ship;
        }
    }
}
#endif
