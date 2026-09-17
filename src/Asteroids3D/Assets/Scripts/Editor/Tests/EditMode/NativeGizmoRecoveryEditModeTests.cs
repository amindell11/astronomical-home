#if UNITY_EDITOR
using System;
using System.IO;
using Capture.GameView;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the hard-kill half of the native-capture state transaction: persistent Editor state is journaled before it is mutated, and a journal a killed process left behind replays on the next launch. In-process interruptions (failure, reload, Play Mode exit, quit) restore through the transaction itself and are covered live in RLCapturePlayModeTests.</summary>
    [Category("Core")]
    public class NativeGizmoRecoveryEditModeTests
    {
        private string journaledName;

        private bool priorGizmoEnabled;
        private bool priorIconEnabled;
        private bool priorRunInBackground;
        private bool priorCompatibilityMode;
        private bool priorGlobalSettingsDirty;

        [SetUp]
        public void SetUp()
        {
            // Any annotation round-trips the same; a user script's needs a graphics-warmed Library/AnnotationManager.
            var registered = GizmoUtility.GetGizmoInfo();
            var annotation = Array.Find(registered, info => info.hasGizmo);
            Assert.IsNotNull(annotation,
                $"none of the {registered.Length} annotations this Editor reports carries a gizmo to journal.");
            journaledName = annotation.name;
            // GizmoInfo is a reference type: hold the flags, never a snapshot object a later apply would alias.
            priorGizmoEnabled = annotation.gizmoEnabled;
            priorIconEnabled = annotation.iconEnabled;
            priorRunInBackground = Application.runInBackground;
            priorCompatibilityMode = UrpGizmoCaptureAdapter.CompatibilityMode;
            priorGlobalSettingsDirty = UrpGizmoCaptureAdapter.GlobalSettingsDirty;
        }

        [TearDown]
        public void TearDown()
        {
            CaptureRecoveryJournal.Delete();
            ApplyAnnotation(priorGizmoEnabled, priorIconEnabled);
            Application.runInBackground = priorRunInBackground;
            UrpGizmoCaptureAdapter.Restore(priorCompatibilityMode);
            UrpGizmoCaptureAdapter.RestoreGlobalSettingsDirtyState(priorGlobalSettingsDirty);
        }

        [Test]
        public void KilledCapture_ReplaysItsJournalAndConsumesIt()
        {
            WriteJournalOfCurrentState();
            MutateLikeALiveCapture();

            CaptureRecoveryJournal.Recover();

            AssertStateRestored();
            Assert.IsFalse(CaptureRecoveryJournal.Exists, "a completed recovery consumes its journal");
        }

        // Unity re-registers annotations across launches, so a recovered index cannot be trusted.
        [Test]
        public void ShiftedAnnotationIndices_StillRestoreByIdentity()
        {
            var state = CurrentState();
            foreach (var gizmo in state.gizmos) gizmo.index += state.gizmos.Length;
            CaptureRecoveryJournal.Write(state);
            MutateLikeALiveCapture();

            CaptureRecoveryJournal.Recover();

            AssertStateRestored();
        }

        [Test]
        public void JournalFromAnotherUnityVersion_IsRefusedAndRetained()
        {
            var state = CurrentState();
            state.unityVersion = "5000.0.0f1";
            CaptureRecoveryJournal.Write(state);

            Assert.Throws<InvalidDataException>(() => CaptureRecoveryJournal.Recover());
            Assert.IsTrue(CaptureRecoveryJournal.Exists,
                "a journal this Editor cannot read is retained for a matching one, never silently dropped");
        }

        [Test]
        public void UnresolvableAnnotation_FailsLoudlyAndRetainsTheJournal()
        {
            var state = CurrentState();
            state.gizmos = new[]
            {
                new CaptureRecoveryState.GizmoState
                {
                    index = 0, name = "NoSuchComponent", scriptPath = "", gizmoEnabled = true, iconEnabled = true,
                },
            };
            CaptureRecoveryJournal.Write(state);

            Assert.Throws<AggregateException>(() => CaptureRecoveryJournal.Recover());
            Assert.IsTrue(CaptureRecoveryJournal.Exists, "an incomplete recovery retains its journal for the next launch");
        }

        private static CaptureRecoveryState CurrentState() => CaptureRecoveryJournal.Create(
            GizmoUtility.GetGizmoInfo(), new UnityGameViewAdapter().Snapshot(),
            UrpGizmoCaptureAdapter.CompatibilityMode, UrpGizmoCaptureAdapter.GlobalSettingsDirty);

        private static void WriteJournalOfCurrentState() => CaptureRecoveryJournal.Write(CurrentState());

        private void MutateLikeALiveCapture()
        {
            ApplyAnnotation(!priorGizmoEnabled, !priorIconEnabled);
            Application.runInBackground = !priorRunInBackground;
            UrpGizmoCaptureAdapter.Restore(!priorCompatibilityMode);
        }

        private GizmoInfo Journaled()
        {
            var annotation = Array.Find(GizmoUtility.GetGizmoInfo(), info => info.name == journaledName);
            Assert.IsNotNull(annotation, $"annotation '{journaledName}' is no longer registered.");
            return annotation;
        }

        private void ApplyAnnotation(bool gizmoEnabled, bool iconEnabled)
        {
            var annotation = Journaled();
            annotation.gizmoEnabled = gizmoEnabled;
            annotation.iconEnabled = iconEnabled;
            GizmoUtility.ApplyGizmoInfo(annotation, false);
        }

        private void AssertStateRestored()
        {
            var restored = Journaled();
            Assert.AreEqual(priorGizmoEnabled, restored.gizmoEnabled, "annotation visibility");
            Assert.AreEqual(priorIconEnabled, restored.iconEnabled, "annotation icon");
            Assert.AreEqual(priorRunInBackground, Application.runInBackground);
            Assert.AreEqual(priorCompatibilityMode, UrpGizmoCaptureAdapter.CompatibilityMode);
        }
    }
}
#endif
