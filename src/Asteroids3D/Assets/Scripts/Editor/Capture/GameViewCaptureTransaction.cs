using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Capture.GameView
{
    internal sealed class GameViewCaptureTransaction
    {
        private readonly UnityGameViewAdapter gameView = new();
        private readonly GizmoInfo[] gizmos;
        private readonly Object[] selection;
        private readonly Object activeSelection;
        private readonly EditorWindow focusedWindow;
        private readonly CaptureRecoveryState recovery;
        private bool restored;

        public GameViewCaptureTransaction(Type[] profileTypes, Object[] subjects, Object activeSubject,
            int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Native Game View capture needs a graphics device; launch without -nographics.");
            ValidateTypes(profileTypes);

            gizmos = GizmoUtility.GetGizmoInfo();
            selection = Selection.objects;
            activeSelection = Selection.activeObject;
            focusedWindow = EditorWindow.focusedWindow;
            recovery = CaptureRecoveryJournal.Create(gizmos, gameView.Snapshot(),
                UrpGizmoCaptureAdapter.CompatibilityMode);
            CaptureRecoveryJournal.Write(recovery);

            try
            {
                UrpGizmoCaptureAdapter.Prepare();
                DisableAllAnnotations();
                foreach (var type in profileTypes) GizmoUtility.SetGizmoEnabled(type, true, false);
                SetSelection(subjects, activeSubject);
                gameView.Prepare(width, height);
            }
            catch
            {
                Restore();
                throw;
            }
        }

        public void SetSelection(Object[] subjects, Object activeSubject)
        {
            Selection.objects = subjects ?? Array.Empty<Object>();
            Selection.activeObject = activeSubject;
        }

        public void Restore()
        {
            if (restored) return;
            restored = true;

            var failures = new List<Exception>();
            CaptureRecoveryJournal.Attempt(() => Application.runInBackground = recovery.runInBackground, failures);
            CaptureRecoveryJournal.Attempt(RestoreAnnotations, failures);
            CaptureRecoveryJournal.Attempt(RestoreSelection, failures);
            CaptureRecoveryJournal.Attempt(() => gameView.Restore(recovery.gameView), failures);
            CaptureRecoveryJournal.Attempt(() => focusedWindow?.Focus(), failures);
            CaptureRecoveryJournal.Attempt(
                () => UrpGizmoCaptureAdapter.Restore(recovery.renderGraphCompatibilityMode), failures);
            if (failures.Count == 0) CaptureRecoveryJournal.Delete();
            else throw new AggregateException("Native gizmo capture state restoration was incomplete; journal retained.", failures);
        }

        private static void ValidateTypes(Type[] profileTypes)
        {
            if (profileTypes == null || profileTypes.Length == 0)
                throw new ArgumentException("Native gizmo capture profile selects no component types.");
            foreach (var type in profileTypes)
                if (!GizmoUtility.TryGetGizmoInfo(type, out _))
                    throw new InvalidOperationException(
                        $"Native gizmo capture profile declares {type.FullName}, but Unity has no registered gizmo for it.");
        }

        private void DisableAllAnnotations()
        {
            foreach (var saved in gizmos)
            {
                var disabled = saved;
                disabled.gizmoEnabled = false;
                disabled.iconEnabled = false;
                GizmoUtility.ApplyGizmoInfo(disabled, false);
            }
        }

        private void RestoreAnnotations()
        {
            CaptureRecoveryJournal.RestoreGizmos(recovery.gizmos);
        }

        private void RestoreSelection()
        {
            var live = new List<Object>(selection.Length);
            foreach (var item in selection)
                if (item)
                    live.Add(item);
            Selection.objects = live.ToArray();
            Selection.activeObject = activeSelection ? activeSelection : null;
        }
    }
}
