#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Damage;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Registry;
using Ships.Visuals.Breakup;
using Substrate;
using Substrate.Sectors.Factions;
using Substrate.Services;
using Substrate.Services.Locales;
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
    /// <summary>A faction skin on a spawned Crimson: its hull wears the faction's set slot for slot, its four thrust systems the faction flame at a white start colour, and its shield ring the faction colour; the elite wears its own set and flame. Shoal's ranks wear the palette their locale maps, slot by region, and their wrecks wear it too.</summary>
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

            Wear(faction, ship, elite: false);
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

            Wear(faction, ship, elite: true);
            yield return null;

            CollectionAssert.AreEqual(faction.elite.hull, HullSlots(ship));
            AssertFlame(ship, faction.eliteFlame);
        }

        [UnityTest]
        public IEnumerator Death_ACrimsonWreckWearsTheSkin_AndTakesItsScorchedCopiesWithIt([Values(false, true)] bool elite)
        {
            var (faction, chassis) = NewFaction(TestAssets.LoadCrimsonPrefab());
            foreach (var material in faction.signature.hull)
                if (material.HasColor("_PaintRegionColor3"))
                    material.SetColor("_PaintRegionColor3", Color.magenta);
            foreach (var material in faction.elite.hull)
                if (material.HasColor("_PaintRegionColor3"))
                    material.SetColor("_PaintRegionColor3", Color.cyan);

            var ship = Spawn(chassis);
            Wear(faction, ship, elite);
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
            Wear(faction, ship, elite: false);
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

        [UnityTest]
        public IEnumerator Dress_ShoalRanksWearTheirLocalesPalette_ByRegion(
            [Values("BlueCrescent", "CoralSunset", "CyanAurora")] string localeName,
            [Values("rival", "minion", "elite")] string rank)
        {
            var shoal = LoadShoal();
            var locale = LoadLocale(localeName);
            var chassis = RankShip(shoal, rank);
            var ship = Spawn(chassis);

            DressRank(shoal, ship, rank, locale);
            yield return null;

            var authored = HullSlots(chassis);
            var worn = HullSlots(ship);
            if (localeName != "CoralSunset")
            {
                CollectionAssert.AreEqual(authored, worn, "rust and unmapped locales keep the chassis's own paint");
                yield break;
            }

            var shoalSet = RankPalettes(shoal, rank).Regions("shoal").ToDictionary(region => region.name);
            var swapped = 0;
            for (var i = 0; i < authored.Length; i++)
            {
                if (!shoalSet.TryGetValue(authored[i].name, out var region))
                {
                    Assert.AreSame(authored[i], worn[i], $"slot {i} '{authored[i].name}' is no region");
                    continue;
                }
                Assert.AreSame(region, worn[i], $"slot {i} '{authored[i].name}'");
                swapped++;
            }
            Assert.Greater(swapped, 0);
        }

        [UnityTest]
        public IEnumerator Death_AShoalWreckWearsItsLocalesPalette()
        {
            var shoal = LoadShoal();
            var ship = Spawn(shoal.signature.ship);
            DressRank(shoal, ship, "rival", LoadLocale("CoralSunset"));
            yield return null;
            ship.Damage.TakeDamage(new DamageInfo(
                100000f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, ship.transform.position));

            var rust = shoal.signature.palettes.Regions("shoal-rust");
            var shoalSet = shoal.signature.palettes.Regions("shoal");
            var debris = Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None).Single();
            var woreShoal = false;
            foreach (var renderer in debris.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    CollectionAssert.DoesNotContain(rust, material);
                    if (shoalSet.Any(region => region.name == material.name)) woreShoal = true;
                }
            Assert.IsTrue(woreShoal);
        }

        private static Faction LoadShoal()
        {
            var shoal = AssetDatabase.LoadAssetAtPath<Faction>("Assets/Settings/Factions/Shoal.asset");
            Assert.IsNotNull(shoal, "Shoal faction loads");
            return shoal;
        }

        private static LocaleSky LoadLocale(string localeName)
        {
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/{localeName}.prefab");
            Assert.IsNotNull(locale, $"{localeName} locale loads");
            return locale;
        }

        private static Ship RankShip(Faction faction, string rank) => rank switch
        {
            "rival" => faction.signature.ship,
            "minion" => faction.minion.ship,
            _ => faction.elite.ship,
        };

        private static ShipPalettes RankPalettes(Faction faction, string rank) => rank switch
        {
            "rival" => faction.signature.palettes,
            "minion" => faction.minion.palettes,
            _ => faction.elite.palettes,
        };

        private static void DressRank(Faction faction, Ship ship, string rank, LocaleSky locale)
        {
            switch (rank)
            {
                case "rival": faction.Dress(ship, faction.Rival, locale); break;
                case "minion": faction.Dress(ship, faction.Minion, locale); break;
                default: faction.DressElite(ship, locale); break;
            }
        }

        private (Faction faction, Ship chassis) NewFaction(Ship chassis)
        {
            var slots = chassis.VisualRig.Silhouette.SelectMany(surface => surface.sharedMaterials).ToArray();
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.signature = new FactionShip
            {
                ship = chassis,
                hull = slots.Select(slot => Track(new Material(slot))).ToArray(),
            };
            faction.minion = new FactionMinion { ship = chassis };
            faction.elite = new FactionElite
            {
                ship = chassis,
                hull = slots.Select(slot => Track(new Material(slot))).ToArray(),
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

        private static void Wear(Faction faction, Ship ship, bool elite)
        {
            if (elite)
                faction.DressElite(ship, null);
            else
                faction.Dress(ship, new FactionRank(faction.signature.ship, null, null, ShipScale.One, faction.signature.hull), null);
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
