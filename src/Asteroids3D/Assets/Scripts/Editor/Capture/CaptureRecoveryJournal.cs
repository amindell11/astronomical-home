using System;
using System.Collections.Generic;
using System.IO;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Game.Capture.GameView
{
    [Serializable]
    internal sealed class CaptureRecoveryState
    {
        public string unityVersion;
        public bool runInBackground;
        public bool renderGraphCompatibilityMode;
        public bool renderPipelineSettingsDirty;
        public string[] gateActive;
        public GameViewState gameView;
        public GizmoState[] gizmos;

        [Serializable]
        public sealed class GameViewState
        {
            public bool hadMainView;
            public int viewType;
            public bool focused;
            public int width;
            public int height;
            public bool wasGameView;
            public bool drawGizmos;
            public int selectedSizeIndex;
        }

        [Serializable]
        public sealed class GizmoState
        {
            public int index;
            public string name;
            public string scriptPath;
            public bool gizmoEnabled;
            public bool iconEnabled;
        }
    }

    internal static class CaptureRecoveryJournal
    {
        private static readonly string DirectoryPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Library", "NativeGizmoCapture"));
        private static readonly string JournalPath = Path.Combine(DirectoryPath, "recovery.json");

        public static bool Exists => File.Exists(JournalPath);

        public static CaptureRecoveryState Create(GizmoInfo[] gizmos,
            CaptureRecoveryState.GameViewState gameView, bool renderGraphCompatibilityMode,
            bool renderPipelineSettingsDirty, string[] gateActive)
        {
            var stored = new CaptureRecoveryState.GizmoState[gizmos.Length];
            for (var i = 0; i < gizmos.Length; i++)
            {
                var info = gizmos[i];
                stored[i] = new CaptureRecoveryState.GizmoState
                {
                    index = i,
                    name = info.name,
                    scriptPath = info.script ? AssetDatabase.GetAssetPath(info.script) : "",
                    gizmoEnabled = info.gizmoEnabled,
                    iconEnabled = info.iconEnabled,
                };
            }

            return new CaptureRecoveryState
            {
                unityVersion = Application.unityVersion,
                runInBackground = Application.runInBackground,
                renderGraphCompatibilityMode = renderGraphCompatibilityMode,
                renderPipelineSettingsDirty = renderPipelineSettingsDirty,
                gateActive = gateActive,
                gameView = gameView,
                gizmos = stored,
            };
        }

        public static void Write(CaptureRecoveryState state)
        {
            Directory.CreateDirectory(DirectoryPath);
            var temp = JournalPath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(state, true));
            if (File.Exists(JournalPath)) File.Delete(JournalPath);
            File.Move(temp, JournalPath);
        }

        public static void Delete()
        {
            if (File.Exists(JournalPath)) File.Delete(JournalPath);
        }

        public static void Recover()
        {
            if (!Exists) return;
            var state = JsonUtility.FromJson<CaptureRecoveryState>(File.ReadAllText(JournalPath));
            if (state == null || state.unityVersion != UnityGameViewAdapter.ExpectedUnityVersion)
                throw new InvalidDataException(
                    $"Native gizmo capture recovery journal targets Unity '{state?.unityVersion}', expected {UnityGameViewAdapter.ExpectedUnityVersion}.");

            var failures = new List<Exception>();
            Attempt(() => Application.runInBackground = state.runInBackground, failures);
            Attempt(() => DiagnosticGate.Replace(state.gateActive), failures);
            Attempt(() => RestoreGizmos(state.gizmos), failures);
            Attempt(() => new UnityGameViewAdapter().Restore(state.gameView), failures);
            Attempt(() => UrpGizmoCaptureAdapter.Restore(state.renderGraphCompatibilityMode), failures);
            Attempt(() => UrpGizmoCaptureAdapter.RestoreGlobalSettingsDirtyState(
                state.renderPipelineSettingsDirty), failures);
            if (failures.Count > 0)
                throw new AggregateException("Native gizmo capture recovery was incomplete; journal retained.", failures);
            Delete();
        }

        internal static void RestoreGizmos(CaptureRecoveryState.GizmoState[] stored)
        {
            var current = GizmoUtility.GetGizmoInfo();
            foreach (var saved in stored)
            {
                var match = Match(current, saved);
                match.gizmoEnabled = saved.gizmoEnabled;
                match.iconEnabled = saved.iconEnabled;
                GizmoUtility.ApplyGizmoInfo(match, false);
            }
        }

        private static GizmoInfo Match(GizmoInfo[] current, CaptureRecoveryState.GizmoState saved)
        {
            if (saved.index >= 0 && saved.index < current.Length && Matches(current[saved.index], saved))
                return current[saved.index];
            foreach (var info in current)
                if (Matches(info, saved))
                    return info;
            throw new InvalidOperationException($"Cannot restore Unity gizmo annotation '{saved.name}'.");
        }

        private static bool Matches(GizmoInfo info, CaptureRecoveryState.GizmoState saved) =>
            info.name == saved.name && (info.script ? AssetDatabase.GetAssetPath(info.script) : "") == saved.scriptPath;

        internal static void Attempt(Action action, List<Exception> failures)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }
    }
}
