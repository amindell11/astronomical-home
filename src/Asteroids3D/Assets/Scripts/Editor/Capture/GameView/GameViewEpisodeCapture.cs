using System;
using System.Collections.Generic;
using Capture.GameView.Recording;
using Capture.GameView.Recovery;
using Capture.GameView.Views;
using Capture.Recording;
using Capture.Views;
using Ships;
using Substrate.Services.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Capture.GameView
{
    [InitializeOnLoad]
    public sealed class GameViewEpisodeCapture : ScriptableObject, IEpisodeCapture, IDiagnosticViewFactory
    {
        private static GameViewEpisodeCapture active;
        private GameViewRecording recording;
        private DiagnosticViewScope view;

        static GameViewEpisodeCapture()
        {
            AssemblyReloadEvents.beforeAssemblyReload += RestoreActive;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) RestoreActive();
            };
            EditorApplication.quitting += RestoreActive;
            EditorApplication.delayCall += () =>
            {
                if (!active && CaptureRecoveryJournal.Exists) CaptureRecoveryJournal.Recover();
            };
        }

        public CaptureRecording Prepare(CaptureRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Claim();
            if (recording != null) throw new InvalidOperationException("A Game View recording is already prepared.");
            try
            {
                return recording = new GameViewRecording(request, () =>
                {
                    recording = null;
                    ReleaseIfEmpty();
                });
            }
            catch
            {
                ReleaseIfEmpty();
                throw;
            }
        }

        public IDisposable PrepareView(DiagnosticViewConfig config, GizmoCaptureProfile profile,
            IReadOnlyList<Ship> subjects, IProjectileService projectiles)
        {
            Claim();
            if (view != null) throw new InvalidOperationException("A diagnostic view is already prepared.");
            try
            {
                return view = new DiagnosticViewScope(config, profile, subjects, projectiles, () =>
                {
                    view = null;
                    ReleaseIfEmpty();
                });
            }
            catch
            {
                ReleaseIfEmpty();
                throw;
            }
        }

        private void Claim()
        {
            if (active && active != this) throw new InvalidOperationException("Another capture module owns the Game View.");
            active = this;
        }

        private void ReleaseIfEmpty()
        {
            if (recording == null && view == null) active = null;
        }

        private void RestoreOwned()
        {
            var failures = new List<Exception>();
            CaptureRecoveryJournal.Attempt(() => recording?.Dispose(), failures);
            CaptureRecoveryJournal.Attempt(() => view?.Dispose(), failures);
            if (failures.Count > 0) throw new AggregateException("Capture module cleanup failed.", failures);
        }

        private static void RestoreActive()
        {
            if (!active) return;
            try { active.RestoreOwned(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void OnDestroy()
        {
            if (active == this) RestoreOwned();
        }
    }
}
