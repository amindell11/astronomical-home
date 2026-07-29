#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using Game.RLHarness;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    // Suffix drift turns the parallel gate's liveness assert into a silent "no JSONL found".
    [Category("AI")]
    public class RLDriverContractEditModeTests
    {
        private static string RunParallelSource() => File.ReadAllText(Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", "..", "training", "rl", "run_parallel.py")));

        private static string SuffixTemplate(string source, string constName)
        {
            var match = Regex.Match(source, $@"^{constName} = ""([^""]*)""", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, $"{constName} not found in run_parallel.py");
            return match.Groups[1].Value;
        }

        [Test]
        public void LogSuffixes_MatchComposeSuffix()
        {
            var source = RunParallelSource();
            var worker = SuffixTemplate(source, "WORKER_SUFFIX");
            var arena = SuffixTemplate(source, "ARENA_SUFFIX");

            for (var k = 0; k < 3; k++)
            {
                var workerPart = worker.Replace("{k}", k.ToString());
                Assert.AreEqual(workerPart, TrainingHost.ComposeSuffix(k, 0, 1),
                    "single-arena workers must land on the launcher's -w{k} filename");
                for (var j = 0; j < 3; j++)
                    Assert.AreEqual(workerPart + arena.Replace("{j}", j.ToString()),
                        TrainingHost.ComposeSuffix(k, j, 3),
                        "fanned-out arenas must land on the launcher's -w{k}-a{j} filename");
            }
        }

        [Test]
        public void EditorRun_ComposesNoSuffix()
        {
            Assert.IsNull(TrainingHost.ComposeSuffix(null, 0, 1),
                "an editor run has no trainer port and no fan-out — run_parallel.py's globs never see it");
        }
    }
}
#endif
