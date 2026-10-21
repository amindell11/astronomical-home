using System.Collections.Generic;
using Balance;
using Damage;
using Game.Player;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Registry;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Runs
{
    /// <summary>The spawn log off a real hull: an entry names the ship's parts and carries its loadout stat hash, times are measured from the run's begin, fate follows the run tally's rule, and the current player's own entry is left out.</summary>
    [Category("Bootstrap")]
    public class SpawnLogEditModeTests
    {
        private const string HangarOfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        private static readonly ShipId Player = new(1);
        private static readonly ShipId Enemy = new(2);
        private static readonly ShipId OtherEnemy = new(3);

        private readonly List<GameObject> scratch = new();
        private SpawnLog log;
        private ShipId playerId;

        [SetUp]
        public void SetUp()
        {
            playerId = Player;
            log = new SpawnLog();
            log.Bind(null, () => playerId);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in scratch)
                if (go) Object.DestroyImmediate(go);
            scratch.Clear();
        }

        private Ship NewHull()
        {
            var offer = AssetDatabase.LoadAssetAtPath<ItemSubset>(HangarOfferPath);
            Assert.IsNotNull(offer, $"Hangar offer missing at {HangarOfferPath}");
            var hull = Object.Instantiate(offer.ships[0]);
            scratch.Add(hull.gameObject);
            return hull;
        }

        private static DamageInfo BlowFrom(ShipId attacker) =>
            new(100f, DamageKind.Laser, attacker, 0f, Vector3.zero, Vector3.zero);

        [Test]
        public void Entry_NamesThePartsAndCarriesTheLoadoutStatHash()
        {
            var hull = NewHull();
            log.Begin(0f);
            log.Log(Enemy, hull, 0f);
            log.End(1f);

            var entry = log.Entries()[0];
            Assert.AreEqual(hull.name.Replace("(Clone)", string.Empty), entry.Chassis, "the clone suffix is dropped");
            Assert.AreEqual(hull.Engine.name, entry.Engine);
            Assert.AreEqual(hull.Shield.name, entry.Shield);
            Assert.AreEqual(hull.Weapons.PrimaryMountPrefab.name, entry.Primary);
            Assert.AreEqual(StatHash.OfLoadout(hull), entry.LoadoutStatHash);
        }

        [Test]
        public void PlayersKillingBlow_IsAKill_AndStopsTheAliveClock()
        {
            log.Begin(10f);
            log.Log(Enemy, NewHull(), 12f);
            log.MarkDead(Enemy, BlowFrom(Player), 17f);
            log.End(30f);

            var entry = log.Entries()[0];
            Assert.AreEqual(2f, entry.SpawnSeconds, 1e-4f);
            Assert.AreEqual(5f, entry.AliveSeconds, 1e-4f);
            Assert.IsTrue(entry.KilledByPlayer);
        }

        [Test]
        public void AnotherShipsKillingBlow_IsNotAKill()
        {
            log.Begin(0f);
            log.Log(Enemy, NewHull(), 0f);
            log.MarkDead(Enemy, BlowFrom(OtherEnemy), 4f);
            log.End(9f);

            var entry = log.Entries()[0];
            Assert.AreEqual(4f, entry.AliveSeconds, 1e-4f);
            Assert.IsFalse(entry.KilledByPlayer);
        }

        [Test]
        public void SurvivorAndDeathAfterEnd_LiveToTheRunsEnd()
        {
            log.Begin(0f);
            log.Log(Enemy, NewHull(), 3f);
            log.Log(OtherEnemy, NewHull(), 5f);
            log.End(20f);
            log.MarkDead(OtherEnemy, BlowFrom(Player), 25f);

            var entries = log.Entries();
            Assert.AreEqual(17f, entries[0].AliveSeconds, 1e-4f);
            Assert.AreEqual(15f, entries[1].AliveSeconds, 1e-4f, "the run is over once the host stamps its end");
            Assert.IsFalse(entries[1].KilledByPlayer);
        }

        [Test]
        public void SpawnBeforeBegin_ReadsAsSecondZero()
        {
            log.Log(Enemy, NewHull(), 4f);
            log.Begin(6f);
            log.End(16f);

            var entry = log.Entries()[0];
            Assert.AreEqual(0f, entry.SpawnSeconds, 1e-4f);
            Assert.AreEqual(10f, entry.AliveSeconds, 1e-4f);
        }

        [Test]
        public void CurrentPlayersEntry_IsLeftOut()
        {
            log.Begin(0f);
            log.Log(Player, NewHull(), 0f);
            log.Log(Enemy, NewHull(), 1f);
            log.End(2f);

            var entries = log.Entries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(Enemy, entries[0].Id);
        }

        [Test]
        public void Reset_ClearsTheLog()
        {
            log.Begin(0f);
            log.Log(Enemy, NewHull(), 0f);
            log.Reset();

            Assert.IsEmpty(log.Entries());
        }
    }
}
