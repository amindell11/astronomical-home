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
        // SessionState survives them, so it carries the run's phase, failure tally, and pending
        // idle-action (UTF's post-run cleanup can ForceDomainReload after RunFinished, wiping an
        // armed EditorApplication.update poll).
        private const string PhaseKey = "GateTestRunner.Phase";
        private const string FailedKey = "GateTestRunner.FailedCount";
        private const string PendingKey = "GateTestRunner.Pending";
        private const string PhaseEdit = "EditMode";
        private const string PhasePlay = "PlayMode";
        private const string PendingPlay = "play";
        private const string PendingExit = "exit";
        private const string PendingAbort = "abort";

        static GateTestRunner()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(PhaseKey, "")))
                TestRunnerApi.RegisterTestCallback(new Callbacks());
            if (!string.IsNullOrEmpty(SessionState.GetString(PendingKey, "")))
                ArmPending();
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
                SessionState.SetString(PendingKey, "");
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
                        SessionState.SetString(PendingKey, PendingPlay);
                    }
                    else
                    {
                        TestRunnerApi.SaveResultToFile(result, Arg("-gatePlayResults"));
                        SessionState.SetString(PhaseKey, "");
                        SessionState.SetString(PendingKey, PendingExit);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GateTestRunner] phase completion failed: {e}");
                    SessionState.SetString(PhaseKey, "");
                    SessionState.SetString(PendingKey, PendingAbort);
                }
                ArmPending();
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

        // The pending key clears only when the action actually fires, so a reload that lands
        // between arming and firing re-arms from the static ctor instead of losing the action.
        private static void ArmPending()
        {
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (s_IsRunActive())
                    return;
                EditorApplication.update -= poll;
                var pending = SessionState.GetString(PendingKey, "");
                SessionState.SetString(PendingKey, "");
                switch (pending)
                {
                    case PendingPlay:
                        Execute(TestMode.PlayMode);
                        break;
                    case PendingExit:
                        EditorApplication.Exit(SessionState.GetInt(FailedKey, 0) > 0 ? 2 : 0);
                        break;
                    case PendingAbort:
                        EditorApplication.Exit(3);
                        break;
                }
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
