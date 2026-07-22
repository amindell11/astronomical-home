#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Single-boot merge-gate runner: EditMode then PlayMode via TestRunnerApi in ONE editor
    /// session (the UTF CLI boots a fresh editor per -testPlatform, ~25s overhead each). Entered via
    /// -executeMethod Tests.EditMode.GateTestRunner.Run with -gateEditResults/-gatePlayResults paths;
    /// stock -testFilter/-testCategory/-assemblyNames strings are honored with CLI-identical semantics.
    /// Exit codes mirror the UTF CLI: 0 pass, 2 test failures, 3 run error.</summary>
    [InitializeOnLoad]
    public static class GateTestRunner
    {
        // Play-mode domain reloads kill both the registered callbacks and this class's statics;
        // SessionState survives them, so it carries the run's phase and failure tally.
        private const string PhaseKey = "GateTestRunner.Phase";
        private const string FailedKey = "GateTestRunner.FailedCount";
        private const string PhaseEdit = "EditMode";
        private const string PhasePlay = "PlayMode";

        static GateTestRunner()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(PhaseKey, "")))
                TestRunnerApi.RegisterTestCallback(new Callbacks());
        }

        public static void Run()
        {
            try
            {
                if (string.IsNullOrEmpty(Arg("-gateEditResults")) || string.IsNullOrEmpty(Arg("-gatePlayResults")))
                    throw new ArgumentException("GateTestRunner requires -gateEditResults and -gatePlayResults paths");
                if (s_IsRunActive == null)
                    throw new MissingMethodException(
                        "TestRunnerApi.IsRunActive is gone (UTF upgrade?) — GateTestRunner cannot observe run completion");

                SessionState.SetString(PhaseKey, PhaseEdit);
                SessionState.SetInt(FailedKey, 0);
                TestRunnerApi.RegisterTestCallback(new Callbacks());
                Execute(TestMode.EditMode);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GateTestRunner] setup failed: {e}");
                EditorApplication.Exit(3);
            }
        }

        private static void Execute(TestMode mode)
        {
            var filter = new Filter
            {
                testMode = mode,
                groupNames = SplitArg("-testFilter"),
                categoryNames = SplitArg("-testCategory"),
                assemblyNames = SplitArg("-assemblyNames"),
            };
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
        }

        private class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    SessionState.SetInt(FailedKey, SessionState.GetInt(FailedKey, 0) + result.FailCount);
                    var phase = SessionState.GetString(PhaseKey, "");
                    if (phase == PhaseEdit)
                    {
                        TestRunnerApi.SaveResultToFile(result, Arg("-gateEditResults"));
                        SessionState.SetString(PhaseKey, PhasePlay);
                        WhenRunIdle(() => Execute(TestMode.PlayMode));
                    }
                    else
                    {
                        TestRunnerApi.SaveResultToFile(result, Arg("-gatePlayResults"));
                        SessionState.SetString(PhaseKey, "");
                        var code = SessionState.GetInt(FailedKey, 0) > 0 ? 2 : 0;
                        WhenRunIdle(() => EditorApplication.Exit(code));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GateTestRunner] phase completion failed: {e}");
                    SessionState.SetString(PhaseKey, "");
                    WhenRunIdle(() => EditorApplication.Exit(3));
                }
            }
        }

        // The CLI's own exit gate (Executer.ExitIfRunIsCompleted) polls internal TestRunnerApi.IsRunActive:
        // RunFinished fires mid-job, before UTF's cleanup tasks (restore project settings, delete the
        // InitTestScene bootstrap) — acting on any earlier signal races them and leaks their artifacts.
        private static readonly Func<bool> s_IsRunActive = BuildIsRunActive();

        private static Func<bool> BuildIsRunActive()
        {
            var method = typeof(TestRunnerApi).GetMethod(
                "IsRunActive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
                return null;
            return () => (bool)method.Invoke(null, null);
        }

        private static void WhenRunIdle(Action action)
        {
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (s_IsRunActive())
                    return;
                EditorApplication.update -= poll;
                action();
            };
            EditorApplication.update += poll;
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return "";
        }

        private static string[] SplitArg(string name)
        {
            var value = Arg(name);
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }
    }
}
#endif
