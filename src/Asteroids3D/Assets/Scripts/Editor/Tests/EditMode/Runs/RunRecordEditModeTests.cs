using System;
using System.Collections.Generic;
using Balance;
using Damage;
using Game.Player;
using Game.Runs;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using Ships.Loadout;
using Ships.Registry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Runs
{
    /// <summary>A run record composed from real recorders: it is one JSON line tagged with the schema, it round-trips, damage rows and the killing blow point at spawn entries by index, and no ship id reaches the text.</summary>
    [Category("Bootstrap")]
    public class RunRecordEditModeTests
    {
        private const string HangarOfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";
        private const string PlayerHash = "a1b2c3d4e5f60718";
        private const string Fingerprint = "f0e1d2c3b4a59687";

        // Distinctive values, so a leaked instance id would be findable in the text.
        private static readonly ShipId Player = new(555000111);
        private static readonly ShipId Laserer = new(987654321);
        private static readonly ShipId Bystander = new(123456789);

        private sealed class FixedTally : IRunTally
        {
            public int Kills => 3;
            public float SecondsSurvived => 42.5f;
        }

        private readonly List<GameObject> scratch = new();
        private ItemSubset offer;
        private RunRecord record;
        private string json;

        [SetUp]
        public void SetUp()
        {
            offer = AssetDatabase.LoadAssetAtPath<ItemSubset>(HangarOfferPath);
            Assert.IsNotNull(offer, $"Hangar offer missing at {HangarOfferPath}");

            var spawns = new SpawnLog();
            spawns.Bind(null, () => Player);
            spawns.Begin(0f);
            spawns.Log(Player, NewHull(), 0f);
            spawns.Log(Bystander, NewHull(), 1f);
            spawns.Log(Laserer, NewHull(), 2f);
            spawns.End(42.5f);

            var victimGo = new GameObject("RecordVictim");
            scratch.Add(victimGo);
            var victim = victimGo.AddComponent<DamageController>();
            victim.PopulateSettings(new ResolvedShipStats { maxHealth = 1000f, maxShield = 0f });
            var ledger = new DamageLedger();
            ledger.Bind(victim, null);
            victim.TakeDamage(Hit(10f, DamageKind.Laser, Laserer));
            victim.TakeDamage(Hit(15f, DamageKind.Laser, Laserer));
            victim.TakeDamage(Hit(40f, DamageKind.Collision, ShipId.Invalid));

            record = RunRecord.Compose(
                new DateTime(2026, 10, 1, 12, 34, 56, DateTimeKind.Utc), "TrialSector",
                new BuildIdentity { commit = "abc123", dirty = true }, Fingerprint,
                new ShipLoadout(offer.ships[0], offer.engines[0], offer.shields[0], offer.weapons[0], null), PlayerHash,
                new FixedTally(), ledger, spawns, Hit(99f, DamageKind.Laser, Laserer));
            json = record.ToJsonLine();
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
            var hull = Object.Instantiate(offer.ships[0]);
            scratch.Add(hull.gameObject);
            return hull;
        }

        private static DamageInfo Hit(float amount, DamageKind kind, ShipId attacker) =>
            new(amount, kind, attacker, 0f, Vector3.zero, Vector3.zero);

        [Test]
        public void JsonLine_IsOneLineTaggedWithTheSchema()
        {
            StringAssert.DoesNotContain("\n", json);
            StringAssert.Contains("\"schema\":\"run-record-v1\"", json);
            Assert.AreEqual(RunRecord.SchemaId, JsonUtility.FromJson<RunRecord>(json).schema);
        }

        [Test]
        public void JsonLine_RoundTripsWhatTheRunWasPlayedOn()
        {
            var read = JsonUtility.FromJson<RunRecord>(json);

            Assert.AreEqual("2026-10-01T12:34:56Z", read.endedUtc);
            Assert.AreEqual("TrialSector", read.sector);
            Assert.AreEqual("abc123", read.buildIdentity.commit);
            Assert.IsTrue(read.buildIdentity.dirty);
            Assert.AreEqual(Fingerprint, read.statFingerprint);
            Assert.AreEqual(3, read.kills);
            Assert.AreEqual(42.5f, read.secondsSurvived, 1e-4f);
        }

        [Test]
        public void PlayerRow_NamesThePendingLoadoutByAsset()
        {
            var player = JsonUtility.FromJson<RunRecord>(json).player;

            Assert.AreEqual(offer.ships[0].name, player.chassis);
            Assert.AreEqual(offer.engines[0].name, player.engine);
            Assert.AreEqual(offer.shields[0].name, player.shield);
            Assert.AreEqual(offer.weapons[0].name, player.primary);
            Assert.AreEqual(string.Empty, player.secondary, "an empty mount is an empty name");
            Assert.AreEqual(PlayerHash, player.statHash);
        }

        [Test]
        public void SpawnRows_LeaveOutThePlayer_AndCarryEachLoadoutStatHash()
        {
            var spawns = JsonUtility.FromJson<RunRecord>(json).spawns;

            Assert.AreEqual(2, spawns.Count, "the player's own spawn is not an entry");
            Assert.AreEqual(1f, spawns[0].spawnSeconds, 1e-4f);
            Assert.AreEqual(2f, spawns[1].spawnSeconds, 1e-4f);
            Assert.AreEqual(40.5f, spawns[1].aliveSeconds, 1e-4f);
            foreach (var spawn in spawns)
                Assert.AreEqual(StatHash.OfLoadout(offer.ships[0]), spawn.loadout.statHash);
        }

        [Test]
        public void DamageRowsAndKillingBlow_PointAtTheirSpawnEntry()
        {
            var read = JsonUtility.FromJson<RunRecord>(json);

            Assert.AreEqual(2, read.damage.Count);
            Assert.AreEqual("Laser", read.damage[0].kind);
            Assert.AreEqual(25f, read.damage[0].total, 1e-4f);
            Assert.AreEqual(2, read.damage[0].hits);
            Assert.AreEqual(1, read.damage[0].spawn, "the laser's attacker is the second spawn entry");
            Assert.AreEqual("Collision", read.damage[1].kind);
            Assert.AreEqual(RunRecord.NoSpawn, read.damage[1].spawn, "a non-ship source links to no entry");
            Assert.AreEqual("Laser", read.killingBlow.kind);
            Assert.AreEqual(1, read.killingBlow.spawn);
        }

        [Test]
        public void JsonLine_CarriesNoShipId()
        {
            foreach (var id in new[] { Player, Laserer, Bystander })
                StringAssert.DoesNotContain(id.Value.ToString(), json);
        }
    }
}
