#if UNITY_EDITOR
using Capture.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RL.Episodes;
using RL.Hosts;
using RL.Hosts.Lanes;
using RL.Probes;
using RL.Reward;
using Ships.Loadout;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.RLHarness
{
    /// <summary>Pins the harness parse boundary: the environment parses ONCE, before play mode, so every illegal value fails there instead of silently reshaping a running matrix.</summary>
    [Category("AI")]
    public class RLHarnessSpecEditModeTests
    {
        private static readonly string[] MatrixSelectors =
        {
            "RL_HARNESS_MATRIX", "RL_HARNESS_MATRIX_REFERENCE", "RL_HARNESS_MATRIX_ROSTER", "RL_HARNESS_MATRIX_KITS",
            "RL_HARNESS_MATRIX_ENEMIES", "RL_HARNESS_MATRIX_REFERENCE_BREAKOFF",
        };

        [Test]
        public void RecordingRequiresLauncherProvenance()
        {
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_RECORD", "all", "RL_HARNESS_CAPTURE_COMMIT", null));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_RECORD", "all", "RL_HARNESS_CAPTURE_COMMIT", "not-a-sha"));
            Assert.DoesNotThrow(() => Parse("RL_HARNESS_CAPTURE_COMMIT", null));
        }

        private HarnessSpec Parse(params string[] keyValuePairs) => Parse(true, keyValuePairs);

        // A null value leaves the variable unset; with no matrix selector given, a Lasers weapon pair is selected.
        private HarnessSpec Parse(bool hasGraphics, params string[] keyValuePairs)
        {
            var env = ToEnv(keyValuePairs);
            if (!env.ContainsKey("RL_HARNESS_CAPTURE_COMMIT"))
                env["RL_HARNESS_CAPTURE_COMMIT"] = new string('a', 40);
            if (!MatrixSelectors.Any(env.ContainsKey))
                env["RL_HARNESS_MATRIX"] = "Lasers";
            return HarnessSpec.Parse(k => env.TryGetValue(k, out var v) ? v : null,
                TrainingBootstrap.CatalogWeapons, TrainingBootstrap.CatalogChassis, TrainingBootstrap.CatalogPilots,
                TrainingBootstrap.CatalogShields, () => hasGraphics);
        }

        private static Dictionary<string, string> ToEnv(string[] keyValuePairs)
        {
            var env = new Dictionary<string, string>();
            for (var i = 0; i < keyValuePairs.Length; i += 2) env[keyValuePairs[i]] = keyValuePairs[i + 1];
            return env;
        }

        private static string[] Names(ProbeSpec[] probes) => Array.ConvertAll(probes, p => p.name);

        [Test]
        public void Defaults_AreTheMatrixLane()
        {
            var spec = Parse();

            Assert.AreEqual(HarnessLane.Matrix, spec.lane);
            Assert.AreEqual(EvalProtocol.HeldOutSeeds, spec.seeds);
            Assert.AreEqual("matrix-held-out", spec.tag);
            Assert.AreEqual(HarnessSpec.DefaultEpisodesPerSeed, spec.episodesPerSeed);
            Assert.AreEqual(new[] { MatrixProbe.ProbeName }, Names(spec.probes));
            Assert.AreEqual(GizmoCaptureProfile.None, spec.gizmoProfile);
            Assert.IsFalse(spec.record.enabled);
            Assert.IsNull(spec.outDir);
        }

        [Test]
        public void NoMatrixSelector_ThrowsAtParse()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_MATRIX", null),
                "with every other lane gone, an unselected matrix has nothing to run");
            StringAssert.Contains("RL_HARNESS_MATRIX", thrown.Message);
        }

        [Test]
        public void LaneSelector_TakesMatrixAndRefusesRemovedLanesByName()
        {
            Assert.AreEqual(HarnessLane.Matrix, Parse("RL_HARNESS_LANE", "matrix").lane);
            Assert.AreEqual(HarnessLane.Matrix, Parse("RL_HARNESS_LANE", "MATRIX").lane);
            foreach (var removed in new[] { "eval", "capture", "sentence", "duel" })
            {
                var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_LANE", removed));
                StringAssert.Contains("removed with the RL training stack", thrown.Message);
            }
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_LANE", "film"), "unknown lane");
        }

        [Test]
        public void RemovedRlVariables_ThrowNamingTheRemoval()
        {
            foreach (var name in new[]
                     {
                         "RL_HARNESS_ONNX", "RL_HARNESS_OPPONENT", "RL_HARNESS_DENSITY", "RL_HARNESS_BUNDLE",
                         "RL_HARNESS_SENTENCE", "RL_HARNESS_DUEL", "RL_EVAL_ONNX",
                     })
            {
                var thrown = Assert.Throws<ArgumentException>(() => Parse(name, "stale"),
                    $"a stale {name} must not run a matrix in its place");
                StringAssert.Contains(name, thrown.Message);
                StringAssert.Contains("removed with the RL training stack", thrown.Message);
            }
        }

        [Test]
        public void ProbeSelection_IsByNameAndAnEmptyListMeansNone()
        {
            Assert.AreEqual(new[] { MatrixProbe.ProbeName }, Names(Parse("RL_HARNESS_PROBES", "matrix").probes));
            Assert.IsEmpty(Parse("RL_HARNESS_PROBES", "").probes, "an explicit empty selection runs no probes");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "gate"),
                "an unregistered probe must fail at the boundary, not run a matrix with no instrument");
        }

        [Test]
        public void ProbeParams_MalformedTokensThrowAtTheBoundary()
        {
            var unknownKey = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "matrix(x=5)"));
            StringAssert.Contains("no params", unknownKey.Message, "a param on a paramless probe must say so");

            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "matrix,matrix"), "duplicate probe name");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "matrix("), "unbalanced paren");
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PROBES", "matrix)"), "unbalanced paren");
        }

        [Test]
        public void MatrixGrammar_SelectsTheLaneAndResolvesItsWeaponPrefabs()
        {
            var spec = Parse("RL_HARNESS_MATRIX", "Lasers, Missiles", "RL_HARNESS_SEEDS", "3001");

            Assert.AreEqual(HarnessLane.Matrix, spec.lane);
            Assert.AreEqual(new[] { "Lasers", "Missiles" }, Array.ConvertAll(spec.matrixWeapons, w => w.name));
            Assert.AreEqual("matrix-custom", spec.tag);
            Assert.AreEqual(new[] { MatrixProbe.ProbeName }, Names(spec.probes));
        }

        [Test]
        public void MatrixGrammar_AllSelectsTheCatalogsWeaponsInCatalogOrder()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            Assert.IsNotEmpty(catalog.Weapons);

            CollectionAssert.AreEqual(catalog.Weapons, Parse("RL_HARNESS_MATRIX", "all").matrixWeapons);
            CollectionAssert.AreEqual(catalog.Weapons, Parse("RL_HARNESS_MATRIX", "ALL").matrixWeapons);
        }

        [Test]
        public void MatrixGrammar_ResolvesANameWithoutRegardToCase()
        {
            Assert.AreEqual(new[] { "Lasers" },
                Array.ConvertAll(Parse("RL_HARNESS_MATRIX", "lasers").matrixWeapons, w => w.name));
        }

        [Test]
        public void MatrixGrammar_RefusesNamesTheCatalogDoesNotList()
        {
            var projectile = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_MATRIX", "Lasers,Laser"));
            StringAssert.Contains("'Laser'", projectile.Message);
            foreach (var weapon in TrainingBootstrap.CatalogWeapons())
                StringAssert.Contains(weapon.name, projectile.Message);
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_MATRIX", "all,Lasers"));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_MATRIX", ""));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_MATRIX", "Lasers,Lasers"));
        }

        [Test]
        public void MatrixPairSharesOnePoseAndTheOddEpisodeOnlySwapsSides()
        {
            Assert.IsFalse(MatrixLane.SidesSwapped(0));
            Assert.IsTrue(MatrixLane.SidesSwapped(1));
            Assert.AreEqual(0, MatrixLane.PoseIndex(0));
            Assert.AreEqual(MatrixLane.PoseIndex(0), MatrixLane.PoseIndex(1));
            Assert.AreEqual(MatrixLane.PoseIndex(2), MatrixLane.PoseIndex(3));
            Assert.AreNotEqual(MatrixLane.PoseIndex(1), MatrixLane.PoseIndex(2));

            var spec = RewardSpec.Default;
            spec.runSeed = 3001;
            var first = EpisodePoses.Derive(in spec, MatrixLane.PoseIndex(0), Vector2.zero);
            var swapped = EpisodePoses.Derive(in spec, MatrixLane.PoseIndex(1), Vector2.zero);
            var next = EpisodePoses.Derive(in spec, MatrixLane.PoseIndex(2), Vector2.zero);
            Assert.AreEqual(first.agentPos, swapped.agentPos);
            Assert.AreEqual(first.baselinePos, swapped.baselinePos);
            Assert.AreEqual(first.agentRotDeg, swapped.agentRotDeg);
            Assert.AreNotEqual(first.agentPos, next.agentPos);
        }

        [Test]
        public void MatrixRoster_ParsesAReferenceAndEachRosterSide()
        {
            var spec = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "crimson:kiterpilot:lasers",
                "RL_HARNESS_MATRIX_ROSTER", "vanguard:aggressorpilot:lasers, nightshade:orbiterpilot:turbolasers",
                "RL_HARNESS_SEEDS", "3001");

            Assert.AreEqual(HarnessLane.Matrix, spec.lane);
            Assert.AreEqual("matrix-custom", spec.tag);
            Assert.IsFalse(spec.matrixKitsOff);
            Assert.IsNull(spec.matrixWeapons);
            Assert.AreEqual(new[] { MatrixProbe.ProbeName }, Names(spec.probes));
            Assert.AreEqual("Crimson", spec.matrixReference.chassis.name);
            Assert.AreEqual("KiterPilot", spec.matrixReference.pilot.name);
            Assert.AreEqual("Lasers", spec.matrixReference.weapon.name);
            Assert.AreEqual("Crimson-KiterPilot-Lasers", spec.matrixReference.Label);
            Assert.AreEqual(2, spec.matrixRoster.Length);
            Assert.AreEqual("Vanguard-AggressorPilot-Lasers", spec.matrixRoster[0].Label);
            Assert.AreEqual("Nightshade-OrbiterPilot-TurboLasers", spec.matrixRoster[1].Label);
        }

        [Test]
        public void MatrixRoster_RequiresBothVariablesAndRefusesWeaponPairMode()
        {
            Assert.Throws<ArgumentException>(() =>
                Parse("RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers"));
            Assert.Throws<ArgumentException>(() =>
                Parse("RL_HARNESS_MATRIX_ROSTER", "Crimson:KiterPilot:Lasers"));
            var paired = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX", "Lasers"));
            StringAssert.Contains("select different matrix modes", paired.Message);
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Crimson:KiterPilot:Lasers, crimson:kiterpilot:lasers"));
        }

        [Test]
        public void MatrixRoster_RefusesUnknownChassisPilotAndWeapon()
        {
            var chassis = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Nope:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Crimson:AggressorPilot:Lasers"));
            StringAssert.Contains("'Nope'", chassis.Message);
            foreach (var ship in TrainingBootstrap.CatalogChassis())
                StringAssert.Contains(ship.name, chassis.Message);

            var pilot = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:NopePilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            StringAssert.Contains("'NopePilot'", pilot.Message);
            foreach (var commander in TrainingBootstrap.CatalogPilots())
                StringAssert.Contains(commander.name, pilot.Message);

            var weapon = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Nope",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            StringAssert.Contains("'Nope'", weapon.Message);
            foreach (var item in TrainingBootstrap.CatalogWeapons())
                StringAssert.Contains(item.name, weapon.Message);
        }

        [Test]
        public void MatrixRoster_ParsesAnOptionalShield()
        {
            var spec = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "crimson:kiterpilot:lasers:default_shield",
                "RL_HARNESS_MATRIX_ROSTER",
                "vanguard:aggressorpilot:lasers, vanguard:aggressorpilot:lasers:default_shield",
                "RL_HARNESS_SEEDS", "3001");

            Assert.AreEqual("Default_Shield", spec.matrixReference.shield.name);
            Assert.AreEqual("Crimson-KiterPilot-Lasers-Default_Shield", spec.matrixReference.Label);
            Assert.IsTrue(spec.matrixRoster[0].shield == null);
            Assert.AreEqual("Vanguard-AggressorPilot-Lasers", spec.matrixRoster[0].Label);
            Assert.AreEqual("Default_Shield", spec.matrixRoster[1].shield.name);
            Assert.AreEqual("Vanguard-AggressorPilot-Lasers-Default_Shield", spec.matrixRoster[1].Label);

            var unknown = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:Nope",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            StringAssert.Contains("'Nope'", unknown.Message);
            foreach (var shield in TrainingBootstrap.CatalogShields())
                StringAssert.Contains(shield.name, unknown.Message);

            var five = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:Default_Shield:Extra",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            StringAssert.Contains("Chassis:Pilot:Weapon[:Shield]", five.Message);
        }

        [Test]
        public void MatrixRoster_ParsesNumericShieldNumbers()
        {
            var spec = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "crimson:kiterpilot:lasers",
                "RL_HARNESS_MATRIX_ROSTER", "crimson:aggressorpilot:lasers:32/2/10",
                "RL_HARNESS_SEEDS", "3001");

            var side = spec.matrixRoster[0];
            Assert.IsTrue(side.shieldNumbers);
            Assert.IsTrue(side.shield == null);
            Assert.AreEqual(32f, side.shieldCapacity);
            Assert.AreEqual(2f, side.shieldDelay);
            Assert.AreEqual(10f, side.shieldRate);
            Assert.AreEqual("Crimson-AggressorPilot-Lasers-S32_2_10", side.Label);
            Assert.AreEqual("Crimson-KiterPilot-Lasers", spec.matrixReference.Label);

            var fractional = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "crimson:kiterpilot:lasers:28/3.5/12",
                "RL_HARNESS_MATRIX_ROSTER", "vanguard:aggressorpilot:lasers",
                "RL_HARNESS_SEEDS", "3001");
            Assert.AreEqual(28f, fractional.matrixReference.shieldCapacity);
            Assert.AreEqual(3.5f, fractional.matrixReference.shieldDelay);
            Assert.AreEqual(12f, fractional.matrixReference.shieldRate);
            Assert.IsTrue(fractional.matrixReference.shield == null);
            Assert.AreEqual("Crimson-KiterPilot-Lasers-S28_3.5_12", fractional.matrixReference.Label);
            Assert.AreEqual("Vanguard-AggressorPilot-Lasers", fractional.matrixRoster[0].Label);

            var zeros = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "crimson:kiterpilot:lasers:32/0/0",
                "RL_HARNESS_MATRIX_ROSTER", "vanguard:aggressorpilot:lasers",
                "RL_HARNESS_SEEDS", "3001");
            Assert.IsTrue(zeros.matrixReference.shieldNumbers);
            Assert.AreEqual(0f, zeros.matrixReference.shieldDelay);
            Assert.AreEqual(0f, zeros.matrixReference.shieldRate);
            Assert.AreEqual("Crimson-KiterPilot-Lasers-S32_0_0", zeros.matrixReference.Label);

            var distinct = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:32/2/10",
                "RL_HARNESS_MATRIX_ROSTER",
                "Crimson:KiterPilot:Lasers:32/3/10, Crimson:KiterPilot:Lasers:32/2/10",
                "RL_HARNESS_SEEDS", "3001");
            Assert.AreEqual(2, distinct.matrixRoster.Length);

            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER",
                "Vanguard:AggressorPilot:Lasers:32/2/10, vanguard:aggressorpilot:lasers:32/2/10"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:32/2",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:32/2/10/1",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:32/nope/10",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:NaN/2/10",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:Infinity/2/10",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:0/2/10",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:-1/2/10",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:32/-1/10",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers:32/2/-1",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers"));
        }

        [Test]
        public void MatrixRoster_KitsOffParsesAndRefusesWeaponPairMode()
        {
            var spec = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_KITS", "off",
                "RL_HARNESS_SEEDS", "3001");

            Assert.IsTrue(spec.matrixKitsOff);
            Assert.AreEqual("matrix-kitsoff-custom", spec.tag);
            Assert.AreEqual(
                "Crimson-KiterPilot-Lasers_vs_Vanguard-AggressorPilot-Lasers_kitsoff",
                MatrixLane.RosterBlockLabel(spec.matrixReference, spec.matrixRoster[0], spec.matrixKitsOff));

            var bad = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_KITS", "no"));
            StringAssert.Contains("off", bad.Message);

            var paired = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_KITS", "off",
                "RL_HARNESS_MATRIX", "Lasers"));
            StringAssert.Contains("and RL_HARNESS_MATRIX select different matrix modes", paired.Message);
        }

        [Test]
        public void MatrixRoster_ParsesEnemyCountAndBreakOffAndRefusesThemElsewhere()
        {
            var plain = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_SEEDS", "3001");
            Assert.AreEqual(1, plain.matrixEnemies);
            Assert.IsFalse(plain.matrixReferenceBreakOffSet);
            Assert.AreEqual(
                "Crimson-KiterPilot-Lasers_vs_Vanguard-AggressorPilot-Lasers",
                MatrixLane.RosterBlockLabel(plain.matrixReference, plain.matrixRoster[0]));

            var spec = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_ENEMIES", "3",
                "RL_HARNESS_MATRIX_REFERENCE_BREAKOFF", "0",
                "RL_HARNESS_SEEDS", "3001");
            Assert.AreEqual(3, spec.matrixEnemies);
            Assert.IsTrue(spec.matrixReferenceBreakOffSet);
            Assert.AreEqual(0f, spec.matrixReferenceBreakOff);
            Assert.AreEqual(
                "Crimson-KiterPilot-Lasers_vs_Vanguard-AggressorPilot-Lasers_x3_bo0",
                MatrixLane.RosterBlockLabel(spec.matrixReference, spec.matrixRoster[0],
                    enemies: spec.matrixEnemies, breakOffSet: true, breakOff: spec.matrixReferenceBreakOff));
            Assert.AreEqual(
                "Crimson-KiterPilot-Lasers_vs_Vanguard-AggressorPilot-Lasers_bo50",
                MatrixLane.RosterBlockLabel(spec.matrixReference, spec.matrixRoster[0],
                    breakOffSet: true, breakOff: 50f));
            Assert.AreEqual(
                "Crimson-KiterPilot-Lasers_vs_Vanguard-AggressorPilot-Lasers_bo12.5",
                MatrixLane.RosterBlockLabel(spec.matrixReference, spec.matrixRoster[0],
                    breakOffSet: true, breakOff: 12.5f));

            var hundred = Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_REFERENCE_BREAKOFF", "100",
                "RL_HARNESS_SEEDS", "3001");
            Assert.AreEqual(100f, hundred.matrixReferenceBreakOff);
            Assert.AreEqual(1, hundred.matrixEnemies);

            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_ENEMIES", "0"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_ENEMIES", "4"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_ENEMIES", "2.5"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_REFERENCE_BREAKOFF", "-0.1"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_REFERENCE_BREAKOFF", "101"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_REFERENCE", "Crimson:KiterPilot:Lasers",
                "RL_HARNESS_MATRIX_ROSTER", "Vanguard:AggressorPilot:Lasers",
                "RL_HARNESS_MATRIX_REFERENCE_BREAKOFF", "NaN"));

            var withWeapons = Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_MATRIX_ENEMIES", "3",
                "RL_HARNESS_MATRIX", "Lasers"));
            StringAssert.Contains("and RL_HARNESS_MATRIX select different matrix modes", withWeapons.Message);
            var alone = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_MATRIX_ENEMIES", "2"));
            StringAssert.Contains("valid only with", alone.Message);
            var breakOffAlone = Assert.Throws<ArgumentException>(() =>
                Parse("RL_HARNESS_MATRIX_REFERENCE_BREAKOFF", "50"));
            StringAssert.Contains("valid only with", breakOffAlone.Message);
        }

        [Test]
        public void ExtraEnemies_SitOnTheBaselineCircleFacingTheReference()
        {
            var reference = new Vector2(3f, -5f);
            var baseline = new Vector2(11f, 1f);
            var plus = EpisodePoses.ExtraOnBaselineCircle(reference, baseline, 0);
            var minus = EpisodePoses.ExtraOnBaselineCircle(reference, baseline, 1);
            var radius = (baseline - reference).magnitude;

            Assert.AreNotEqual(plus.pos, minus.pos);
            Assert.AreNotEqual(plus.pos, baseline);
            Assert.AreNotEqual(minus.pos, baseline);
            Assert.AreEqual(radius, (plus.pos - reference).magnitude, 1e-4f);
            Assert.AreEqual(radius, (minus.pos - reference).magnitude, 1e-4f);
            Assert.AreEqual(EpisodePoses.ExtraEnemyOffsetDeg,
                Vector2.Angle(baseline - reference, plus.pos - reference), 1e-3f);
            Assert.AreEqual(EpisodePoses.ExtraEnemyOffsetDeg,
                Vector2.Angle(baseline - reference, minus.pos - reference), 1e-3f);
            Assert.AreEqual(1f, Vector2.Dot(ForwardOf(plus.rotDeg), (reference - plus.pos).normalized), 1e-4f);
            Assert.AreEqual(1f, Vector2.Dot(ForwardOf(minus.rotDeg), (reference - minus.pos).normalized), 1e-4f);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EpisodePoses.ExtraOnBaselineCircle(reference, baseline, 2));
        }

        private static Vector2 ForwardOf(float rotDeg) =>
            new(-Mathf.Sin(rotDeg * Mathf.Deg2Rad), Mathf.Cos(rotDeg * Mathf.Deg2Rad));

        [Test]
        public void RetiredEnvName_ThrowsNamingItsReplacement()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_EVAL_SEEDS", "7,8"),
                "a stale script's retired name must not be silently ignored");
            StringAssert.Contains("RL_HARNESS_SEEDS", thrown.Message);
        }

        [Test]
        public void RetiredPaintersSelector_ThrowsAsRemoved()
        {
            var thrown = Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_PAINTERS", "steering"),
                "the deleted painter selector must fail loudly, not be silently ignored into plain footage");
            StringAssert.Contains("RL_HARNESS_PAINTERS", thrown.Message);
            StringAssert.Contains("no replacement", thrown.Message);
        }

        [Test]
        public void GarbageValues_ThrowAtTheBoundaryInsteadOfBeingIgnored()
        {
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_EPISODES_PER_SEED", "abc"));
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_EPISODES_PER_SEED", "0"));
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
            Assert.AreEqual(HarnessSpec.DefaultRecordWidth, def.width);
            Assert.AreEqual(HarnessSpec.DefaultRecordHeight, def.height);
            Assert.AreEqual(HarnessSpec.DefaultRecordEvery, def.everyFixedSteps);

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
        public void GizmoProfile_SelectsOneCodeDefinedCaptureProfile()
        {
            var prefix = new[]
            {
                "RL_HARNESS_SEEDS", "2001",
                "RL_HARNESS_RECORD", "all",
            };

            Assert.AreEqual(GizmoCaptureProfile.Steering,
                Parse(prefix.Concat(new[] { "RL_HARNESS_GIZMOS", "steering" }).ToArray()).gizmoProfile);
            Assert.AreEqual(GizmoCaptureProfile.Combat,
                Parse(prefix.Concat(new[] { "RL_HARNESS_GIZMOS", "COMBAT" }).ToArray()).gizmoProfile);
            Assert.AreEqual(GizmoCaptureProfile.Everything,
                Parse(prefix.Concat(new[] { "RL_HARNESS_GIZMOS", "everything" }).ToArray()).gizmoProfile);
            Assert.IsFalse(
                Parse(prefix.Concat(new[] { "RL_HARNESS_GIZMOS", "steering" }).ToArray()).Presentation,
                "native gizmo capture uses collider silhouettes without presentation meshes");
            Assert.IsTrue(Parse(prefix).Presentation,
                "the offscreen recorder without a gizmo profile retains its presentation path");
        }

        [Test]
        public void GizmoProfile_RejectsUnknownOrUnrecordedUseAtTheBoundary()
        {
            Assert.Throws<ArgumentException>(() => Parse("RL_HARNESS_GIZMOS", "heatmap"));
            Assert.Throws<ArgumentException>(() => Parse(
                "RL_HARNESS_SEEDS", "2001",
                "RL_HARNESS_GIZMOS", "steering"), "profile without recorded episodes");
        }
    }
}
#endif
