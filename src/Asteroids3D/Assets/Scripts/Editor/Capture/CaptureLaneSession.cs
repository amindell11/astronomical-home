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
    /// capture_request_scenario (relays to CaptureDispatch). State lives in an
    /// injectable store so tests never touch the production lane.
    /// </summary>
    [InitializeOnLoad]
    internal static class CaptureLaneSession
    {
        internal sealed class LaneStore
        {
            public readonly string activeMarker;
            public readonly string journalPath;

            public LaneStore(string activeMarker, string journalPath)
            {
                this.activeMarker = activeMarker;
                this.journalPath = journalPath;
            }
        }

        private static readonly LaneStore Production = new(
            "Game.Capture.CaptureLaneSession.Active",
            Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Library", "NativeGizmoCapture", "lane_session.json")));

        [Serializable]
        private sealed class Journal
        {
            public bool epoEnabled;
            public int epoOptions;
        }

        static CaptureLaneSession()
        {
            EditorApplication.delayCall += () => RecoverAbandoned(Production);
        }

        [CliCommand("capture_lane_attach",
            "Begin a warm-capture lane session: journal Enter Play Mode Options and switch to no-reload play. Restored by capture_lane_release; a hard-killed lane restores on the editor's next load.",
            Tags = new[] { "capture" })]
        public static string Attach() => Attach(Production);

        internal static string Attach(LaneStore store)
        {
            if (SessionState.GetBool(store.activeMarker, false))
                return "lane already attached; Enter Play Mode Options unchanged.";
            RecoverAbandoned(store);

            var journal = new Journal
            {
                epoEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                epoOptions = (int)EditorSettings.enterPlayModeOptions,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(store.journalPath));
            File.WriteAllText(store.journalPath, JsonUtility.ToJson(journal, true));
            SessionState.SetBool(store.activeMarker, true);

            // Unity 6 ignores the checked-in m_EnterPlayModeOptionsEnabled; set and restore it here.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            return "lane attached: play mode now enters without domain or scene reload.";
        }

        [CliCommand("capture_lane_release",
            "End the warm-capture lane session: restore the journaled Enter Play Mode Options.",
            Tags = new[] { "capture" })]
        public static string Release() => Release(Production);

        internal static string Release(LaneStore store)
        {
            if (!SessionState.GetBool(store.activeMarker, false))
            {
                if (!File.Exists(store.journalPath)) return "no lane attached.";
                Restore(store);
                return "abandoned lane journal found and restored.";
            }
            Restore(store);
            SessionState.SetBool(store.activeMarker, false);
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

        internal static void RecoverAbandoned(LaneStore store)
        {
            if (SessionState.GetBool(store.activeMarker, false) || !File.Exists(store.journalPath)) return;
            try { Restore(store); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void Restore(LaneStore store)
        {
            var journal = JsonUtility.FromJson<Journal>(File.ReadAllText(store.journalPath));
            if (journal == null) throw new InvalidDataException($"Unreadable capture-lane journal at {store.journalPath}.");
            EditorSettings.enterPlayModeOptionsEnabled = journal.epoEnabled;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)journal.epoOptions;
            File.Delete(store.journalPath);
        }
    }
}
