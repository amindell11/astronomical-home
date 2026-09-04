#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using AI.Context;
using Combat;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the two lanes the commander owns outright once boost leaves the solver: the ability lane reaches the actuator as the decision's boost and nothing else, and the anchor is re-resolved from the registry every tick — so a decision held across its 5 Hz interval never steers at the enemy's decision-time pose.</summary>
    [Category("AI")]
    public class AICommanderAbilityLaneEditModeTests
    {
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_1.prefab";
        private static readonly ShipId AnchorId = new(4242);

        private sealed class TestableCommander : AICommander
        {
            public void CallAwake() => Awake();
            public void Step() => FixedUpdate();
        }

        private sealed class StubStatus : IShipStatus
        {
            public Transform transform;
            public Dynamics dynamics;
            public ShipId Id => default;
            public Transform Transform => transform;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => dynamics;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => dynamics.maxSpeed;
            public float MaxYawRate => dynamics.maxYawRate;
        }

        private sealed class SpyPilot : IPilot
        {
            public readonly List<PilotCommand> Commands = new();
            public void Drive(in PilotCommand cmd) => Commands.Add(cmd);
            public PilotCommand Last => Commands[^1];
        }

        private sealed class StubWeaponContext : IWeaponContext
        {
            private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => true;
            public float ProjectileSpeed(WeaponSlot slot) => 40f;
            public Gunsight Sight(WeaponSlot slot) => null;
        }

        private sealed class NoWeapons : IWeapons
        {
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) { }
        }

        private sealed class ScriptedBrain : Brain
        {
            public BrainDecision? decision;
            public override BrainDecision? Decide(AIContext ctx) => decision;
        }

        private GameObject host;
        private GameObject anchorHost;
        private TestableCommander commander;
        private ScriptedBrain brain;
        private SpyPilot pilot;
        private StubShipRegistry registry;
        private MpcSettings createdSettings;

        [SetUp]
        public void SetUp()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(ship, Is.Not.Null, $"Missing ship prefab at {ShipPrefabPath}");

            host = new GameObject("AbilityLaneCommander");
            commander = host.AddComponent<TestableCommander>();
            commander.CallAwake(); // EditMode: Unity does not run Awake, so cache the composed parts explicitly.
            brain = commander.InstallBrain<ScriptedBrain>();

            // A bare Ship is enough to be resolvable — the anchor's kinematics are the Navigator's concern, not the commander's.
            anchorHost = new GameObject("Anchor");
            registry = new StubShipRegistry();
            registry.Ships[AnchorId] = anchorHost.AddComponent<Ship>();

            pilot = new SpyPilot();
            var status = new StubStatus { transform = host.transform, dynamics = ship.ResolveStats().Dynamics };
            commander.SetWorld(TestWorld.On(registry));
            commander.Initialize(new ShipControl(status, pilot, new SeedScope(1),
                new StubWeaponContext(), new NoWeapons()));
            createdSettings = commander.Navigator.mpcSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            if (anchorHost) Object.DestroyImmediate(anchorHost);
            if (createdSettings) Object.DestroyImmediate(createdSettings);
        }

        private static BrainDecision Drifting(bool boost) =>
            new(NavObjective.Planar(Vector2.zero), boost: boost);

        private static BrainDecision Anchored() =>
            new(NavObjective.Anchored(AnchorId).Velocity(1f, 0f, 1f).Facing(0f, 1f));

        private float StepBoost(BrainDecision? decision)
        {
            brain.decision = decision;
            commander.Step();
            return pilot.Last.boost;
        }

        [Test]
        public void AbilityLane_BoostReachesTheActuator()
        {
            Assert.AreEqual(1f, StepBoost(Drifting(boost: true)));
        }

        [Test]
        public void AbilityLane_NoBoostDrivesZero_TheSolverContributesNothing()
        {
            Assert.AreEqual(0f, StepBoost(Drifting(boost: false)),
                "boost left the solver: the only source is the decision");
        }

        [Test]
        public void AbilityLane_DoesNotLatchAcrossDecisions()
        {
            StepBoost(Drifting(boost: true));
            Assert.AreEqual(0f, StepBoost(Drifting(boost: false)),
                "a later decision without boost must drop the prior one");
        }

        [Test]
        public void AbilityLane_NoDecision_ClearsBoost()
        {
            StepBoost(Drifting(boost: true));
            Assert.AreEqual(0f, StepBoost(null));
        }

        [Test]
        public void AbilityLane_ResetState_ClearsBoost()
        {
            StepBoost(Drifting(boost: true));
            commander.ResetState();
            brain.decision = null;
            commander.Step();
            Assert.AreEqual(0f, pilot.Last.boost);
        }

        [Test]
        public void Anchor_ResolvesEveryTick_EvenWhileTheDecisionIsHeld()
        {
            // One decision object, re-routed each tick — exactly what a 5 Hz brain's cache hands the commander.
            brain.decision = Anchored();

            var before = registry.ShipLookups;
            commander.Step();
            commander.Step();
            commander.Step();

            Assert.AreEqual(before + 3, registry.ShipLookups,
                "a held decision must re-resolve its anchor per tick — a snapshot would resolve once");
        }

        [Test]
        public void Anchor_AnchorlessObjective_NeverTouchesTheRegistry()
        {
            brain.decision = Drifting(boost: false);

            var before = registry.ShipLookups;
            commander.Step();

            Assert.AreEqual(before, registry.ShipLookups,
                "a world-frame objective names no ship, so there is nothing to resolve");
        }

        [Test]
        public void Anchor_UnresolvableShip_TakesTheNoDecisionPath()
        {
            brain.decision = Anchored();
            commander.Step();

            registry.Ships.Remove(AnchorId); // the enemy left between the decision and this tick
            commander.Step();

            Assert.That(commander.Navigator.ShouldIdle(), Is.True,
                "an anchor the registry cannot produce must idle the navigator, not steer at a stale pose");
            Assert.AreEqual(0f, pilot.Last.boost);
        }
    }
}
#endif
