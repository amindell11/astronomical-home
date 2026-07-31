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

        private static string[] Names(ProbeSpec[] probes) => Array.ConvertAll(probes, p => p.name);

        [Test]
        public void Defaults_AreTodaysEvalLane()
        {
            var spec = Parse();

            Assert.AreEqual(SessionLane.Eval, spec.lane);
            Assert.AreEqual(ShipAgentFactory.SmokeFixturePath, spec.onnxAssetPath, "no RL_HARNESS_ONNX: the smoke fixture");
            Assert.IsNull(spec.onnxSourcePath);
            Assert.AreEqual(EvalProtocol.HeldOutSeeds, spec.seeds);
            Assert.AreEqual("held-out", spec.tag);
            Assert.AreEqual(SessionSpec.DefaultEpisodesPerSeed, spec.episodesPerSeed);
            Assert.AreEqual(EvalProtocol.CanonicalFieldDensityScale, spec.fieldDensityScale);
            Assert.AreEqual(OpponentKind.Roster, spec.opponentKind);
            Assert.IsNull(spec.opponentOnnxSourcePath);
            Assert.AreEqual(new[] { ArchetypeGateProbe.ProbeName, CombatTelemetryProbe.ProbeName }, Names(spec.probes));
            Assert.IsNull(spec.outDir);
        }

        [Test]
        public void CheckpointSource_IsImportedAndRecorded()
        {
            var spec = Parse("RL_HARNESS_ONNX", "results/rl-training/run/ShipCombat-42.onnx");

            Assert.AreEqual(ImportedCandidatePath, spec.onnxAssetPath);
            Assert.AreEqual("results/rl-training/run/ShipCombat-42.onnx", spec.onnxSourcePath,
                "the summary carries provenance the imported path erases");
        }

        [Test]
        public void NonCanonicalDensity_MarksTheArtifactTag()
        {
            Assert.AreEqual("custom-d3", Parse("RL_HARNESS_SEEDS", "7,8", "RL_HARNESS_DENSITY", "3.0").tag);
            Assert.AreEqual("held-out", Parse("RL_HARNESS_DENSITY", "2.0").tag, "the canonical density never suffixes");
        }

        [Test]
        public void OpponentGrammar_ResolvesRosterArchetypeAndMirror()
        {
            Assert.AreEqual(OpponentKind.Roster, Parse().opponentKind);
            Assert.AreEqual(OpponentKind.Roster, Parse("RL_HARNESS_OPPONENT", "roster").opponentKind);
            Assert.AreEqual(OpponentKind.Mirror, Parse("RL_HARNESS_OPPONENT", "mirror").opponentKind);

            var pinned = Parse("RL_HARNESS_OPPONENT", "evader");
            Assert.AreEqual(OpponentKind.Archetype, pinned.opponentKind);
            Assert.AreEqual(OpponentArchetype.Evader, pinned.opponentArchetype);
        }

        [Test]
        public void OpponentGrammar_RoutesACheckpointPathToTheSecondSlot()
        {
            var spec = Parse("RL_HARNESS_OPPONENT", "frozen/ShipCombat-999950.onnx");

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
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_OPPONENT", "brawler"));
            StringAssert.Contains("Aggressor", thrown.Message);
            StringAssert.Contains("mirror", thrown.Message);
        }

        [Test]
        public void ProbeSelection_IsByNameAndAnEmptyListMeansNone()
        {
            Assert.AreEqual(new[] { ArchetypeGateProbe.ProbeName }, Names(Parse("RL_HARNESS_PROBES", "gate").probes));
            Assert.IsEmpty(Parse("RL_HARNESS_PROBES", "").probes, "an explicit empty selection runs no probes");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "heat"),
                "an unregistered probe must fail at the boundary, not run an eval with no instrument");
        }

        [Test]
        public void ProbeParams_ParseInlineAndSurviveWhitespace()
        {
            var probes = Parse("RL_HARNESS_PROBES", "gate, facing( wFacing = 5 )").probes;

            Assert.AreEqual(new[] { ArchetypeGateProbe.ProbeName, FacingProbe.ProbeName }, Names(probes));
            Assert.AreEqual(new[] { FacingProbe.AuthorityScaleKey }, probes[1].keys);
            Assert.AreEqual(new[] { 5f }, probes[1].values);
            Assert.IsEmpty(probes[0].keys);
        }

        [Test]
        public void ProbeParams_CommasInsideParensSeparateParamsNotProbes()
        {
            var probes = Parse("RL_HARNESS_PROBES", "facing(wFacing=1,wFacing=2),gate").probes;

            Assert.AreEqual(new[] { FacingProbe.ProbeName, ArchetypeGateProbe.ProbeName }, Names(probes));
            Assert.AreEqual(new[] { FacingProbe.AuthorityScaleKey, FacingProbe.AuthorityScaleKey }, probes[0].keys);
            Assert.AreEqual(new[] { 1f, 2f }, probes[0].values);
        }

        [Test]
        public void ProbeParams_MalformedTokensThrowAtTheBoundary()
        {
            var unknownKey = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "facing(wFacig=5)"));
            StringAssert.Contains(FacingProbe.AuthorityScaleKey, unknownKey.Message,
                "an unknown param key must name the legal set");

            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "gate,gate"), "duplicate probe name");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "facing(wFacing=abc)"), "non-float value");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "facing("), "unbalanced paren");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "facing(wFacing=NaN)"), "non-finite value");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "facing(wFacing=Infinity)"),
                "non-finite value");
        }

        [Test]
        public void FacingProbe_RefusesANonFiniteOrNegativeAuthorityScaleAtCreation()
        {
            Assert.Throws<ArgumentException>(() => SessionProbes.Create(FacingProbe.ProbeName,
                new Dictionary<string, float> { [FacingProbe.AuthorityScaleKey] = -1f }));
            Assert.Throws<ArgumentException>(() => SessionProbes.Create(FacingProbe.ProbeName,
                new Dictionary<string, float> { [FacingProbe.AuthorityScaleKey] = float.NaN }));
        }

        [Test]
        public void RetiredEnvName_ThrowsNamingItsReplacement()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_ONNX", "stale-script.onnx"),
                "a stale script's retired name must not silently eval the smoke fixture");
            StringAssert.Contains("RL_HARNESS_ONNX", thrown.Message);
        }

        [Test]
        public void GarbageValues_ThrowAtTheBoundaryInsteadOfBeingIgnored()
        {
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_EPISODES_PER_SEED", "abc"));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_EPISODES_PER_SEED", "0"));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_DENSITY", "dense"));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_SEEDS", "1001,oops"));
        }
    }
}
#endif
