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
        private bool candidateResolved;
        private string candidateSource;
        private string opponentSource;
        private readonly List<(string bundle, string asset)> bundleLoads = new();

        [SetUp]
        public void SetUp()
        {
            candidateResolved = false;
            candidateSource = null;
            opponentSource = null;
            bundleLoads.Clear();
        }

        private SessionSpec Parse(params string[] keyValuePairs) => Parse(true, keyValuePairs);

        private SessionSpec Parse(bool hasGraphics, params string[] keyValuePairs)
        {
            var env = ToEnv(keyValuePairs);
            return SessionSpec.ParseEval(k => env.TryGetValue(k, out var v) ? v : null,
                s =>
                {
                    candidateResolved = true;
                    candidateSource = s;
                    return null;
                },
                s =>
                {
                    opponentSource = s;
                    return null;
                }, () => hasGraphics);
        }

        private SessionSpec ParsePlayer(params string[] keyValuePairs)
        {
            var env = ToEnv(keyValuePairs);
            return SessionSpec.ParsePlayerEval(k => env.TryGetValue(k, out var v) ? v : null,
                (bundle, asset) =>
                {
                    bundleLoads.Add((bundle, asset));
                    return null;
                }, () => false);
        }

        private static Dictionary<string, string> ToEnv(string[] keyValuePairs)
        {
            var env = new Dictionary<string, string>();
            for (var i = 0; i < keyValuePairs.Length; i += 2) env[keyValuePairs[i]] = keyValuePairs[i + 1];
            return env;
        }

        private static string[] Names(ProbeSpec[] probes) => Array.ConvertAll(probes, p => p.name);

        [Test]
        public void Defaults_AreTodaysEvalLane()
        {
            var spec = Parse();

            Assert.AreEqual(SessionLane.Eval, spec.lane);
            Assert.IsTrue(candidateResolved && candidateSource == null,
                "no RL_HARNESS_ONNX: the boundary resolves a null source (the smoke fixture)");
            Assert.AreEqual("ShipCombat-smoke", spec.CandidateStem);
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
        public void CheckpointSource_IsResolvedAndRecorded()
        {
            var spec = Parse("RL_HARNESS_ONNX", "results/rl-training/run/ShipCombat-42.onnx");

            Assert.AreEqual("results/rl-training/run/ShipCombat-42.onnx", candidateSource,
                "the boundary resolver receives the source it must import");
            Assert.AreEqual("results/rl-training/run/ShipCombat-42.onnx", spec.onnxSourcePath,
                "the summary carries provenance the resolved asset erases");
            Assert.AreEqual("ShipCombat-42", spec.CandidateStem, "Python keys on the stem");
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
                "the summary carries slot-2 provenance the resolved asset erases");
            Assert.AreEqual("frozen/ShipCombat-999950.onnx", opponentSource,
                "the opponent resolver must receive the opponent source, never the candidate's");
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
        public void OpponentGrammar_RefusesAHostileCheckpointStemBeforeResolving()
        {
            var thrown = Assert.Throws<ArgumentException>(() =>
                Parse("RL_HARNESS_OPPONENT", "frozen/Ship Combat.v2.onnx"));
            StringAssert.Contains("Ship Combat.v2", thrown.Message);
            Assert.IsNull(opponentSource, "a hostile stem must fail before the opponent import runs");
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
        public void OpenLoopGrammar_SelectsTheLaneAndItsMeasuredArchetypes()
        {
            var all = Parse("RL_HARNESS_OPENLOOP", "all");
            Assert.AreEqual(SessionLane.OpenLoop, all.lane);
            Assert.AreEqual(new[]
            {
                OpponentArchetype.Aggressor, OpponentArchetype.Evader,
                OpponentArchetype.Orbiter, OpponentArchetype.Kiter,
            }, all.openLoopArchetypes);
            StringAssert.StartsWith("velrebase-", all.tag, "open-loop artifacts must never pass as an eval");

            var single = Parse("RL_HARNESS_OPENLOOP", "orbiter");
            Assert.AreEqual(new[] { OpponentArchetype.Orbiter }, single.openLoopArchetypes);
        }

        [Test]
        public void OpenLoopLane_DefaultsToTheVelRebaseProbe()
        {
            Assert.AreEqual(new[] { VelRebaseProbe.ProbeName },
                Names(Parse("RL_HARNESS_OPENLOOP", "all").probes),
                "the eval-lane default probes would throw on a composition with no policy agent");
            Assert.AreEqual(new[] { ArchetypeGateProbe.ProbeName, CombatTelemetryProbe.ProbeName },
                Names(Parse().probes), "the eval-lane default is unchanged");
        }

        [Test]
        public void OpenLoopGrammar_RefusesDummyUnknownsAndConflictingSelectors()
        {
            var dummy = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_OPENLOOP", "dummy"));
            StringAssert.Contains("no velocity law", dummy.Message);
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_OPENLOOP", "brawler"));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_OPENLOOP", ""));
            Assert.Throws<ArgumentException>(() =>
                Parse("RL_HARNESS_OPENLOOP", "all", "RL_HARNESS_ONNX", "ckpt.onnx"));
            Assert.Throws<ArgumentException>(() =>
                Parse("RL_HARNESS_OPENLOOP", "all", "RL_HARNESS_OPPONENT", "mirror"));
        }

        [Test]
        public void OpenLoopBlockLabels_CarryTheArm()
        {
            Assert.AreEqual("Orbiter-legacy",
                OpponentSpec.OpenLoop(OpponentArchetype.Orbiter, ArchetypeDrive.OpenLoopLegacy).Label);
            Assert.AreEqual("Orbiter-anchored",
                OpponentSpec.OpenLoop(OpponentArchetype.Orbiter, ArchetypeDrive.OpenLoopAnchored).Label);
            Assert.AreEqual("Orbiter", OpponentSpec.Pinned(OpponentArchetype.Orbiter).Label,
                "eval-lane labels are unchanged");
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

        [Test]
        public void RecordGrammar_OffAllAndIndices()
        {
            Assert.IsFalse(Parse().record.enabled, "no RL_HARNESS_RECORD: recording off");
            Assert.IsFalse(Parse("RL_HARNESS_RECORD", "").record.enabled, "empty RL_HARNESS_RECORD: recording off");

            var all = Parse("RL_HARNESS_RECORD", "all").record;
            Assert.IsTrue(all.enabled);
            Assert.IsTrue(all.all);
            Assert.IsTrue(all.Records(0) && all.Records(4), "\"all\" records every episode");

            var indices = Parse("RL_HARNESS_EPISODES_PER_SEED", "5", "RL_HARNESS_RECORD", "0,2").record;
            Assert.IsTrue(indices.enabled);
            Assert.IsFalse(indices.all);
            Assert.IsTrue(indices.Records(0));
            Assert.IsFalse(indices.Records(1));
            Assert.IsTrue(indices.Records(2));
        }

        [Test]
        public void RecordSizeAndCadence_DefaultAndOverride()
        {
            var def = Parse("RL_HARNESS_RECORD", "all").record;
            Assert.AreEqual(SessionSpec.DefaultRecordWidth, def.width);
            Assert.AreEqual(SessionSpec.DefaultRecordHeight, def.height);
            Assert.AreEqual(SessionSpec.DefaultRecordEvery, def.everyFixedSteps);

            var custom = Parse("RL_HARNESS_RECORD", "all", "RL_HARNESS_RECORD_SIZE", "1280x720",
                "RL_HARNESS_RECORD_EVERY", "3").record;
            Assert.AreEqual(1280, custom.width);
            Assert.AreEqual(720, custom.height);
            Assert.AreEqual(3, custom.everyFixedSteps);
        }

        [Test]
        public void RecordGrammar_MalformedValuesThrowAtTheBoundary()
        {
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_RECORD", "garbage"), "non-index token");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_EPISODES_PER_SEED", "3", "RL_HARNESS_RECORD", "5"),
                "index out of range for episodesPerSeed");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_RECORD", "0,0"), "duplicate index");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_RECORD", "all", "RL_HARNESS_RECORD_SIZE", "961x540"),
                "odd width rejected (yuv420p)");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_RECORD", "all", "RL_HARNESS_RECORD_SIZE", "960"),
                "not WxH");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_RECORD", "all", "RL_HARNESS_RECORD_EVERY", "0"),
                "zero cadence");
        }

        [Test]
        public void RecordWithoutGraphics_ThrowsAtParse()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Parse(false, "RL_HARNESS_RECORD", "all"),
                "recording headless can never render — the parse refuses it");
            StringAssert.Contains("graphics", thrown.Message);
            Assert.DoesNotThrow(() => Parse(false), "a non-recording run is legal headless");
        }

        [Test]
        public void LaneSelector_DefaultsEvalAndSelectsCapture()
        {
            Assert.AreEqual(SessionLane.Eval, Parse().lane);
            Assert.AreEqual(SessionLane.Eval, Parse("RL_HARNESS_LANE", "eval").lane);
            Assert.AreEqual(SessionLane.Capture,
                Parse("RL_HARNESS_LANE", "capture", "RL_HARNESS_SEEDS", "2001", "RL_HARNESS_OPPONENT", "aggressor").lane);
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_LANE", "film"), "unknown lane");
        }

        [Test]
        public void CaptureLane_RefusesMultiSeedAndRoster()
        {
            Assert.Throws<ArgumentException>(() =>
                    Parse("RL_HARNESS_LANE", "capture", "RL_HARNESS_SEEDS", "2001,2002", "RL_HARNESS_OPPONENT", "mirror"),
                "capture films exactly one seed");
            Assert.Throws<ArgumentException>(() =>
                    Parse("RL_HARNESS_LANE", "capture", "RL_HARNESS_SEEDS", "2001"),
                "capture forbids the default roster (single block only)");
            Assert.Throws<ArgumentException>(() =>
                    Parse("RL_HARNESS_LANE", "capture", "RL_HARNESS_SEEDS", "2001", "RL_HARNESS_OPPONENT", "roster"),
                "capture forbids explicit roster too");
        }

        [Test]
        public void BundleVar_InAnEditorSessionThrowsAtParse()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_BUNDLE", "run/eval-models.bundle"),
                "RL_HARNESS_BUNDLE must never silently shift meaning inside an editor session");
            StringAssert.Contains("player", thrown.Message);
        }

        [Test]
        public void PlayerParse_RequiresBothBundleVars()
        {
            Assert.Throws<ArgumentException>(() => ParsePlayer(), "no bundle vars at all");
            Assert.Throws<ArgumentException>(() => ParsePlayer("RL_HARNESS_BUNDLE", "run/eval-models.bundle"),
                "RL_HARNESS_ONNX missing: a player has no smoke default");
            Assert.Throws<ArgumentException>(() => ParsePlayer("RL_HARNESS_ONNX", "run/ShipCombat-42.onnx"),
                "RL_HARNESS_BUNDLE missing: the player has nothing to resolve models from");
        }

        [Test]
        public void PlayerParse_ResolvesModelsFromTheBundleUnderFixedNames()
        {
            var spec = ParsePlayer("RL_HARNESS_BUNDLE", "run/eval-models.bundle",
                "RL_HARNESS_ONNX", "run/ShipCombat-42.onnx");

            Assert.AreEqual(new[] { ("run/eval-models.bundle", "candidate") }, bundleLoads,
                "a roster eval resolves exactly the candidate slot");
            Assert.AreEqual("run/ShipCombat-42.onnx", spec.onnxSourcePath, "provenance rides the env, not the bundle");
            Assert.AreEqual("ShipCombat-42", spec.CandidateStem);

            bundleLoads.Clear();
            var slot2 = ParsePlayer("RL_HARNESS_BUNDLE", "run/eval-models.bundle",
                "RL_HARNESS_ONNX", "run/ShipCombat-42.onnx",
                "RL_HARNESS_OPPONENT", "frozen/ShipCombat-999950.onnx");
            Assert.AreEqual(new[] { ("run/eval-models.bundle", "candidate"), ("run/eval-models.bundle", "opponent") },
                bundleLoads);
            Assert.AreEqual(OpponentKind.Checkpoint, slot2.opponentKind);
            Assert.AreEqual("ShipCombat-999950", slot2.opponentLabel);
        }

        [Test]
        public void PlayerParse_RefusesEditorOnlyLanes()
        {
            Assert.Throws<ArgumentException>(() => ParsePlayer("RL_HARNESS_BUNDLE", "run/eval-models.bundle",
                "RL_HARNESS_ONNX", "run/ShipCombat-42.onnx", "RL_HARNESS_LANE", "capture"), "capture lane");
            Assert.Throws<ArgumentException>(() => ParsePlayer("RL_HARNESS_BUNDLE", "run/eval-models.bundle",
                "RL_HARNESS_ONNX", "run/ShipCombat-42.onnx", "RL_HARNESS_OPENLOOP", "all"), "open-loop lane");
        }

        [Test]
        public void PainterSelection_DefaultsShipDiagnosticsAndValidatesNames()
        {
            Assert.AreEqual(new[] { Game.Diagnostics.DiagnosticPainters.ShipDiagnostics }, Parse().painters);
            Assert.AreEqual(
                new[] { Game.Diagnostics.DiagnosticPainters.ShipDiagnostics, Game.Diagnostics.DiagnosticPainters.Policy },
                Parse("RL_HARNESS_PAINTERS", "ship-diagnostics,policy").painters);
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PAINTERS", "heatmap"),
                "an unknown painter must fail at the boundary naming the registered set");
            StringAssert.Contains(Game.Diagnostics.DiagnosticPainters.ShipDiagnostics, thrown.Message);
        }
    }
}
#endif
