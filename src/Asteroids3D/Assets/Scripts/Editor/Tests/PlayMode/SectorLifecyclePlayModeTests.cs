#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.Sectors;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for sector load/lifecycle determinism: the inactive-holder instantiate
    /// (content children must not Awake before adoption wires them) and completion signalling.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorLifecyclePlayModeTests : PlayModeWorldFixture
    {
        /// <summary>Records when its Awake runs (used to prove the inactive-holder gating).</summary>
        private class AwakeProbe : MonoBehaviour
        {
            // NonSerialized so Instantiate does not COPY a true value from an already-awoken
            // template (the clone must report only its OWN Awake).
            [System.NonSerialized] public bool Awoke;
            private void Awake() => Awoke = true;
        }

        private class CompletableTestSector : Sector
        {
            public void FireComplete(SectorResult r) => CompleteSector(r);
        }

        private readonly List<GameObject> _created = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
            base.TearDown();
        }

        private GameObject Track(GameObject go) { _created.Add(go); return go; }

        [UnityTest]
        public IEnumerator InactiveHolder_ContentDoesNotAwakeUntilReparentedOut()
        {
            // Template is active (like an authored prefab); the AwakeProbe flag is [NonSerialized]
            // so Instantiate does not copy the template probe's already-true Awoke onto the clone.
            var template = Track(new GameObject("SectorTemplate"));
            var contentChild = new GameObject("Content");
            contentChild.transform.SetParent(template.transform);
            contentChild.AddComponent<AwakeProbe>();

            // Inactive holder — mirrors MainGameManager.HandleLoadSector.
            var holder = Track(new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave });
            holder.SetActive(false);

            var instance = Object.Instantiate(template, holder.transform);
            _created.Add(instance);
            var probe = instance.GetComponentInChildren<AwakeProbe>(true);

            // While under the inactive holder, the content child must NOT have Awoken.
            yield return null;
            Assert.IsFalse(probe.Awoke,
                "Content under an inactive holder must not Awake before it is reparented out.");

            // Reparent out (world pose preserved) → now it becomes active and Awakes.
            instance.transform.SetParent(null, true);
            yield return null;
            Assert.IsTrue(probe.Awoke,
                "Content must Awake once reparented out of the inactive holder.");
        }

        [UnityTest]
        public IEnumerator Completion_FiresOnSectorComplete()
        {
            var go = Track(new GameObject("CompletableSector"));
            var sector = go.AddComponent<CompletableTestSector>();

            SectorResult? received = null;
            ((ISector)sector).OnSectorComplete += r => received = r;

            sector.FireComplete(SectorResult.Extracted());
            yield return null;

            Assert.IsTrue(received.HasValue, "OnSectorComplete must fire when the sector completes.");
        }
    }
}
#endif
