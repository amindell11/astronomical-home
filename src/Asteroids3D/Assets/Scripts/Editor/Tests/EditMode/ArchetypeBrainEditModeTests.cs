#if UNITY_EDITOR
using System;
using System.Reflection;
using AI;
using AI.Context;
using AI.Tactics;
using Movement;
using Substrate;
using AI.Navigation.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEditor;
using UnityEngine;
using Ships.Registry;
using AI.Pilots;
using AI.Strategy;
using System.Collections.Generic;
using Combat.Targeting;

namespace Tests.EditMode
{
    /// <summary>Pins the collapsed archetype seam: the Aggressor law through Configure/Decide (the matrix lane and the authored pilot ride the same component), the Dummy's no-target constant, the unknown-enum failure, the ArchetypePilot authoring that makes an archetype flyable in a live sector, the waypoint roam a live-targeting pilot flies once its target is gone, the tactics switch (each shipped pilot flies its kind's temper), and the pure missile-evasion laws.</summary>
    [Category("AI")]
    public class ArchetypeBrainEditModeTests
    {
        private const string ArchetypePilotPath = "Assets/Prefabs/Pilots/ArchetypePilot.prefab";
        private const int JukeSeed = 7;

        private static OpponentDraw Shape() => new()
        {
            speedFraction = 0.8f,
            jukePeriod = 1f,
            orbitRadius = 16f,
            orbitDirection = 1,
            desiredRange = 12f,
        };

        private sealed class StubStatus : IShipStatus
        {
            public Kinematics Kinematics { get; set; }
            public ShipId Id { get; set; }
            public Transform Transform => null;
            public Dynamics Dynamics => Movement.Dynamics.Default;
            public float HealthPct { get; set; } = 1f;
            public float ShieldPct { get; set; } = 1f;
            public bool BoostAvailable { get; set; } = true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
            public IReadOnlyList<ITrackingMissile> TrackingMissiles => System.Array.Empty<ITrackingMissile>();
        }

        private sealed class StubMissile : ITrackingMissile
        {
            public StubMissile(Vector2 pos, Vector2 vel) => Kinematics = new Kinematics(pos, vel, 0f, 0f, 0f);

            public Kinematics Kinematics { get; }
        }

        private GameObject brainHost;
        private GameObject targetHost;
        private readonly List<TacticsSettings> createdTempers = new();

        [SetUp]
        public void SetUp()
        {
            brainHost = new GameObject("ArchetypeHost");
            targetHost = new GameObject("ArchetypeTarget");
        }

        [TearDown]
        public void TearDown()
        {
            if (brainHost) UnityEngine.Object.DestroyImmediate(brainHost);
            if (targetHost) UnityEngine.Object.DestroyImmediate(targetHost);
            foreach (var temper in createdTempers)
                if (temper) UnityEngine.Object.DestroyImmediate(temper);
        }

        private ArchetypeBrain NewBrain(OpponentArchetype archetype, Ship target, bool liveTargeting = false,
            GameObject host = null)
        {
            var brain = (host ? host : brainHost).AddComponent<ArchetypeBrain>();
            if (liveTargeting)
            {
                var temper = ScriptableObject.CreateInstance<TacticsSettings>();
                createdTempers.Add(temper);
                var serialized = new SerializedObject(brain);
                serialized.FindProperty("tactics").objectReferenceValue = temper;
                serialized.FindProperty("liveTargeting").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var shape = Shape();
            brain.Configure(target, archetype, in shape, JukeSeed, Vector2.zero, borderRadius: 500f);
            return brain;
        }

        private ArchetypeBrain BrainWithMargin(Ship target, float attackRunMargin)
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, target);
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.attackRunMargin = attackRunMargin;
            createdTempers.Add(temper);
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("tactics").objectReferenceValue = temper;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return brain;
        }

        private static TacticsSettings TemperOf(ArchetypeBrain brain) =>
            (TacticsSettings)new SerializedObject(brain).FindProperty("tactics").objectReferenceValue;

        private AIContext Ctx(Kinematics self, float? holdRange = null) =>
            new(new StubStatus { Kinematics = self }, brainHost.AddComponent<AI.Scout>(), holdRange);

        [TestCase(18f)]
        [TestCase(null)]
        public void Decide_Aggressor_HoldsTheWeaponsDeclaredRange_ElseItsOwn(float? holdRange)
        {
            var target = targetHost.AddComponent<Ship>();
            var brain = NewBrain(OpponentArchetype.Aggressor, target);
            var self = new Kinematics(new Vector2(3f, -4f), new Vector2(1f, 2f), 0f, 0f, 0f);
            var shape = Shape();

            var decision = brain.Decide(Ctx(self, holdRange));

            var expected = RangerBrain.HoldRangeVelocity(in self, target.Kinematics, holdRange ?? shape.desiredRange,
                shape.speedFraction * Dynamics.Default.maxSpeed);
            Assert.AreNotEqual(18f, shape.desiredRange, "the authored range must differ for the cases to discriminate");
            Assert.IsTrue(decision.HasValue);
            Assert.AreEqual(expected, decision.Value.nav.planarVelocity);
        }

        [Test]
        public void Decide_Dummy_HoldsZeroVelocityWithoutATarget()
        {
            var brain = NewBrain(OpponentArchetype.Dummy, target: null);

            var decision = brain.Decide(Ctx(default));

            Assert.IsTrue(decision.HasValue, "the Dummy needs no target");
            Assert.IsTrue(decision.Value.nav.hasPlanarVelocity);
            Assert.AreEqual(Vector2.zero, decision.Value.nav.planarVelocity);
            Assert.IsFalse(decision.Value.nav.TryGetAnchorId(out _), "the Dummy never packs against a target");
            Assert.IsFalse(decision.Value.engagePrimary);
        }

        // The test context never acquires an enemy, so Decide drops the Configured target.
        [Test]
        public void Decide_LiveTargeting_TargetGone_RoamVelocityIgnoresTheNose()
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>(), liveTargeting: true);
            targetHost.SetActive(false);
            var pos = new Vector2(3f, -4f);

            var noseUp = brain.Decide(Ctx(new Kinematics(pos, Vector2.zero, 0f, 0f, 0f)));
            var noseTurned = brain.Decide(Ctx(new Kinematics(pos, Vector2.zero, 135f, 0f, 0f)));

            Assert.IsTrue(noseUp.HasValue && noseTurned.HasValue, "a pilot whose target is gone keeps flying");
            Assert.AreEqual(noseUp.Value.nav.planarVelocity, noseTurned.Value.nav.planarVelocity,
                "a roam velocity that follows the nose turns with it and spirals");
            Assert.AreEqual(Shape().speedFraction * Dynamics.Default.maxSpeed,
                noseTurned.Value.nav.planarVelocity.magnitude, 1e-4f, "inside the border it roams at patrol speed");
            Assert.IsFalse(noseTurned.Value.nav.TryGetAnchorId(out _));
            Assert.IsFalse(noseTurned.Value.engagePrimary);
            Assert.IsFalse(noseTurned.Value.engageSecondary);
        }

        [TestCase(true, 35f, 60f)]
        [TestCase(false, 0f, 120f)]
        public void Roam_TemperCirclesTheWreckOrWandersFromTheStart(bool circlesWreck, float minDistance,
            float maxDistance)
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>(), liveTargeting: true);
            TemperOf(brain).circlesWreck = circlesWreck;
            targetHost.SetActive(false);

            brain.Decide(Ctx(new Kinematics(new Vector2(3f, -4f), Vector2.zero, 0f, 0f, 0f)));

            Assert.AreEqual(circlesWreck, brain.RoamCircling);
            Assert.That(Vector2.Distance(brain.RoamWaypoint, brain.RoamCenter),
                Is.InRange(minDistance - 1e-3f, maxDistance + 1e-3f));
        }

        [Test]
        public void Roam_LostTarget_CirclesItsLaps_ThenRecallsOnce()
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>(), liveTargeting: true);
            TemperOf(brain).circlesWreck = false;
            var start = new Kinematics(new Vector2(3f, -4f), Vector2.zero, 0f, 0f, 0f);

            var first = brain.Decide(Ctx(start));

            Assert.IsTrue(first.HasValue);
            Assert.IsFalse(first.Value.recall);
            Assert.IsTrue(brain.RoamCircling, "a lost target is circled even when the temper does not circle a wreck");
            Assert.That(Vector2.Distance(brain.RoamWaypoint, brain.RoamCenter),
                Is.InRange(35f - 1e-3f, 60f + 1e-3f));

            BrainDecision? finishing = null;
            var ringWaypoints = 0;
            while (brain.RoamCircling && ringWaypoints < 50)
            {
                ringWaypoints++;
                finishing = brain.Decide(Ctx(new Kinematics(brain.RoamWaypoint, Vector2.zero, 0f, 0f, 0f)));
            }

            const float twoLaps = 720f;
            Assert.That(ringWaypoints, Is.InRange(Mathf.CeilToInt(twoLaps / ArchetypeRoam.StepMaxDegrees),
                Mathf.CeilToInt(twoLaps / ArchetypeRoam.StepMinDegrees)), "two laps at the ring's angular steps");
            Assert.IsTrue(finishing.HasValue && finishing.Value.recall,
                "finishing the laps asks for the lost target back");
            Assert.IsFalse(brain.RoamCircling);

            var again = brain.Decide(Ctx(new Kinematics(brain.RoamWaypoint, Vector2.zero, 0f, 0f, 0f)));
            Assert.IsTrue(again.HasValue);
            Assert.IsFalse(again.Value.recall, "a search recalls once");
        }

        [Test]
        public void Roam_DeadTarget_WithCirclingOff_WandersAndNeverRecalls()
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>(), liveTargeting: true);
            TemperOf(brain).circlesWreck = false;
            targetHost.SetActive(false);
            var center = new Vector2(3f, -4f);

            var decision = brain.Decide(Ctx(new Kinematics(center, Vector2.zero, 0f, 0f, 0f)));
            Assert.IsTrue(decision.HasValue);
            Assert.IsFalse(brain.RoamCircling, "a dead target with circling off wanders from the start");
            Assert.IsFalse(decision.Value.recall);
            Assert.That(Vector2.Distance(brain.RoamWaypoint, brain.RoamCenter), Is.LessThanOrEqualTo(120f + 1e-3f));

            for (var i = 0; i < 5; i++)
            {
                decision = brain.Decide(Ctx(new Kinematics(brain.RoamWaypoint, Vector2.zero, 0f, 0f, 0f)));
                Assert.IsTrue(decision.HasValue);
                Assert.IsFalse(brain.RoamCircling);
                Assert.IsFalse(decision.Value.recall, "a dead target is never recalled");
                Assert.That(Vector2.Distance(brain.RoamWaypoint, brain.RoamCenter),
                    Is.LessThanOrEqualTo(120f + 1e-3f));
            }
        }

        [Test]
        public void Roam_CirclesItsLaps_ThenWanders()
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>(), liveTargeting: true);
            targetHost.SetActive(false);
            brain.Decide(Ctx(new Kinematics(new Vector2(3f, -4f), Vector2.zero, 0f, 0f, 0f)));
            var center = brain.RoamCenter;

            var ringWaypoints = 0;
            while (brain.RoamCircling && ringWaypoints < 50)
            {
                ringWaypoints++;
                brain.Decide(Ctx(new Kinematics(brain.RoamWaypoint, Vector2.zero, 0f, 0f, 0f)));
            }

            const float twoLaps = 720f;
            Assert.That(ringWaypoints, Is.InRange(Mathf.CeilToInt(twoLaps / ArchetypeRoam.StepMaxDegrees),
                Mathf.CeilToInt(twoLaps / ArchetypeRoam.StepMinDegrees)), "two laps at the ring's angular steps");
            for (var i = 0; i < 10; i++)
            {
                Assert.IsFalse(brain.RoamCircling, "once wandering, the pilot never circles again");
                Assert.That(Vector2.Distance(brain.RoamWaypoint, center), Is.LessThanOrEqualTo(120f + 1e-3f),
                    "wander waypoints stay in the disc around the wreck");
                brain.Decide(Ctx(new Kinematics(brain.RoamWaypoint, Vector2.zero, 0f, 0f, 0f)));
            }
        }

        [Test]
        public void Roam_TwoPilots_PickDifferentWaypoints()
        {
            var otherHost = new GameObject("ArchetypeOtherHost");
            try
            {
                var target = targetHost.AddComponent<Ship>();
                var first = NewBrain(OpponentArchetype.Aggressor, target, liveTargeting: true);
                var second = NewBrain(OpponentArchetype.Aggressor, target, liveTargeting: true, otherHost);
                targetHost.SetActive(false);
                var self = new Kinematics(new Vector2(3f, -4f), Vector2.zero, 0f, 0f, 0f);

                first.Decide(Ctx(self));
                second.Decide(Ctx(self));

                Assert.AreNotEqual(first.RoamWaypoint, second.RoamWaypoint,
                    "pilots sharing an authored seed must not bunch onto the same points");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherHost);
            }
        }

        [Test]
        public void Decide_LiveTargeting_TargetConcealed_ReturnsNoDecision()
        {
            var target = targetHost.AddComponent<Ship>();
            var brain = NewBrain(OpponentArchetype.Aggressor, target, liveTargeting: true);
            target.Lock.Conceal();

            Assert.IsNull(brain.Decide(Ctx(default)),
                "the commander hunts a concealed target's last-seen point only on a null decision");
        }

        [Test]
        public void Decide_UnknownArchetype_Throws()
        {
            var brain = NewBrain((OpponentArchetype)99, targetHost.AddComponent<Ship>());

            Assert.Throws<ArgumentOutOfRangeException>(() => brain.Decide(Ctx(default)));
        }

        [Test]
        public void ArchetypePilot_AuthorsALiveTargetingArchetypeBrain()
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(ArchetypePilotPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {ArchetypePilotPath}");

            var brain = pilot.GetComponent<Brain>();
            Assert.IsInstanceOf<ArchetypeBrain>(brain);
            Assert.IsTrue(new SerializedObject(brain).FindProperty("liveTargeting").boolValue,
                "an authored pilot without the live bit idles: nothing pins its target");
        }

        [Test]
        public void Decide_TacticsOff_PicksNoTactic()
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>());

            Assert.IsTrue(brain.Decide(Ctx(default)).HasValue);
            Assert.IsNull(brain.CurrentTactic, "without tactics settings the brain flies its archetype alone");
        }

        [Test]
        public void Decide_TacticsOn_FliesTheAttackRunInsteadOfTheArchetypesLaw()
        {
            var target = targetHost.AddComponent<Ship>();
            var brain = NewBrain(OpponentArchetype.Aggressor, target);
            var tactics = ScriptableObject.CreateInstance<TacticsSettings>();
            try
            {
                var serialized = new SerializedObject(brain);
                serialized.FindProperty("tactics").objectReferenceValue = tactics;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var self = new Kinematics(new Vector2(3f, -4f), new Vector2(1f, 2f), 0f, 0f, 0f);
                var shape = Shape();

                var decision = brain.Decide(Ctx(self));

                Assert.AreEqual(Tactic.AttackRun, brain.CurrentTactic, "a full shield with nothing in the way presses");
                Assert.IsTrue(decision.HasValue);
                Assert.AreEqual(RangerBrain.HoldRangeVelocity(in self, target.Kinematics, shape.desiredRange,
                    shape.speedFraction * Dynamics.Default.maxSpeed), decision.Value.nav.planarVelocity,
                    "the attack run flies the Aggressor's hold-range law");
                Assert.IsTrue(decision.Value.engagePrimary, "the attack run fires");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tactics);
            }
        }

        [Test]
        public void AttackRun_Margin_BacksOffBetweenTheHoldRangeAndTheMargin()
        {
            const float holdRange = 10f;
            const float margin = 8f;
            var target = targetHost.AddComponent<Ship>();
            var atHold = BrainWithMargin(target, 0f);
            var further = BrainWithMargin(target, margin);
            var between = holdRange + margin * 0.5f;
            var self = new Kinematics(new Vector2(between, 0f), Vector2.zero, 0f, 0f, 0f);

            var closing = atHold.Decide(Ctx(self, holdRange));
            var backing = further.Decide(Ctx(self, holdRange));

            Assert.IsTrue(closing.HasValue && backing.HasValue);
            var toward = (target.Kinematics.pos - self.pos).normalized;
            Assert.Greater(Vector2.Dot(closing.Value.nav.planarVelocity, toward), 0f,
                "no margin, outside the hold range, closes");
            Assert.Less(Vector2.Dot(backing.Value.nav.planarVelocity, toward), 0f,
                "between the hold range and the margin, backs off");
            var speed = Shape().speedFraction * Dynamics.Default.maxSpeed;
            Assert.AreEqual(RangerBrain.HoldRangeVelocity(in self, target.Kinematics, holdRange, speed),
                closing.Value.nav.planarVelocity);
            Assert.AreEqual(RangerBrain.HoldRangeVelocity(in self, target.Kinematics, holdRange + margin, speed),
                backing.Value.nav.planarVelocity);
        }

        private (ArchetypeBrain brain, StubStatus status, AIContext ctx) WoundedPilot(TacticsSettings temper)
        {
            temper.minimumHold = 0f;
            temper.woundedBelow = 25f;
            temper.shieldBack = 80f;
            temper.woundedEnds = WoundedEnds.WhenShieldIsBack;
            temper.hitWithin = 2f;
            createdTempers.Add(temper);
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>());
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("tactics").objectReferenceValue = temper;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var status = new StubStatus();
            var ctx = new AIContext(status, brainHost.AddComponent<Scout>(), null);
            return (brain, status, ctx);
        }

        private static void Fly(ArchetypeBrain brain, AIContext ctx)
        {
            for (var i = 0; i < 10; i++) brain.Decide(ctx);
        }

        private static void Hurt(StubStatus status, AIContext ctx)
        {
            status.ShieldPct = 0.1f;
            ctx.Update(0.02f);
        }

        private static void Heal(StubStatus status, AIContext ctx)
        {
            status.ShieldPct = 1f;
            ctx.Update(0.02f);
        }

        [Test]
        public void OneWoundedStretch_CountsOneRetreat()
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.wounded = WoundedResponse.BreakOff;
            temper.commits = true;
            temper.retreats = 2;
            var (brain, status, ctx) = WoundedPilot(temper);

            Hurt(status, ctx);
            for (var i = 0; i < 4; i++) Fly(brain, ctx);
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic, "still the one stretch");

            Heal(status, ctx);
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.AttackRun, brain.CurrentTactic, "the shield is back");

            Hurt(status, ctx);
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic,
                "a second stretch is still allowed, so the first counted once");
        }

        [Test]
        public void RetreatsSpent_Commits_StaysOnItsHomeAtALowShieldJustHit()
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.wounded = WoundedResponse.BreakOff;
            temper.home = Tactic.StandOff;
            temper.commits = true;
            temper.retreats = 1;
            var (brain, status, ctx) = WoundedPilot(temper);

            Hurt(status, ctx);
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic);

            Heal(status, ctx);
            Fly(brain, ctx);
            Hurt(status, ctx);
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.StandOff, brain.CurrentTactic, "the retreat is spent and the temper commits");
        }

        [Test]
        public void ResetState_ClearsTheRetreatCount()
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.wounded = WoundedResponse.BreakOff;
            temper.commits = true;
            temper.retreats = 1;
            var (brain, status, ctx) = WoundedPilot(temper);

            Hurt(status, ctx);
            Fly(brain, ctx);
            Heal(status, ctx);
            Fly(brain, ctx);
            Hurt(status, ctx);
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.AttackRun, brain.CurrentTactic, "spent");

            brain.ResetState();
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic, "a reset spends none of the new life");
        }

        [Test]
        public void Retreat_IsSpentWhenTheFallBackFlies()
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.wounded = WoundedResponse.FallBack;
            temper.woundedSeconds = 0.4f;
            temper.commits = true;
            temper.retreats = 1;
            var (brain, status, ctx) = WoundedPilot(temper);
            temper.minimumHold = 1f;
            temper.woundedEnds = WoundedEnds.AfterSeconds;

            Fly(brain, ctx);
            Assert.AreEqual(Tactic.AttackRun, brain.CurrentTactic);

            Hurt(status, ctx);
            Fly(brain, ctx);
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.AttackRun, brain.CurrentTactic, "the hold still has the attack run");

            const int ticksPerFly = 10;
            var heldTicks = 2 * ticksPerFly;
            var flies = 0;
            while (heldTicks * Time.fixedDeltaTime < temper.minimumHold)
            {
                Fly(brain, ctx);
                heldTicks += ticksPerFly;
                Assert.Less(++flies, 30, "the hold never released the attack run");
            }

            Assert.AreEqual(Tactic.FallBack, brain.CurrentTactic, "the fall back flies once the hold ends");

            var flownTicks = 0;
            flies = 0;
            while (flownTicks * Time.fixedDeltaTime < temper.minimumHold
                || flownTicks * Time.fixedDeltaTime < temper.woundedSeconds)
            {
                Fly(brain, ctx);
                flownTicks += ticksPerFly;
                Assert.Less(++flies, 30, "the fall back never ended");
            }

            Assert.AreEqual(Tactic.AttackRun, brain.CurrentTactic,
                "its own hold and its wounded seconds have run out");

            Heal(status, ctx);
            Hurt(status, ctx);
            Fly(brain, ctx);
            Assert.AreEqual(Tactic.AttackRun, brain.CurrentTactic, "the one retreat is spent");
        }

        [Test]
        public void JumpOut_SetsJumpBeyondJumpsBeyond_AndABreakOffShipNeverDoes()
        {
            Assert.IsFalse(FlyWounded(WoundedResponse.JumpOut, 69f).jump);
            Assert.IsTrue(FlyWounded(WoundedResponse.JumpOut, 71f).jump);
            Assert.IsFalse(FlyWounded(WoundedResponse.BreakOff, 200f).jump);
        }

        private BrainDecision FlyWounded(WoundedResponse response, float distance)
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.wounded = response;
            temper.jumpsBeyond = 70f;
            var (brain, status, ctx) = WoundedPilot(temper);
            status.Kinematics = new Kinematics(new Vector2(distance, 0f), Vector2.zero, 0f, 0f, 0f);
            Hurt(status, ctx);
            var decision = brain.Decide(ctx);
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic);
            Assert.IsTrue(decision.HasValue);
            return decision.Value;
        }

        [TestCase("Assets/Prefabs/Pilots/AggressorPilot.prefab", "Aggressor")]
        [TestCase("Assets/Prefabs/Pilots/OrbiterPilot.prefab", "Orbiter")]
        [TestCase("Assets/Prefabs/Pilots/KiterPilot.prefab", "Kiter")]
        [TestCase("Assets/Prefabs/Pilots/LurkerPilot.prefab", "Lurker")]
        [TestCase("Assets/Prefabs/Pilots/ElitePilot.prefab", "Elite")]
        [TestCase("Assets/Prefabs/Pilots/EscortPilot.prefab", "Escort")]
        public void ShippedPilots_FlyTheirKindsTemper(string path, string kind)
        {
            Assert.AreEqual($"Assets/Settings/AI/Tactics/{kind}Temper.asset",
                AssetDatabase.GetAssetPath(AuthoredTactics(path)));
        }

        private static UnityEngine.Object AuthoredTactics(string path)
        {
            var pilot = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            Assert.IsNotNull(pilot, $"Missing prefab: {path}");
            return new SerializedObject(pilot.GetComponent<ArchetypeBrain>()).FindProperty("tactics")
                .objectReferenceValue;
        }

        [Test]
        public void ArchetypePilot_NavigatorAuthorsScriptedTrackerWeight()
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(ArchetypePilotPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {ArchetypePilotPath}");

            var settings = pilot.GetComponent<Navigator>().mpcSettings;
            Assert.IsNotNull(settings, "ArchetypePilot must author MPC settings");
            Assert.AreEqual(50f, settings.wVelTrack,
                "the scripted velocity laws need the roster's tracker weight — the script default is too loose to hold a reference");
        }

        [TestCase(5f, 10f / 15f, TestName = "TimeToImpact_SlowMissile_ClosesAtTheSpeedFloor")]
        [TestCase(20f, 10f / 20f, TestName = "TimeToImpact_FastMissile_ClosesAtItsSpeed")]
        public void TimeToImpact_FloorsTheClosingSpeed(float missileSpeed, float expected)
        {
            var self = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
            var missile = new Kinematics(new Vector2(0f, -10f), new Vector2(0f, missileSpeed), 0f, 0f, 0f);

            Assert.AreEqual(expected, ArchetypeLaws.TimeToImpact(in self, in missile, speedFloor: 15f), 1e-5f);
        }

        [TestCase(3f, 0f)]
        [TestCase(2f, 0f)]
        [TestCase(1.25f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0.1f, 1f)]
        public void EvasionUrgency_RampsFromTheFarTimeToTheNearTime(float timeToImpact, float expected)
        {
            Assert.AreEqual(expected, ArchetypeLaws.EvasionUrgency(timeToImpact, farTime: 2f, nearTime: 0.5f), 1e-5f);
        }

        [TestCase(0f, 3f, 180f, 1f, TestName = "EscapeSide_Moving_BreaksToTheVelocitySideOverTheNose")]
        [TestCase(0f, 0f, 0f, 1f, TestName = "EscapeSide_AtRest_BreaksToTheNoseSide")]
        [TestCase(0f, 0f, 180f, -1f, TestName = "EscapeSide_AtRestNoseReversed_BreaksToTheOtherSide")]
        public void EscapeHeading_IsSquareToTheApproachOnTheChosenSide(float velX, float velY, float yaw,
            float expectedY)
        {
            var self = new Kinematics(Vector2.zero, new Vector2(velX, velY), yaw, 0f, 0f);
            var missilePos = new Vector2(10f, 0f);

            var side = ArchetypeLaws.EscapeSide(in self, missilePos);
            var heading = ArchetypeLaws.EscapeHeading(self.pos, missilePos, side);

            Assert.AreEqual(0f, heading.x, 1e-5f, "square to the missile's approach");
            Assert.AreEqual(expectedY, heading.y, 1e-5f);
        }

        [Test]
        public void MostUrgentTracker_PicksTheShortestFlooredTimeToImpact()
        {
            var self = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
            var fastFar = new StubMissile(new Vector2(0f, -20f), new Vector2(0f, 40f));
            var slowNear = new StubMissile(new Vector2(6f, 0f), Vector2.zero);
            var cruising = new StubMissile(new Vector2(-30f, 0f), new Vector2(15f, 0f));

            var picked = ArchetypeLaws.MostUrgentTracker(in self, new ITrackingMissile[] { fastFar, slowNear, cruising },
                speedFloor: 15f, out var timeToImpact);

            Assert.AreSame(slowNear, picked, "a parked missile 6 u away closes at the speed floor: 0.4 s beats 0.5 s and 2 s");
            Assert.AreEqual(0.4f, timeToImpact, 1e-5f);
        }

        [TestCase(0f, 4f, 2f)]
        [TestCase(1f, 0f, 20f)]
        [TestCase(0.5f, 2f, 11f)]
        public void EvasionVelocity_BlendsTheLawTowardTheEscapeByUrgency(float urgency, float expectedX, float expectedY)
        {
            var velocity = ArchetypeLaws.EvasionVelocity(new Vector2(4f, 2f), Vector2.up, escapeSpeed: 20f, urgency);

            Assert.AreEqual(expectedX, velocity.x, 1e-5f);
            Assert.AreEqual(expectedY, velocity.y, 1e-5f);
        }

        [Test]
        public void LeaderLost_FliesTheWoundedResponse_AtFullHull()
        {
            FlyLostLeader(WoundedResponse.LastStand, Tactic.Charge, engages: true);
            FlyLostLeader(WoundedResponse.JumpOut, Tactic.BreakOff, engages: false);
            FlyLostLeader(WoundedResponse.Never, Tactic.AttackRun, engages: true);
        }

        [TestCase(10f, false)]
        [TestCase(80f, true)]
        public void LeaderLost_ARegroupingTemperFleesColdAndJumpsOnlyPastItsDistance(float range, bool jumps)
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.wounded = WoundedResponse.Never;
            temper.regroupsWithoutLeader = true;
            temper.jumpsBeyond = 70f;
            var (brain, status, ctx) = WoundedPilot(temper);
            var ships = targetHost.GetComponents<Ship>();
            PinPose(ships[ships.Length - 1], Vector2.zero);
            status.Kinematics = new Kinematics(new Vector2(range, 0f), Vector2.zero, 0f, 0f, 0f);
            var leader = new GameObject("Leader").AddComponent<Ship>();
            leader.transform.SetParent(brainHost.transform);
            ctx.FollowLeader(leader, 1);
            leader.gameObject.SetActive(false);

            var decision = brain.Decide(ctx);

            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic);
            Assert.IsTrue(decision.HasValue);
            Assert.IsFalse(decision.Value.engagePrimary, "a regrouping minion holds its fire");
            Assert.AreEqual(jumps, decision.Value.jump);
        }

        [Test]
        public void LeaderLost_ALeavingMinionThatLosesSight_KeepsFleeingItsLastSeenTarget()
        {
            var target = targetHost.AddComponent<Ship>();
            PinPose(target, Vector2.zero);
            var brain = NewBrain(OpponentArchetype.Aggressor, target, liveTargeting: true);
            var temper = TemperOf(brain);
            temper.wounded = WoundedResponse.Never;
            temper.regroupsWithoutLeader = true;
            temper.jumpsBeyond = 70f;
            var self = new GameObject("Self").AddComponent<Ship>();
            self.transform.SetParent(brainHost.transform);
            var status = new StubStatus { Kinematics = new Kinematics(new Vector2(10f, 0f), Vector2.zero, 0f, 0f, 0f) };
            var ctx = Fighting(status, self, target);
            var leader = new GameObject("Leader").AddComponent<Ship>();
            leader.transform.SetParent(brainHost.transform);
            ctx.FollowLeader(leader, 1);
            leader.gameObject.SetActive(false);
            brain.Decide(ctx);

            typeof(EnemyTracker).GetField("cachedEnemy", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(ctx.Combat, null);
            var near = brain.Decide(ctx);
            status.Kinematics = new Kinematics(new Vector2(80f, 0f), Vector2.zero, 0f, 0f, 0f);
            var far = brain.Decide(ctx);

            Assert.IsTrue(near.HasValue && far.HasValue, "an out-of-sight target does not stop the withdrawal");
            Assert.AreEqual(Tactic.BreakOff, brain.CurrentTactic);
            Assert.Greater(near.Value.nav.planarVelocity.x, 0f, "it flees away from where it last saw the target");
            Assert.IsFalse(near.Value.engagePrimary);
            Assert.IsFalse(near.Value.jump);
            Assert.IsTrue(far.Value.jump, "past its distance from the last sighting it leaves");
        }

        [Test]
        public void Ambush_BoostsWhileTheKitIsReady_AndHoldsFireWhileConcealed()
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.ambushes = true;
            temper.ambushAhead = 15f;
            temper.minimumHold = 0f;
            createdTempers.Add(temper);
            var target = targetHost.AddComponent<Ship>();
            PinPose(target, new Vector2(20f, 0f));

            var ready = DecideFight(temper, target, concealed: false, kitReady: true);
            Assert.AreEqual(Tactic.Ambush, ready.brain.CurrentTactic);
            Assert.IsTrue(ready.decision.boost, "a ready kit is fired to start the ambush");
            Assert.IsFalse(ready.decision.engagePrimary, "guns stay cold while the ship is not yet shown");
            Assert.IsFalse(ready.decision.engageSecondary);
            var them = target.Kinematics;
            var ahead = them.pos + them.Forward * temper.ambushAhead;
            var speed = Shape().speedFraction * Dynamics.Default.maxSpeed;
            Assert.AreEqual(ArchetypeRoam.SeekVelocity(Vector2.zero, ahead, speed), ready.decision.nav.planarVelocity);

            var hidden = DecideFight(temper, target, concealed: true, kitReady: false);
            Assert.AreEqual(Tactic.Ambush, hidden.brain.CurrentTactic);
            Assert.IsFalse(hidden.decision.boost);
            Assert.IsFalse(hidden.decision.engagePrimary, "a concealed ship holds its fire");
            Assert.IsFalse(hidden.decision.engageSecondary);
        }

        [Test]
        public void HoldLine_SteersTowardThePlace_OnTheMinusPerpendicularSide()
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.holdsLine = true;
            temper.lineSpacing = 8f;
            temper.minimumHold = 0f;
            createdTempers.Add(temper);
            var target = targetHost.AddComponent<Ship>();
            var leader = new GameObject("Leader").AddComponent<Ship>();
            leader.transform.SetParent(brainHost.transform);
            PinPose(target, new Vector2(20f, 0f));
            PinPose(leader, Vector2.zero);

            var flown = DecideFight(temper, target, concealed: false, kitReady: false, leader: leader, place: 1);
            Assert.AreEqual(Tactic.HoldLine, flown.brain.CurrentTactic);

            var them = target.Kinematics;
            var leaderKin = leader.Kinematics;
            var toTarget = them.pos - leaderKin.pos;
            Assert.Greater(toTarget.magnitude, 1f, "the target sits off the leader");
            var point = leaderKin.pos - Vector2.Perpendicular(toTarget.normalized) * temper.lineSpacing;
            Assert.Less(point.y, leaderKin.pos.y, "place +1 lies on -Perpendicular(to the target)");
            var speed = Shape().speedFraction * Dynamics.Default.maxSpeed;
            var expected = Vector2.ClampMagnitude(leaderKin.vel + (point - Vector2.zero), speed);
            Assert.That(Vector2.Distance(flown.decision.nav.planarVelocity, expected), Is.LessThan(1e-3f));
        }

        private void FlyLostLeader(WoundedResponse response, Tactic tactic, bool engages)
        {
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.wounded = response;
            temper.minimumHold = 0f;
            var (brain, status, ctx) = WoundedPilot(temper);
            var ships = targetHost.GetComponents<Ship>();
            PinPose(ships[ships.Length - 1], Vector2.zero);
            var leader = new GameObject("Leader").AddComponent<Ship>();
            leader.transform.SetParent(brainHost.transform);
            ctx.FollowLeader(leader, 1);
            leader.gameObject.SetActive(false);

            Assert.AreEqual(1f, status.HealthPct, "the hull is full");
            var decision = brain.Decide(ctx);

            Assert.AreEqual(tactic, brain.CurrentTactic);
            Assert.IsTrue(decision.HasValue);
            Assert.AreEqual(engages, decision.Value.engagePrimary);
        }

        private (ArchetypeBrain brain, BrainDecision decision) DecideFight(TacticsSettings temper, Ship target,
            bool concealed, bool kitReady, Ship leader = null, int place = 0)
        {
            var self = new GameObject("Self").AddComponent<Ship>();
            self.transform.SetParent(brainHost.transform);
            if (concealed) self.Lock.Conceal();
            var status = new StubStatus
            {
                Kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f),
                BoostAvailable = kitReady,
            };
            var ctx = Fighting(status, self, target);
            if (leader) ctx.FollowLeader(leader, place);
            var brain = NewBrain(OpponentArchetype.Aggressor, target);
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("tactics").objectReferenceValue = temper;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var decision = brain.Decide(ctx);
            Assert.IsTrue(decision.HasValue);
            return (brain, decision.Value);
        }

        private AIContext Fighting(StubStatus status, Ship self, Ship enemy)
        {
            var registry = new ShipRegistry();
            // EditMode never wakes the ship, so set the colliders and id the registry keys on.
            typeof(Ship).GetProperty(nameof(Ship.Colliders)).SetValue(self, self.GetComponentsInChildren<Collider>());
            registry.ActiveShips.Add(self);
            var id = new ShipId(self.GetInstanceID());
            status.Id = id;
            var scout = brainHost.AddComponent<Scout>();
            scout.Initialize(self.transform, id, Dynamics.Default, registry, null, null);
            var ctx = new AIContext(status, scout, null);
            var seen = typeof(EnemyTracker).GetField("cachedEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(seen);
            seen.SetValue(ctx.Combat, enemy);
            return ctx;
        }

        private static void PinPose(Ship ship, Vector2 pos)
        {
            ship.transform.SetPositionAndRotation(GamePlane.PlanePointToWorld(pos), GamePlane.Rotation);
            // EditMode never wakes the ship, so its movement link and pose are set by hand.
            var movement = ship.GetComponent<global::Ships.Movement.MovementController>();
            typeof(Ship).GetProperty(nameof(Ship.Movement)).SetValue(ship, movement);
            var pose = new Kinematics(pos, Vector2.zero, 0f, 0f, 0f);
            movement.Initialize(null, () => pose, null, null);
        }
    }
}
#endif
