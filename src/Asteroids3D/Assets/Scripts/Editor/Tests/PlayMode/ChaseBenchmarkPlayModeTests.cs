#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AI;
using AI.States;
using Asteroids.Fields;
using Game;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{

[TestFixture]
[Category("AI")]
public class ChaseBenchmarkPlayModeTests : AIIntegrationFixture
{
    private const string FieldPrefabPath = "Assets/Prefabs/Asteroid/AsteroidController.prefab";
    private const string PursuitProfilePath = "Assets/Settings/AI/StateProfiles/Pursuit.asset";
    private const string EvadeProfilePath = "Assets/Settings/AI/StateProfiles/Evade.asset";

    private readonly List<GameObject> createdObjects = new();
    private readonly List<BenchmarkCollisionProbe> collisionProbes = new();

    public override void TearDown()
    {
        foreach (var go in createdObjects)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
        createdObjects.Clear();
        collisionProbes.Clear();

        base.TearDown();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator ChaseVsEvade_ShortBenchmark_IsRepeatable()
    {
        var first = default(BenchmarkMetrics);
        yield return Drain(RunScenario(new BenchmarkScenario("short-a", 12345, 4f, new Vector2(-22f, -8f), new Vector2(18f, 8f)), metrics => first = metrics));

        var second = default(BenchmarkMetrics);
        yield return Drain(RunScenario(new BenchmarkScenario("short-a", 12345, 4f, new Vector2(-22f, -8f), new Vector2(18f, 8f)), metrics => second = metrics));

        WriteResult(first);
        WriteResult(second);

        Assert.That(Mathf.Abs(first.meanSeparation - second.meanSeparation), Is.LessThan(1.5f),
            "Same seed/setup should produce comparable mean separation.");
        Assert.That(Mathf.Abs(first.pursuerMeanSpeed - second.pursuerMeanSpeed), Is.LessThan(1.5f),
            "Same seed/setup should produce comparable pursuer speed.");
        Assert.That(first.pursuerCollisions, Is.EqualTo(second.pursuerCollisions),
            "Same seed/setup should produce matching pursuer collision counts.");
    }

    [UnityTest]
    [Category("Slow")]
    public IEnumerator ChaseVsEvade_LongBenchmark_LogsVariants()
    {
        var scenarios = new[]
        {
            new BenchmarkScenario("offset-cross", 12345, 10f, new Vector2(-26f, -12f), new Vector2(22f, 10f)),
            new BenchmarkScenario("wide-lateral", 22345, 10f, new Vector2(-30f, 14f), new Vector2(26f, -14f)),
            new BenchmarkScenario("near-cluster", 32345, 10f, new Vector2(-12f, -12f), new Vector2(12f, 12f)),
        };

        foreach (var scenario in scenarios)
        {
            var captured = default(BenchmarkMetrics);
            yield return Drain(RunScenario(scenario, metrics => captured = metrics));
            WriteResult(captured);
        }
    }

    private static IEnumerator Drain(IEnumerator routine)
    {
        while (routine.MoveNext())
            yield return routine.Current;
    }

    private IEnumerator RunScenario(BenchmarkScenario scenario, Action<BenchmarkMetrics> capture)
    {
        UnityEngine.Random.InitState(scenario.seed);

        var field = CreateField(scenario.seed, scenario.pursuerStart);
        var (pursuer, pursuerCmdr) = CreateAIShip(GamePlane.PlanePointToWorld(scenario.pursuerStart), team: 0);
        var (evader, evaderCmdr) = CreateAIShip(GamePlane.PlanePointToWorld(scenario.evaderStart), team: 1);

        field.SetPlayer(pursuer.transform);
        field.SetPlayerStart(scenario.pursuerStart);

        var pursuitState = CreateState(LoadProfile(PursuitProfilePath), pursuerCmdr);
        var evadeState = CreateState(LoadProfile(EvadeProfilePath), evaderCmdr);
        InitializeWithStates(pursuerCmdr, pursuitState);
        InitializeWithStates(evaderCmdr, evadeState);

        var pursuerProbe = AddCollisionProbe(pursuer, "pursuer");
        var evaderProbe = AddCollisionProbe(evader, "evader");

        // Let field streaming, kinematics polling, and enemy scans settle before sampling.
        for (var i = 0; i < 20; i++)
            yield return new WaitForFixedUpdate();

        yield return WaitForEnemyLocks(pursuerCmdr, evaderCmdr);

        var samples = 0;
        var elapsed = 0f;
        var meanSeparation = 0f;
        var minSeparation = float.MaxValue;
        var pursuerSpeed = 0f;
        var evaderSpeed = 0f;
        var pursuerSolveMs = 0f;
        var pursuerChatter = 0f;
        var interceptTime = -1f;
        var previousCommand = pursuerCmdr.Navigator.CurrentCommand;

        while (elapsed < scenario.durationSec)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            samples++;

            var separation = Vector2.Distance(pursuer.Kinematics.pos, evader.Kinematics.pos);
            meanSeparation += separation;
            minSeparation = Mathf.Min(minSeparation, separation);
            pursuerSpeed += pursuer.Kinematics.Speed;
            evaderSpeed += evader.Kinematics.Speed;
            pursuerSolveMs += pursuerCmdr.Navigator.lastSolveTimeMs;

            var command = pursuerCmdr.Navigator.CurrentCommand;
            pursuerChatter += Mathf.Abs(command.thrust - previousCommand.thrust);
            pursuerChatter += Mathf.Abs(command.strafe - previousCommand.strafe);
            pursuerChatter += Mathf.Abs(command.yawTorque - previousCommand.yawTorque);
            previousCommand = command;

            if (interceptTime < 0f && separation <= 6f)
                interceptTime = elapsed;
        }

        samples = Mathf.Max(samples, 1);
        capture(new BenchmarkMetrics
        {
            scenario = scenario.name,
            seed = scenario.seed,
            durationSec = scenario.durationSec,
            sampleCount = samples,
            interceptTimeSec = interceptTime,
            finalSeparation = Vector2.Distance(pursuer.Kinematics.pos, evader.Kinematics.pos),
            meanSeparation = meanSeparation / samples,
            minSeparation = minSeparation,
            pursuerMeanSpeed = pursuerSpeed / samples,
            evaderMeanSpeed = evaderSpeed / samples,
            pursuerCollisions = pursuerProbe.CollisionCount,
            evaderCollisions = evaderProbe.CollisionCount,
            pursuerImpactImpulse = pursuerProbe.TotalImpulse,
            evaderImpactImpulse = evaderProbe.TotalImpulse,
            pursuerMeanSolveMs = pursuerSolveMs / samples,
            pursuerControlChatterPerSec = pursuerChatter / Mathf.Max(elapsed, 0.0001f),
            // A3 Track A telemetry: tight/bank-only gaps the pursuer threaded (was -1 placeholder).
            gapsThreaded = pursuerCmdr.Navigator.GapsThreaded,
        });

        CleanupScenario(field, pursuer, evader);
    }

    private UpdatingAsteroidField CreateField(int seed, Vector2 anchor)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<UpdatingAsteroidField>(FieldPrefabPath);
        Assert.That(prefab, Is.Not.Null, $"Missing asteroid field prefab at {FieldPrefabPath}");

        var field = Object.Instantiate(prefab, GamePlane.PlanePointToWorld(anchor), GamePlane.Rotation);
        field.name = $"BenchmarkAsteroidField_{seed}";
        createdObjects.Add(field.gameObject);

        var serialized = new SerializedObject(field);
        serialized.FindProperty("seed").intValue = seed;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return field;
    }

    private BenchmarkCollisionProbe AddCollisionProbe(Ship ship, string label)
    {
        var probe = ship.gameObject.AddComponent<BenchmarkCollisionProbe>();
        probe.Label = label;
        collisionProbes.Add(probe);
        return probe;
    }

    private void CleanupScenario(UpdatingAsteroidField field, Ship pursuer, Ship evader)
    {
        registry?.ActiveShips.Clear();

        if (field)
        {
            field.DespawnAll();
            createdObjects.Remove(field.gameObject);
            Object.DestroyImmediate(field.gameObject);
        }

        if (pursuer) Object.DestroyImmediate(pursuer.gameObject);
        if (evader) Object.DestroyImmediate(evader.gameObject);
        collisionProbes.Clear();
    }

    private static StateProfile LoadProfile(string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<StateProfile>(path);
        Assert.That(profile, Is.Not.Null, $"Missing state profile at {path}");
        return profile;
    }

    private static AIState CreateState(StateProfile profile, AICommander cmdr) =>
        new(profile, cmdr.Navigator, cmdr.Gunner);

    private static IEnumerator WaitForEnemyLocks(AICommander pursuer, AICommander evader)
    {
        var deadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (pursuer.UtilityChooser.Context != null &&
                evader.UtilityChooser.Context != null &&
                pursuer.UtilityChooser.Context.Combat.HasEnemy &&
                evader.UtilityChooser.Context.Combat.HasEnemy)
            {
                yield break;
            }

            yield return new WaitForFixedUpdate();
        }

        Assert.Fail("Both benchmark ships must acquire each other before sampling.");
    }

    private static void WriteResult(BenchmarkMetrics metrics)
    {
        var dir = ResultDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "chase-benchmark-baseline.jsonl");
        File.AppendAllText(path, JsonUtility.ToJson(metrics) + Environment.NewLine);
        Assert.That(File.Exists(path), Is.True, $"Benchmark JSONL was not written to {path}");
        Debug.Log($"[ChaseBenchmark] {metrics.ToSummary()} jsonl={path}");
    }

    private static string ResultDirectory()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        return Path.Combine(repoRoot, "results", "unity-tests-agent", "chase-benchmark");
    }

    private readonly struct BenchmarkScenario
    {
        public readonly string name;
        public readonly int seed;
        public readonly float durationSec;
        public readonly Vector2 pursuerStart;
        public readonly Vector2 evaderStart;

        public BenchmarkScenario(string name, int seed, float durationSec, Vector2 pursuerStart, Vector2 evaderStart)
        {
            this.name = name;
            this.seed = seed;
            this.durationSec = durationSec;
            this.pursuerStart = pursuerStart;
            this.evaderStart = evaderStart;
        }
    }

    [Serializable]
    private struct BenchmarkMetrics
    {
        public string scenario;
        public int seed;
        public float durationSec;
        public int sampleCount;
        public float interceptTimeSec;
        public float finalSeparation;
        public float meanSeparation;
        public float minSeparation;
        public float pursuerMeanSpeed;
        public float evaderMeanSpeed;
        public int pursuerCollisions;
        public int evaderCollisions;
        public float pursuerImpactImpulse;
        public float evaderImpactImpulse;
        public float pursuerMeanSolveMs;
        public float pursuerControlChatterPerSec;
        public int gapsThreaded;

        public string ToSummary() =>
            string.Format(CultureInfo.InvariantCulture,
                "scenario={0} seed={1} meanSep={2:F2} finalSep={3:F2} minSep={4:F2} " +
                "pursuerSpeed={5:F2} evaderSpeed={6:F2} pursuerCollisions={7} evaderCollisions={8} " +
                "solveMs={9:F3} chatter={10:F3}",
                scenario, seed, meanSeparation, finalSeparation, minSeparation,
                pursuerMeanSpeed, evaderMeanSpeed, pursuerCollisions, evaderCollisions,
                pursuerMeanSolveMs, pursuerControlChatterPerSec);
    }

    private sealed class BenchmarkCollisionProbe : MonoBehaviour
    {
        public string Label { get; set; }
        public int CollisionCount { get; private set; }
        public float TotalImpulse { get; private set; }

        private void OnCollisionEnter(Collision collision)
        {
            CollisionCount++;
            TotalImpulse += collision.impulse.magnitude;
        }
    }
}

} // namespace Tests.PlayMode
#endif
