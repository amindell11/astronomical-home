#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AI.Navigation.MPC;
using NUnit.Framework;
using RL.SolverRig;
using Ships;
using Unity.Burst;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.TerminalField
{
    [Category("MPC")]
    public class TerminalFieldRigTests
    {
        private static readonly uint[] DevelopmentSeeds = { 1234u, 7u, 99u, 2001u, 2002u };

        [Test]
        public void DevelopmentWeightSweep()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Set MPC_RIG_EMIT=1 for development experiments.");
            RunMatrix(new[] { 0f, 0.01f, 0.03f, 0.1f, 0.3f, 1f, 3f }, DevelopmentSeeds, false);
        }

        [Test]
        public void RefinedWeightSweep()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in refinement after the first sweep.");
            RunMatrix(new[] { 0f, 0.4f, 0.6f, 0.8f, 1f, 1.25f, 1.5f, 2f, 2.5f, 4f, 5f, 7.5f, 10f }, DevelopmentSeeds, false);
        }

        [Test]
        public void LowerWeightSweep()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in lower-weight development search after session regressions.");
            RunMatrix(new[] { 0f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f, 0.45f, 0.5f, 0.55f, 0.65f, 0.7f, 0.75f, 0.9f, 1.1f, 1.2f }, DevelopmentSeeds, false);
        }
        [Test]
        public void FrozenTransitValidation()
        {
            if (Environment.GetEnvironmentVariable("MPC_FIELD_VALIDATE") != "1") Assert.Ignore("Final validation only after settings freeze.");
            var settings = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var seeds = new List<uint>(DevelopmentSeeds);
            for (uint i = 3101; i <= 3115; i++) seeds.Add(i);
            RunMatrix(new[] { settings.wTerminalField, 0f }, seeds.ToArray(), true);
        }

        [Test]
        public void NoPosAndFieldZeroed_PreserveControlTraces()
        {
            var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset"));
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            try
            {
                var scenario = Array.Find(RigBingoCard.Rows(), row => row.name == "minefield-transit").scenario;
                scenario.durationSeconds = 2f;
                foreach (var hasPos in new[] { false, true })
                {
                    scenario.intent.pos = new PosSlot { armed = hasPos, setpoint = 20f, weight = 1f };
                    if (hasPos) scenario = RigBingoCard.FieldZeroed(scenario);
                    var on = new List<RigTraceRow>();
                    var off = new List<RigTraceRow>();
                    settings.wTerminalField = 1f;
                    MpcSolverRig.Run(settings, dynamics, scenario, 1234u, on);
                    settings.wTerminalField = 0f;
                    MpcSolverRig.Run(settings, dynamics, scenario, 1234u, off);
                    Assert.That(on.Count, Is.EqualTo(off.Count));
                    for (var i = 0; i < on.Count; i++)
                    {
                        Assert.That(on[i].thrust, Is.EqualTo(off[i].thrust));
                        Assert.That(on[i].strafe, Is.EqualTo(off[i].strafe));
                        Assert.That(on[i].yawTorque, Is.EqualTo(off[i].yawTorque));
                        Assert.That(on[i].costTerminalField, Is.Zero);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void FourArmBingoSweep()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in ablation matrix.");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var settings = UnityEngine.Object.Instantiate(asset);
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var outRoot = Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent");
            var outDir = Path.Combine(outRoot, "bingo-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(outDir);
            try
            {
                using var report = new StreamWriter(Path.Combine(outDir, "rows.csv"));
                report.WriteLine("row,arm,seed,finalRange,collisionSteps,pathLength,threatFraction");
                foreach (var row in RigBingoCard.Rows())
                for (var arm = 0; arm < 4; arm++)
                foreach (var seed in DevelopmentSeeds)
                {
                    settings.wTerminalField = arm == 1 || arm == 3 ? 0f : asset.wTerminalField;
                    settings.wObstacle = arm >= 2 ? 0f : asset.wObstacle;
                    var trace = new List<RigTraceRow>();
                    var result = MpcSolverRig.Run(settings, dynamics, row.scenario, seed, trace);
                    report.WriteLine(FormattableString.Invariant($"{row.name},{arm},{seed},{result.finalRange},{result.collisionSteps},{result.pathLength},{result.threatStepFraction}"));
                    report.Flush();
                    RigTraceCsv.Write(Path.Combine(outDir, $"{row.name}-arm{arm}-seed{seed}.csv"), trace);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void FarGoalSpacingProbe()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in terrain resolution experiment.");
            var settings = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var config = settings.ToConfig();
            config.ApplyDynamics(dynamics);
            var rocks = new[]
            {
                new RigCircle { center = new Unity.Mathematics.float2(-10f, 20f), radius = 3f },
                new RigCircle { center = new Unity.Mathematics.float2(10f, 20f), radius = 3f },
            };
            var scanner = new AI.Scanning.ObstacleScanner(null, dynamics.maxSpeed, 0f, settings.horizonSeconds, new RigObstacleField(rocks));
            var outRoot = Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent");
            Directory.CreateDirectory(outRoot);
            using var report = new StreamWriter(Path.Combine(outRoot, $"far-goal-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
            report.WriteLine("goalRange,spacing,occupiedCells,occupiedArea,shipExcess,gapExcess,nearestGapCellOccupied");
            foreach (var distance in new[] { 30f, 90f, 180f, 360f, 720f, 1440f })
            {
                using var field = new AI.Navigation.MPC.TerminalField.TerminalField(settings, dynamics, scanner);
                var goal = new Unity.Mathematics.float2(0f, distance);
                field.Update(0.02f, default, true, goal, config);
                var view = field.View;
                var occupied = field.Occupied;
                var count = 0;
                for (var i = 0; i < occupied.Length; i++) count += occupied[i];
                var gap = new Unity.Mathematics.float2(0f, 20f);
                var coordinate = (Unity.Mathematics.int2)Unity.Mathematics.math.round((gap - view.origin) / view.spacing);
                var index = coordinate.x + coordinate.y * view.resolution;
                report.WriteLine(FormattableString.Invariant($"{distance},{view.spacing},{count},{count * view.spacing * view.spacing},{view.Sample(default)},{view.Sample(gap)},{occupied[index]}"));
                Assert.That(view.Contains(default), Is.True);
                Assert.That(view.Contains(goal), Is.True);
            }
        }
        [Test]
        public void AdditionalSolverSeedSweep()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in solver-seed check on the same authored minefield.");
            var seeds = new uint[20];
            for (var i = 0; i < seeds.Length; i++) seeds[i] = (uint)(3601 + i);
            RunMatrix(new[] { 0f, 0.01f, 0.03f, 0.1f, 0.3f, 1f }, seeds, false);
        }
        [Test]
        public void GridTranslationProbe()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in grid translation diagnosis.");
            var settings = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var scenario = Array.Find(RigBingoCard.Rows(), row => row.name == "minefield-transit").scenario;
            var scanner = new AI.Scanning.ObstacleScanner(null, dynamics.maxSpeed, 0f, settings.horizonSeconds, new RigObstacleField(scenario.obstacles));
            var config = settings.ToConfig();
            config.ApplyDynamics(dynamics);
            var outRoot = Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent");
            Directory.CreateDirectory(outRoot);
            using var report = new StreamWriter(Path.Combine(outRoot, $"grid-translation-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
            report.WriteLine("shipX,shipY,originX,originY,spacing,occupiedCells,atStart,oneMetreAhead,atFixedEndpoint");
            using var field = new AI.Navigation.MPC.TerminalField.TerminalField(settings, dynamics, scanner);
            var firstSample = 0f;
            var maximumChange = 0f;
            for (var i = 0; i <= 80; i++)
            {
                var ship = new Unity.Mathematics.float2(i * 0.1f, 20f);
                field.Update(1f, ship, true, new Unity.Mathematics.float2(0f, 90f), config);
                var view = field.View;
                var sample = view.Sample(new Unity.Mathematics.float2(0f, 35f));
                if (i == 0) firstSample = sample;
                maximumChange = Mathf.Max(maximumChange, Mathf.Abs(sample - firstSample));
                var occupiedCount = 0;
                for (var j = 0; j < field.Occupied.Length; j++) occupiedCount += field.Occupied[j];
                report.WriteLine(FormattableString.Invariant($"{ship.x},{ship.y},{view.origin.x},{view.origin.y},{view.spacing},{occupiedCount},{view.Sample(new Unity.Mathematics.float2(0f,20f))},{view.Sample(new Unity.Mathematics.float2(0f,21f))},{view.Sample(new Unity.Mathematics.float2(0f,35f))}"));
            }
            Assert.That(field.BakeCount, Is.EqualTo(81));
            Assert.That(maximumChange, Is.LessThan(1e-4f), "Static terrain changed only because the grid followed the ship.");
        }
        [Test]
        public void RefinedStableGridWeights()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in stable-grid development refinement.");
            RunMatrix(new[] { 0f, 0.125f, 0.15f, 0.175f, 0.2f, 0.225f, 0.25f, 0.275f, 0.3f }, DevelopmentSeeds, false);
        }
        [Test]
        public void GoalRegionIntermediateWeights()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in goal-region development weights.");
            RunMatrix(new[] { 0f, 0.4f, 0.6f, 0.8f }, DevelopmentSeeds, false);
        }
        [Test]
        public void FreshSolverSeedDiagnostic()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in predeclared fresh solver seeds.");
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var seeds = new uint[15];
            for (var i = 0; i < seeds.Length; i++) seeds[i] = (uint)(3701 + i);
            RunMatrix(new[] { settings.wTerminalField, 0f }, seeds, false);
        }
        private static void RunMatrix(float[] weights, uint[] seeds, bool validate)
        {
            var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset"));
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var row = Array.Find(RigBingoCard.Rows(), value => value.name == "minefield-transit");
            var outRoot = Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent");
            var outDir = Path.Combine(outRoot, (validate ? "validation-" : "development-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(outDir);
            var previousBurstSync = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
            File.WriteAllText(Path.Combine(outDir, "settings.json"), JsonUtility.ToJson(settings, true));
            File.WriteAllText(Path.Combine(outDir, "conditions.txt"),
                $"BurstEnabled={BurstCompiler.Options.EnableBurstCompilation}; SynchronousCompilation=true; maxSpeed={dynamics.maxSpeed}; shipRadius={dynamics.shipRadius}; horizon={settings.horizonSeconds}; bakeInterval={settings.terminalFieldBakeInterval}; resolution={settings.terminalFieldResolution}; minimumSpacing={settings.terminalFieldMinSpacing}");
            var successes = new int[weights.Length];
            var collisions = new int[weights.Length];
            var collisionEpisodes = new int[weights.Length];
            try
            {
                using var report = new StreamWriter(Path.Combine(outDir, "transit.csv"));
                report.WriteLine("weight,seed,finalRange,collisionSteps,pathLength,threatFraction");
                for (var arm = 0; arm < weights.Length; arm++)
                {
                    settings.wTerminalField = weights[arm];
                    foreach (var seed in seeds)
                    {
                        var trace = new List<RigTraceRow>();
                        var result = MpcSolverRig.Run(settings, dynamics, row.scenario, seed, trace);
                        var success = result.finalRange < 20f;
                        if (success) successes[arm]++;
                        collisions[arm] += result.collisionSteps;
                        if (result.collisionSteps > 0) collisionEpisodes[arm]++;
                        var line = FormattableString.Invariant($"{weights[arm]},{seed},{result.finalRange},{result.collisionSteps},{result.pathLength},{result.threatStepFraction}");
                        report.WriteLine(line);
                        report.Flush();
                        RigTraceCsv.Write(Path.Combine(outDir, $"w{weights[arm].ToString(CultureInfo.InvariantCulture)}-seed{seed}.csv"), trace);
                        Debug.Log("[TerminalFieldRig] " + line);
                    }
                    Debug.Log($"[TerminalFieldRig] weight={weights[arm]} successes={successes[arm]}/{seeds.Length} collisionEpisodes={collisionEpisodes[arm]} collisionSteps={collisions[arm]}");
                }
                if (!validate) return;
                Assert.That(successes[0], Is.GreaterThanOrEqualTo(18));
                Assert.That(successes[0] - successes[1], Is.GreaterThanOrEqualTo(4));
                Assert.That(collisions[0], Is.LessThanOrEqualTo(collisions[1]));
                Assert.That(collisionEpisodes[0], Is.LessThanOrEqualTo(collisionEpisodes[1]));
            }
            finally
            {
                BurstCompiler.Options.EnableBurstCompileSynchronously = previousBurstSync;
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
#endif
