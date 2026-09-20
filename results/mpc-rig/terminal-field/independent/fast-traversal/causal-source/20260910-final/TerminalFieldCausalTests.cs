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
    public sealed class TerminalFieldCausalTests
    {
        [Test]
        public void FirstEncounterHorizons() => Run(false);

        [Test]
        public void GoalFacing() => Run(true);

        [Test]
        public void LiveSensingHorizons() => Run(false, true);

        private static void Run(bool faceGoal, bool liveSensing = false)
        {
            if (Environment.GetEnvironmentVariable("MPC_CAUSAL") != (liveSensing ? "live-sensing" : faceGoal ? "facing" : "horizon")) Assert.Ignore("Opt-in causal probe.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var directory = Path.Combine(output, (liveSensing ? "live-sensing-" : faceGoal ? "facing-" : "encounter-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var priorSync = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
            try
            {
                var warmup = TerminalFieldTraversalTests.Course(dynamics.maxSpeed, 4103);
                warmup.obstacles = Array.Empty<RigCircle>(); warmup.durationSeconds = .1f;
                MpcSolverRig.Run(asset, dynamics, warmup, 4103);
                using var report = new StreamWriter(Path.Combine(directory, "summary.csv"));
                report.WriteLine("seed,case,horizon,weight,spacing,progressSpeed,sweptCollisions,backwardSeconds,meanForwardSpeed,planMedianMs,planP95Ms,bakeMedianMs,bakeP95Ms");
                foreach (var seed in new uint[] { 4103, 4105 })
                for (var variant = 0; variant < (liveSensing ? 5 : faceGoal ? 4 : seed == 4103 ? 7 : 5); variant++)
                {
                    var settings = UnityEngine.Object.Instantiate(asset);
                    try
                    {
                        var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed, seed);
                        scenario.durationSeconds = 12f;
                        if (liveSensing)
                        {
                            var maxAccel = math.sqrt(dynamics.forwardAcc * dynamics.forwardAcc + dynamics.maxStrafeAcc * dynamics.maxStrafeAcc) / dynamics.mass;
                            var lookahead = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Pilots/AgentPilot.prefab").GetComponent<AI.Scout>().obstacleLookaheadTime;
                            scenario.obstacleScanHalfExtent = dynamics.maxSpeed * lookahead + .5f * maxAccel * lookahead * lookahead;
                        }
                        if (faceGoal) scenario.intent.aim = new AimSlot { armed = true, referent = 1, weight = 1f };
                        settings.horizonSeconds = variant == 0 || variant == 1 || variant == 5 ? 1.7f : 1.2f;
                        settings.wTerminalField = variant == 1 || variant == 3 || variant >= 5 ? 0f : 3f;
                        var name = new[] { "long-on", "long-off", "short-on", "short-off", "short-fixed-grid", "empty-long", "empty-short" }[variant];
                        if (variant >= 5) scenario.obstacles = Array.Empty<RigCircle>();
                        if (variant == 4)
                        {
                            var cfg = asset.ToConfig(); cfg.ApplyDynamics(dynamics);
                            var reach = dynamics.maxSpeed * (cfg.horizon * cfg.dt + asset.terminalFieldBakeInterval) + dynamics.shipRadius + cfg.collisionSafetyMargin;
                            settings.terminalFieldMinSpacing = math.max(asset.terminalFieldMinSpacing,
                                (math.cmax(math.abs(scenario.referent1Law.p0)) * .5f + reach) / ((asset.terminalFieldResolution - 1) * .5f - 2f));
                        }
                        File.WriteAllText(Path.Combine(directory, $"seed{seed}-{name}-settings.json"), UnityEngine.JsonUtility.ToJson(settings, true));
                        var trace = new List<RigTraceRow>();
                        var result = MpcSolverRig.Run(settings, dynamics, scenario, seed, trace);
                        Assert.That(trace.Count, Is.EqualTo(600));
                        RigTraceCsv.Write(Path.Combine(directory, $"seed{seed}-{name}.csv"), trace);
                        var config = settings.ToConfig(); config.ApplyDynamics(dynamics);
                        var backward = 0f; var forward = 0f; var collisions = 0;
                        for (var i = 0; i < trace.Count; i++)
                        {
                            var row = trace[i]; var yaw = math.radians(row.yawDeg);
                            var bodySpeed = math.dot(new float2(-math.sin(yaw), math.cos(yaw)), new float2(row.velX, row.velY));
                            if (bodySpeed < -1f) backward += scenario.simDt;
                            forward += bodySpeed;
                            var start = new float2(row.posX, row.posY);
                            var end = i + 1 < trace.Count ? new float2(trace[i + 1].posX, trace[i + 1].posY) : result.finalPosition;
                            foreach (var rock in scenario.obstacles)
                            {
                                var radius = dynamics.shipRadius * Cost.BankProfileScale(row.strafe, config) + rock.radius;
                                if (!TerminalFieldTraversalTests.Hits(start, end, rock.center, radius)) continue;
                                collisions++; break;
                            }
                        }
                        var plan = trace.Skip(10).Where(r => !r.fieldBaked).Select(r => r.planMilliseconds).OrderBy(v => v).ToArray();
                        var bake = trace.Skip(10).Where(r => r.fieldBaked).Select(r => r.planMilliseconds).OrderBy(v => v).ToArray();
                        var speed = (math.length(scenario.referent1Law.p0) - result.finalRange) / scenario.durationSeconds;
                        report.WriteLine(FormattableString.Invariant($"{seed},{name},{settings.horizonSeconds},{settings.wTerminalField},{trace[0].fieldSpacing},{speed},{collisions},{backward},{forward / trace.Count},{Percentile(plan,.5f)},{Percentile(plan,.95f)},{Percentile(bake,.5f)},{Percentile(bake,.95f)}"));
                        report.Flush();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(settings); }
                }
            }
            finally { BurstCompiler.Options.EnableBurstCompileSynchronously = priorSync; }
        }

        [Test]
        public void GridCoverage()
        {
            if (Environment.GetEnvironmentVariable("MPC_CAUSAL") != "coverage") Assert.Ignore("Opt-in grid probe.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            Directory.CreateDirectory(output);
            using var report = new StreamWriter(Path.Combine(output, $"coverage-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
            report.WriteLine("seed,goalRange,horizon,resolution,spacing,localRocks,inDomain,represented,sharedWithLong,occupiedCells,bakeMs");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            foreach (var seed in new uint[] { 4103, 4105 })
            {
                var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed, seed);
                var reference = new HashSet<int>();
                foreach (var variant in new[] { 0, 1, 2, 3, 4 })
                {
                    var settings = UnityEngine.Object.Instantiate(asset);
                    try
                    {
                        var goalRange = variant == 3 ? 360f : variant == 4 ? 90f : 1500f;
                        settings.horizonSeconds = variant == 1 ? 1.2f : 1.7f;
                        settings.terminalFieldResolution = variant == 2 ? 96 : 48;
                        var config = settings.ToConfig(); config.ApplyDynamics(dynamics);
                        var scanner = new AI.Scanning.ObstacleScanner(null, dynamics.maxSpeed, 0, settings.horizonSeconds, new RigObstacleField(scenario.obstacles));
                        using var field = new AI.Navigation.MPC.TerminalField.TerminalField(settings, dynamics, scanner);
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        field.Update(.02f, default, true, new float2(0, goalRange), config);
                        clock.Stop();
                        var view = field.View;
                        var local = 0; var inDomain = 0; var occupied = 0;
                        var represented = new HashSet<int>();
                        for (var i = 0; i < field.Occupied.Length; i++) occupied += field.Occupied[i];
                        for (var i = 0; i < scenario.obstacles.Length; i++)
                        {
                            var rock = scenario.obstacles[i];
                            if (math.abs(rock.center.x) > 100 || rock.center.y < -60 || rock.center.y > 350) continue;
                            local++;
                            if (!view.Contains(rock.center)) continue;
                            inDomain++;
                            var cell = (int2)math.round((rock.center - view.origin) / view.spacing);
                            var index = cell.x + cell.y * view.resolution;
                            var radius = rock.radius + dynamics.shipRadius + config.collisionSafetyMargin;
                            if (math.distancesq(view.CellCenter(index), rock.center) > radius * radius) continue;
                            Assert.That(field.Occupied[index], Is.EqualTo(1));
                            represented.Add(i);
                        }
                        if (variant == 0) reference.UnionWith(represented);
                        report.WriteLine(FormattableString.Invariant($"{seed},{goalRange},{settings.horizonSeconds},{settings.terminalFieldResolution},{view.spacing},{local},{inDomain},{represented.Count},{represented.Intersect(reference).Count()},{occupied},{clock.Elapsed.TotalMilliseconds}"));
                        report.Flush();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(settings); }
                }
            }
        }
        [Test]
        public void FullSpeedSingleRock()
        {
            if (Environment.GetEnvironmentVariable("MPC_CAUSAL") != "single-rock") Assert.Ignore("Opt-in safety boundary probe.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var directory = Path.Combine(output, "single-rock-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            using var report = new StreamWriter(Path.Combine(directory, "summary.csv"));
            report.WriteLine("goalRange,horizon,weight,progressSpeed,sweptCollisions,minimumClearance,spacing");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            foreach (var goalRange in new[] { 90f, 1500f })
            foreach (var horizon in new[] { 1.7f, 1.2f, .7f })
            foreach (var weight in new[] { 3f, 0f })
            {
                var settings = UnityEngine.Object.Instantiate(asset);
                try
                {
                    settings.horizonSeconds = horizon; settings.wTerminalField = weight;
                    var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed, 4103);
                    scenario.durationSeconds = 2f;
                    scenario.startVel = new float2(0, dynamics.maxSpeed);
                    scenario.referent1Law = RigLaw.Static(new float2(0, goalRange));
                    scenario.obstacles = new[] { new RigCircle(new float2(0, 25), 4f) };
                    var trace = new List<RigTraceRow>();
                    var result = MpcSolverRig.Run(settings, dynamics, scenario, 4103, trace);
                    RigTraceCsv.Write(Path.Combine(directory, $"goal{goalRange}-horizon{horizon}-weight{weight}.csv"), trace);
                    var config = settings.ToConfig(); config.ApplyDynamics(dynamics);
                    var collisions = 0; var minimum = float.PositiveInfinity;
                    for (var i = 0; i < trace.Count; i++)
                    {
                        var row = trace[i]; var start = new float2(row.posX, row.posY);
                        var end = i + 1 < trace.Count ? new float2(trace[i + 1].posX, trace[i + 1].posY) : result.finalPosition;
                        var rock = scenario.obstacles[0];
                        var radius = dynamics.shipRadius * Cost.BankProfileScale(row.strafe, config) + rock.radius;
                        if (TerminalFieldTraversalTests.Hits(start, end, rock.center, radius)) collisions++;
                        var delta = end - start; var lengthSq = math.lengthsq(delta);
                        var t = lengthSq > 0 ? math.saturate(math.dot(rock.center - start, delta) / lengthSq) : 0f;
                        minimum = math.min(minimum, math.distance(start + delta * t, rock.center) - radius);
                    }
                    Assert.That(trace.Count, Is.EqualTo(100));
                    report.WriteLine(FormattableString.Invariant($"{goalRange},{horizon},{weight},{(goalRange-result.finalRange)/2},{collisions},{minimum},{trace[0].fieldSpacing}"));
                    report.Flush();
                }
                finally { UnityEngine.Object.DestroyImmediate(settings); }
            }
        }
        [Test]
        public void AvoidanceEnvelope()
        {
            if (Environment.GetEnvironmentVariable("MPC_CAUSAL") != "envelope") Assert.Ignore("Opt-in maneuverability probe.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            Directory.CreateDirectory(output);
            using var report = new StreamWriter(Path.Combine(output, $"envelope-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
            report.WriteLine("distance,kind,horizon,minimumClearance,thrust,strafe,yaw,progress");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var config = asset.ToConfig(); config.ApplyDynamics(dynamics); config.dt = .02f;
            foreach (var distance in new[] { 25f, 40f, 60f })
            {
                var center = new float2(0, distance);
                var best = float.NegativeInfinity; var bestControl = default(Control); var bestProgress = 0f;
                foreach (var thrust in new[] { -1f, 0f, 1f })
                foreach (var strafe in new[] { -1f, 0f, 1f })
                foreach (var yaw in new[] { -1f, 0f, 1f })
                {
                    var control = new Control { thrust = thrust, strafe = strafe, yawTorque = yaw };
                    var state = new State { vel = new float2(0, dynamics.maxSpeed) };
                    var minimum = float.PositiveInfinity;
                    for (var tick = 0; tick < 200; tick++)
                    {
                        var next = Model.Step(state, control, config, dynamics);
                        minimum = math.min(minimum, Clearance(state.pos, next.pos, center, 4 + dynamics.shipRadius * Cost.BankProfileScale(strafe, config)));
                        state = next;
                    }
                    if (minimum <= best) continue;
                    best = minimum; bestControl = control; bestProgress = state.pos.y;
                }
                report.WriteLine(FormattableString.Invariant($"{distance},fixed-control,0,{best},{bestControl.thrust},{bestControl.strafe},{bestControl.yawTorque},{bestProgress}"));
                foreach (var horizon in new[] { 1.7f, 1.2f, .7f })
                {
                    var settings = UnityEngine.Object.Instantiate(asset);
                    try
                    {
                        settings.wTerminalField = 0; settings.horizonSeconds = horizon;
                        var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed, 4103);
                        scenario.durationSeconds = 4; scenario.startVel = new float2(0, dynamics.maxSpeed);
                        scenario.obstacles = new[] { new RigCircle(center, 4) };
                        var trace = new List<RigTraceRow>();
                        var result = MpcSolverRig.Run(settings, dynamics, scenario, 4103, trace);
                        var minimum = float.PositiveInfinity;
                        for (var i = 0; i < trace.Count; i++)
                        {
                            var row = trace[i]; var start = new float2(row.posX, row.posY);
                            var end = i + 1 < trace.Count ? new float2(trace[i+1].posX, trace[i+1].posY) : result.finalPosition;
                            minimum = math.min(minimum, Clearance(start, end, center, 4 + dynamics.shipRadius * Cost.BankProfileScale(row.strafe, config)));
                        }
                        report.WriteLine(FormattableString.Invariant($"{distance},mpc,{horizon},{minimum},0,0,0,{result.finalPosition.y}"));
                        RigTraceCsv.Write(Path.Combine(output, $"envelope-{distance}-{horizon}.csv"), trace);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(settings); }
                }
                report.Flush();
            }
        }

        [Test]
        public void ShortHorizonSensing()
        {
            if (Environment.GetEnvironmentVariable("MPC_CAUSAL") != "sensing") Assert.Ignore("Opt-in sensing isolation.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var directory = Path.Combine(output, "sensing-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            using var report = new StreamWriter(Path.Combine(directory,"summary.csv"));
            report.WriteLine("case,goalRange,weight,sensingSeconds,minimumClearance,progress");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            for (var variant = 0; variant < 4; variant++)
            {
                var settings = UnityEngine.Object.Instantiate(asset);
                try
                {
                    settings.horizonSeconds = .7f; settings.wTerminalField = variant == 3 ? 3f : 0f;
                    var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed,4103);
                    scenario.durationSeconds = 4; scenario.startVel = new float2(0,dynamics.maxSpeed);
                    scenario.obstacles = new[] { new RigCircle(new float2(0,40),4) };
                    scenario.obstacleScanHalfExtent = dynamics.maxSpeed * (variant == 1 ? 1.7f : .7f);
                    scenario.referent1Law = RigLaw.Static(new float2(0,variant >= 2 ? 90 : 1500));
                    var trace = new List<RigTraceRow>();
                    var result = MpcSolverRig.Run(settings,dynamics,scenario,4103,trace);
                    RigTraceCsv.Write(Path.Combine(directory,$"case{variant}.csv"),trace);
                    var config=settings.ToConfig(); config.ApplyDynamics(dynamics);
                    var minimum=float.PositiveInfinity;
                    for(var i=0;i<trace.Count;i++)
                    {
                        var row=trace[i]; var start=new float2(row.posX,row.posY);
                        var end=i+1<trace.Count?new float2(trace[i+1].posX,trace[i+1].posY):result.finalPosition;
                        minimum=math.min(minimum,Clearance(start,end,scenario.obstacles[0].center,4+dynamics.shipRadius*Cost.BankProfileScale(row.strafe,config)));
                    }
                    report.WriteLine(FormattableString.Invariant($"{variant},{scenario.referent1Law.p0.y},{settings.wTerminalField},{scenario.obstacleScanHalfExtent / dynamics.maxSpeed},{minimum},{result.finalPosition.y}")); report.Flush();
                }
                finally { UnityEngine.Object.DestroyImmediate(settings); }
            }
        }
        [Test]
        public void InitialQueryCost()
        {
            if (Environment.GetEnvironmentVariable("MPC_CAUSAL") != "query-cost") Assert.Ignore("Opt-in query timing probe.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed,4103);
            var cfg = asset.ToConfig(); cfg.ApplyDynamics(dynamics);
            var reach = dynamics.maxSpeed * (cfg.horizon * cfg.dt + asset.terminalFieldBakeInterval) + dynamics.shipRadius + cfg.collisionSafetyMargin;
            var spacing = (750f + reach) / ((asset.terminalFieldResolution - 1) * .5f - 2f);
            var extent = spacing * asset.terminalFieldResolution * .5f + dynamics.shipRadius + cfg.collisionSafetyMargin;
            Directory.CreateDirectory(output);
            using var report = new StreamWriter(Path.Combine(output,$"query-cost-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
            report.WriteLine("condition,count,capacity,milliseconds");
            var expected = -1;
            foreach(var initialCapacity in new[] { 64, scenario.obstacles.Length+1 })
            {
                var scanner = new AI.Scanning.ObstacleScanner(null,dynamics.maxSpeed,0,1.7f,new RigObstacleField(scenario.obstacles));
                var buffer = new AI.Scanning.DetectedObstacle[initialCapacity];
                foreach(var pass in new[]{0,1})
                {
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    var scan=scanner.Query(new UnityEngine.Vector2(0,750),extent,ref buffer);
                    clock.Stop();
                    if(expected<0)expected=scan.count;
                    Assert.That(scan.count,Is.EqualTo(expected));
                    report.WriteLine(FormattableString.Invariant($"capacity{initialCapacity}-pass{pass},{scan.count},{buffer.Length},{clock.Elapsed.TotalMilliseconds}"));
                }
            }
        }
        [Test]
        public void TerminalPlateau()
        {
            if(Environment.GetEnvironmentVariable("MPC_CAUSAL")!="plateau")Assert.Ignore("Opt-in endpoint-value probe.");
            var output=Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            Directory.CreateDirectory(output);
            var settings=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset"));
            try
            {
                var dynamics=AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
                settings.horizonSeconds=.7f;
                var config=settings.ToConfig();config.ApplyDynamics(dynamics);
                var scanner=new AI.Scanning.ObstacleScanner(null,dynamics.maxSpeed,0,.7f,new RigObstacleField(new[]{new RigCircle(new float2(0,40),4)}));
                using var field=new AI.Navigation.MPC.TerminalField.TerminalField(settings,dynamics,scanner);
                field.Update(.02f,default,true,new float2(0,90),config);
                using var report=new StreamWriter(Path.Combine(output,$"plateau-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
                report.WriteLine("kind,thrust,strafe,yaw,x,y,terminalCost");
                using (var grid = new StreamWriter(Path.Combine(output, $"plateau-grid-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv")))
                {
                    grid.WriteLine("index,x,y,occupied,distance,excess");
                    var view = field.View;
                    for (var i = 0; i < view.distances.Length; i++)
                    {
                        var point = view.CellCenter(i);
                        grid.WriteLine(FormattableString.Invariant($"{i},{point.x},{point.y},{view.occupied[i]},{view.distances[i]},{view.Excess(i)}"));
                    }
                }
                foreach(var thrust in new[]{-1f,0f,1f})
                foreach(var strafe in new[]{-1f,0f,1f})
                foreach(var yaw in new[]{-1f,0f,1f})
                {
                    var state=new State{vel=new float2(0,dynamics.maxSpeed)};
                    var control=new Control{thrust=thrust,strafe=strafe,yawTorque=yaw};
                    for(var i=0;i<config.horizon;i++)state=Model.Step(state,control,config,dynamics);
                    report.WriteLine(FormattableString.Invariant($"reachable,{thrust},{strafe},{yaw},{state.pos.x},{state.pos.y},{field.View.Sample(state.pos)*3}"));
                }
                for(var x=-10;x<=10;x++)
                    report.WriteLine(FormattableString.Invariant($"cross-section,0,0,0,{x},17,{field.View.Sample(new float2(x,17))*3}"));
            }
            finally{UnityEngine.Object.DestroyImmediate(settings);}
        }
        [Test]
        public void WarmStartPersistence()
        {
            if(Environment.GetEnvironmentVariable("MPC_CAUSAL")!="warm-start")Assert.Ignore("Opt-in optimizer-history probe.");
            var output=Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var directory=Path.Combine(output,"warm-start-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            using var report=new StreamWriter(Path.Combine(directory,"summary.csv"));
            report.WriteLine("variant,progressSpeed,backwardSeconds");
            var settings=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset"));
            try
            {
                settings.wTerminalField=0;
                var dynamics=AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
                var scenario=TerminalFieldTraversalTests.Course(dynamics.maxSpeed,4103);scenario.durationSeconds=12;
                List<RigTraceRow> baseline=null;
                for(var variant=0;variant<3;variant++)
                {
                    var trace=new List<RigTraceRow>();
                    var arm=variant;
                    var result=MpcSolverRig.Run(settings,dynamics,scenario,4103,trace,(mpc,tick)=>
                    {
                        if(arm==1 && tick==200 || arm==2 && tick>=200 && tick%50==0)
                            Array.Clear(mpc.BestSequence,0,mpc.BestSequence.Length);
                    });
                    if(variant==0)baseline=trace;
                    else for(var i=0;i<200;i++)
                        Assert.That((trace[i].posX,trace[i].posY,trace[i].thrust,trace[i].strafe,trace[i].yawTorque),Is.EqualTo((baseline[i].posX,baseline[i].posY,baseline[i].thrust,baseline[i].strafe,baseline[i].yawTorque)));
                    var backward=trace.Count(r=>math.dot(new float2(-math.sin(math.radians(r.yawDeg)),math.cos(math.radians(r.yawDeg))),new float2(r.velX,r.velY)) < -1f)*.02f;
                    report.WriteLine(FormattableString.Invariant($"{variant},{(1500-result.finalRange)/12},{backward}"));report.Flush();
                    RigTraceCsv.Write(Path.Combine(directory,$"case{variant}.csv"),trace);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(settings);}
        }
        [Test]
        public void RouteValueEncounter()
        {
            var mode = Environment.GetEnvironmentVariable("MPC_CAUSAL");
            if (mode != "route-excess" && mode != "route-raw" && mode != "route-preserved" && mode != "route-fine-boundary") Assert.Ignore("Opt-in route-value prototype.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var directory = Path.Combine(output, mode + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            using var report = new StreamWriter(Path.Combine(directory, "summary.csv"));
            report.WriteLine("case,minimumClearance,collisions,progressSpeed,finalX,earlyX,firstFieldCost");
            var settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset"));
            try
            {
                settings.horizonSeconds = .7f;
                if (mode == "route-fine-boundary")
                {
                    settings.terminalFieldResolution = 192;
                    settings.terminalFieldMinSpacing = 1;
                    settings.wTerminalField = 30;
                }
                File.WriteAllText(Path.Combine(directory, "settings.json"), UnityEngine.JsonUtility.ToJson(settings, true));
                var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
                var config = settings.ToConfig(); config.ApplyDynamics(dynamics);
                var centerClearance = float.NegativeInfinity;
                foreach (var name in mode == "route-fine-boundary" ? new[] { "center", "left", "right", "far" } : new[] { "center", "left", "right", "empty", "empty-rest", "center-wide" })
                {
                    var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed, 4103);
                    scenario.durationSeconds = 4;
                    scenario.startVel = name == "empty-rest" ? default : new float2(0, dynamics.maxSpeed);
                    var goalRange = name == "far" ? 1500f : 90f;
                    scenario.referent1Law = RigLaw.Static(new float2(0, goalRange));
                    var center = new float2(name == "left" ? -3 : name == "right" ? 3 : 0, 40);
                    scenario.obstacles = name.StartsWith("empty") ? Array.Empty<RigCircle>() : new[] { new RigCircle(center, 4) };
                    scenario.obstacleScanHalfExtent = name == "center-wide" ? 42.5f : 17.5f;
                    var trace = new List<RigTraceRow>();
                    var result = MpcSolverRig.Run(settings, dynamics, scenario, 4103, trace);
                    Assert.That(trace.Count, Is.EqualTo(200));
                    var tracePath = Path.Combine(directory, name + ".csv");
                    RigTraceCsv.Write(tracePath, trace);
                    if (mode == "route-preserved")
                    {
                        var reference = Environment.GetEnvironmentVariable("MPC_ROUTE_REFERENCE");
                        if (string.IsNullOrWhiteSpace(reference)) throw new InvalidOperationException("MPC_ROUTE_REFERENCE is required.");
                        var expected = File.ReadAllLines(Path.Combine(reference, name + ".csv"));
                        var actual = File.ReadAllLines(tracePath);
                        Assert.That(actual.Length, Is.EqualTo(expected.Length));
                        var columns = actual[0].Split(',');
                        for (var row = 1; row < actual.Length; row++)
                        {
                            var a = actual[row].Split(','); var b = expected[row].Split(',');
                            for (var column = 0; column < columns.Length; column++)
                                if (columns[column] != "planMilliseconds")
                                    Assert.That(a[column], Is.EqualTo(b[column]), $"{name}, row {row}, {columns[column]}");
                        }
                    }
                    var minimum = float.PositiveInfinity;
                    var collisions = 0;
                    for (var i = 0; i < trace.Count; i++)
                    {
                        var row = trace[i];
                        var end = i + 1 < trace.Count ? new float2(trace[i + 1].posX, trace[i + 1].posY) : result.finalPosition;
                        foreach (var rock in scenario.obstacles)
                        {
                            var clearance = Clearance(new float2(row.posX, row.posY), end, rock.center,
                                rock.radius + dynamics.shipRadius * Cost.BankProfileScale(row.strafe, config));
                            minimum = math.min(minimum, clearance);
                            if (clearance < 0) collisions++;
                        }
                    }
                    if (name == "center") centerClearance = minimum;
                    report.WriteLine(FormattableString.Invariant($"{name},{minimum},{collisions},{(goalRange-result.finalRange)/4},{result.finalPosition.x},{trace[25].posX},{trace[0].costTerminalField}"));
                    report.Flush();
                }
                if (mode != "route-preserved")
                    Assert.That(centerClearance, Is.GreaterThanOrEqualTo(0), "The resolved field must steer early enough in the single-rock repro.");
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        [Test]
        public void ResolutionPlateau()
        {
            var mode = Environment.GetEnvironmentVariable("MPC_CAUSAL");
            if (mode != "resolution-plateau" && mode != "resolution-pressure") Assert.Ignore("Opt-in local-resolution probe.");
            var output = Environment.GetEnvironmentVariable("MPC_FIELD_OUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("MPC_FIELD_OUT is required.");
            var directory = Path.Combine(output, mode + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            using var report = new StreamWriter(Path.Combine(directory, "summary.csv"));
            report.WriteLine("resolution,weight,empty,spacing,minimumClearance,progressSpeed,earlyX,endpointCostRange");
            var asset = AssetDatabase.LoadAssetAtPath<MpcSettings>("Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset");
            var dynamics = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_1.prefab").ResolveStats().Dynamics;
            foreach (var resolution in mode == "resolution-pressure" ? new[] { 48, 192 } : new[] { 48, 96, 192 })
            foreach (var weight in mode == "resolution-pressure" ? new[] { 3f, 30f } : new[] { 3f })
            foreach (var empty in new[] { false, true })
            {
                var settings = UnityEngine.Object.Instantiate(asset);
                try
                {
                    settings.horizonSeconds = .7f;
                    settings.wTerminalField = weight;
                    settings.terminalFieldResolution = resolution;
                    settings.terminalFieldMinSpacing = 192f / resolution;
                    var config = settings.ToConfig(); config.ApplyDynamics(dynamics);
                    var scenario = TerminalFieldTraversalTests.Course(dynamics.maxSpeed, 4103);
                    scenario.durationSeconds = 4; scenario.startVel = new float2(0, dynamics.maxSpeed);
                    scenario.obstacleScanHalfExtent = 17.5f;
                    scenario.referent1Law = RigLaw.Static(new float2(0, 90));
                    scenario.obstacles = empty ? Array.Empty<RigCircle>() : new[] { new RigCircle(new float2(0, 40), 4) };
                    var scanner = new AI.Scanning.ObstacleScanner(null, dynamics.maxSpeed, 0, .7f, new RigObstacleField(scenario.obstacles));
                    using var field = new AI.Navigation.MPC.TerminalField.TerminalField(settings, dynamics, scanner);
                    field.Update(.02f, default, true, new float2(0, 90), config);
                    var minCost = float.PositiveInfinity; var maxCost = float.NegativeInfinity;
                    using (var values = new StreamWriter(Path.Combine(directory, $"values-{resolution}-{weight}-{empty}.csv")))
                    {
                        values.WriteLine("thrust,strafe,yaw,x,y,cost");
                        foreach (var thrust in new[] { -1f, 0f, 1f })
                        foreach (var strafe in new[] { -1f, 0f, 1f })
                        foreach (var yaw in new[] { -1f, 0f, 1f })
                        {
                            var state = new State { vel = new float2(0, dynamics.maxSpeed) };
                            var control = new Control { thrust = thrust, strafe = strafe, yawTorque = yaw };
                            for (var i = 0; i < config.horizon; i++) state = Model.Step(state, control, config, dynamics);
                            var cost = field.View.Sample(state.pos) * settings.wTerminalField;
                            minCost = math.min(minCost, cost); maxCost = math.max(maxCost, cost);
                            values.WriteLine(FormattableString.Invariant($"{thrust},{strafe},{yaw},{state.pos.x},{state.pos.y},{cost}"));
                        }
                    }
                    var trace = new List<RigTraceRow>();
                    var result = MpcSolverRig.Run(settings, dynamics, scenario, 4103, trace, (mpc, tick) =>
                    {
                        if (tick != 1 || empty) return;
                        var input = mpc.Solver.BuildCostInput(new float2(float.NaN, 0), projectileSpeed: 60,
                            initialVel: mpc.LastInitialState.vel, sentence: scenario.intent,
                            referent1: new ReferentSnapshot { valid = true, pos = new float2(0, 90) });
                        using var costs = new StreamWriter(Path.Combine(directory, $"ranking-{resolution}-{weight}.csv"));
                        costs.WriteLine("kind,thrust,strafe,yaw,stageCost,fieldCost,total");
                        var chosen = Cost.EvaluateTrajectoryBreakdown(mpc.LastInitialState, mpc.BestSequence, input, mpc.Config, dynamics, default);
                        costs.WriteLine(FormattableString.Invariant($"chosen,0,0,0,{chosen.total-chosen.terminalField},{chosen.terminalField},{chosen.total}"));
                        for (var sample = 0; sample < mpc.Solver.LastSampleCount; sample++)
                        {
                            var sequence = new Control[mpc.Config.horizon];
                            for (var i = 0; i < sequence.Length; i++) sequence[i] = mpc.Solver.Candidates[sample * sequence.Length + i];
                            var cost = Cost.EvaluateTrajectoryBreakdown(mpc.LastInitialState, sequence, input, mpc.Config, dynamics, default);
                            Assert.That(cost.total, Is.EqualTo(mpc.Solver.Costs[sample]).Within(.002f));
                            costs.WriteLine(FormattableString.Invariant($"sample{sample},{sequence[0].thrust},{sequence[0].strafe},{sequence[0].yawTorque},{cost.total-cost.terminalField},{cost.terminalField},{cost.total}"));
                        }
                        foreach (var thrust in new[] { -1f, 0f, 1f })
                        foreach (var strafe in new[] { -1f, 0f, 1f })
                        foreach (var yaw in new[] { -1f, 0f, 1f })
                        {
                            var sequence = Enumerable.Repeat(new Control { thrust = thrust, strafe = strafe, yawTorque = yaw }, mpc.Config.horizon).ToArray();
                            var cost = Cost.EvaluateTrajectoryBreakdown(mpc.LastInitialState, sequence, input, mpc.Config, dynamics, default);
                            costs.WriteLine(FormattableString.Invariant($"constant,{thrust},{strafe},{yaw},{cost.total-cost.terminalField},{cost.terminalField},{cost.total}"));
                        }
                    });
                    RigTraceCsv.Write(Path.Combine(directory, $"trace-{resolution}-{weight}-{empty}.csv"), trace);
                    var minimum = float.PositiveInfinity;
                    for (var i = 0; i < trace.Count; i++)
                    {
                        var row = trace[i];
                        var end = i + 1 < trace.Count ? new float2(trace[i + 1].posX, trace[i + 1].posY) : result.finalPosition;
                        foreach (var rock in scenario.obstacles)
                            minimum = math.min(minimum, Clearance(new float2(row.posX, row.posY), end, rock.center,
                                rock.radius + dynamics.shipRadius * Cost.BankProfileScale(row.strafe, config)));
                    }
                    report.WriteLine(FormattableString.Invariant($"{resolution},{weight},{empty},{trace[0].fieldSpacing},{minimum},{(90-result.finalRange)/4},{trace[25].posX},{maxCost-minCost}"));
                    report.Flush();
                }
                finally { UnityEngine.Object.DestroyImmediate(settings); }
            }
        }

        private static float Clearance(float2 start, float2 end, float2 center, float radius)
        {
            var delta = end - start; var lengthSq = math.lengthsq(delta);
            var t = lengthSq > 0 ? math.saturate(math.dot(center - start, delta) / lengthSq) : 0f;
            return math.distance(start + t * delta, center) - radius;
        }
        private static float Percentile(float[] sorted, float fraction) => sorted.Length == 0 ? float.NaN : sorted[(int)math.floor((sorted.Length - 1) * fraction)];
    }
}
