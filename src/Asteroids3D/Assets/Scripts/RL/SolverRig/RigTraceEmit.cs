#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AI.Navigation.MPC;
using Movement;
using Ships;
using Substrate.Results;
using UnityEditor;
using UnityEngine;

namespace RL.SolverRig
{
    /// <summary>
    /// Editor entries that write full solver-rig traces as CSVs under results/mpc-rig, for offline
    /// plotting with training/rl/plot_rig_trace.py. No code calls them: run one from a held editor
    /// over the unity CLI (eval), or from a batch child with -executeMethod and -quit. They run the
    /// production MpcSettings asset and Vanguard dynamics, so a trace characterizes the shipped controller.
    /// </summary>
    public static class RigTraceEmit
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset";
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const string ResultsFolder = "mpc-rig";

        public static void BingoRows()
        {
            LoadShippedController(out var settings, out var dynamics);
            var outDir = Path.Combine(ResultsRoot.Folder(ResultsFolder), "bingo");
            Directory.CreateDirectory(outDir);

            foreach (var (name, scenario) in BingoVariants())
            {
                var trace = new List<RigTraceRow>();
                var result = MpcSolverRig.Run(settings, dynamics, in scenario, 1234u, trace);
                RigTraceCsv.Write(Path.Combine(outDir, $"{name}.csv"), trace);
                Debug.Log($"[Bingo] {name} | strict {result.torqueReversalsPerSec:F2}/s | " +
                          $"deadband {result.torqueDeadbandReversalsPerSec:F2}/s | " +
                          $"|yawRate| {result.meanAbsYawRateDegPerSec:F1} deg/s | " +
                          $"facing err {result.meanFacingErrorDeg:F1} deg (p90 {result.p90FacingErrorDeg:F1}) | " +
                          $"range {result.finalRange:F1} | threat {result.threatStepFraction:P1} | " +
                          $"incumbent wins {result.incumbentWinFraction:P1}");
            }
        }

        public static void VersusDummy()
        {
            LoadShippedController(out var settings, out var dynamics);
            var outDir = ResultsRoot.Folder(ResultsFolder);
            Directory.CreateDirectory(outDir);

            foreach (var startErrorDeg in new[] { 0f, 90f })
            foreach (var seed in new uint[] { 1234u, 99u, 7u })
            {
                var scenario = RigScenario.VersusDummy(40f, startErrorDeg);
                var trace = new List<RigTraceRow>();
                var result = MpcSolverRig.Run(settings, dynamics, in scenario, seed, trace);
                var path = Path.Combine(outDir, $"trace-dummy-err{startErrorDeg:F0}-seed{seed}.csv");
                RigTraceCsv.Write(path, trace);
                Debug.Log($"[MpcSolverRig] err{startErrorDeg:F0} seed {seed} | strict {result.torqueReversalsPerSec:F2}/s | " +
                          $"deadband {result.torqueDeadbandReversalsPerSec:F2}/s | " +
                          $"|yawRate| {result.meanAbsYawRateDegPerSec:F1} deg/s | " +
                          $"facing err {result.meanFacingErrorDeg:F1} deg (p90 {result.p90FacingErrorDeg:F1}) | " +
                          $"range {result.finalRange:F1} | incumbent wins {result.incumbentWinFraction:P1} | " +
                          $"|emit-incumbent yaw| {result.meanAbsEmitYawDeltaFromIncumbent:F3}");
            }
        }

        /// <summary>Every card row, plus the movement rows' VEL-zeroed protocol arms.</summary>
        private static IEnumerable<(string name, RigScenario scenario)> BingoVariants()
        {
            foreach (var row in RigBingoCard.Rows())
            {
                yield return (row.name, row.scenario);
                if (row.movement)
                    yield return (row.name + "-velzero", RigBingoCard.VelZeroed(row.scenario));
            }
        }

        private static void LoadShippedController(out MpcSettings settings, out Dynamics dynamics)
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            if (!settings) throw new InvalidOperationException($"Missing MPC settings at {MpcSettingsPath}");
            if (!ship) throw new InvalidOperationException($"Missing ship prefab at {ShipPrefabPath}");
            dynamics = ship.ResolveStats().Dynamics;
        }
    }
}
#endif
