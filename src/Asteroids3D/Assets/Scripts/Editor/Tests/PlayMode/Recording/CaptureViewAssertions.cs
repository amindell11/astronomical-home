#if UNITY_EDITOR
using System;
using System.Collections;
using Tests.PlayMode.Common;
using UnityEngine;
using NUnit.Framework;

namespace Tests.PlayMode.Recording
{
    internal static class CaptureViewAssertions
    {
        public static string PresentationSnapshot()
        {
            var state = Activator.CreateInstance(Type.GetType("Capture.GameView.Recovery.CaptureRecoveryState, Capture.GameView.Editor", true));
            Type.GetType("Capture.GameView.Views.PresentationSettings, Capture.GameView.Editor", true)
                .GetMethod("Snapshot").Invoke(null, new[] { state });
            return JsonUtility.ToJson(state);
        }

        public static string ViewSnapshot()
        {
            var type = Type.GetType("Capture.GameView.UnityGameViewAdapter, Capture.GameView.Editor", true);
            return JsonUtility.ToJson(type.GetMethod("Snapshot").Invoke(Activator.CreateInstance(type), null));
        }

        public static IEnumerator Restored(string presentation, string view)
        {
            Assert.AreEqual(presentation, PresentationSnapshot());
            yield return AsyncAssert.WaitUntil(() => ViewSnapshot() == view, 30f,
                "named preparation restores the prior Game View after repaint");
        }
    }
}
#endif
