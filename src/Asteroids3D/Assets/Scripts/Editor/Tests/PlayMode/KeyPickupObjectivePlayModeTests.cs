using System.Collections;
using System.Collections.Generic;
using System;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>KeyPickup → ObjectiveTracker pipeline against rigidbody player identity; physics triggers require PlayMode.</summary>
    [Category("Objectives")]
    public class KeyPickupObjectivePlayModeTests : PlayModeWorldFixture
    {
        private KeyPickup CreateKeyPickup(float radius = 2f)
        {
            var go = new GameObject("KeyPickup");
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var kp = go.AddComponent<KeyPickup>();
            kp.SpawnKey(Vector3.zero);
            go.transform.position = Vector3.zero;
            return kp;
        }

        /// <summary>Rigidbody on the root, collider on a child — mirrors real player ships.</summary>
        private Rigidbody CreatePlayerBody(Vector3 pos)
        {
            var root = new GameObject("PlayerRoot");
            root.transform.position = pos;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var child = new GameObject("HullCollider");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<SphereCollider>().radius = 0.5f;

            return rb;
        }

        private ObjectiveTracker CreateTracker(KeyPickup kp)
        {
            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(kp),
                ["key"] = () => new KeyAcquiredState(),
                ["extraction"] = () => new ExtractionChallengeState(new StubExtractionZone()),
                ["extracted"] = () => new ExtractedState(),
                ["failed"] = () => new FailedState()
            };
            return new ObjectiveTracker(MissionDefinition.CreateDefault(), builders);
        }

        [UnityTest]
        public IEnumerator KeyPickup_WithoutInjectedIdentity_DoesNotCollect()
        {
            var kp = CreateKeyPickup(radius: 2f);
            var tracker = CreateTracker(kp);
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);

            var player = CreatePlayerBody(Vector3.zero);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(kp.PlayerHasKey, "Key must NOT be collected before player identity is injected");

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState,
                "Tracker must stay in Explore when key is not collected");

            Object.Destroy(player.gameObject);
            Object.Destroy(kp.gameObject);
        }

        [UnityTest]
        public IEnumerator KeyPickup_ChildCollider_WithInjectedIdentity_CollectsKey()
        {
            var kp = CreateKeyPickup(radius: 2f);
            var tracker = CreateTracker(kp);
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);

            var player = CreatePlayerBody(Vector3.zero);
            kp.Initialize(player);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(kp.PlayerHasKey, "A child collider must collect via its attachedRigidbody identity");
            Assert.IsFalse(kp.gameObject.activeSelf, "Key GO must deactivate on collect");

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState,
                "Tracker must advance to KeyAcquired after key collected");

            Object.Destroy(player.gameObject);
            Object.Destroy(kp.gameObject);
        }

        [UnityTest]
        public IEnumerator KeyPickup_FullPipeline_ExploreToExtractionChallenge()
        {
            var kp = CreateKeyPickup(radius: 2f);
            var tracker = CreateTracker(kp);

            var player = CreatePlayerBody(Vector3.zero);
            kp.Initialize(player);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState);

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            Object.Destroy(player.gameObject);
            Object.Destroy(kp.gameObject);
        }

        [UnityTest]
        public IEnumerator KeyPickup_PlayerOutsideRadius_DoesNotCollect()
        {
            var kp = CreateKeyPickup(radius: 1f);

            var player = CreatePlayerBody(new Vector3(10f, 0f, 0f));
            kp.Initialize(player);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(kp.PlayerHasKey, "Key must not be collected when player is out of range");

            Object.Destroy(player.gameObject);
            Object.Destroy(kp.gameObject);
        }

        [UnityTest]
        public IEnumerator KeyPickup_NonPlayerRigidbody_IsIgnored()
        {
            var kp = CreateKeyPickup(radius: 2f);

            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f));
            kp.Initialize(player);

            var enemy = new GameObject("Enemy");
            enemy.transform.position = Vector3.zero;
            var rb = enemy.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            enemy.AddComponent<SphereCollider>().radius = 0.5f;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(kp.PlayerHasKey, "A non-player rigidbody must not trigger key collection");

            Object.Destroy(player.gameObject);
            Object.Destroy(enemy);
            Object.Destroy(kp.gameObject);
        }

        private sealed class StubExtractionZone : IExtractionZone
        {
            public bool IsPlayerInZone => false;
        }
    }
}
