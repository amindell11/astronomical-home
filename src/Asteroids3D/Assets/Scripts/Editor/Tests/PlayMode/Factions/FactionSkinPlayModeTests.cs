#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships;
using Substrate;
using Substrate.Sectors.Factions;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Factions
{
    /// <summary>A faction skin on a spawned Crimson: its hull wears the faction's set slot for slot, its four thrust systems the faction flame at a white start colour, and its shield ring the faction colour; the elite wears its own set and flame.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class FactionSkinPlayModeTests : PlayModeWorldFixture
    {
        private static readonly int RingColor = Shader.PropertyToID("_Color");

        private UnitService _unitService;
        private readonly List<Object> _created = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            var unitServiceGO = Track(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();
            ShipServices.Compose(_unitService, unitServiceGO.transform, presentationEnabled: true);
        }

        [TearDown]
        public override void TearDown()
        {
            if (_unitService) _unitService.Clear();
            foreach (var item in _created)
                if (item) Object.DestroyImmediate(item);
            _created.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Dress_PutsTheFactionSetOnTheHull_TheFlameOnTheThrust_AndTheColourOnTheRing()
        {
            var (faction, chassis) = NewFaction();
            var ship = Spawn(chassis);

            faction.Dress(chassis, ship, elite: false);
            yield return null;

            CollectionAssert.AreEqual(faction.common.hull, HullSlots(ship));
            AssertFlame(ship, faction.flame);
            Assert.AreEqual(faction.shieldRing, ship.VisualRig.GetComponentInChildren<ShieldUI>(true)
                .GetComponent<Image>().material.GetColor(RingColor));
        }

        [UnityTest]
        public IEnumerator Dress_TheEliteWearsItsWholeSetAndFlame()
        {
            var (faction, chassis) = NewFaction();
            var ship = Spawn(chassis);

            faction.Dress(chassis, ship, elite: true);
            yield return null;

            CollectionAssert.AreEqual(faction.common.eliteHull, HullSlots(ship));
            AssertFlame(ship, faction.eliteFlame);
        }

        private (Faction faction, Ship chassis) NewFaction()
        {
            var chassis = TestAssets.LoadCrimsonPrefab();
            var slots = chassis.VisualRig.Silhouette.SelectMany(surface => surface.sharedMaterials).ToArray();
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.common = new FactionShip
            {
                ship = chassis,
                hull = slots.Select(slot => Track(new Material(slot))).ToArray(),
                eliteHull = slots.Select(slot => Track(new Material(slot))).ToArray(),
            };
            faction.flame = Track(new Material(slots[0]));
            faction.eliteFlame = Track(new Material(slots[0]));
            faction.shieldRing = Color.magenta;
            return (faction, chassis);
        }

        private Ship Spawn(Ship chassis)
        {
            var ship = _unitService.SpawnShip(chassis, null, 1, GamePlane.PlanePointToWorld(new Vector2(500f, 500f)),
                GamePlane.Rotation, null);
            Track(ship.gameObject);
            return ship;
        }

        private static Material[] HullSlots(Ship ship) =>
            ship.VisualRig.Silhouette.SelectMany(surface => surface.sharedMaterials).ToArray();

        private static void AssertFlame(Ship ship, Material flame)
        {
            var thrust = ship.VisualRig.GetComponentsInChildren<ParticleSystem>(true)
                .Where(system => system.name is "ThrustMain" or "ThrustSmall").ToArray();
            Assert.AreEqual(4, thrust.Length, "test premise: two main and two small thrust systems");
            foreach (var system in thrust)
            {
                Assert.AreSame(flame, system.GetComponent<ParticleSystemRenderer>().sharedMaterial, system.name);
                Assert.AreEqual(Color.white, system.main.startColor.color, system.name);
            }
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }
    }
}
#endif
