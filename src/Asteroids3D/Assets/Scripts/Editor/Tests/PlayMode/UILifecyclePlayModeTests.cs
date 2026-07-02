using System.Collections;
using Combat.Targeting;
using NUnit.Framework;
using Ships.Presentation;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    [Category("Regression")]
    [Category("UI")]
    public class UILifecyclePlayModeTests : PlayModeWorldFixture
    {
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
            indicator.Bind(new ShipView(indicatorGo.transform, null, null, channel));
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

        /// <summary>
        /// An unbound ShieldUI (never injected) is inert — it neither throws nor logs, it simply does
        /// nothing until a ShipView is bound.
        /// </summary>
        [UnityTest]
        public IEnumerator ShieldUI_Unbound_IsInertAndDoesNotThrow()
        {
            var go = new GameObject("ShieldUI");
            go.AddComponent<Image>();
            go.AddComponent<ShieldUI>();

            yield return null;

            Assert.IsTrue(go.activeInHierarchy);
            Object.Destroy(go);
        }
    }
}
