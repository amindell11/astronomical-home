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
    /// <summary>The HUD lists each sector boost in force, one line each, and hides once every boost is closed or cleared.</summary>
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
        public IEnumerator EachBoostInForce_ShowsALine_UntilItCloses()
        {
            yield return null;
            Assert.IsFalse(text.enabled, "No boost, no readout.");

            var first = service.ShowBoost("+25% damage");
            yield return null;
            Assert.IsTrue(text.enabled);
            Assert.AreEqual("+25% DAMAGE", text.text);

            service.ShowBoost("+25% damage");
            yield return null;
            Assert.AreEqual("+25% DAMAGE\n+25% DAMAGE", text.text.Replace("\r", string.Empty), "Two claims show two lines.");

            first.Close();
            yield return null;
            Assert.AreEqual("+25% DAMAGE", text.text);

            service.ClearAll();
            yield return null;
            Assert.IsFalse(text.enabled, "Clearing the service hides the readout.");
        }
    }
}
