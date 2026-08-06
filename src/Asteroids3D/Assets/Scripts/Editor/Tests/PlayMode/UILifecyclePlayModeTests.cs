using System.Collections;
using Combat.Targeting;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Damage;
using Ships.Presentation;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    [Category("UI")]
    public class UILifecyclePlayModeTests : PlayModeWorldFixture
    {
        private sealed class FakeDamageEvents : IDamageEvents
        {
            public event System.Action<Damage.DamageInfo> OnDamaged { add { } remove { } }
            public event System.Action<ShipId, Damage.DamageInfo> OnDeath { add { } remove { } }
            public Resource Health { get; } = new Resource(100f);
            public RegenResource Shield { get; } = new RegenResource(50f, 0f, 999f);
        }

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
        }

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
            var parent = new GameObject("RigRoot");
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

            Object.Destroy(parent);
        }

        // Bars live under a parent in the rig; LateUpdate reads transform.parent.
        private static StatusBarUI CreateBar(Transform parent, StatusBarUI.TrackedResource tracked, out Image fill)
        {
            var root = new GameObject(tracked + "Bar");
            root.transform.SetParent(parent, false);
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            fill = fillGo.AddComponent<Image>();
            fill.fillAmount = 1f;
            var bar = root.AddComponent<StatusBarUI>();
            bar.Tracked = tracked;
            bar.Fill = fill;
            return bar;
        }

        /// <summary>
        /// An unbound StatusBarUI (never injected) is inert — it neither throws nor logs, it simply does
        /// nothing until a ShipView is bound.
        /// </summary>
        [UnityTest]
        public IEnumerator StatusBarUI_Unbound_IsInertAndDoesNotThrow()
        {
            var parent = new GameObject("RigRoot");
            var bar = CreateBar(parent.transform, StatusBarUI.TrackedResource.Shield, out _);

            yield return null;

            Assert.IsTrue(bar.gameObject.activeInHierarchy);
            Object.Destroy(parent);
        }

        /// <summary>
        /// Bind seeds the bar's fill from the bound resource's current fraction — the bar reads
        /// correctly from frame 0 instead of showing the prefab's fill until the first damage event.
        /// </summary>
        [UnityTest]
        public IEnumerator StatusBarUI_Bind_SeedsFillFromBoundResource()
        {
            var damage = new FakeDamageEvents();
            damage.Health.ApplyDamage(40f); // 60 %
            damage.Shield.ApplyDamage(25f); // 50 %

            var parent = new GameObject("RigRoot");
            var shieldBar = CreateBar(parent.transform, StatusBarUI.TrackedResource.Shield, out var shieldFill);
            var healthBar = CreateBar(parent.transform, StatusBarUI.TrackedResource.Health, out var healthFill);

            yield return null;

            shieldBar.Bind(new ShipView(parent.transform, damage, null, null, isPlayer: false));
            healthBar.Bind(new ShipView(parent.transform, damage, null, null, isPlayer: false));

            Assert.AreEqual(0.5f, shieldFill.fillAmount, 0.001f, "Shield bar should seed from the bound shield fraction");
            Assert.AreEqual(0.6f, healthFill.fillAmount, 0.001f, "Health bar should seed from the bound health fraction");

            Object.Destroy(parent);
        }

        /// <summary>
        /// The boost gauge charges toward full as the cooldown runs down and recolors at the
        /// ready edge; Initialize seeds from current state so a rebind never shows stale fill.
        /// </summary>
        [UnityTest]
        public IEnumerator BoostGaugeUI_TracksCooldown_AndRecolorsAtReadyEdge()
        {
            var status = new StubStatus { BoostAvailable = false, BoostCooldownPct = 0.6f };

            var go = new GameObject("BoostGauge");
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

            Object.Destroy(go);
        }
    }
}
