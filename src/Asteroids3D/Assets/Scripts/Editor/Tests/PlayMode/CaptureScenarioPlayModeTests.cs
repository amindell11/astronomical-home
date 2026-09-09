#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Capture;
using Game.Sessions;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>Generic runner for capture scenarios. Two dispatch paths pick the CaptureScenario: a one-shot CaptureDispatch request (warm lane, queued via capture_request_scenario) or -captureScenario &lt;TypeName&gt; on Unity's command line (cold runs, forwarded by unity_test_agent.ps1 -CaptureScenario); with neither the test ignores, so the suite stays green. Composes a sector-less Session — scenarios get the real service container and UnitService spawn path — with presentation decided pre-spawn by the scenario's gizmo profile (GizmoCaptureProfiles.PresentationFor).</summary>
    [TestFixture]
    [Category("Camera")]
    [Category("RequiresGraphics")]
    public class CaptureScenarioPlayModeTests : PlayModeWorldFixture
    {
        private GameObject sessionRoot;
        private IEpisodeCapture capture;
        private Session session;
        private bool savedPresentation;

        public override void SetUp()
        {
            base.SetUp();
            savedPresentation = GameSettings.PresentationEnabled;
        }

        public override void TearDown()
        {
            // Crash-path net: spawned ships live under the session root, so destroying it unwinds a scenario the test never got to tear down; Services is null when Teardown already flushed.
            DestroyTestObject(sessionRoot);
            sessionRoot = null;

            session?.Services?.Projectiles.ReturnAllToPool();
            session = null;

            // Crash-path net: a scenario that died mid-episode never reached the runner's End.
            if (capture != null)
            {
                capture.End();
                UnityEngine.Object.DestroyImmediate(capture as ScriptableObject);
                capture = null;
            }

            // Compose overrides this process-global and Teardown does not restore it.
            GameSettings.SetPresentationEnabled(savedPresentation);

            base.TearDown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RunsRequestedScenario()
        {
            var typeName = CaptureDispatch.ConsumeRequest() ?? CommandLineArg("-captureScenario");
            if (string.IsNullOrEmpty(typeName))
                Assert.Ignore("Queue a scenario via `unity command capture_request_scenario` (warm lane) or run via unity_test_agent.ps1 -WithGraphics -CaptureScenario <TypeName>.");

            var scenario = CreateScenario(typeName);

            sessionRoot = new GameObject("CaptureScenarioSession");
            session = TestSession.Create(sessionRoot, new SessionProfile
            {
                sectorEntry = null,
                buildPlayer = false,
                presentation = GizmoCaptureProfiles.PresentationFor(scenario.Profile),
            });
            yield return session.Compose();
            scenario.Session = session;

            using var pacing = CapturePacing.Locked();
            capture = TestAssets.NewNativeCapture();
            scenario.Capture = capture;
            string frameDir = null;
            try
            {
                yield return scenario.Run();
                frameDir = capture.FrameDir;
            }
            finally
            {
                capture.End();
            }

            Assert.IsNotNull(frameDir, "Scenario completed without filming — did it ever call Film?");
            Assert.IsTrue(Directory.Exists(frameDir), $"Capture wrote no frame directory at {frameDir}");
            var frames = Directory.GetFiles(frameDir, "*.png");
            Assert.IsNotEmpty(frames, $"Scenario filmed no frames into {frameDir} — did it ever call FilmStep?");
            Debug.Log($"[Capture] {frames.Length} frames -> {frameDir}");

            yield return session.Teardown();
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
