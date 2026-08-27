using System;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Game.Capture.GameView
{
    /// <summary>
    /// Warm-capture-lane editor-session policy: attach journals the current Enter
    /// Play Mode Options and switches to no-reload play (the ~7× cheaper play-enter
    /// the lane exists for); release restores them. The journal survives a
    /// hard-killed lane — recovery runs on the next editor load, gated by a
    /// SessionState marker so mid-lane domain reloads never restore early. Sibling
    /// of the clip-scoped CaptureRecoveryJournal; this one is lane-session-scoped.
    /// Driven over the `unity` CLI: capture_lane_attach / capture_lane_release /
    /// capture_request_scenario (relays to CaptureDispatch).
    /// </summary>
    [InitializeOnLoad]
    internal static class CaptureLaneSession
    {
        internal const string ActiveMarker = "Game.Capture.CaptureLaneSession.Active";

        internal static readonly string JournalPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Library", "NativeGizmoCapture", "lane_session.json"));

        [Serializable]
        private sealed class Journal
        {
            public bool epoEnabled;
            public int epoOptions;
        }

        static CaptureLaneSession()
        {
            EditorApplication.delayCall += RecoverAbandoned;
        }

        [CliCommand("capture_lane_attach",
            "Begin a warm-capture lane session: journal Enter Play Mode Options and switch to no-reload play. Restored by capture_lane_release; a hard-killed lane restores on the editor's next load.",
            Tags = new[] { "capture" })]
        public static string Attach()
        {
            if (SessionState.GetBool(ActiveMarker, false))
                return "lane already attached; Enter Play Mode Options unchanged.";
            RecoverAbandoned();

            var journal = new Journal
            {
                epoEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                epoOptions = (int)EditorSettings.enterPlayModeOptions,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(JournalPath));
            File.WriteAllText(JournalPath, JsonUtility.ToJson(journal, true));
            SessionState.SetBool(ActiveMarker, true);

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            return "lane attached: play mode now enters without domain or scene reload.";
        }

        [CliCommand("capture_lane_release",
            "End the warm-capture lane session: restore the journaled Enter Play Mode Options.",
            Tags = new[] { "capture" })]
        public static string Release()
        {
            if (!SessionState.GetBool(ActiveMarker, false))
            {
                if (!File.Exists(JournalPath)) return "no lane attached.";
                Restore();
                return "abandoned lane journal found and restored.";
            }
            Restore();
            SessionState.SetBool(ActiveMarker, false);
            return "lane released: Enter Play Mode Options restored.";
        }

        [CliCommand("capture_request_scenario",
            "Queue a one-shot capture scenario for the next routed CaptureScenarioPlayModeTests run. Cleared on read; survives domain reloads, dies with the editor.",
            Tags = new[] { "capture" })]
        public static string RequestScenario(
            [CliArg("scenario", "CaptureScenario type name, e.g. TwoShipSkirmishScenario.")] string scenario)
        {
            CaptureDispatch.Request(scenario);
            return "capture request queued: " + scenario;
        }

        internal static void RecoverAbandoned()
        {
            if (SessionState.GetBool(ActiveMarker, false) || !File.Exists(JournalPath)) return;
            try { Restore(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void Restore()
        {
            var journal = JsonUtility.FromJson<Journal>(File.ReadAllText(JournalPath));
            if (journal == null) throw new InvalidDataException($"Unreadable capture-lane journal at {JournalPath}.");
            EditorSettings.enterPlayModeOptionsEnabled = journal.epoEnabled;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)journal.epoOptions;
            File.Delete(JournalPath);
        }
    }
}
