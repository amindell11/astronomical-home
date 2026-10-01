using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Runs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Runs
{
    /// <summary>The run-record store on a temp path: each append adds one parseable line, the default path sits under the results root, and a write that cannot land is logged with its path, not thrown.</summary>
    [Category("Bootstrap")]
    public class RunRecordStoreEditModeTests
    {
        private string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "run-record-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(dir, true);

        private static RunRecord Record(int kills) => new() { schema = RunRecord.SchemaId, kills = kills };

        [Test]
        public void Append_AddsOneParseableLinePerRecord_CreatingTheFolder()
        {
            var store = new RunRecordStore(Path.Combine(dir, "nested", "run-records.jsonl"));

            store.Append(Record(1));
            store.Append(Record(2));

            var lines = File.ReadAllLines(store.Path);
            Assert.AreEqual(2, lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                var read = JsonUtility.FromJson<RunRecord>(lines[i]);
                Assert.AreEqual(RunRecord.SchemaId, read.schema);
                Assert.AreEqual(i + 1, read.kills, "appends keep their order");
            }
        }

        [Test]
        public void DefaultPath_IsTheBalanceFileUnderTheResultsRoot()
        {
            var path = new RunRecordStore().Path.Replace('\\', '/');

            StringAssert.EndsWith("/results/balance/run-records.jsonl", path);
        }

        [Test]
        public void Append_ThatCannotLand_LogsThePathAndDoesNotThrow()
        {
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "a file where the store wants a folder");
            var store = new RunRecordStore(Path.Combine(blocker, "run-records.jsonl"));

            LogAssert.Expect(LogType.Error, new Regex("Run record not written to '" + Regex.Escape(store.Path) + "'"));
            Assert.DoesNotThrow(() => store.Append(Record(1)));
        }
    }
}
