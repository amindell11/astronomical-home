#if UNITY_EDITOR
using System;
using System.IO;
using Game.Capture;
using Game.Capture.GameView;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>Pins the warm-capture lane seams: the one-home presentation rule, the one-shot dispatch request, and the lane session's journaled Enter Play Mode Options flip — including recovery after a hard-killed lane.</summary>
    [Category("Camera")]
    public class CaptureLaneEditModeTests
    {
        private bool savedEpoEnabled;
        private EnterPlayModeOptions savedEpoOptions;

        [SetUp]
        public void SetUp()
        {
            savedEpoEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedEpoOptions = EditorSettings.enterPlayModeOptions;
            ClearLaneState();
        }

        [TearDown]
        public void TearDown()
        {
            ClearLaneState();
            EditorSettings.enterPlayModeOptionsEnabled = savedEpoEnabled;
            EditorSettings.enterPlayModeOptions = savedEpoOptions;
        }

        private static void ClearLaneState()
        {
            SessionState.EraseBool(CaptureLaneSession.ActiveMarker);
            if (File.Exists(CaptureLaneSession.JournalPath)) File.Delete(CaptureLaneSession.JournalPath);
            CaptureDispatch.ConsumeRequest();
        }

        [Test]
        public void PresentationFor_OnlyNoneFilmsWithPresentation()
        {
            foreach (GizmoCaptureProfile profile in Enum.GetValues(typeof(GizmoCaptureProfile)))
                Assert.AreEqual(profile == GizmoCaptureProfile.None, GizmoCaptureProfiles.PresentationFor(profile),
                    $"profile {profile}: any gizmo profile films with presentation off, None films plain gameplay");
        }

        [Test]
        public void Dispatch_RequestIsConsumedExactlyOnce()
        {
            CaptureDispatch.Request("TwoShipSkirmishScenario");
            Assert.AreEqual("TwoShipSkirmishScenario", CaptureDispatch.ConsumeRequest());
            Assert.IsNull(CaptureDispatch.ConsumeRequest(), "a consumed request must never refire a stale scenario");
        }

        [Test]
        public void Dispatch_BlankRequestFailsAtTheBoundary()
        {
            Assert.Throws<ArgumentException>(() => CaptureDispatch.Request(" "));
            Assert.IsNull(CaptureDispatch.ConsumeRequest());
        }

        [Test]
        public void LaneAttach_FlipsToNoReloadAndReleaseRestores()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach();
            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload,
                EditorSettings.enterPlayModeOptions);
            Assert.IsTrue(File.Exists(CaptureLaneSession.JournalPath), "attach journals the prior values");

            CaptureLaneSession.Release();
            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
            Assert.IsFalse(File.Exists(CaptureLaneSession.JournalPath), "release consumes the journal");
        }

        [Test]
        public void LaneAttach_SecondAttachKeepsTheOriginalJournal()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach();
            CaptureLaneSession.Attach();
            CaptureLaneSession.Release();

            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled,
                "re-attach must not journal the already-flipped values");
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
        }

        [Test]
        public void LaneRecovery_RestoresAfterAHardKilledLane()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach();
            // A hard-killed lane leaves the journal but no SessionState (it dies with the editor).
            SessionState.EraseBool(CaptureLaneSession.ActiveMarker);

            CaptureLaneSession.RecoverAbandoned();
            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
            Assert.IsFalse(File.Exists(CaptureLaneSession.JournalPath));
        }

        [Test]
        public void LaneRecovery_NeverRestoresWhileTheLaneIsLive()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach();
            CaptureLaneSession.RecoverAbandoned();

            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled,
                "a mid-lane domain reload re-runs recovery, which must not undo the live lane");
            Assert.IsTrue(File.Exists(CaptureLaneSession.JournalPath));
        }
    }
}
#endif
