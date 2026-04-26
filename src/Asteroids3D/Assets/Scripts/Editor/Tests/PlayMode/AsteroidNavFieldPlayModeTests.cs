using System.Collections;
using AI.Planning;
using Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [Category("Planning")]
    public class AsteroidNavFieldPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            GamePlane.Reset();
        }

        [UnityTest]
        public IEnumerator NoAnchor_ReturnsFalse()
        {
            GamePlane.Configure(PlaneAxis.Y, Vector3.zero);
            var go = new GameObject("NavField");
            var nav = go.AddComponent<AsteroidNavField>();
            // No anchor set
            var ok = nav.TryGetRoutedTarget(Vector3.zero, null, RoutingMode.Chase, out _);
            Assert.IsFalse(ok);
            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoTarget_ReturnsFalse()
        {
            GamePlane.Configure(PlaneAxis.Y, Vector3.zero);
            var anchor = new GameObject("Anchor");
            var go = new GameObject("NavField");
            var nav = go.AddComponent<AsteroidNavField>();
            nav.SetAnchor(anchor.transform);
            var ok = nav.TryGetRoutedTarget(Vector3.zero, null, RoutingMode.Chase, out _);
            Assert.IsFalse(ok);
            Object.Destroy(go);
            Object.Destroy(anchor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GamePlaneNotConfigured_ReturnsFalse()
        {
            GamePlane.Reset();
            var anchor = new GameObject("Anchor");
            var go = new GameObject("NavField");
            var nav = go.AddComponent<AsteroidNavField>();
            nav.SetAnchor(anchor.transform);
            var ok = nav.TryGetRoutedTarget(Vector3.zero, null, RoutingMode.Chase, out _);
            Assert.IsFalse(ok);
            Object.Destroy(go);
            Object.Destroy(anchor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Service_Instantiates_Without_Errors()
        {
            GamePlane.Configure(PlaneAxis.Y, Vector3.zero);
            var go = new GameObject("NavField");
            Assert.DoesNotThrow(() => go.AddComponent<AsteroidNavField>());
            Object.Destroy(go);
            yield return null;
        }
    }
}
