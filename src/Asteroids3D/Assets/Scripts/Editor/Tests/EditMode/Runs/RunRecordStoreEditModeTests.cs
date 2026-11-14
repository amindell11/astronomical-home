using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Game.Runs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Runs
{
    /// <summary>The run-record store on a temp path: each append adds one parseable line, the default path sits under the results root, a write that cannot land is logged with its path, not thrown, and export copies the file under a name taken from the build label or the commit.</summary>
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
        public void Append_ThatCannotLand_LogsThePathAndReportsIt()
        {
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "a file where the store wants a folder");
            var store = new RunRecordStore(Path.Combine(blocker, "run-records.jsonl"));

            LogAssert.Expect(LogType.Error, new Regex("Run record not written to '" + Regex.Escape(store.Path) + "'"));
            Assert.IsFalse(store.Append(Record(1)), "a write that cannot land returns false instead of throwing");
        }

        [Test]
        public void Export_CopiesTheBytes_AndNamesTheFileFromTheLabel()
        {
            var storePath = Path.Combine(dir, "run-records.jsonl");
            var bytes = Encoding.UTF8.GetBytes("{\"kills\":1}\n");
            File.WriteAllBytes(storePath, bytes);
            var folder = Path.Combine(dir, "out");
            Directory.CreateDirectory(folder);

            var export = new RunRecordStore(storePath).Export(folder,
                new BuildIdentity { commit = "abc123def4567890", label = "friends-1" });

            Assert.AreEqual(RunRecordExport.Kind.Saved, export.Outcome);
            Assert.IsTrue(Regex.IsMatch(Path.GetFileName(export.WrittenPath),
                "^andromeda-runs-friends-1-\\d{8}-\\d{4}\\.jsonl$"));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(export.WrittenPath));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(storePath));
        }

        [Test]
        public void Export_WithoutALabel_NamesTheFileFromTheCommitPrefix()
        {
            var storePath = Path.Combine(dir, "run-records.jsonl");
            File.WriteAllText(storePath, "{\"kills\":1}\n");
            var folder = Path.Combine(dir, "out");
            Directory.CreateDirectory(folder);

            var export = new RunRecordStore(storePath).Export(folder,
                new BuildIdentity { commit = "abc123def4567890", label = "" });

            Assert.AreEqual(RunRecordExport.Kind.Saved, export.Outcome);
            Assert.IsTrue(Regex.IsMatch(Path.GetFileName(export.WrittenPath),
                "^andromeda-runs-abc123de-\\d{8}-\\d{4}\\.jsonl$"));
        }

        [Test]
        public void Export_WithNoStoreFile_ReportsNothingYet_AndWritesNothing()
        {
            var folder = Path.Combine(dir, "out");
            Directory.CreateDirectory(folder);
            var store = new RunRecordStore(Path.Combine(dir, "missing.jsonl"));

            var export = store.Export(folder, new BuildIdentity { commit = "abc123def4567890", label = "friends-1" });

            Assert.AreEqual(RunRecordExport.Kind.NothingYet, export.Outcome);
            Assert.IsFalse(File.Exists(store.Path));
            Assert.AreEqual(0, Directory.GetFiles(folder).Length);
        }
    }
}
