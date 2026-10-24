#if UNITY_EDITOR
using System;
using AI;
using AI.Context;
using AI.Gunnery;
using Combat.Weapons;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Registry;
using Substrate;
using Tests.Common;
using UnityEditor;
using UnityEngine;
using Utils;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
    /// <summary>Pins the Gunner's aim perception (reaction delay, wander, earned accuracy: each off at 0, all replayable from the seed) and the rule that an armed AI pilot cannot spawn without aim settings.</summary>
    [Category("AI")]
    public class AimPerceptionEditModeTests
    {
        // Binary-exact steps, so a delay of whole steps lands on a stored sample.
        private const double Dt = 1.0 / 64.0;
        private static readonly Kinematics NoseUp = new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
        private static readonly ShipId TargetId = new(7);

        private AimSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<AimSettings>();
            settings.reactionDelay = 0f;
            settings.wanderSize = 0f;
            settings.startingError = 0f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        private AimPerception Perception(int seed = 1) => new(settings, new SeedScope(seed));

        private static EnemyTarget Sighted(Vector2 pos, Vector2 vel = default, ShipId? id = null,
            float boostCooldown = 0f) => new()
        {
            kinematics = new Kinematics(pos, vel, 0f, 0f, 0f),
            id = id ?? TargetId,
            boostCooldownRemaining = boostCooldown,
        };

        private static Vector2 Seen(AimPerception perception, in EnemyTarget sighted, int step)
        {
            perception.Perceive(NoseUp, in sighted, step * Dt, out var pos, out _);
            return pos;
        }

        private static float BearingErrorDeg(Vector2 seen, Vector2 truth) => Vector2.SignedAngle(truth, seen);

        [Test]
        public void ReactionDelay_SeesTheTargetWhereItWasTheDelayAgo()
        {
            settings.reactionDelay = 0.125f;
            const int delaySteps = 8;
            Vector2 At(int step) => new(10f * (float)(step * Dt), 10f);

            var perception = Perception();
            for (var step = 0; step < 40; step++)
            {
                var seen = Seen(perception, Sighted(At(step), new Vector2(10f, 0f)), step);
                var expected = At(Mathf.Max(0, step - delaySteps));
                Assert.That(Vector2.Distance(seen, expected), Is.LessThan(1e-5f),
                    $"step {step}: the target as it was {settings.reactionDelay} s ago, or its first sighting before that");
            }
        }

        [TestCase(0f, 0f, 0.5f, TestName = "AllAtZero_SeesTheTrueTarget")]
        [TestCase(0f, 11.25f, 0f, TestName = "ZeroWanderCorrelationTime_TurnsTheWanderOff")]
        public void PerceptionKnobsAtZero_SeeTheTrueTarget(float delay, float wanderSize, float correlationTime)
        {
            settings.reactionDelay = delay;
            settings.wanderSize = wanderSize;
            settings.wanderCorrelationTime = correlationTime;

            var perception = Perception();
            for (var step = 0; step < 60; step++)
            {
                var truth = new Vector2(3f * (float)(step * Dt), 12f);
                Assert.AreEqual(truth, Seen(perception, Sighted(truth, new Vector2(3f, 0f)), step),
                    $"step {step}: nothing distorts the target");
            }
        }

        [Test]
        public void Wander_ReplaysFromTheSeed_AndTurnsAboutTheShooter()
        {
            settings.wanderSize = 11.25f;
            settings.wanderCorrelationTime = 0.5f;
            var truth = new Vector2(0f, 20f);

            float[] Run(AimPerception perception)
            {
                var errors = new float[120];
                for (var step = 0; step < errors.Length; step++)
                {
                    var seen = Seen(perception, Sighted(truth), step);
                    Assert.AreEqual(truth.magnitude, seen.magnitude, 1e-3f, "the wander turns the bearing, not the range");
                    errors[step] = BearingErrorDeg(seen, truth);
                }
                return errors;
            }

            var first = Perception(seed: 1);
            var run = Run(first);
            Assert.That(Mathf.Max(Mathf.Abs(Mathf.Min(run)), Mathf.Max(run)), Is.GreaterThan(1f),
                "a live wander moves the bearing");
            CollectionAssert.AreEqual(run, Run(Perception(seed: 1)), "the same seed replays the same wander");
            CollectionAssert.AreNotEqual(run, Run(Perception(seed: 2)), "another seed wanders elsewhere");

            first.Reset();
            CollectionAssert.AreEqual(run, Run(first), "a reset replays from the spawn seed");
        }

        private void TrackFor(AimPerception perception, ref int step, int steps, Vector2 truth)
        {
            for (var end = step + steps; step < end; step++) Seen(perception, Sighted(truth), step);
        }

        private void ArmEarnedAccuracy()
        {
            settings.startingError = 20f;
            settings.timeToTighten = 1f;
        }

        [Test]
        public void EarnedAccuracy_ErrorTightensToZero_WhileTheTargetIsTracked()
        {
            ArmEarnedAccuracy();
            var truth = new Vector2(2f, 10f);
            var perception = Perception();

            var step = 0;
            var earlyError = 0f;
            for (; step < 8; step++)
                earlyError = Mathf.Max(earlyError, Mathf.Abs(BearingErrorDeg(Seen(perception, Sighted(truth), step), truth)));
            Assert.That(earlyError, Is.GreaterThan(0.5f), "a fresh target is seen loosely");

            TrackFor(perception, ref step, 24, truth);
            Assert.AreEqual(0.5f, perception.Earned.Progress, 1.5f * (float)Dt, "half the time to tighten, half the progress");

            TrackFor(perception, ref step, 40, truth);
            Assert.AreEqual(1f, perception.Earned.Progress);
            Assert.That(Vector2.Distance(Seen(perception, Sighted(truth), step), truth), Is.LessThan(1e-4f),
                "a target tracked for the full time is seen exactly");
        }

        [Test]
        public void EarnedAccuracy_DoesNotGrow_OutsideTheConeOrBeyondTheRange()
        {
            ArmEarnedAccuracy();
            var step = 0;

            var abeam = Perception();
            TrackFor(abeam, ref step, 40, new Vector2(10f, 0f));
            Assert.AreEqual(0f, abeam.Earned.Progress, "90° off the nose is outside the 30° cone");

            var distant = Perception();
            TrackFor(distant, ref step, 40, new Vector2(0f, 40f));
            Assert.AreEqual(0f, distant.Earned.Progress, "40 u is beyond the 25 u range");
        }

        public enum ResetTrigger { NewTarget, Jink, Boost, LostLineOfSight }

        [TestCase(ResetTrigger.NewTarget)]
        [TestCase(ResetTrigger.Jink)]
        [TestCase(ResetTrigger.Boost)]
        [TestCase(ResetTrigger.LostLineOfSight)]
        public void EarnedAccuracy_ResetsOnItsTrigger(ResetTrigger trigger)
        {
            ArmEarnedAccuracy();
            var truth = new Vector2(0f, 10f);
            var perception = Perception();
            var step = 0;
            TrackFor(perception, ref step, 32, truth);
            Assert.That(perception.Earned.Progress, Is.GreaterThan(0.4f), "tracking has earned progress");

            GameObject wall = null;
            try
            {
                var sighted = trigger switch
                {
                    ResetTrigger.NewTarget => Sighted(truth, id: new ShipId(8)),
                    ResetTrigger.Jink => Sighted(truth, new Vector2(3f, 0f)),
                    ResetTrigger.Boost => Sighted(truth, boostCooldown: 3f),
                    _ => Sighted(truth),
                };
                if (trigger == ResetTrigger.LostLineOfSight) wall = AsteroidWallAt(new Vector2(0f, 5f));

                Seen(perception, in sighted, step);
                Assert.That(perception.Earned.Progress, Is.LessThanOrEqualTo((float)Dt / settings.timeToTighten),
                    $"a {trigger} restarts the tracking; at most this step's growth remains");
            }
            finally
            {
                if (wall) Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
            }
        }

        [Test]
        public void EarnedAccuracy_ResetSwitches_LeaveTheBoostAndLineOfSightAlone()
        {
            ArmEarnedAccuracy();
            settings.resetOnBoost = false;
            settings.resetOnLostLineOfSight = false;
            var truth = new Vector2(0f, 10f);
            var perception = Perception();
            var step = 0;
            TrackFor(perception, ref step, 32, truth);
            var earned = perception.Earned.Progress;

            var wall = AsteroidWallAt(new Vector2(0f, 5f));
            try
            {
                Seen(perception, Sighted(truth, boostCooldown: 3f), step);
                Assert.That(perception.Earned.Progress, Is.GreaterThan(earned), "with both switches off, tracking keeps growing");
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
            }
        }

        private static GameObject AsteroidWallAt(Vector2 planePos)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerIds.Asteroid;
            wall.transform.position = GamePlane.PlanePointToWorld(planePos);
            wall.transform.localScale = new Vector3(4f, 4f, 4f);
            Physics.SyncTransforms();
            return wall;
        }

        private sealed class StubPilot : IPilot
        {
            public void Drive(in PilotCommand cmd) { }
        }

        private sealed class NoWeapons : IWeapons
        {
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) { }
        }

        [Test]
        public void ArmedPilot_WithoutAimSettings_FailsAtSpawn()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            Assert.That(ship, Is.Not.Null);
            var host = new GameObject("UnsetAimPilot");
            try
            {
                host.AddComponent<Gunner>();
                var commander = host.AddComponent<TestableCommander>();
                commander.CallAwake();
                commander.SetSensing(new StubShipRegistry(), null);
                var status = new StubShipStatus { transform = host.transform, dynamics = ship.ResolveStats().Dynamics };

                var thrown = Assert.Throws<ArgumentNullException>(() => commander.Initialize(new ShipControl(status,
                    new StubPilot(), new SeedScope(1), new StubWeaponContext(), new NoWeapons())));
                Assert.AreEqual("aimSettings", thrown.ParamName);
            }
            finally
            {
                var created = host.GetComponent<Navigator>().mpcSettings;
                Object.DestroyImmediate(host);
                if (created) Object.DestroyImmediate(created);
            }
        }

        [TestCase("Assets/Prefabs/Pilots/AgentPilot.prefab")]
        [TestCase("Assets/Prefabs/Pilots/ArchetypePilot.prefab")]
        [TestCase("Assets/Prefabs/Pilots/AggressorPilot.prefab")]
        [TestCase("Assets/Prefabs/Pilots/KiterPilot.prefab")]
        [TestCase("Assets/Prefabs/Pilots/OrbiterPilot.prefab")]
        [TestCase("Assets/Prefabs/Pilots/TestPilotMPC.prefab")]
        public void ArmedAIPilotPrefab_AuthorsAimSettings(string prefabPath)
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {prefabPath}");
            Assert.IsNotNull(pilot.GetComponent<Gunner>(), "an armed AI pilot carries a Gunner");
            Assert.IsTrue(pilot.GetComponent<AICommander>().aimSettings != null,
                "an armed AI pilot authors its aim settings");
        }
    }
}
#endif
