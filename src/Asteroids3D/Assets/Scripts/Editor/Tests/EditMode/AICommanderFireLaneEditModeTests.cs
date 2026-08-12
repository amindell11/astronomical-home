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
    /// <summary>Pins the commander's fire-lane routing: the Gunner is the sole path from an AI ship to the weapon actuator — engaged or disengaged, every step pushes each slot's command through it with press and hold together.</summary>
    [Category("AI")]
    public class AICommanderFireLaneEditModeTests
    {
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_1.prefab";

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

        private sealed class SpyWeapons : IWeapons
        {
            public readonly List<(WeaponSlot slot, WeaponCommand cmd)> Commands = new();
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) => Commands.Add((slot, cmd));
        }

        private sealed class StubWeaponContext : IWeaponContext
        {
            private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => true;
            public float ProjectileSpeed(WeaponSlot slot) => 40f;
            public Gunsight Sight(WeaponSlot slot) => null;
        }

        private sealed class ScriptedBrain : Brain
        {
            public BrainDecision? decision;
            public override BrainDecision? Decide(AIContext ctx) => decision;
        }

        private GameObject host;
        private TestableCommander commander;
        private Gunner gunner;
        private SpyWeapons weapons;
        private ScriptedBrain brain;
        private MpcSettings createdSettings;

        [SetUp]
        public void SetUp()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(ship, Is.Not.Null, $"Missing ship prefab at {ShipPrefabPath}");

            host = new GameObject("FireLaneCommander");
            gunner = host.AddComponent<Gunner>();
            commander = host.AddComponent<TestableCommander>();
            commander.CallAwake(); // EditMode: Unity does not run Awake, so cache the composed parts explicitly.

            brain = commander.InstallBrain<ScriptedBrain>();

            weapons = new SpyWeapons();
            var status = new StubStatus { transform = host.transform, dynamics = ship.ResolveStats().Dynamics };
            commander.SetArena(TestArena.On(host));
            commander.Initialize(new ShipControl(status, new StubPilot(), new SeedScope(1),
                new StubWeaponContext(), weapons));
            createdSettings = commander.Navigator.mpcSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            if (createdSettings) Object.DestroyImmediate(createdSettings);
        }

        private static BrainDecision Decision(bool engagePrimary) => new(
            NavObjective.Planar(Vector2.zero), engagePrimary: engagePrimary);

        [Test]
        public void EveryStep_PushesEachSlotThroughTheGunner_EngagedOrNot()
        {
            brain.decision = Decision(engagePrimary: true);
            commander.Step();
            Assert.AreEqual(1, weapons.Commands.Count, "an engaged step pushes exactly one primary command");

            brain.decision = Decision(engagePrimary: false);
            commander.Step();
            Assert.AreEqual(2, weapons.Commands.Count,
                "a disengaged slot still receives a released trigger, not silence");
            Assert.IsFalse(weapons.Commands[^1].cmd.held);

            foreach (var (slot, cmd) in weapons.Commands)
            {
                Assert.AreEqual(WeaponSlot.Primary, slot);
                Assert.AreEqual(cmd.held, cmd.pressed, "the gunner pushes press and hold together");
            }
        }

        [Test]
        public void WithoutAGunner_TheCommanderNeverTouchesTheActuator()
        {
            Object.DestroyImmediate(gunner);
            brain.decision = Decision(engagePrimary: true);
            commander.Step();
            Assert.IsEmpty(weapons.Commands,
                "the Gunner is the sole path from an AI ship to the weapon actuator");
        }
    }
}
#endif
