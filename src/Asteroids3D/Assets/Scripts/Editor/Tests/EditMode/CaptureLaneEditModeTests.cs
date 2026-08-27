#if UNITY_EDITOR
using System;
using System.IO;
using Game.Capture;
using Game.Capture.GameView;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>Pins the warm-capture lane seams: the one-home presentation rule, the one-shot dispatch request, and the lane session's journaled Enter Play Mode Options flip — including recovery after a hard-killed lane. Runs against an injected store and request key, so a live lane in this editor is never touched.</summary>
    [Category("Camera")]
    public class CaptureLaneEditModeTests
    {
        private const string TestRequestKey = "Tests.EditMode.CaptureLane.RequestedScenario";

        private bool savedEpoEnabled;
        private EnterPlayModeOptions savedEpoOptions;
        private CaptureLaneSession.LaneStore store;

        [SetUp]
        public void SetUp()
        {
            savedEpoEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedEpoOptions = EditorSettings.enterPlayModeOptions;
            store = new CaptureLaneSession.LaneStore(
                "Tests.EditMode.CaptureLane.Active",
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Temp",
                    "CaptureLaneEditModeTests", "lane_session.json")));
            ClearTestLaneState();
        }

        [TearDown]
        public void TearDown()
        {
            ClearTestLaneState();
            EditorSettings.enterPlayModeOptionsEnabled = savedEpoEnabled;
            EditorSettings.enterPlayModeOptions = savedEpoOptions;
        }

        private void ClearTestLaneState()
        {
            SessionState.EraseBool(store.activeMarker);
            if (File.Exists(store.journalPath)) File.Delete(store.journalPath);
            CaptureDispatch.ConsumeRequest(TestRequestKey);
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
            CaptureDispatch.Request(TestRequestKey, "TwoShipSkirmishScenario");
            Assert.AreEqual("TwoShipSkirmishScenario", CaptureDispatch.ConsumeRequest(TestRequestKey));
            Assert.IsNull(CaptureDispatch.ConsumeRequest(TestRequestKey),
                "a consumed request must never refire a stale scenario");
        }

        [Test]
        public void Dispatch_BlankRequestFailsAtTheBoundary()
        {
            Assert.Throws<ArgumentException>(() => CaptureDispatch.Request(TestRequestKey, " "));
            Assert.IsNull(CaptureDispatch.ConsumeRequest(TestRequestKey));
        }

        [Test]
        public void LaneAttach_FlipsToNoReloadAndReleaseRestores()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach(store);
            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload,
                EditorSettings.enterPlayModeOptions);
            Assert.IsTrue(File.Exists(store.journalPath), "attach journals the prior values");

            CaptureLaneSession.Release(store);
            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
            Assert.IsFalse(File.Exists(store.journalPath), "release consumes the journal");
        }

        [Test]
        public void LaneAttach_SecondAttachKeepsTheOriginalJournal()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach(store);
            CaptureLaneSession.Attach(store);
            CaptureLaneSession.Release(store);

            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled,
                "re-attach must not journal the already-flipped values");
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
        }

        [Test]
        public void LaneRecovery_RestoresAfterAHardKilledLane()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach(store);
            // A hard-killed lane leaves the journal but no SessionState (it dies with the editor).
            SessionState.EraseBool(store.activeMarker);

            CaptureLaneSession.RecoverAbandoned(store);
            Assert.IsFalse(EditorSettings.enterPlayModeOptionsEnabled);
            Assert.AreEqual(EnterPlayModeOptions.None, EditorSettings.enterPlayModeOptions);
            Assert.IsFalse(File.Exists(store.journalPath));
        }

        [Test]
        public void LaneRecovery_NeverRestoresWhileTheLaneIsLive()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;

            CaptureLaneSession.Attach(store);
            CaptureLaneSession.RecoverAbandoned(store);

            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled,
                "a mid-lane domain reload re-runs recovery, which must not undo the live lane");
            Assert.IsTrue(File.Exists(store.journalPath));
        }
    }
}
#endif
