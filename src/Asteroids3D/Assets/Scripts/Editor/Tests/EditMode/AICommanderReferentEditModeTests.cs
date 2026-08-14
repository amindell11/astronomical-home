#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using AI.Context;
using Asteroids;
using Combat;
using Game;
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
    /// <summary>Pins the commander's rock-referent wiring (Intent_Grammar §Stage C brief, fork 4): rock seats re-resolve into fresh snapshots every tick, a despawned rock rides in invalid (its slots weight 0) while the gunner holds fire, and the AIM-referent swap — the shoot-the-rock hand sentence — aims the gunner at the rock with the anchor path untouched when AIM stays on referent 0.</summary>
    [Category("AI")]
    public class AICommanderReferentEditModeTests
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

        private sealed class StubPilot : IPilot
        {
            public void Drive(in PilotCommand cmd) { }
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
        private GameObject rockHost;
        private TestableCommander commander;
        private Gunner gunner;
        private ScriptedBrain brain;
        private AsteroidController rock;
        private MpcSettings createdSettings;

        [SetUp]
        public void SetUp()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(ship, Is.Not.Null, $"Missing ship prefab at {ShipPrefabPath}");

            host = new GameObject("ReferentCommander");
            gunner = host.AddComponent<Gunner>();
            commander = host.AddComponent<TestableCommander>();
            commander.CallAwake(); // EditMode: Unity does not run Awake, so cache the composed parts explicitly.
            brain = commander.InstallBrain<ScriptedBrain>();

            anchorHost = new GameObject("Anchor");
            var registry = new StubShipRegistry();
            registry.Ships[AnchorId] = anchorHost.AddComponent<Ship>();

            rock = TestRocks.Spawn(new Vector2(10f, 5f));
            rockHost = rock.gameObject;

            var status = new StubStatus { transform = host.transform, dynamics = ship.ResolveStats().Dynamics };
            commander.SetArena(TestArena.On(host, registry));
            commander.Initialize(new ShipControl(status, new StubPilot(), new SeedScope(1),
                new StubWeaponContext(), new NoWeapons()));
            createdSettings = commander.Navigator.mpcSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            if (anchorHost) Object.DestroyImmediate(anchorHost);
            if (rockHost) Object.DestroyImmediate(rockHost);
            if (createdSettings) Object.DestroyImmediate(createdSettings);
        }

        private BrainDecision AimAtRock() => new(
            NavObjective.Anchored(AnchorId).Facing(AsteroidRef.Of(rock), 0f, 1f), engagePrimary: true);

        [Test]
        public void ShootTheRock_HandSentence_AimsTheGunnerAtTheRock()
        {
            brain.decision = AimAtRock();
            commander.Step();

            Assert.That(gunner.HasTarget, "an armed AIM with a live rock referent gives the fire lane a target");
            var expected = GamePlane.PlanePointToWorld(new Vector2(10f, 5f));
            Assert.That(Vector3.Distance(gunner.Target, expected), Is.LessThan(1e-3f),
                "a stationary rock's intercept point is the rock itself");
        }

        [Test]
        public void RockSeat_ReResolvesEveryTick_WhileTheDecisionIsHeld()
        {
            brain.decision = AimAtRock();
            commander.Step();

            rockHost.transform.position = GamePlane.PlanePointToWorld(new Vector2(20f, -3f));
            commander.Step();

            var expected = GamePlane.PlanePointToWorld(new Vector2(20f, -3f));
            Assert.That(Vector3.Distance(gunner.Target, expected), Is.LessThan(1e-3f),
                "binding is identity — a held decision must track the live rock, not its decision-time pose");
        }

        [Test]
        public void DespawnedAimRock_HoldsFire_AndInvalidatesTheSeat()
        {
            brain.decision = AimAtRock();
            commander.Step();
            Assert.That(gunner.HasTarget);

            rockHost.SetActive(false); // the rock died mid-hold
            commander.Step();

            Assert.That(gunner.HasTarget, Is.False, "no resolvable AIM referent = hold fire until the next decision");
            Assert.That(commander.Navigator.referent1.valid, Is.False,
                "the dead rock's seat rides to the solver invalid, dropping its slots to weight 0");
        }

        [Test]
        public void AimOnTheAnchor_KeepsTheNavAnchorPath()
        {
            // A bare Ship anchor reports default kinematics (plane origin) — distinct from the rock at (10, 5).
            brain.decision = new BrainDecision(
                NavObjective.Anchored(AnchorId).Facing(0f, 1f), engagePrimary: true);
            commander.Step();

            Assert.That(gunner.HasTarget, "AIM on referent 0 is today's behavior: the gunner tracks the anchor");
            var anchorPoint = GamePlane.PlanePointToWorld(Vector2.zero);
            Assert.That(Vector3.Distance(gunner.Target, anchorPoint), Is.LessThan(1e-3f),
                "the gunner aimed at the anchor, not the unbound rock");
        }
    }
}
#endif
