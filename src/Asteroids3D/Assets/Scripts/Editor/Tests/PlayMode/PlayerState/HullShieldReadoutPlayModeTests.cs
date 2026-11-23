using System;
using System.Collections;
using System.Reflection;
using Damage;
using NUnit.Framework;
using Ships.Damage;
using Ships.Registry;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    [Category("UI")]
    public class HullShieldReadoutPlayModeTests
    {
        private GameObject root;
        private HullShieldReadout readout;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Hull shield readout");
            var canvas = new GameObject("HUD", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            readout = HullShieldReadout.Create(canvas.transform,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), null, null, () => true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
        }

        [Test]
        public void Binding_PutsShieldLeftAndHullRight_AboveTheirLabels()
        {
            readout.Initialize(new FakeDamage());
            var bars = readout.GetComponentsInChildren<StatusBarUI>();
            Assert.AreEqual(2, bars.Length);
            StatusBarUI shield = null;
            StatusBarUI hull = null;
            foreach (var bar in bars)
            {
                if (bar.rectTransform.anchoredPosition.x < 0f) shield = bar;
                else hull = bar;
            }

            Assert.IsNotNull(shield);
            Assert.IsNotNull(hull);
            Assert.AreEqual(StatusBarUI.ShieldTint, shield.color);
            Assert.AreEqual(StatusBarUI.HullTint, hull.color);
            var labels = readout.GetComponentsInChildren<Text>();
            Assert.AreEqual(2, labels.Length);
            string shieldText = null;
            string hullText = null;
            foreach (var label in labels)
            {
                if (label.rectTransform.anchoredPosition.x < 0f)
                {
                    shieldText = label.text;
                    Assert.AreEqual(shield.rectTransform.anchoredPosition.x, label.rectTransform.anchoredPosition.x, .001f);
                }
                else
                {
                    hullText = label.text;
                    Assert.AreEqual(hull.rectTransform.anchoredPosition.x, label.rectTransform.anchoredPosition.x, .001f);
                }
            }

            Assert.AreEqual("SHIELD", shieldText);
            Assert.AreEqual("HULL", hullText);
        }

        [UnityTest]
        public IEnumerator LowHull_PulsesTheHullBar()
        {
            var damage = new FakeDamage();
            readout.Initialize(damage);
            StatusBarUI shield = null;
            StatusBarUI hull = null;
            foreach (var bar in readout.GetComponentsInChildren<StatusBarUI>())
            {
                if (bar.rectTransform.anchoredPosition.x < 0f) shield = bar;
                else hull = bar;
            }

            Assert.IsNotNull(shield);
            Assert.IsNotNull(hull);
            damage.Health.SetFraction(.2f);
            yield return null;
            var hullFill = FillEdge(hull).color;
            Assert.Greater(hullFill.r, hullFill.g, "Low hull turns the hull fill red");
            Assert.AreEqual((Color32)StatusBarUI.ShieldTint, FillEdge(shield).color, "Shields never pulse");
        }

        [UnityTest]
        public IEnumerator Rebinding_ReplacesBothBars()
        {
            var a = new FakeDamage();
            var b = new FakeDamage();
            readout.Initialize(a);
            readout.Initialize(b);
            yield return null;
            var bars = readout.GetComponentsInChildren<StatusBarUI>();
            Assert.AreEqual(2, bars.Length);
            b.Health.SetFraction(.55f);
            StatusBarUI hull = null;
            foreach (var bar in bars)
                if (bar.rectTransform.anchoredPosition.x > 0f) hull = bar;
            Assert.IsNotNull(hull);
            Assert.AreEqual(-44f + 88f * .55f, FillEdge(hull).position.x, .001f);
        }

        [UnityTest]
        public IEnumerator KillRefill_FlashesTheHullGained_SummingUntilItFades()
        {
            readout.Initialize(new FakeDamage());
            readout.ShowGain(0f);
            Assert.IsNull(GainLabel(), "A refill at full hull gains nothing, so nothing shows.");

            readout.ShowGain(10f);
            Assert.AreEqual("+10 HULL", GainLabel().text);
            readout.ShowGain(40f);
            Assert.AreEqual("+50 HULL", GainLabel().text, "A kill while the flash is up adds to it.");

            yield return new WaitForSeconds(2f);
            Assert.IsNull(GainLabel(), "The flash fades out.");
            readout.ShowGain(.4f);
            Assert.AreEqual("+1 HULL", GainLabel().text, "A fresh flash starts over and never reads +0.");
        }

        private Text GainLabel()
        {
            foreach (var label in readout.GetComponentsInChildren<Text>())
                if (label.text.StartsWith("+")) return label;
            return null;
        }

        private static UIVertex FillEdge(StatusBarUI bar)
        {
            using var mesh = new VertexHelper();
            typeof(StatusBarUI).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                .Invoke(bar, new object[] { mesh });
            var vertex = new UIVertex();
            // The fill draws last, so the last quad's top-right corner is where it ends.
            mesh.PopulateUIVertex(ref vertex, mesh.currentVertCount - 2);
            return vertex;
        }

        private sealed class FakeDamage : IDamageEvents
        {
            public event Action<AppliedDamage> OnDamaged { add { } remove { } }
            public event Action<ShipId, DamageInfo> OnDeath { add { } remove { } }
            public Resource Health { get; } = new Resource(400f);
            public RegenResource Shield { get; } = new RegenResource(50f, 0f, 999f);
        }
    }
}
