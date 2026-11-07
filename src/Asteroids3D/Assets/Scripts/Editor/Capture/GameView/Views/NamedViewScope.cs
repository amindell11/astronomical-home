using System;
using System.Collections.Generic;
using Cameras;
using Capture.GameView.Recovery;
using Capture.Views;
using Diagnostics;
using Ships;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capture.GameView.Views
{
    internal sealed class NamedViewScope : IDisposable
    {
        private readonly DiagnosticViewTransaction transaction;
        private readonly Action released;
        private GameObject root;
        private ObserverCam observer;
        private bool observerVisible;
        private bool disposed;

        private NamedViewScope(Action released)
        {
            this.released = released;
            transaction = new DiagnosticViewTransaction(Type.EmptyTypes, Array.Empty<Object>(), null,
                GizmoScope.All, 0, presentation: true);
        }

        public static IDisposable GameTopdown(ObserverCam observer, Action released)
        {
            if (!observer) throw new ArgumentException("Game topdown requires the caller's ObserverCam.");
            var scope = new NamedViewScope(released);
            try
            {
                scope.observer = observer;
                scope.observerVisible = observer.Cam.enabled;
                observer.SetVisible(true);
                PresentationSettings.HideDebugOverlay();
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        public static IDisposable LineupNeutral(IReadOnlyList<Ship> subjects, Action released)
        {
            if (subjects == null || subjects.Count == 0) throw new ArgumentException("Neutral lineup requires visual ship prefabs.");
            foreach (var subject in subjects)
                if (!subject || !subject.VisualRig)
                    throw new ArgumentException($"Neutral lineup requires a visual rig on { (subject ? subject.name : "null subject") }.");
            var scope = new NamedViewScope(released);
            try
            {
                PresentationSettings.ApplyNeutralLighting();
                PresentationSettings.HideDebugOverlay();
                scope.root = new GameObject("[Capture] Neutral lineup");
                scope.root.SetActive(false);
                for (var index = 0; index < subjects.Count; index++)
                {
                    var prefab = subjects[index];
                    var anchor = new GameObject(prefab.name).transform;
                    anchor.SetParent(scope.root.transform, false);
                    anchor.localPosition = new Vector3((index - (subjects.Count - 1) * .5f) * 3.2f, 0, 0);
                    anchor.localScale = prefab.transform.localScale;
                    Object.Instantiate(prefab.VisualRig, anchor, false);
                }
                scope.root.AddComponent<NeutralLineupView>();
                scope.root.SetActive(true);
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        public static IDisposable Hud1280x720(Action released)
        {
            var scope = new NamedViewScope(released);
            try
            {
                PresentationSettings.HideDebugOverlay();
                new UnityGameViewAdapter().Prepare(1280, 720, false);
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            CaptureRecoveryJournal.Attempt(() => { if (root) Object.DestroyImmediate(root); }, failures);
            CaptureRecoveryJournal.Attempt(() => { if (observer) observer.SetVisible(observerVisible); }, failures);
            CaptureRecoveryJournal.Attempt(transaction.Restore, failures);
            released();
            if (failures.Count > 0) throw new AggregateException("Named capture view restoration failed.", failures);
        }
    }
}
