#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using AI.Navigation.MPC;
using AI.Navigation.MPC.TerminalField;
using AI.Scanning;
using NUnit.Framework;
using Movement;
using RL.Arena;
using Tests.PlayMode.Common;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    [Category("MPC")]
    public class TerminalFieldPerformanceTests : PlayModeWorldFixture
    {
        [UnityTest]
        public IEnumerator WarmedBurstBakeMatrix()
        {
            if (Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in solo-machine benchmark.");
            var assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            var dynamics = assets.ShipPrefab.ResolveStats().Dynamics;
            var settings = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var originalPresentation = GameSettings.PresentationEnabled;
            var outDir = Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent");
            Directory.CreateDirectory(outDir);
            using var report = new StreamWriter(Path.Combine(outDir, $"microbench-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
            report.WriteLine("density,resolution,obstacles,allocationMs,gatherMedianMs,gatherP95Ms,runMedianMs,runP95Ms,scheduleMedianMs,scheduleP95Ms,managedBytes");
            GameSettings.SetPresentationEnabled(false);
            try
            {
                foreach (var density in new[] { 2f, 2.5f })
                {
                    using var field = HarnessField.Spawn(Vector2.zero, assets, density, presentationEnabled: false);
                    field.Rebuild(3501, Vector2.zero, new Vector2(0f, 90f));
                    yield return new WaitForFixedUpdate();
                    var scanner = new ObstacleScanner(null, dynamics.maxSpeed, 0f, settings.horizonSeconds, field.Field);
                    MeasureSolverLoop(settings, dynamics, field, outDir, density);
                    foreach (var resolution in new[] { 32, 48, 64 })
                    {
                        var half = (resolution - 1) * 2f;
                        var clearance = dynamics.shipRadius + settings.collisionSafetyMargin;
                        var buffer = new DetectedObstacle[64];
                        var scan = scanner.Query(Vector2.zero, half + 2f + clearance, ref buffer);
                        var clock = Stopwatch.StartNew();
                        using var obstacles = new NativeArray<ObstacleData>(math.max(1, scan.count * 3), Allocator.TempJob);
                        using var distance = new NativeArray<float>(resolution * resolution, Allocator.TempJob);
                        using var emptyDistance = new NativeArray<float>(resolution * resolution, Allocator.TempJob);
                        using var occupied = new NativeArray<byte>(resolution * resolution, Allocator.TempJob);
                        using var heap = new NativeArray<int>(resolution * resolution, Allocator.TempJob);
                        using var positions = new NativeArray<int>(resolution * resolution, Allocator.TempJob);
                        using var result = new NativeArray<TerminalFieldBakeJob.BakeResult>(1, Allocator.TempJob);
                        var allocationMs = clock.Elapsed.TotalMilliseconds;
                        var count = 0;
                        var writableObstacles = obstacles;
                        for (var i = 0; i < scan.count; i++)
                        {
                            var obstacle = scan.buffer[i];
                            if (settings.multiSphereObstacles && obstacle.lobeCount > 1)
                                for (var l = 0; l < obstacle.lobeCount; l++)
                                {
                                    var lobe = obstacle.Lobe(l);
                                    writableObstacles[count++] = new ObstacleData { position = new float2(lobe.center.x, lobe.center.y), radius = lobe.radius };
                                }
                            else writableObstacles[count++] = new ObstacleData { position = new float2(obstacle.position.x, obstacle.position.y), radius = obstacle.radius };
                        }
                        var job = new TerminalFieldBakeJob
                        {
                            obstacles = obstacles, obstacleCount = count, resolution = resolution, spacing = 4f,
                            origin = new float2(-half), goal = new float2(0f, 90f), clearance = clearance,
                            distances = distance, emptyDistances = emptyDistance, occupied = occupied, heap = heap, heapPosition = positions, result = result,
                        };
                        for (var i = 0; i < 20; i++) { job.Run(); job.Schedule().Complete(); }
                        var gather = new double[200];
                        var run = new double[200];
                        var scheduled = new double[200];
                        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                        for (var i = 0; i < 200; i++)
                        {
                            clock.Restart();
                            scanner.Query(Vector2.zero, half + 2f + clearance, ref buffer);
                            gather[i] = clock.Elapsed.TotalMilliseconds;
                            clock.Restart(); job.Run(); run[i] = clock.Elapsed.TotalMilliseconds;
                            clock.Restart(); job.Schedule().Complete(); scheduled[i] = clock.Elapsed.TotalMilliseconds;
                        }
                        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                        Array.Sort(gather); Array.Sort(run); Array.Sort(scheduled);
                        report.WriteLine(FormattableString.Invariant($"{density},{resolution},{count},{allocationMs},{gather[100]},{gather[189]},{run[100]},{run[189]},{scheduled[100]},{scheduled[189]},{allocated}"));
                        report.Flush();
                        Assert.That(allocated, Is.Zero, "Steady-state scanner + bake allocated managed memory.");
                    }
                }
            }
            finally { GameSettings.SetPresentationEnabled(originalPresentation); }
        }
        private static void MeasureSolverLoop(MpcSettings settings, Dynamics dynamics, HarnessField field, string outDir, float density)
        {
            var origin = new GameObject("terminal-field-performance-origin");
            try
            {
                var scanner = new ObstacleScanner(origin.transform, dynamics.maxSpeed,
                    dynamics.forwardAcc / dynamics.mass, settings.horizonSeconds, field.Field);
                scanner.Scan();
                using var withField = new Mpc(settings, dynamics, 3501u, scanner);
                using var baseline = new Mpc(settings, dynamics, 3501u);
                var inputs = new MpcInputs
                {
                    dt = 0.02f,
                    kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f),
                    enemyYaw = float.NaN, facingRad = float.NaN, velocityReference = new float2(float.NaN, 0f),
                    referent1 = new ReferentSnapshot { valid = true, pos = new float2(0f, 90f) },
                    sentence = new IntentSentence { pos = new PosSlot { armed = true, referent = 1, weight = 1f } },
                    obstacleScan = new ObstacleScan(scanner.DetectedBuffer, scanner.DetectedCount),
                    enableObstacleAvoidance = true,
                };
                for (var i = 0; i < 100; i++) { baseline.Plan(inputs); withField.Plan(inputs); }
                var before = new double[1000];
                var after = new double[1000];
                var clock = new Stopwatch();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < before.Length; i++)
                {
                    clock.Restart(); baseline.Plan(inputs); before[i] = clock.Elapsed.TotalMilliseconds;
                    clock.Restart(); withField.Plan(inputs); after[i] = clock.Elapsed.TotalMilliseconds;
                }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                using var report = new StreamWriter(Path.Combine(outDir, $"solver-loop-d{density}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
                report.WriteLine("sample,noTerrainSourceMs,withFieldMs");
                for (var i = 0; i < before.Length; i++) report.WriteLine(FormattableString.Invariant($"{i},{before[i]},{after[i]}"));
                Assert.That(allocated, Is.Zero, "Warmed full solver loop allocated managed memory.");
            }
            finally { UnityEngine.Object.DestroyImmediate(origin); }
        }

    }
}
#endif
