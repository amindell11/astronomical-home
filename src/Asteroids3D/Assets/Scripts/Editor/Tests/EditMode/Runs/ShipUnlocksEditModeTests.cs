using System;
using System.Collections.Generic;
using System.IO;
using Game.Player;
using Game.Runs;
using NUnit.Framework;
using Ships;
using UnityEditor;

namespace Tests.EditMode.Runs
{
    /// <summary>Ship unlocks over a temp run-record file: only an elite the player killed earns its chassis, unreadable lines and a missing file earn nothing, a claim raises one unlock per chassis ever, and the editor switch earns every ship.</summary>
    [Category("Bootstrap")]
    public class ShipUnlocksEditModeTests
    {
        private string dir;
        private string path;
        private bool hadUnlockAll;
        private bool unlockAll;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "ship-unlocks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "run-records.jsonl");
            hadUnlockAll = EditorPrefs.HasKey(ShipUnlocks.UnlockAllPref);
            unlockAll = EditorPrefs.GetBool(ShipUnlocks.UnlockAllPref, false);
            EditorPrefs.SetBool(ShipUnlocks.UnlockAllPref, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadUnlockAll) EditorPrefs.SetBool(ShipUnlocks.UnlockAllPref, unlockAll);
            else EditorPrefs.DeleteKey(ShipUnlocks.UnlockAllPref);
            Directory.Delete(dir, true);
        }

        private static Ship ShipAsset(string name)
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>($"Assets/Prefabs/Ships/{name}.prefab");
            Assert.IsNotNull(ship, $"ship prefab {name} loads");
            return ship;
        }

        private static SpawnRow Spawn(string chassis, bool elite, bool killedByPlayer) => new()
        {
            loadout = new LoadoutRow { chassis = chassis },
            elite = elite,
            killedByPlayer = killedByPlayer,
        };

        private static string Line(string schema, params SpawnRow[] spawns) =>
            new RunRecord { schema = schema, spawns = new List<SpawnRow>(spawns) }.ToJsonLine();

        private ShipUnlocks Read(params string[] lines)
        {
            File.WriteAllLines(path, lines);
            var unlocks = new ShipUnlocks(path);
            unlocks.Read();
            return unlocks;
        }

        [Test]
        public void Read_EarnsOnlyTheEliteChassisThePlayerKilled()
        {
            var unlocks = Read(
                Line(RunRecord.SchemaId,
                    Spawn("Valis", elite: true, killedByPlayer: true),
                    Spawn("Nightshade", elite: true, killedByPlayer: false),
                    Spawn("Bulwark", elite: false, killedByPlayer: true)));

            Assert.IsTrue(unlocks.Earned(ShipAsset("Valis")), "an elite the player killed earns its chassis");
            Assert.IsFalse(unlocks.Earned(ShipAsset("Nightshade")), "an elite someone else killed earns nothing");
            Assert.IsFalse(unlocks.Earned(ShipAsset("Bulwark")), "a regular ship's kill earns nothing");
        }

        [Test]
        public void Read_SkipsBlankMalformedAndOtherSchemaLines()
        {
            var unlocks = Read(
                "",
                "not json",
                Line("run-record-v1", Spawn("Nightshade", elite: true, killedByPlayer: true)),
                "{\"schema\":\"run-record-v2\"}",
                Line(RunRecord.SchemaId, Spawn("Valis", elite: true, killedByPlayer: true)));

            Assert.IsTrue(unlocks.Earned(ShipAsset("Valis")), "a good line after the bad ones still reads");
            Assert.IsFalse(unlocks.Earned(ShipAsset("Nightshade")), "another schema's line earns nothing");
        }

        [Test]
        public void Read_MissingFile_EarnsNothing()
        {
            var unlocks = new ShipUnlocks(path);
            unlocks.Read();

            Assert.IsFalse(unlocks.Earned(ShipAsset("Valis")));
        }

        [Test]
        public void Claim_RaisesOneUnlockPerChassis_FirstOfAnyOnlyForTheFirst()
        {
            var unlocks = new ShipUnlocks(path);
            unlocks.Read();
            var raised = new List<ShipUnlocked>();
            unlocks.Unlocked += raised.Add;

            unlocks.Claim(new SpawnLog.EliteKill(0, "shoal", "Valis"));
            unlocks.Claim(new SpawnLog.EliteKill(0, "shoal", "Valis"));
            unlocks.Claim(new SpawnLog.EliteKill(1, "veil", "Nightshade"));

            Assert.AreEqual(2, raised.Count, "a chassis unlocks once");
            Assert.AreEqual("Valis", raised[0].Chassis);
            Assert.AreEqual(0, raised[0].SectorIndex);
            Assert.AreEqual("shoal", raised[0].FactionId);
            Assert.IsTrue(raised[0].FirstOfAny);
            Assert.AreEqual("Nightshade", raised[1].Chassis);
            Assert.AreEqual(1, raised[1].SectorIndex);
            Assert.AreEqual("veil", raised[1].FactionId);
            Assert.IsFalse(raised[1].FirstOfAny);
            Assert.IsTrue(unlocks.Earned(ShipAsset("Nightshade")), "a claimed chassis is earned at once");
        }

        [Test]
        public void Claim_OfAChassisEarnedFromTheStore_RaisesNothing()
        {
            var unlocks = Read(Line(RunRecord.SchemaId, Spawn("Valis", elite: true, killedByPlayer: true)));
            var raised = new List<ShipUnlocked>();
            unlocks.Unlocked += raised.Add;

            unlocks.Claim(new SpawnLog.EliteKill(0, "shoal", "Valis"));

            Assert.IsEmpty(raised);
        }

        [Test]
        public void UnlockAll_EarnsAShipNoRecordEarned()
        {
            var unlocks = new ShipUnlocks(path);
            unlocks.Read();
            Assert.IsFalse(unlocks.Earned(ShipAsset("Valis")), "test premise: nothing earned");

            EditorPrefs.SetBool(ShipUnlocks.UnlockAllPref, true);

            Assert.IsTrue(unlocks.Earned(ShipAsset("Valis")));
        }
    }
}
