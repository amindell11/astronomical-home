using System;
using System.Collections.Generic;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode unit tests for the ObjectiveTracker state machine.
    /// All stubs are inline — no MonoBehaviours required, no scene loading.
    /// Uses string-keyed builder dictionaries instead of ObjectiveStateFactory.
    ///
    /// One transition per Tick — ObjectiveTracker intentionally advances by at most one
    /// state per call so UI/audio hooks see every intermediate state for at least one frame.
    /// </summary>
    [Category("Objectives")]
    public class ObjectiveTrackerEditModeTests
    {
        private readonly List<ObjectiveParams> createdParams = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var paramsAsset in createdParams)
            {
                if (paramsAsset != null)
                    UnityEngine.Object.DestroyImmediate(paramsAsset);
            }

            createdParams.Clear();
        }

        // ── Factory helpers ───────────────────────────────────────────────────────

        private (ObjectiveTracker tracker, StubKeyTracker key, BoolRef alive, Vector3Ref playerPos, BoolRef blocker)
            BuildExploreTracker(Vector3? zonePos = null)
        {
            var key = new StubKeyTracker(false);
            var alive = new BoolRef(true);
            var playerPos = new Vector3Ref(Vector3.zero);
            var blocker = new BoolRef(false);
            var zone = zonePos ?? new Vector3(100f, 0f, 0f);
            var paramsAsset = ScriptableObject.CreateInstance<ObjectiveParams>();
            createdParams.Add(paramsAsset);

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(key),
                ["key"] = () => new KeyAcquiredState(),
                ["extraction"] = () => new ExtractionChallengeState(
                    () => playerPos.Value,
                    () => zone,
                    () => blocker.Value,
                    paramsAsset.ExtractionRadius),
                ["extracted"] = () => new ExtractedState(),
                ["failed"] = () => new FailedState()
            };

            var tracker = new ObjectiveTracker(
                MissionDefinition.CreateDefault(
                    failCriteria: () => !alive.Value),
                builders);

            return (tracker, key, alive, playerPos, blocker);
        }

        // ── Initial state ─────────────────────────────────────────────────────────

        [Test]
        public void InitialState_IsExplore()
        {
            var (tracker, _, _, _, _) = BuildExploreTracker();
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);
        }

        // ── Explore → KeyAcquired (one Tick after key picked up) ─────────────────

        [Test]
        public void Explore_TransitionsToKeyAcquired_WhenKeyPickedUp()
        {
            var (tracker, key, _, _, _) = BuildExploreTracker();

            tracker.Tick(0.1f); // key=false → stays Explore
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);

            key.HasKey = true;
            tracker.Tick(0.1f); // ExploreState.IsComplete=true → KeyAcquired
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState);
        }

        // ── KeyAcquired → ExtractionChallenge (next Tick) ────────────────────────

        [Test]
        public void KeyAcquired_TransitionsToExtractionChallenge_OnNextTick()
        {
            var (tracker, key, _, _, _) = BuildExploreTracker();

            key.HasKey = true;
            tracker.Tick(0.1f); // Explore → KeyAcquired
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState);

            tracker.Tick(0.1f); // KeyAcquired.IsComplete=true → ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);
        }

        // ── ExtractionChallenge → Extracted ───────────────────────────────────────

        [Test]
        public void ExtractionChallenge_TransitionsToExtracted_WhenPlayerEntersZone()
        {
            var zoneCenter = new Vector3(50f, 0f, 0f);
            var (tracker, key, _, playerPos, _) = BuildExploreTracker(zonePos: zoneCenter);

            // Advance to ExtractionChallenge (2 Ticks with key in hand)
            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            // Move player into zone (within default radius 10)
            playerPos.Value = zoneCenter;
            tracker.Tick(0.1f); // → Extracted
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);
        }

        // ── Failure: player destroyed ─────────────────────────────────────────────

        [Test]
        public void AnyState_TransitionsToFailed_WhenPlayerDies_DuringExplore()
        {
            var (tracker, _, alive, _, _) = BuildExploreTracker();

            alive.Value = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        [Test]
        public void AnyState_TransitionsToFailed_WhenPlayerDies_DuringExtraction()
        {
            var (tracker, key, alive, _, _) = BuildExploreTracker();

            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            alive.Value = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        // ── Terminal state guards ─────────────────────────────────────────────────

        [Test]
        public void Extracted_IsTerminal_IgnoresSubsequentTicks()
        {
            var zoneCenter = Vector3.zero;
            var (tracker, key, _, _, _) = BuildExploreTracker(zonePos: zoneCenter);

            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge (player already at zone=origin)
            tracker.Tick(0.1f); // → Extracted
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);

            tracker.Tick(9999f); // no-op
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);
        }

        [Test]
        public void Failed_IsTerminal_IgnoresSubsequentTicks()
        {
            var (tracker, _, alive, _, _) = BuildExploreTracker();

            alive.Value = false;
            tracker.Tick(0.1f); // → Failed
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);

            tracker.Tick(9999f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        // ── Restart ───────────────────────────────────────────────────────────────

        [Test]
        public void Restart_FromExtracted_ResetsToExplore_WithNoConsequences()
        {
            var zoneCenter = Vector3.zero;
            var (tracker, key, _, _, _) = BuildExploreTracker(zonePos: zoneCenter);

            key.HasKey = true;
            tracker.Tick(0.1f);
            tracker.Tick(0.1f);
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);

            key.HasKey = false; // reset game state before restart
            tracker.Restart();
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);
        }

        [Test]
        public void Restart_FromFailed_ResetsToExplore_WithNoConsequences()
        {
            var (tracker, _, alive, _, _) = BuildExploreTracker();

            alive.Value = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);

            alive.Value = true;
            tracker.Restart();
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);
        }

        // ── OnStateChanged event ──────────────────────────────────────────────────

        [Test]
        public void OnStateChanged_FiresOncePerTransition_FullSuccessPath()
        {
            var zoneCenter = Vector3.zero;
            var (tracker, key, _, _, _) = BuildExploreTracker(zonePos: zoneCenter);

            var transitions = new List<(ObjectiveType from, ObjectiveType to)>();
            tracker.OnStateChanged += (f, t) => transitions.Add((f, t));

            key.HasKey = true;
            tracker.Tick(0.1f); // Explore → KeyAcquired
            tracker.Tick(0.1f); // KeyAcquired → ExtractionChallenge
            tracker.Tick(0.1f); // ExtractionChallenge → Extracted

            Assert.AreEqual(3, transitions.Count, "Expected exactly 3 transitions on the success path.");
            Assert.AreEqual((ObjectiveType.Explore,              ObjectiveType.KeyAcquired),         transitions[0]);
            Assert.AreEqual((ObjectiveType.KeyAcquired,          ObjectiveType.ExtractionChallenge),  transitions[1]);
            Assert.AreEqual((ObjectiveType.ExtractionChallenge,  ObjectiveType.Extracted),            transitions[2]);
        }

        [Test]
        public void OnStateChanged_FiresOnRestart()
        {
            var (tracker, _, alive, _, _) = BuildExploreTracker();

            var transitions = new List<(ObjectiveType from, ObjectiveType to)>();
            tracker.OnStateChanged += (f, t) => transitions.Add((f, t));

            alive.Value = false;
            tracker.Tick(0.1f); // → Failed
            alive.Value = true;
            tracker.Restart();  // Failed → Explore

            Assert.AreEqual(2, transitions.Count);
            Assert.AreEqual((ObjectiveType.Explore, ObjectiveType.Failed), transitions[0]);
            Assert.AreEqual((ObjectiveType.Failed,  ObjectiveType.Explore), transitions[1]);
        }

        // ── MissionDefinition ─────────────────────────────────────────────────────

        [Test]
        public void MissionDefinition_CreateDefault_HasExpectedTransitions()
        {
            var mission = MissionDefinition.CreateDefault();
            Assert.AreEqual("explore", mission.InitialStep);

            Assert.IsTrue(mission.TryGetNext("explore",    out var n1)); Assert.AreEqual("key",        n1);
            Assert.IsTrue(mission.TryGetNext("key",         out var n2)); Assert.AreEqual("extraction", n2);
            Assert.IsTrue(mission.TryGetNext("extraction",  out var n3)); Assert.AreEqual("extracted",  n3);

            Assert.IsFalse(mission.TryGetNext("extracted", out _), "Extracted is terminal — no transition.");
            Assert.IsFalse(mission.TryGetNext("failed",    out _), "Failed is terminal — no transition.");
        }

        [Test]
        public void ExtractionChallenge_DoesNotComplete_WhileBlockedByEnemyProximity()
        {
            var zoneCenter = Vector3.zero;
            var (tracker, key, _, playerPos, blocker) = BuildExploreTracker(zonePos: zoneCenter);

            key.HasKey = true;
            tracker.Tick(0.1f); // -> KeyAcquired
            tracker.Tick(0.1f); // -> ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            playerPos.Value = zoneCenter;
            blocker.Value = true;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState, "Should remain in extraction while enemy is close enough to follow/block.");

            blocker.Value = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);
        }

        // ── CurrentStep ───────────────────────────────────────────────────────────

        [Test]
        public void CurrentStep_ReflectsStringStepId()
        {
            var (tracker, key, _, _, _) = BuildExploreTracker();
            Assert.AreEqual("explore", tracker.CurrentStep);

            key.HasKey = true;
            tracker.Tick(0.1f);
            Assert.AreEqual("key", tracker.CurrentStep);

            tracker.Tick(0.1f);
            Assert.AreEqual("extraction", tracker.CurrentStep);
        }

        // ── KeyPickup ─────────────────────────────────────────────────────────────

        [Test]
        public void KeyPickup_SpawnKey_ResetsCollectedFlag()
        {
            var go = new GameObject("Key");
            try
            {
                var kp = go.AddComponent<KeyPickup>();
                kp.SpawnKey(Vector3.zero, 10f);
                Assert.IsFalse(kp.PlayerHasKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void KeyPickup_ResetKey_RepositionsAndReactivates()
        {
            var go = new GameObject("Key");
            try
            {
                var kp = go.AddComponent<KeyPickup>();
                kp.SpawnKey(Vector3.zero, 0f);
                go.SetActive(false);

                kp.ResetKey(new Vector3(10f, 0f, 0f), 0f);
                Assert.IsTrue(go.activeSelf, "ResetKey should reactivate the key");
                Assert.AreEqual(new Vector3(10f, 0f, 0f), kp.KeyPosition);
                Assert.IsFalse(kp.PlayerHasKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void KeyPickup_Implements_IKeyTracker()
        {
            Assert.IsTrue(typeof(IKeyTracker).IsAssignableFrom(typeof(KeyPickup)));
        }

        // ── Stubs ─────────────────────────────────────────────────────────────────

        private sealed class StubKeyTracker : IKeyTracker
        {
            public bool HasKey;
            public StubKeyTracker(bool hasKey) => HasKey = hasKey;
            public bool PlayerHasKey => HasKey;
        }

        /// <summary>Mutable reference wrapper for bool, used in Func closures.</summary>
        private sealed class BoolRef
        {
            public bool Value;
            public BoolRef(bool value) => Value = value;
        }

        /// <summary>Mutable reference wrapper for Vector3, used in Func closures.</summary>
        private sealed class Vector3Ref
        {
            public Vector3 Value;
            public Vector3Ref(Vector3 value) => Value = value;
        }
    }
}
