using System;
using System.Collections.Generic;
using Game.Diagnostics;
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
        private readonly GizmoViewState gizmoView;
        private bool restored;

        public GameViewCaptureTransaction(Type[] profileTypes, Object[] subjects, Object activeSubject,
            int width, int height, GizmoScope scope, int scopeTeam)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Native Game View capture needs a graphics device; launch without -nographics.");
            ValidateTypes(profileTypes);

            gizmos = GizmoUtility.GetGizmoInfo();
            selection = Selection.objects;
            activeSelection = Selection.activeObject;
            focusedWindow = EditorWindow.focusedWindow;
            gizmoView = GizmoViewState.Snapshot();
            recovery = CaptureRecoveryJournal.Create(gizmos, gameView.Snapshot(),
                UrpGizmoCaptureAdapter.CompatibilityMode, UrpGizmoCaptureAdapter.GlobalSettingsDirty);
            CaptureRecoveryJournal.Write(recovery);

            try
            {
                UrpGizmoCaptureAdapter.Prepare();
                DisableAllAnnotations();
                foreach (var type in profileTypes) GizmoUtility.SetGizmoEnabled(type, true, false);
                DriveGizmoView(profileTypes, scope, scopeTeam);
                SetSelection(subjects, activeSubject);
                gameView.Prepare(width, height);
            }
            catch
            {
                Restore();
                throw;
            }
        }

        // Drawers gate on GizmoView.IsOn (default OFF), not GizmoType.Selected, so a capture must turn
        // the profile's subviews on itself; colliders are the presentation-off silhouette source.
        private static void DriveGizmoView(Type[] profileTypes, GizmoScope scope, int scopeTeam)
        {
            foreach (var subview in GizmoView.Subviews)
                if (Array.IndexOf(profileTypes, subview.ComponentType) >= 0)
                    GizmoView.SetOn(subview.ComponentType, subview.Key, true);
            GizmoView.Scope = scope;
            if (scope == GizmoScope.Team) GizmoView.ScopeTeam = scopeTeam;
            GizmoView.CollidersOn = true;
        }

        // activeObject narrows the selection to itself, so the full subject set must land last or child drawers go dark.
        public void SetSelection(Object[] subjects, Object activeSubject)
        {
            Selection.activeObject = activeSubject;
            Selection.objects = subjects ?? Array.Empty<Object>();
        }

        public void Restore()
        {
            if (restored) return;
            restored = true;

            var failures = new List<Exception>();
            CaptureRecoveryJournal.Attempt(() => Application.runInBackground = recovery.runInBackground, failures);
            // CollidersOn re-drives GizmoUtility; RestoreAnnotations must run after to own the native table.
            CaptureRecoveryJournal.Attempt(gizmoView.Restore, failures);
            CaptureRecoveryJournal.Attempt(RestoreAnnotations, failures);
            CaptureRecoveryJournal.Attempt(RestoreSelection, failures);
            CaptureRecoveryJournal.Attempt(() => gameView.Restore(recovery.gameView), failures);
            CaptureRecoveryJournal.Attempt(RestoreFocus, failures);
            CaptureRecoveryJournal.Attempt(
                () => UrpGizmoCaptureAdapter.Restore(recovery.renderGraphCompatibilityMode), failures);
            CaptureRecoveryJournal.Attempt(
                () => UrpGizmoCaptureAdapter.RestoreGlobalSettingsDirtyState(
                    recovery.renderPipelineSettingsDirty), failures);
            if (failures.Count == 0) CaptureRecoveryJournal.Delete();
            else throw new AggregateException("Native gizmo capture state restoration was incomplete; journal retained.", failures);
        }

        private static void ValidateTypes(Type[] profileTypes)
        {
            if (profileTypes == null)
                throw new ArgumentException("Native gizmo capture resolved a null profile.");
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

        // Lifetime-aware: a window closed during the capture is destroyed, not null.
        private void RestoreFocus()
        {
            if (focusedWindow) focusedWindow.Focus();
        }

        private void RestoreSelection()
        {
            var live = new List<Object>(selection.Length);
            foreach (var item in selection)
                if (item)
                    live.Add(item);
            Selection.activeObject = activeSelection ? activeSelection : null;
            Selection.objects = live.ToArray();
        }

        // Prior GizmoView state (every subview's on/off, scope, team, colliders), restored after the run:
        // EditorPrefs is machine-global, so a dev's window state must survive. Registry is static → stable indices.
        private sealed class GizmoViewState
        {
            private readonly (Type type, string key, bool on)[] subviews;
            private readonly GizmoScope scope;
            private readonly int scopeTeam;
            private readonly bool colliders;

            private GizmoViewState((Type, string, bool)[] subviews, GizmoScope scope, int scopeTeam, bool colliders)
            {
                this.subviews = subviews;
                this.scope = scope;
                this.scopeTeam = scopeTeam;
                this.colliders = colliders;
            }

            public static GizmoViewState Snapshot()
            {
                var registered = GizmoView.Subviews;
                var saved = new (Type, string, bool)[registered.Count];
                for (var i = 0; i < registered.Count; i++)
                    saved[i] = (registered[i].ComponentType, registered[i].Key,
                        GizmoView.IsOn(registered[i].ComponentType, registered[i].Key));
                return new GizmoViewState(saved, GizmoView.Scope, GizmoView.ScopeTeam, GizmoView.CollidersOn);
            }

            public void Restore()
            {
                foreach (var (type, key, on) in subviews) GizmoView.SetOn(type, key, on);
                GizmoView.Scope = scope;
                GizmoView.ScopeTeam = scopeTeam;
                GizmoView.CollidersOn = colliders;
            }
        }
    }
}
