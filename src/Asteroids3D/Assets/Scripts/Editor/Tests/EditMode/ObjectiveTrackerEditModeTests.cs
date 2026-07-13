using System;
using System.Collections.Generic;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>ObjectiveTracker state machine; the tracker advances at most one step per Tick so UI/audio hooks see every intermediate state.</summary>
    [Category("Objectives")]
    public class ObjectiveTrackerEditModeTests
    {

        private (ObjectiveTracker tracker, StubKeyTracker key, BoolRef alive, StubExtractionZone zone)
            BuildExploreTracker(bool playerInZone = false)
        {
            var key = new StubKeyTracker(false);
            var alive = new BoolRef(true);
            var zone = new StubExtractionZone(playerInZone);

            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["explore"] = () => new ExploreState(key),
                ["key"] = () => new KeyAcquiredState(),
                ["extraction"] = () => new ExtractionChallengeState(zone),
                ["extracted"] = () => new ExtractedState(),
                ["failed"] = () => new FailedState()
            };

            var tracker = new ObjectiveTracker(
                MissionDefinition.CreateDefault(
                    failCriteria: () => !alive.Value),
                builders);

            return (tracker, key, alive, zone);
        }

        [Test]
        public void InitialState_IsExplore()
        {
            var (tracker, _, _, _) = BuildExploreTracker();
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);
        }

        [Test]
        public void Explore_TransitionsToKeyAcquired_WhenKeyPickedUp()
        {
            var (tracker, key, _, _) = BuildExploreTracker();

            tracker.Tick(0.1f); // key=false → stays Explore
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);

            key.HasKey = true;
            tracker.Tick(0.1f); // ExploreState.IsComplete=true → KeyAcquired
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState);
        }

        [Test]
        public void KeyAcquired_TransitionsToExtractionChallenge_OnNextTick()
        {
            var (tracker, key, _, _) = BuildExploreTracker();

            key.HasKey = true;
            tracker.Tick(0.1f); // Explore → KeyAcquired
            Assert.AreEqual(ObjectiveType.KeyAcquired, tracker.CurrentState);

            tracker.Tick(0.1f); // KeyAcquired.IsComplete=true → ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);
        }

        [Test]
        public void ExtractionChallenge_TransitionsToExtracted_WhenPlayerEntersZone()
        {
            var (tracker, key, _, zone) = BuildExploreTracker();

            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            zone.InZone = true;
            tracker.Tick(0.1f); // → Extracted
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);
        }

        [Test]
        public void AnyState_TransitionsToFailed_WhenPlayerDies_DuringExplore()
        {
            var (tracker, _, alive, _) = BuildExploreTracker();

            alive.Value = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        [Test]
        public void AnyState_TransitionsToFailed_WhenPlayerDies_DuringExtraction()
        {
            var (tracker, key, alive, _) = BuildExploreTracker();

            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            alive.Value = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        [Test]
        public void Extracted_IsTerminal_IgnoresSubsequentTicks()
        {
            var (tracker, key, _, zone) = BuildExploreTracker(playerInZone: true);

            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge (zone.InZone=true)
            tracker.Tick(0.1f); // → Extracted
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);

            tracker.Tick(9999f); // no-op
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);
        }

        [Test]
        public void Failed_IsTerminal_IgnoresSubsequentTicks()
        {
            var (tracker, _, alive, _) = BuildExploreTracker();

            alive.Value = false;
            tracker.Tick(0.1f); // → Failed
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);

            tracker.Tick(9999f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        [Test]
        public void Fail_TransitionsToFailed_FromAnyNonTerminalState()
        {
            var (tracker, _, _, _) = BuildExploreTracker();
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);

            tracker.Fail();
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        [Test]
        public void Fail_IsNoOp_WhenAlreadyFailed()
        {
            var (tracker, _, _, _) = BuildExploreTracker();
            tracker.Fail();
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);

            tracker.Fail();
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);
        }

        [Test]
        public void Fail_IsNoOp_WhenExtracted()
        {
            var (tracker, key, _, zone) = BuildExploreTracker(playerInZone: true);

            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge
            tracker.Tick(0.1f); // → Extracted
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);

            tracker.Fail(); // no-op — already terminal
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);
        }

        [Test]
        public void Restart_FromExtracted_ResetsToExplore_WithNoConsequences()
        {
            var (tracker, key, _, zone) = BuildExploreTracker(playerInZone: true);

            key.HasKey = true;
            tracker.Tick(0.1f);
            tracker.Tick(0.1f);
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);

            key.HasKey = false;
            zone.InZone = false;
            tracker.Restart();
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);
        }

        [Test]
        public void Restart_FromFailed_ResetsToExplore_WithNoConsequences()
        {
            var (tracker, _, alive, _) = BuildExploreTracker();

            alive.Value = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Failed, tracker.CurrentState);

            alive.Value = true;
            tracker.Restart();
            Assert.AreEqual(ObjectiveType.Explore, tracker.CurrentState);
        }

        [Test]
        public void OnStateChanged_FiresOncePerTransition_FullSuccessPath()
        {
            var (tracker, key, _, zone) = BuildExploreTracker(playerInZone: true);

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
            var (tracker, _, alive, _) = BuildExploreTracker();

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
        public void ExtractionChallenge_DoesNotComplete_WhileBlocked()
        {
            var (tracker, key, _, zone) = BuildExploreTracker();

            key.HasKey = true;
            tracker.Tick(0.1f); // → KeyAcquired
            tracker.Tick(0.1f); // → ExtractionChallenge
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState);

            zone.InZone = false;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, tracker.CurrentState,
                "Should remain in extraction while zone reports player not in zone.");

            zone.InZone = true;
            tracker.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Extracted, tracker.CurrentState);
        }

        [Test]
        public void CurrentStep_ReflectsStringStepId()
        {
            var (tracker, key, _, _) = BuildExploreTracker();
            Assert.AreEqual("explore", tracker.CurrentStep);

            key.HasKey = true;
            tracker.Tick(0.1f);
            Assert.AreEqual("key", tracker.CurrentStep);

            tracker.Tick(0.1f);
            Assert.AreEqual("extraction", tracker.CurrentStep);
        }

        [Test]
        public void OnStepChanged_FiresOncePerTransition_WithStepIds()
        {
            var (tracker, key, _, _) = BuildExploreTracker(playerInZone: true);

            var steps = new List<string>();
            tracker.OnStepChanged += steps.Add;

            key.HasKey = true;
            tracker.Tick(0.1f);
            tracker.Tick(0.1f);
            tracker.Tick(0.1f);

            CollectionAssert.AreEqual(new[] { "key", "extraction", "extracted" }, steps);
        }

        [Test]
        public void OnStepChanged_FiresForSameTypeStepTransition_WhereOnStateChangedStaysSilent()
        {
            var mission = new MissionDefinition("a", new Dictionary<string, string> { { "a", "b" } });
            var builders = new Dictionary<string, Func<ObjectiveState>>
            {
                ["a"] = () => new AlwaysCompleteExploreState(),
                ["b"] = () => new AlwaysCompleteExploreState()
            };
            var tracker = new ObjectiveTracker(mission, builders);

            var steps = new List<string>();
            var typeTransitions = 0;
            tracker.OnStepChanged += steps.Add;
            tracker.OnStateChanged += (_, _) => typeTransitions++;

            tracker.Tick(0.1f);

            CollectionAssert.AreEqual(new[] { "b" }, steps,
                "Same-type step transitions must raise the step event.");
            Assert.AreEqual(0, typeTransitions,
                "Sanity: the type-level event suppresses same-type transitions.");
        }

        private sealed class AlwaysCompleteExploreState : ObjectiveState
        {
            public override ObjectiveType StateType => ObjectiveType.Explore;
            public override void Tick(float deltaTime) { }
            public override bool IsComplete => true;
        }

        [Test]
        public void KeyPickup_SpawnKey_ResetsCollectedFlag()
        {
            var go = new GameObject("Key");
            go.AddComponent<SphereCollider>();
            try
            {
                var kp = go.AddComponent<KeyPickup>();
                kp.SpawnKey(Vector3.zero);
                Assert.IsFalse(kp.PlayerHasKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void KeyPickup_SpawnKey_RepositionsAndReactivates()
        {
            var go = new GameObject("Key");
            go.AddComponent<SphereCollider>();
            try
            {
                var kp = go.AddComponent<KeyPickup>();
                kp.SpawnKey(Vector3.zero);
                go.SetActive(false);

                kp.SpawnKey(new Vector3(10f, 0f, 0f));
                Assert.IsTrue(go.activeSelf, "SpawnKey should reactivate the key");
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

        [Test]
        public void ExtractionZone_Implements_IExtractionZone()
        {
            Assert.IsTrue(typeof(IExtractionZone).IsAssignableFrom(typeof(ExtractionZone)));
        }

        private sealed class StubKeyTracker : IKeyTracker
        {
            public bool HasKey;
            public StubKeyTracker(bool hasKey) => HasKey = hasKey;
            public bool PlayerHasKey => HasKey;
        }

        private sealed class StubExtractionZone : IExtractionZone
        {
            public bool InZone;
            public StubExtractionZone(bool inZone) => InZone = inZone;
            public bool IsPlayerInZone => InZone;
        }

        /// <summary>Mutable reference wrapper for bool, used in Func closures.</summary>
        private sealed class BoolRef
        {
            public bool Value;
            public BoolRef(bool value) => Value = value;
        }
    }
}
