using System;
using System.Collections.Generic;
using System.IO;
using Game.Runs;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Runs
{
    [Category("Bootstrap")]
    public class EnemiesMetEditModeTests
    {
        private string dir;
        private string path;
        private bool hadMeetAgain;
        private bool meetAgain;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "enemies-met-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "run-records.jsonl");
            hadMeetAgain = EditorPrefs.HasKey(EnemiesMet.MeetAgainPref);
            meetAgain = EditorPrefs.GetBool(EnemiesMet.MeetAgainPref, false);
            EditorPrefs.SetBool(EnemiesMet.MeetAgainPref, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadMeetAgain) EditorPrefs.SetBool(EnemiesMet.MeetAgainPref, meetAgain);
            else EditorPrefs.DeleteKey(EnemiesMet.MeetAgainPref);
            Directory.Delete(dir, true);
        }

        private static string Line(params string[] chassis)
        {
            var spawns = new List<SpawnRow>();
            foreach (var name in chassis) spawns.Add(new SpawnRow { loadout = new LoadoutRow { chassis = name } });
            return new RunRecord { schema = RunRecord.SchemaId, spawns = spawns }.ToJsonLine();
        }

        private EnemiesMet Read(params string[] lines)
        {
            File.WriteAllLines(path, lines);
            return EnemiesMet.Read(path);
        }

        [Test]
        public void Meet_IsFirstOnlyForAChassisNoRecordSpawned()
        {
            var met = Read(Line("BulwarkRival", "BulwarkMinion"), Line("ValisMinion"));

            Assert.IsFalse(met.Meet("BulwarkRival"), "a recorded spawn is already met");
            Assert.IsTrue(met.Meet("ValisRival"), "a chassis no record spawned is new");
        }

        [Test]
        public void Meet_CountsAMeetingThisRunBeforeItsRecordIsWritten()
        {
            var met = Read();

            Assert.IsTrue(met.Meet("NightshadeRival"));
            Assert.IsFalse(met.Meet("NightshadeRival"), "the second meeting is not a first");
        }

        [Test]
        public void MissingStore_MeetsEveryChassisFresh()
        {
            var met = EnemiesMet.Read(Path.Combine(dir, "absent.jsonl"));

            Assert.IsTrue(met.Meet("BulwarkRival"));
        }

        [Test]
        public void MeetAgainSwitch_MakesARecordedChassisNew()
        {
            var met = Read(Line("BulwarkRival"));
            EditorPrefs.SetBool(EnemiesMet.MeetAgainPref, true);

            Assert.IsTrue(met.Meet("BulwarkRival"));
        }
    }
}
