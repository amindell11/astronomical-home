using System.Collections;
using Combat.Targeting;
using NUnit.Framework;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    [Category("Regression")]
    [Category("UI")]
    public class UILifecyclePlayModeTests
    {
        private sealed class FakeTargetable : MonoBehaviour, ITargetable
        {
            public Transform TargetPoint => transform;
            public LockChannel Lock { get; } = new LockChannel();
        }

        [UnityTest]
        public IEnumerator LockOnIndicator_Reenable_ReacquiresParentAndRespondsToProgress()
        {
            var parent = new GameObject("TargetableParent");
            parent.AddComponent<FakeTargetable>();

            var indicatorGo = new GameObject("LockOnIndicator");
            indicatorGo.transform.SetParent(parent.transform, false);
            indicatorGo.AddComponent<CanvasGroup>();
            indicatorGo.AddComponent<Image>();
            var indicator = indicatorGo.AddComponent<LockOnIndicator>();

            yield return null;

            var channel = parent.GetComponent<FakeTargetable>().Lock;
            var canvasGroup = indicatorGo.GetComponent<CanvasGroup>();

            channel.Progress?.Invoke(0.4f);
            yield return null;
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f, "Indicator should show when receiving lock progress");

            channel.Released?.Invoke();
            yield return null;
            Assert.AreEqual(0f, canvasGroup.alpha, 0.0001f, "Indicator should hide on release");

            indicatorGo.SetActive(false);
            yield return null;
            indicatorGo.SetActive(true);
            yield return null;

            channel.Progress?.Invoke(0.6f);
            yield return null;
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f,
                "Indicator should resubscribe and show again after disable/enable");

            Object.Destroy(parent);
        }

        [UnityTest]
        public IEnumerator ShieldUI_MissingDamageController_DoesNotThrow()
        {
            LogAssert.Expect(LogType.Warning, "[ShieldUI] No DamageController source found for ShieldUI");

            var go = new GameObject("ShieldUI");
            go.AddComponent<Image>();
            go.AddComponent<ShieldUI>();

            yield return null;

            Assert.IsTrue(go.activeInHierarchy);
            Object.Destroy(go);
        }
    }
}
