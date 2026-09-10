#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using AI;
using AI.Navigation.MPC;
using Game.Services.Units;
using NUnit.Framework;
using RL.Arena;
using RL.Episodes;
using RL.Hosts;
using RL.Hosts.Lanes;
using RL.Opponents;
using Tests.PlayMode.Common;
using Tests.PlayMode.Scenarios;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode.TerminalField
{
    [Category("MPC")]
    public class TerminalFieldSessionTests : PlayModeWorldFixture
    {
        [Serializable]
        private struct Measurement
        {
            public string row;
            public int seed;
            public int arm;
            public string outcome;
            public float seconds;
            public int samples;
            public float meanError;
            public float integratedError;
            public float closeoutSeconds;
            public bool reachedCloseout;
            public int collisionSteps;
            public bool success;
        }

        [UnityTest]
        [Timeout(7200000)]
        public IEnumerator SentenceMatrix()
        {
            var validate = Environment.GetEnvironmentVariable("MPC_FIELD_VALIDATE") == "1";
            if (!validate && Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1") Assert.Ignore("Opt-in terminal-field session experiments.");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var developmentWeight = Environment.GetEnvironmentVariable("MPC_FIELD_DEV_WEIGHT");
            var developmentRow = Environment.GetEnvironmentVariable("MPC_FIELD_DEV_ROW");
            if (validate && (developmentWeight != null || developmentRow != null))
                throw new InvalidOperationException("Final validation cannot use development overrides.");
            var weight = developmentWeight == null ? asset.wTerminalField : float.Parse(developmentWeight, CultureInfo.InvariantCulture);
            var rows = developmentRow == null ? SentenceRows.SessionRows
                : developmentRow.Split(',').Select(token => (SentenceRow)Enum.Parse(typeof(SentenceRow), token)).ToArray();
            var assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            var root = new GameObject("terminal-field-session");
            var units = root.AddComponent<UnitService>();
            units.SetProjectiles(Projectiles);
            var hostObject = new GameObject("terminal-field-host");
            hostObject.transform.SetParent(root.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessSessionHost>();
            host.Initialize(new SessionSpec { probes = Array.Empty<ProbeSpec>() }, assets, units, Projectiles);
            var outRoot = Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent");
            Directory.CreateDirectory(outRoot);
            var output = Path.Combine(outRoot, $"sessions-{(validate ? "validation" : "development")}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl");
            File.WriteAllText(output + ".settings.json", JsonUtility.ToJson(asset, true));
            File.WriteAllText(output + ".conditions.txt", $"terminalWeight={weight}; developmentRow={developmentRow ?? "all"}; density=2; firstSeed={(validate ? 3201 : 3401)}; seedCount={(validate ? 15 : 3)}");
            var results = new List<Measurement>();
            var previousPresentation = GameSettings.PresentationEnabled;
            var previousScale = Time.timeScale;
            var previousCapture = Time.captureDeltaTime;
            GameSettings.SetPresentationEnabled(false);
            PacingContract.Apply();
            try
            {
                using var field = HarnessField.Spawn(Vector2.zero, assets, 2f, root.transform, presentationEnabled: false);
                yield return new WaitForFixedUpdate();
                foreach (var row in rows)
                for (var arm = 0; arm < 4; arm++)
                for (var index = 0; index < (validate ? 15 : 3); index++)
                {
                    var spec = EvalProtocol.EvalSpec(2f);
                    spec.runSeed = (validate ? 3201 : 3401) + index;
                    using var composition = host.NewSentenceComposition(spec, field, row);
                    var nav = composition.Pair.Agent.GetComponentInChildren<Navigator>();
                    var settings = UnityEngine.Object.Instantiate(asset);
                    settings.wTerminalField = arm == 1 || arm == 3 ? 0f : weight;
                    settings.wObstacle = arm >= 2 ? 0f : asset.wObstacle;
                    try
                    {
                        nav.mpcSettings = settings;
                        var opponent = SentenceRows.Block(row);
                        var draw = composition.InstallOpponent(opponent, spec, 0, Vector2.zero);
                        var contacts = composition.Pair.Agent.gameObject.AddComponent<TerminalFieldContactCounter>();
                        var start = Vector2.zero;
                        var measurement = new Measurement
                        {
                            row = SentenceRows.Token(row), seed = spec.runSeed, arm = arm,
                            closeoutSeconds = spec.timeoutDecisions * spec.decisionIntervalSteps * Time.fixedDeltaTime,
                        };
                        using var trace = Environment.GetEnvironmentVariable("MPC_FIELD_TRACE") == "1"
                            ? new StreamWriter(output + $".{measurement.row}-{measurement.seed}-arm{arm}.csv") : null;
                        trace?.WriteLine("seconds,x,y,targetX,targetY,range,endpointX,endpointY,endpointRange,fieldHere,fieldEndpoint,spacing,bakes,thrust,strafe,yawTorque,contacts");
                        var lastTracedBake = -1;
                        var lastTraceOrigin = new Unity.Mathematics.float2(float.NaN);
                        var elapsed = 0f;
                        var error = 0f;
                        yield return composition.Driver.RunEpisode(spec, 0,
                            onBegin: () => start = composition.Pair.Agent.Kinematics.pos,
                            onFixedStep: () =>
                            {
                                elapsed += Time.fixedDeltaTime;
                                var ship = composition.Pair.Agent.Kinematics;
                                var target = composition.Pair.Baseline.Kinematics;
                                var range = Vector2.Distance(ship.pos, target.pos);
                                if (trace != null && nav.mpc.TerminalField.BakeCount != lastTracedBake)
                                {
                                    var owner = nav.mpc.TerminalField;
                                    var view = owner.View;
                                    if (!view.origin.Equals(lastTraceOrigin))
                                    {
                                        var snapshot = output + $".{measurement.row}-{measurement.seed}-arm{arm}-bake{owner.BakeCount}";
                                        using var gridTrace = new StreamWriter(snapshot + ".grid.csv");
                                        gridTrace.WriteLine("x,y,occupied,distance,excess");
                                        for (var cell = 0; cell < view.distances.Length; cell++)
                                        {
                                            var center = view.CellCenter(cell);
                                            gridTrace.WriteLine(FormattableString.Invariant($"{center.x},{center.y},{owner.Occupied[cell]},{view.distances[cell]},{view.Excess(cell)}"));
                                        }
                                        var scanner = new AI.Scanning.ObstacleScanner(null, 0f, 0f, 0f, field.Field);
                                        AI.Scanning.DetectedObstacle[] buffer = null;
                                        var centerPlane = view.origin + view.spacing * (view.resolution - 1) * .5f;
                                        var obstacles = scanner.Query(new Vector2(centerPlane.x, centerPlane.y), view.spacing * view.resolution, ref buffer);
                                        using var terrainTrace = new StreamWriter(snapshot + ".terrain.csv");
                                        terrainTrace.WriteLine("x,y,radius,clearance");
                                        var clearance = nav.mpc.Dynamics.shipRadius + nav.mpc.Config.collisionSafetyMargin;
                                        for (var rock = 0; rock < obstacles.count; rock++)
                                        {
                                            var obstacle = obstacles.buffer[rock];
                                            var useLobes = settings.multiSphereObstacles && obstacle.lobeCount > 1;
                                            var lobes = useLobes ? obstacle.lobeCount : 1;
                                            for (var lobeIndex = 0; lobeIndex < lobes; lobeIndex++)
                                            {
                                                var lobe = useLobes ? obstacle.Lobe(lobeIndex)
                                                    : new AI.Scanning.DetectedObstacle.PlaneCircle(obstacle.position, obstacle.radius);
                                                terrainTrace.WriteLine(FormattableString.Invariant($"{lobe.center.x},{lobe.center.y},{lobe.radius},{clearance}"));
                                            }
                                        }
                                        lastTraceOrigin = view.origin;
                                    }
                                    var endpoint = nav.mpc.PredictedStates[nav.mpc.PredictedStates.Length - 1].pos;
                                    var control = nav.mpc.LastControl;
                                    trace.WriteLine(FormattableString.Invariant($"{elapsed},{ship.pos.x},{ship.pos.y},{target.pos.x},{target.pos.y},{range},{endpoint.x},{endpoint.y},{Vector2.Distance(new Vector2(endpoint.x, endpoint.y), target.pos)},{view.Sample(new Unity.Mathematics.float2(ship.pos.x, ship.pos.y))},{view.Sample(endpoint)},{view.spacing},{owner.BakeCount},{control.thrust},{control.strafe},{control.yawTorque},{contacts.Steps}"));
                                    lastTracedBake = owner.BakeCount;
                                }
                                if (!measurement.reachedCloseout && range < 10f)
                                {
                                    measurement.reachedCloseout = true;
                                    measurement.closeoutSeconds = elapsed;
                                }
                                if (elapsed < 2f) return;
                                var movementError = MovementError(row, ship.pos, target.pos, target.yaw, start);
                                error += movementError;
                                measurement.integratedError += movementError * Time.fixedDeltaTime;
                                measurement.samples++;
                            });
                        composition.Driver.Runner.RecordOpponent(draw);
                        var outcome = composition.Driver.Runner.Result;
                        measurement.outcome = outcome.outcome;
                        measurement.seconds = outcome.simSeconds;
                        measurement.collisionSteps = contacts.Steps;
                        if (measurement.samples > 0) measurement.meanError = error / measurement.samples;
                        measurement.success = row == SentenceRow.DummyCloseout ? outcome.outcome == "Win" : outcome.outcome != "Loss";
                        File.AppendAllText(output, JsonUtility.ToJson(measurement) + "\n");
                        Assert.That(measurement.samples, Is.GreaterThan(0), "No post-warmup movement samples; raw outcome was recorded.");
                        results.Add(measurement);
                        Debug.Log($"[TerminalFieldSession] {measurement.row} seed={spec.runSeed} arm={arm} outcome={outcome.outcome} error={measurement.meanError:F3}");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(settings); }
                    Projectiles.ReturnAllToPool();
                }
                if (validate) CheckRegression(results);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Time.timeScale = previousScale;
                Time.captureDeltaTime = previousCapture;
                GameSettings.SetPresentationEnabled(previousPresentation);
            }
        }

        private static float MovementError(SentenceRow row, Vector2 ship, Vector2 target, float yawDeg, Vector2 start)
        {
            var range = Vector2.Distance(ship, target);
            switch (row)
            {
                case SentenceRow.Orbit: return Mathf.Abs(range - 16f);
                case SentenceRow.Kite: return Mathf.Abs(range - 18f);
                case SentenceRow.CoverTake: return Mathf.Abs(range - 30f);
                case SentenceRow.DummyCloseout: return Mathf.Abs(range - 6f);
                case SentenceRow.DriftHold: return Vector2.Distance(ship, start);
                case SentenceRow.FireLaneDodge:
                    var angle = yawDeg * Mathf.Deg2Rad + Mathf.PI * 0.5f;
                    return Vector2.Distance(ship, target + 12f * new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)));
                default: throw new ArgumentOutOfRangeException(nameof(row));
            }
        }

        private static void CheckRegression(List<Measurement> results)
        {
            foreach (var row in SentenceRows.SessionRows)
            {
                var on = results.FindAll(m => m.row == SentenceRows.Token(row) && m.arm == 0);
                var off = results.FindAll(m => m.row == SentenceRows.Token(row) && m.arm == 1);
                Assert.That(on.FindAll(m => m.success).Count, Is.GreaterThanOrEqualTo(off.FindAll(m => m.success).Count - 1), row.ToString());
                var errorOff = Median(off.ConvertAll(m => row == SentenceRow.DummyCloseout ? m.integratedError : m.meanError));
                Assert.That(Median(on.ConvertAll(m => row == SentenceRow.DummyCloseout ? m.integratedError : m.meanError)), Is.LessThanOrEqualTo(errorOff + Mathf.Max(1f, errorOff * 0.1f)), row.ToString());
                if (row == SentenceRow.DummyCloseout)
                {
                    var timeOff = Median(off.ConvertAll(m => m.closeoutSeconds));
                    Assert.That(Median(on.ConvertAll(m => m.closeoutSeconds)), Is.LessThanOrEqualTo(timeOff + Mathf.Max(1f, timeOff * 0.1f)));
                }
                Assert.That(on.FindAll(m => m.collisionSteps > 0).Count, Is.LessThanOrEqualTo(off.FindAll(m => m.collisionSteps > 0).Count), row.ToString());
                Assert.That(on.ConvertAll(m => m.collisionSteps).ToArray().Sum(), Is.LessThanOrEqualTo(off.ConvertAll(m => m.collisionSteps).ToArray().Sum()), row.ToString());
            }
        }

        private static float Median(List<float> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }
    }
}
#endif
