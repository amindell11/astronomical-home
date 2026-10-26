using System.Collections;
using Combat.Targeting;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UI.PlayerState;
using Ships.Registry;
using System.Collections.Generic;

namespace Tests.PlayMode
{
    [Category("UI")]
    public class UILifecyclePlayModeTests : PlayModeWorldFixture
    {
        private sealed class StubStatus : IShipStatus
        {
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable { get; set; }
            public float BoostCooldownRemaining { get; set; }
            public float BoostCooldownPct { get; set; }
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
            public IReadOnlyList<ITrackingMissile> TrackingMissiles => System.Array.Empty<ITrackingMissile>();
        }

        private GameObject ownedRoot;

        public override void TearDown()
        {
            DestroyTestObject(ownedRoot);
            base.TearDown();
        }

        private GameObject CreateRoot(string name) => ownedRoot = new GameObject(name);

        /// <summary>
        /// Rig visuals are wired by injection (<see cref="IShipVisual.Bind"/>), not parent discovery:
        /// once a LockChannel is injected, the indicator responds to lock progress, and it re-subscribes
        /// across a disable/enable cycle (death/respawn).
        /// </summary>
        [UnityTest]
        public IEnumerator LockOnIndicator_Injected_RespondsToProgress_AndResubscribesOnReenable()
        {
            var channel = new LockChannel();

            // The indicator lives under a parent in the rig; LateUpdate reads transform.parent.
            var parent = CreateRoot("RigRoot");
            var indicatorGo = new GameObject("LockOnIndicator");
            indicatorGo.transform.SetParent(parent.transform, false);
            indicatorGo.AddComponent<CanvasGroup>();
            indicatorGo.AddComponent<Image>();
            var indicator = indicatorGo.AddComponent<LockOnIndicator>();
            var canvasGroup = indicatorGo.GetComponent<CanvasGroup>();

            yield return null; // Awake/Start (Hide)

            // Inject the lock channel exactly as the presentation installer does.
            indicator.Bind(new ShipView(indicatorGo.transform, null, null, channel, isPlayer: false));
            yield return null;

            channel.RaiseProgress(0.4f);
            yield return null;
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f, "Indicator should show when receiving lock progress");

            channel.RaiseReleased();
            yield return null;
            Assert.AreEqual(0f, canvasGroup.alpha, 0.0001f, "Indicator should hide on release");

            indicatorGo.SetActive(false);
            yield return null;
            indicatorGo.SetActive(true);
            yield return null;

            channel.RaiseProgress(0.6f);
            yield return null;
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f,
                "Indicator should resubscribe and show again after disable/enable");
        }

        /// <summary>
        /// The boost gauge charges toward full as the cooldown runs down and recolors at the
        /// ready edge; Initialize seeds from current state so a rebind never shows stale fill.
        /// </summary>
        [UnityTest]
        public IEnumerator BoostGaugeUI_TracksCooldown_AndRecolorsAtReadyEdge()
        {
            var status = new StubStatus { BoostAvailable = false, BoostCooldownPct = 0.6f };

            var go = CreateRoot("BoostGauge");
            var image = go.AddComponent<Image>();
            var gauge = go.AddComponent<BoostGaugeUI>();

            gauge.Initialize(status);
            Assert.AreEqual(0.4f, image.fillAmount, 0.001f, "Fill should charge as the cooldown runs down");
            var coolingColor = image.color;

            status.BoostAvailable = true;
            status.BoostCooldownPct = 0f;
            yield return null;

            Assert.AreEqual(1f, image.fillAmount, 0.001f, "Fill should be full once boost is ready");
            Assert.AreNotEqual(coolingColor, image.color, "Gauge should recolor at the ready edge");
        }
    }
}
