#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the harness session boundary: the environment parses ONCE, before play mode, so every illegal value fails there instead of silently reshaping a running eval.</summary>
    [Category("AI")]
    public class RLSessionSpecEditModeTests
    {
        private const string ImportedCandidatePath = "Assets/Tests/Fixtures/EvalCandidate.onnx";
        private const string ImportedOpponentPath = "Assets/Tests/Fixtures/EvalOpponent.onnx";

        private static SessionSpec Parse(params string[] keyValuePairs)
        {
            var env = new Dictionary<string, string>();
            for (var i = 0; i < keyValuePairs.Length; i += 2) env[keyValuePairs[i]] = keyValuePairs[i + 1];
            return SessionSpec.ParseEval(k => env.TryGetValue(k, out var v) ? v : null,
                _ => ImportedCandidatePath, _ => ImportedOpponentPath);
        }

        [Test]
        public void Defaults_AreTodaysEvalLane()
        {
            var spec = Parse();

            Assert.AreEqual(SessionLane.Eval, spec.lane);
            Assert.AreEqual(ShipAgentFactory.SmokeFixturePath, spec.onnxAssetPath, "no RL_EVAL_ONNX: the smoke fixture");
            Assert.IsNull(spec.onnxSourcePath);
            Assert.AreEqual(EvalProtocol.HeldOutSeeds, spec.seeds);
            Assert.AreEqual("held-out", spec.tag);
            Assert.AreEqual(SessionSpec.DefaultEpisodesPerSeed, spec.episodesPerSeed);
            Assert.AreEqual(EvalProtocol.CanonicalFieldDensityScale, spec.fieldDensityScale);
            Assert.AreEqual(OpponentKind.Roster, spec.opponentKind);
            Assert.IsNull(spec.opponentOnnxSourcePath);
            Assert.AreEqual(new[] { ArchetypeGateProbe.ProbeName }, spec.probes);
            Assert.IsNull(spec.outDir);
        }

        [Test]
        public void CheckpointSource_IsImportedAndRecorded()
        {
            var spec = Parse("RL_EVAL_ONNX", "results/rl-training/run/ShipCombat-42.onnx");

            Assert.AreEqual(ImportedCandidatePath, spec.onnxAssetPath);
            Assert.AreEqual("results/rl-training/run/ShipCombat-42.onnx", spec.onnxSourcePath,
                "the summary carries provenance the imported path erases");
        }

        [Test]
        public void NonCanonicalDensity_MarksTheArtifactTag()
        {
            Assert.AreEqual("custom-d3", Parse("RL_EVAL_SEEDS", "7,8", "RL_EVAL_DENSITY", "3.0").tag);
            Assert.AreEqual("held-out", Parse("RL_EVAL_DENSITY", "2.0").tag, "the canonical density never suffixes");
        }

        [Test]
        public void OpponentGrammar_ResolvesRosterArchetypeAndMirror()
        {
            Assert.AreEqual(OpponentKind.Roster, Parse().opponentKind);
            Assert.AreEqual(OpponentKind.Roster, Parse("RL_EVAL_OPPONENT", "roster").opponentKind);
            Assert.AreEqual(OpponentKind.Mirror, Parse("RL_EVAL_OPPONENT", "mirror").opponentKind);

            var pinned = Parse("RL_EVAL_OPPONENT", "evader");
            Assert.AreEqual(OpponentKind.Archetype, pinned.opponentKind);
            Assert.AreEqual(OpponentArchetype.Evader, pinned.opponentArchetype);
        }

        [Test]
        public void OpponentGrammar_RoutesACheckpointPathToTheSecondSlot()
        {
            var spec = Parse("RL_EVAL_OPPONENT", "frozen/ShipCombat-999950.onnx");

            Assert.AreEqual(OpponentKind.Checkpoint, spec.opponentKind);
            Assert.AreEqual("frozen/ShipCombat-999950.onnx", spec.opponentOnnxSourcePath,
                "the summary carries slot-2 provenance the imported path erases");
            Assert.AreEqual(ImportedOpponentPath, spec.opponentOnnxAssetPath,
                "the opponent must land in its own fixture slot, never the candidate's");
            Assert.AreEqual("ShipCombat-999950", spec.opponentLabel, "summary blocks are labeled by the stem");
        }

        [Test]
        public void OpponentCheckpointImport_MissingFileFailsAtTheBoundary()
        {
            Assert.Throws<System.IO.FileNotFoundException>(() =>
                TrainingBootstrap.ImportEvalOpponent("missing-opponent.onnx"));
        }

        [Test]
        public void OpponentGrammar_RefusesAnUnknownTokenNamingTheLegalSet()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_OPPONENT", "brawler"));
            StringAssert.Contains("Aggressor", thrown.Message);
            StringAssert.Contains("mirror", thrown.Message);
        }

        [Test]
        public void ProbeSelection_IsByNameAndAnEmptyListMeansNone()
        {
            Assert.AreEqual(new[] { ArchetypeGateProbe.ProbeName }, Parse("RL_EVAL_PROBES", "gate").probes);
            Assert.IsEmpty(Parse("RL_EVAL_PROBES", "").probes, "an explicit empty selection runs no probes");
            Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_PROBES", "facing"),
                "an unregistered probe must fail at the boundary, not run an eval with no instrument");
        }

        [Test]
        public void GarbageValues_ThrowAtTheBoundaryInsteadOfBeingIgnored()
        {
            Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_EPISODES_PER_SEED", "abc"));
            Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_EPISODES_PER_SEED", "0"));
            Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_DENSITY", "dense"));
            Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_SEEDS", "1001,oops"));
        }
    }
}
#endif
