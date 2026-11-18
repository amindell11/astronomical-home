#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Damage;
using NUnit.Framework;
using Ships;
using Ships.Registry;
using Ships.Visuals.Breakup;
using Substrate;
using Substrate.Sectors.Factions;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UI.PlayerState;
using UnityEditor;
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
            var (faction, chassis) = NewFaction(TestAssets.LoadCrimsonPrefab());
            var ship = Spawn(chassis);

            faction.Dress(ship, new FactionRank(chassis, null, null, null, faction.signature.hull));
            yield return null;

            CollectionAssert.AreEqual(faction.signature.hull, HullSlots(ship));
            AssertFlame(ship, faction.flame);
            Assert.AreEqual(faction.shieldRing, ship.VisualRig.GetComponentInChildren<ShieldUI>(true)
                .GetComponent<Image>().material.GetColor(RingColor));
        }

        [UnityTest]
        public IEnumerator Dress_TheEliteWearsItsWholeSetAndFlame()
        {
            var (faction, chassis) = NewFaction(TestAssets.LoadCrimsonPrefab());
            var ship = Spawn(chassis);

            faction.DressElite(ship);
            yield return null;

            CollectionAssert.AreEqual(faction.signature.eliteHull, HullSlots(ship));
            AssertFlame(ship, faction.eliteFlame);
        }

        [UnityTest]
        public IEnumerator Death_ACrimsonWreckWearsTheSkin_AndTakesItsScorchedCopiesWithIt([Values(false, true)] bool elite)
        {
            var (faction, chassis) = NewFaction(TestAssets.LoadCrimsonPrefab());
            foreach (var material in faction.signature.hull)
                if (material.HasColor("_PaintRegionColor3"))
                    material.SetColor("_PaintRegionColor3", Color.magenta);
            foreach (var material in faction.signature.eliteHull)
                if (material.HasColor("_PaintRegionColor3"))
                    material.SetColor("_PaintRegionColor3", Color.cyan);

            var ship = Spawn(chassis);
            if (elite) faction.DressElite(ship);
            else faction.Dress(ship, new FactionRank(chassis, null, null, null, faction.signature.hull));
            yield return null;
            ship.Damage.TakeDamage(new DamageInfo(
                100000f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, ship.transform.position));

            var debris = Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None).Single();
            var marker = elite ? Color.cyan : Color.magenta;
            Material skin = null;
            var seen = 0;
            foreach (var renderer in debris.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material.HasColor("_PaintRegionColor3")) continue;
                    Assert.AreEqual(marker, material.GetColor("_PaintRegionColor3"));
                    skin = material;
                    seen++;
                }
            Assert.Greater(seen, 0);

            yield return new WaitForSeconds(2f);
            Assert.That(debris == null, Is.True);
            Assert.That(skin == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Death_AValisWreckWearsTheSkinItself()
        {
            var chassis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            var (faction, _) = NewFaction(chassis);
            var ship = Spawn(chassis);
            faction.Dress(ship, new FactionRank(chassis, null, null, null, faction.signature.hull));
            yield return null;
            ship.Damage.TakeDamage(new DamageInfo(
                100000f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, ship.transform.position));

            var authored = HullSlots(chassis);
            var worn = faction.signature.hull;
            var debris = Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None).Single();
            var woreSkin = false;
            foreach (var renderer in debris.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    foreach (var slot in authored)
                        Assert.AreNotSame(slot, material);
                    foreach (var slot in worn)
                        if (ReferenceEquals(slot, material)) woreSkin = true;
                }
            Assert.IsTrue(woreSkin);
        }

        private (Faction faction, Ship chassis) NewFaction(Ship chassis)
        {
            var slots = chassis.VisualRig.Silhouette.SelectMany(surface => surface.sharedMaterials).ToArray();
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.signature = new FactionShip
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
