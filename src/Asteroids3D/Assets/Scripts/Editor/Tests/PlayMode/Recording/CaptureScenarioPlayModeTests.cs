#if UNITY_EDITOR
using Capture.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capture;
using Capture.Recording;
using Substrate.Sessions;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Recording
{
    /// <summary>Dispatches a requested scenario and owns its recording cleanup. Diagnostic scenarios receive a Session; preview scenarios compose their own host. Presentation is decided before diagnostic subjects spawn.</summary>
    [TestFixture]
    [Category("Camera")]
    [Category("RequiresGraphics")]
    public class CaptureScenarioPlayModeTests : PlayModeWorldFixture
    {
        private GameObject sessionRoot;
        private IEpisodeCapture capture;
        private Session session;
        private CaptureScenario scenario;

        public override void TearDown()
        {
            // Destroying the owner unloads a session the test never tore down; a finished Teardown nulls the pool.
            DestroyTestObject(sessionRoot);
            sessionRoot = null;

            session?.Projectiles?.ReturnAllToPool();
            session = null;

            scenario?.Dispose();
            scenario = null;
            if (capture != null)
            {
                UnityEngine.Object.DestroyImmediate(capture as ScriptableObject);
                capture = null;
            }

            base.TearDown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RunsRequestedScenario()
        {
            var typeName = CaptureDispatch.ConsumeRequest() ?? CommandLineArg("-captureScenario");
            if (string.IsNullOrEmpty(typeName))
                Assert.Ignore("Queue a scenario via `unity command capture_request_scenario` (warm lane) or run via unity_test_agent.ps1 -WithGraphics -CaptureScenario <TypeName>.");

            scenario = CreateScenario(typeName);
            scenario.Commit = CaptureDispatch.ConsumeCommit() ?? CommandLineArg("-captureCommit");
            var sectorEntry = scenario.SectorEntry;

            if (scenario.NeedsSession)
            {
                sessionRoot = new GameObject("CaptureScenarioSession");
                session = TestSession.Create(sessionRoot, new SessionProfile
                {
                    sectors = sectorEntry != null ? new[] { sectorEntry } : null,
                    presentation = GizmoCaptureProfiles.PresentationFor(scenario.Profile),
                });
                yield return session.Compose();
                if (sectorEntry != null) yield return session.LoadSector();
                scenario.Session = session;
            }

            capture = TestAssets.NewNativeCapture();
            scenario.Capture = capture;
            string frameDir = null;
            try
            {
                yield return scenario.Run();
                yield return new WaitForEndOfFrame();
                scenario.Finish();
                frameDir = scenario.Result?.FrameDirectory;
            }
            finally
            {
                scenario.Dispose();
            }

            Assert.IsNotNull(frameDir, "Scenario completed without filming — did it ever call Film?");
            Assert.IsTrue(Directory.Exists(frameDir), $"Capture wrote no frame directory at {frameDir}");
            var frames = Directory.GetFiles(frameDir, "*.png");
            Assert.IsNotEmpty(frames, $"Scenario filmed no rendered frames into {frameDir}");
            Debug.Log($"[Capture] {frames.Length} frames -> {frameDir}");

            if (session != null) yield return session.Teardown();
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
