using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.RLHarness
{
    public sealed class DecisionTransitionJsonl : IDisposable
    {
        private readonly StreamWriter stream;

        public string Path { get; }
        public string RunId { get; }
        public int WorkerIndex { get; }
        public int ArenaIndex { get; }

        private DecisionTransitionJsonl(string path, string runId, int workerIndex, int arenaIndex)
        {
            Path = path;
            RunId = runId;
            WorkerIndex = workerIndex;
            ArenaIndex = arenaIndex;
            stream = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public static DecisionTransitionJsonl Create(string runId, int workerIndex, int arenaIndex,
            string dirOverride = null, string workerSuffix = null)
        {
            if (string.IsNullOrWhiteSpace(runId))
                throw new ArgumentException("A transition dataset requires a non-empty run id.", nameof(runId));
            if (workerIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(workerIndex), workerIndex,
                    "Transition worker indices must be non-negative.");
            if (arenaIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arenaIndex), arenaIndex,
                    "Transition arena indices must be non-negative.");

            var path = NewRunPath(runId, dirOverride, workerSuffix);
            return new DecisionTransitionJsonl(path, runId, workerIndex, arenaIndex);
        }

        public DecisionTransitionRecorder BeginEpisode(in RewardSpec spec, int episodeIndex, int teamId) =>
            new(this, in spec, episodeIndex, teamId);

        internal void Append(in DecisionTransition transition)
        {
            transition.Validate();
            stream.WriteLine(transition.ToJsonLine());
        }

        public void Flush() => stream.Flush();

        public void Dispose() => stream.Dispose();

        private static string NewRunPath(string runId, string dirOverride, string workerSuffix)
        {
            string dir;
            if (!string.IsNullOrEmpty(dirOverride))
                dir = dirOverride;
            else
            {
                var baseDir = Application.isEditor
                    ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", ".."))
                    : Application.persistentDataPath;
                dir = System.IO.Path.Combine(baseDir, "results", "rl-transitions");
            }

            Directory.CreateDirectory(dir);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            return System.IO.Path.Combine(dir, $"{stamp}-{runId}{workerSuffix}-transitions.jsonl");
        }
    }
}
