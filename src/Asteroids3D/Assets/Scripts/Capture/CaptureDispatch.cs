#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Capture
{
    /// <summary>
    /// Warm-capture-lane dispatch seam: a one-shot scenario request handed to the
    /// resident editor's next CaptureScenarioPlayModeTests run. SessionState-backed,
    /// so it survives domain reloads, dies with the editor, and can never refire —
    /// <see cref="ConsumeRequest"/> clears it on read. The boot-frozen
    /// -captureScenario argument stays the cold-run path. Queued over the CLI via
    /// the capture_request_scenario command (CaptureLane).
    /// </summary>
    public static class CaptureDispatch
    {
        private const string ProductionKey = "Game.Capture.CaptureDispatch.RequestedScenario";
        private const string CommitKey = "Game.Capture.CaptureDispatch.Commit";

        public static void Request(string scenarioTypeName)
        {
            Request(ProductionKey, scenarioTypeName);
            SessionState.EraseString(CommitKey);
        }

        public static void SetCommit(string commit)
        {
            if (string.IsNullOrEmpty(commit)) SessionState.EraseString(CommitKey);
            else SessionState.SetString(CommitKey, commit);
        }

        public static string ConsumeCommit()
        {
            var commit = SessionState.GetString(CommitKey, "");
            if (string.IsNullOrEmpty(commit)) return null;
            SessionState.EraseString(CommitKey);
            return commit;
        }

        // Key-injected overloads keep tests off the production request slot.
        internal static void Request(string key, string scenarioTypeName)
        {
            if (string.IsNullOrWhiteSpace(scenarioTypeName))
                throw new ArgumentException("A capture request names a CaptureScenario type.", nameof(scenarioTypeName));
            SessionState.SetString(key, scenarioTypeName);
        }

        /// <summary>The pending scenario type name, cleared by this read; null when none is queued.</summary>
        public static string ConsumeRequest() => ConsumeRequest(ProductionKey);

        internal static string ConsumeRequest(string key)
        {
            var requested = SessionState.GetString(key, "");
            if (string.IsNullOrEmpty(requested)) return null;
            SessionState.EraseString(key);
            return requested;
        }
    }
}
#endif
