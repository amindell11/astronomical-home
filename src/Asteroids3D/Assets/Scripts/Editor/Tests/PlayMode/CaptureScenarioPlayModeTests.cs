#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game;
using Game.Capture;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>Generic runner for capture scenarios: -captureScenario &lt;TypeName&gt; on Unity's command line (forwarded by unity_test_agent.ps1 -CaptureScenario) picks the CaptureScenario to run; without it the test ignores, so the suite stays green.</summary>
    [TestFixture]
    [Category("Camera")]
    [Category("RequiresGraphics")]
    public class CaptureScenarioPlayModeTests : PlayModeWorldFixture
    {
        private ShipRegistry registry;
        private ArenaContext scenarioArena;

        public override void SetUp()
        {
            base.SetUp();
            registry = new ShipRegistry();
            scenarioArena = new ArenaContext(Vector2.zero, registry, NavField);
        }

        public override void TearDown()
        {
            foreach (var ship in UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(ship.gameObject);
            ProjectileFlush.ReturnAllToPool();
            CaptureRecorder.SweepStranded();

            registry?.Dispose();
            registry = null;
            scenarioArena = null;

            base.TearDown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RunsRequestedScenario()
        {
            var typeName = CommandLineArg("-captureScenario");
            if (string.IsNullOrEmpty(typeName))
                Assert.Ignore("Run via unity_test_agent.ps1 -WithGraphics -CaptureScenario <TypeName> to capture a scenario.");

            var scenario = CreateScenario(typeName);
            scenario.Arena = scenarioArena;
            scenario.Registry = registry;

            using var pacing = CapturePacing.Locked();
            using var recorder = new CaptureRecorder(scenario.Config);
            yield return scenario.Run(recorder);

            Assert.Greater(recorder.FrameCount, 0, "Scenario completed without capturing a single frame — did it ever call recorder.Step?");
            Debug.Log($"[Capture] {recorder.FrameCount} frames -> {recorder.FrameDir}");
        }

        private static string CommandLineArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static CaptureScenario CreateScenario(string typeName)
        {
            var scenarioType = ResolveScenarioType(typeName);
            if (scenarioType.GetConstructor(Type.EmptyTypes) == null)
                Assert.Fail($"{scenarioType.FullName} needs a public parameterless constructor");
            return (CaptureScenario)Activator.CreateInstance(scenarioType);
        }

        private static Type ResolveScenarioType(string typeName)
        {
            var shortMatches = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, t => t != null); }

                foreach (var type in types)
                {
                    if (type.IsAbstract || !typeof(CaptureScenario).IsAssignableFrom(type)) continue;
                    if (type.FullName == typeName) return type;
                    if (type.Name == typeName) shortMatches.Add(type);
                }
            }

            if (shortMatches.Count == 0)
                Assert.Fail($"No CaptureScenario type named '{typeName}' is loaded — is its file staged/committed and compiling?");
            if (shortMatches.Count > 1)
                Assert.Fail($"Scenario name '{typeName}' is ambiguous: {string.Join(", ", shortMatches.Select(t => t.FullName))} — rename one.");
            return shortMatches[0];
        }
    }
}
#endif
