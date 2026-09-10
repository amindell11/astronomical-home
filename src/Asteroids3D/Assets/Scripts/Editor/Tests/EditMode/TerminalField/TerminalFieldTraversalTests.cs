using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AI.Navigation.MPC;
using NUnit.Framework;
using RL.SolverRig;
using Ships;
using Unity.Burst;
using Unity.Mathematics;
using UnityEditor;

namespace Tests.EditMode.TerminalField
{
    [Category("MPC")]
    public sealed class TerminalFieldTraversalTests
    {
        private const float Seconds = 30f;

        [Test]
        public void Development() => Run(false);

        [Test]
        public void HeldOut() => Run(true);

        [Test]
        public void SweptCollision_CatchesTunnellingTangencyAndStationaryOverlap()
        {
            Assert.That(Hits(new float2(-5, 0), new float2(5, 0), default, 1), Is.True);
            Assert.That(Hits(new float2(-5, 1), new float2(5, 1), default, 1), Is.True);
            Assert.That(Hits(new float2(-5, 1.01f), new float2(5, 1.01f), default, 1), Is.False);
            Assert.That(Hits(default, default, default, 1), Is.True);
            Assert.That(Hits(new float2(2, 0), new float2(2, 0), default, 1), Is.False);
        }

        [Test]
        public void FarClutter_DoesNotHideNearObstacleFromSolver()
        {
            var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset"));
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            try
            {
                settings.wTerminalField = 0;
                var scenario = Course(dynamics.maxSpeed, 4101);
                scenario.durationSeconds = 1;
                var near = new RigCircle(new float2(0, 10), 3);
                scenario.obstacles = new[] { near };
                var clean = new List<RigTraceRow>();
                MpcSolverRig.Run(settings, dynamics, scenario, 4101, clean);
                scenario.obstacles = Enumerable.Range(0, 120).Select(i => new RigCircle(new float2(1000 + i, 0), 3)).Concat(new[] { near }).ToArray();
                var cluttered = new List<RigTraceRow>();
                MpcSolverRig.Run(settings, dynamics, scenario, 4101, cluttered);
                Assert.That(cluttered.Select(r => (r.thrust, r.strafe, r.yawTorque)), Is.EqualTo(clean.Select(r => (r.thrust, r.strafe, r.yawTorque))));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        [Test]
        public void ResolutionDiagnostic() => Run(false, 96, 4104);

        [Test]
        public void WeightDiagnostic() => Run(false, 0, 4104, .3f);

        [Test]
        public void WeightDevelopment() => Run(false, 0, 0, .3f);

        private static void Run(bool heldOut, int resolution = 0, uint seedOverride = 0, float? weightOverride = null)
        {
            if (Environment.GetEnvironmentVariable("MPC_TRAVERSAL") != (weightOverride.HasValue ? "weight" : resolution > 0 ? "resolution" : heldOut ? "held-out" : "development"))
                Assert.Ignore("Opt-in traversal benchmark.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var directory = Path.Combine(output, (weightOverride.HasValue ? $"weight{weightOverride.Value}-seed{seedOverride}-" : resolution > 0 ? $"resolution{resolution}-seed{seedOverride}-" : heldOut ? "held-out-" : "development-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var settings = UnityEngine.Object.Instantiate(asset);
            if (resolution > 0) settings.terminalFieldResolution = resolution;
            if (weightOverride.HasValue) settings.wTerminalField = weightOverride.Value;
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var priorSync = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
            var on = new List<float>();
            var off = new List<float>();
            var collisions = 0;
            try
            {
                File.WriteAllText(Path.Combine(directory, "settings.json"), UnityEngine.JsonUtility.ToJson(settings, true));
                File.WriteAllText(Path.Combine(directory, "conditions.txt"), FormattableString.Invariant($"maxSpeed={dynamics.maxSpeed}; shipRadius={dynamics.shipRadius}; seconds={Seconds}; goalRange={2 * dynamics.maxSpeed * Seconds}; Burst={BurstCompiler.Options.EnableBurstCompilation}"));
                using var report = new StreamWriter(Path.Combine(directory, "summary.csv"));
                report.WriteLine("seed,arm,weight,progressSpeed,actualSpeed,stalledSeconds,routeEfficiency,emptyProgressSpeed,emptyFraction,sweptCollisionSteps,endpointCollisionSteps,fieldSpacing,meanTerminalCost,obstacles");
                var first = seedOverride != 0 ? seedOverride : heldOut ? 4201u : 4101u;
                var count = seedOverride != 0 ? 1 : heldOut ? 20 : 5;
                for (var index = 0; index < count; index++)
                {
                    var seed = first + (uint)index;
                    var scenario = Course(dynamics.maxSpeed, seed);
                    using (var terrain = new StreamWriter(Path.Combine(directory, $"seed{seed}-terrain.csv")))
                    {
                        terrain.WriteLine("x,y,radius");
                        foreach (var rock in scenario.obstacles)
                            terrain.WriteLine(FormattableString.Invariant($"{rock.center.x},{rock.center.y},{rock.radius}"));
                    }
                    for (var arm = 0; arm < 2; arm++)
                    {
                        settings.wTerminalField = arm == 0 ? weightOverride ?? asset.wTerminalField : 0f;
                        var trace = new List<RigTraceRow>();
                        var result = MpcSolverRig.Run(settings, dynamics, scenario, seed, trace);
                        RigTraceCsv.Write(Path.Combine(directory, $"seed{seed}-arm{arm}.csv"), trace);
                        Assert.That(trace.Count, Is.EqualTo(1500));
                        var empty = scenario;
                        empty.obstacles = Array.Empty<RigCircle>();
                        var emptyResult = MpcSolverRig.Run(settings, dynamics, empty, seed);
                        var initialRange = math.length(scenario.referent1Law.p0);
                        var progress = initialRange - result.finalRange;
                        var speed = progress / Seconds;
                        var emptySpeed = (initialRange - emptyResult.finalRange) / Seconds;
                        Assert.That(emptySpeed, Is.GreaterThan(0f), "Empty-course command must make forward progress.");
                        var cfg = settings.ToConfig();
                        cfg.ApplyDynamics(dynamics);
                        cfg.dt = scenario.simDt;
                        var swept = 0;
                        var stalled = 0f;
                        for (var tick = 0; tick < trace.Count; tick++)
                        {
                            var row = trace[tick];
                            var start = new float2(row.posX, row.posY);
                            var control = new Control { thrust = row.thrust, strafe = row.strafe, yawTorque = row.yawTorque };
                            var next = tick + 1 < trace.Count ? new float2(trace[tick + 1].posX, trace[tick + 1].posY)
                                : Model.Step(new State { pos = start, vel = new float2(row.velX, row.velY), yaw = math.radians(row.yawDeg), yawRate = math.radians(row.yawRateDegPerSec) }, control, cfg, dynamics).pos;
                            var toward = (math.distance(start, scenario.referent1Law.p0) - math.distance(next, scenario.referent1Law.p0)) / scenario.simDt;
                            if (toward < .05f * dynamics.maxSpeed) stalled += scenario.simDt;
                            foreach (var rock in scenario.obstacles)
                            {
                                var radius = dynamics.shipRadius * Cost.BankProfileScale(row.strafe, cfg) + rock.radius;
                                if (!Hits(start, next, rock.center, radius)) continue;
                                swept++;
                                break;
                            }
                            if (tick == trace.Count - 1)
                                Assert.That(math.distance(next, scenario.referent1Law.p0), Is.EqualTo(result.finalRange).Within(.001f));
                        }
                        Assert.That(swept, Is.GreaterThanOrEqualTo(result.collisionSteps));
                        if (arm == 0) { on.Add(speed); collisions += swept; } else off.Add(speed);
                        var efficiency = result.pathLength > 0 ? progress / result.pathLength : 0f;
                        report.WriteLine(FormattableString.Invariant($"{seed},{arm},{settings.wTerminalField},{speed},{result.pathLength / Seconds},{stalled},{efficiency},{emptySpeed},{speed / emptySpeed},{swept},{result.collisionSteps},{trace[0].fieldSpacing},{trace.Average(r => r.costTerminalField)},{scenario.obstacles.Length}"));
                        report.Flush();
                    }
                }
                on.Sort(); off.Sort();
                var medianOn = (on[(on.Count - 1) / 2] + on[on.Count / 2]) * .5f;
                var medianOff = (off[(off.Count - 1) / 2] + off[off.Count / 2]) * .5f;
                File.WriteAllText(Path.Combine(directory, "verdict.txt"), FormattableString.Invariant($"fieldOnMedian={medianOn}; fieldOffMedian={medianOff}; ratio={medianOn / medianOff}; fieldOnCollisionSteps={collisions}; accepted={collisions == 0 && medianOn >= 1.1f * medianOff}"));
                Assert.That(collisions == 0 && medianOn >= 1.1f * medianOff, Is.True,
                    $"Requires zero collisions and 10% higher median progress: collisions={collisions}, on={medianOn}, off={medianOff}.");
            }
            finally
            {
                BurstCompiler.Options.EnableBurstCompileSynchronously = priorSync;
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static RigScenario Course(float maxSpeed, uint seed)
        {
            var reach = maxSpeed * Seconds;
            var rng = new Unity.Mathematics.Random(seed);
            var rocks = new List<RigCircle>();
            var cells = (int)math.ceil((reach + 8f) / 16f);
            for (var x = -cells; x <= cells; x++)
            for (var y = -cells; y <= cells; y++)
            {
                var center = new float2(x * 16, y * 16) + rng.NextFloat2(-4f, 4f);
                var radius = rng.NextFloat(2.5f, 4f);
                if (math.length(center) < 12f + radius || math.length(center) > reach + 8f) continue;
                rocks.Add(new RigCircle(center, radius));
            }
            return new RigScenario
            {
                referent1Law = RigLaw.Static(new float2(0, 2 * reach)),
                obstacles = rocks.ToArray(),
                intent = new IntentSentence
                {
                    pos = new PosSlot { armed = true, referent = 1, weight = 1 },
                    vel = new VelSlot { armed = true, referent = 1, radialSpeed = maxSpeed, weight = 1 },
                    field = new FieldSlot { armed = true, weight = 1 },
                },
                simDt = .02f,
                durationSeconds = Seconds,
                projectileSpeed = 60f,
            };
        }

        private static bool Hits(float2 start, float2 end, float2 center, float radius)
        {
            var delta = end - start;
            var lengthSq = math.lengthsq(delta);
            var t = lengthSq > 0 ? math.saturate(math.dot(center - start, delta) / lengthSq) : 0f;
            return math.distancesq(start + t * delta, center) <= radius * radius;
        }
    }
}
