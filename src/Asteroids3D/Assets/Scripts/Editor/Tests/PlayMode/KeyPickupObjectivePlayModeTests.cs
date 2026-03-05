using System.Collections;
using System.Collections.Generic;
using System;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using Player;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the KeyPickup → ObjectiveTracker pipeline.
    /// Physics (OnTriggerEnter) is required, so these must run in PlayMode.
    ///
    /// Bug reproduced and fixed:
    ///   Trigger fired on KeyPickup but objective did not advance from Explore.
    ///   Root cause: player's collider was on a child object without the "Player" tag.
    ///   Fix: use GetComponentInParent&lt;PlayerMarker&gt;() instead of CompareTag,
    ///   matching the IDamageable pattern used by projectiles.
    /// </summary>
    [Category("Objectives")]
    public class KeyPickupObjectivePlayModeTests : PlayModeWorldFixture
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a KeyPickup at origin with a SphereCollider trigger.
        /// </summary>
        private KeyPickup CreateKeyPickup(float radius = 2f)
        {
            var go = new GameObject("KeyPickup");
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var kp = go.AddComponent<KeyPickup>();
            kp.SpawnKey(Vector3.zero);
            return kp;
        }

        /// <summary>
        /// Build a player-like hierarchy: root with PlayerMarker, child with collider.
        /// Mirrors real player ships where the collider is on a child object.
        /// </summary>
        private GameObject CreatePlayerHierarchy(Vector3 pos, bool withMarker)
        {
            var root = new GameObject("PlayerRoot");
            root.transform.position = pos;
            if (withMarker)
                root.AddComponent<PlayerMarker>();

            // Collider on a child — just like real ships
            var child = new GameObject("HullCollider");
            child.transform.SetParent(root.transform, false);
            var rb = child.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            child.AddComponent<SphereCollider>().radius = 0.5f;

            return root;
        }

        /// <summary>
        /// Build an ObjectiveTracker wired to the given KeyPickup.
        /// </summary>
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

        // ── Bug reproduction: no PlayerMarker ─────────────────────────────────────

        /// <summary>
        /// Reproduces the original bug: collider on child, no PlayerMarker on root.
        /// GetComponentInParent finds nothing → collected stays false → tracker stuck.
        /// </summary>
        [UnityTest]
        public IEnumerator KeyPickup_ChildCollider_NoMarker_DoesNotCollect()
        {
            var kp = CreateKeyPickup(radius: 2f);
            var tracker = CreateTracker(kp);
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);

            var player = CreatePlayerHierarchy(Vector3.zero, withMarker: false);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(kp.PlayerHasKey, "Key should NOT be collected without PlayerMarker");

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState,
                "Tracker should stay in Explore when key is not collected");

            Object.Destroy(player);
            Object.Destroy(kp.gameObject);
        }

        // ── Fix verified: PlayerMarker on root, collider on child ─────────────────

        [UnityTest]
        public IEnumerator KeyPickup_ChildCollider_WithMarker_CollectsKey()
        {
            var kp = CreateKeyPickup(radius: 2f);
            var tracker = CreateTracker(kp);
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);

            var player = CreatePlayerHierarchy(Vector3.zero, withMarker: true);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(kp.PlayerHasKey, "Key should be collected via GetComponentInParent<PlayerMarker>");
            Assert.IsFalse(kp.gameObject.activeSelf, "Key GO should deactivate on collect");

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState,
                "Tracker should advance to KeyAcquired after key collected");

            Object.Destroy(player);
            Object.Destroy(kp.gameObject);
        }

        // ── Full pipeline: key collect → tracker advances to ExtractionChallenge ──

        [UnityTest]
        public IEnumerator KeyPickup_FullPipeline_ExploreToExtractionChallenge()
        {
            var kp = CreateKeyPickup(radius: 2f);
            var tracker = CreateTracker(kp);

            var player = CreatePlayerHierarchy(Vector3.zero, withMarker: true);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState);

            tracker.Tick(Time.deltaTime);
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            Object.Destroy(player);
            Object.Destroy(kp.gameObject);
        }

        // ── OnKeyCollected event fires ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator KeyPickup_OnKeyCollected_FiresExactlyOnce()
        {
            var kp = CreateKeyPickup(radius: 2f);
            var fireCount = 0;
            kp.OnKeyCollected += () => fireCount++;

            var player = CreatePlayerHierarchy(Vector3.zero, withMarker: true);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, fireCount, "OnKeyCollected should fire exactly once");

            // Move player away and back — should not fire again (collected guard)
            player.transform.position = new Vector3(100f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, fireCount, "OnKeyCollected should not fire a second time");

            Object.Destroy(player);
            Object.Destroy(kp.gameObject);
        }

        // ── Player outside radius does not collect ────────────────────────────────

        [UnityTest]
        public IEnumerator KeyPickup_PlayerOutsideRadius_DoesNotCollect()
        {
            var kp = CreateKeyPickup(radius: 1f);

            var player = CreatePlayerHierarchy(new Vector3(10f, 0f, 0f), withMarker: true);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(kp.PlayerHasKey, "Key should not be collected when player is out of range");

            Object.Destroy(player);
            Object.Destroy(kp.gameObject);
        }

        // ── Non-player collider ignored ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator KeyPickup_NonPlayerCollider_IsIgnored()
        {
            var kp = CreateKeyPickup(radius: 2f);

            // An enemy ship — has a collider but no PlayerMarker
            var enemy = new GameObject("Enemy");
            enemy.transform.position = Vector3.zero;
            var rb = enemy.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            enemy.AddComponent<SphereCollider>().radius = 0.5f;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(kp.PlayerHasKey, "Enemy should not trigger key collection");

            Object.Destroy(enemy);
            Object.Destroy(kp.gameObject);
        }

        // ── Stubs ─────────────────────────────────────────────────────────────────

        private sealed class StubExtractionZone : IExtractionZone
        {
            public bool IsPlayerInZone => false;
        }
    }
}
