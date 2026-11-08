using System.Collections;
using NUnit.Framework;
using Substrate.Services.Objectives;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    /// <summary>The HUD lists each timed boost in force with its whole seconds left, one line each, and hides once every boost is closed or cleared.</summary>
    [Category("UI")]
    public class BoostReadoutPlayModeTests
    {
        private GameObject root;
        private ObjectiveService service;
        private Text text;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Boost readout");
            service = root.AddComponent<ObjectiveService>();
            var canvas = new GameObject("HUD", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var readout = BoostReadout.Create(canvas.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), null);
            readout.Initialize(service);
            text = readout.GetComponent<Text>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [UnityTest]
        public IEnumerator EachBoostInForce_ShowsALineWithItsSecondsLeft_UntilItCloses()
        {
            yield return null;
            Assert.IsFalse(text.enabled, "No boost, no readout.");

            var first = service.ShowBoost("Damage surge +100% damage", Time.time + 19.5f);
            yield return null;
            Assert.IsTrue(text.enabled);
            Assert.AreEqual("DAMAGE SURGE +100% DAMAGE  20s", text.text);

            service.ShowBoost("Shield surge +100% shield regen", Time.time + 4.2f);
            yield return null;
            Assert.AreEqual("DAMAGE SURGE +100% DAMAGE  20s|SHIELD SURGE +100% SHIELD REGEN  5s",
                text.text.Replace("\r", string.Empty).Replace("\n", "|"), "Each boost counts down on its own line.");

            first.Close();
            yield return null;
            Assert.AreEqual("SHIELD SURGE +100% SHIELD REGEN  5s", text.text);

            service.ClearAll();
            yield return null;
            Assert.IsFalse(text.enabled, "Clearing the service hides the readout.");
        }
    }
}
