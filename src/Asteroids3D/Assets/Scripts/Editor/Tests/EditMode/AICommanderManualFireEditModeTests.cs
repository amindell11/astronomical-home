#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using AI.Context;
using AI.States;
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
    /// <summary>Pins the commander's manual trigger authority: a manual-fire intent pushes raw held + rising-edge press straight to the weapon actuator (Gunner skipped), a legacy intent pushes nothing without a Gunner, and ResetState clears the press-edge state.</summary>
    [Category("AI")]
    public class AICommanderManualFireEditModeTests
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

        private sealed class ScriptedIntentChooser : IIntentChooser
        {
            public NavigationIntent intent;
            public NavigationIntent Decide(AIContext ctx, float dt) => intent;
        }

        private GameObject host;
        private TestableCommander commander;
        private SpyWeapons weapons;
        private ScriptedIntentChooser chooser;
        private MpcSettings createdSettings;

        [SetUp]
        public void SetUp()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(ship, Is.Not.Null, $"Missing ship prefab at {ShipPrefabPath}");

            host = new GameObject("ManualFireCommander");
            commander = host.AddComponent<TestableCommander>();
            commander.CallAwake(); // EditMode: Unity does not run Awake, so cache the composed parts explicitly.

            chooser = new ScriptedIntentChooser { intent = ManualIntent(held: true) };
            host.GetComponent<Brain>().InstallChooser(chooser);

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

        private static NavigationIntent ManualIntent(bool held) => new()
        {
            isValid = true,
            velocityReference = Vector2.zero,
            hasFacing = true,
            facingRad = 0f,
            manualFire = true,
            primaryHeld = held,
        };

        private WeaponCommand StepOnce(bool held)
        {
            chooser.intent = ManualIntent(held);
            var before = weapons.Commands.Count;
            commander.Step();
            Assert.AreEqual(before + 1, weapons.Commands.Count, "each manual-fire step pushes exactly one primary command");
            var (slot, cmd) = weapons.Commands[^1];
            Assert.AreEqual(WeaponSlot.Primary, slot);
            return cmd;
        }

        [Test]
        public void ManualFire_PushesHeldWithRisingEdgePress()
        {
            var first = StepOnce(held: true);
            Assert.IsTrue(first.held);
            Assert.IsTrue(first.pressed, "the first held step is the press edge");

            var second = StepOnce(held: true);
            Assert.IsTrue(second.held);
            Assert.IsFalse(second.pressed, "a sustained hold must not re-press");

            var released = StepOnce(held: false);
            Assert.IsFalse(released.held);
            Assert.IsFalse(released.pressed);

            var rePressed = StepOnce(held: true);
            Assert.IsTrue(rePressed.pressed, "a release re-arms the press edge");
        }

        [Test]
        public void ResetState_ClearsThePressEdge()
        {
            StepOnce(held: true);
            commander.ResetState();
            Assert.IsTrue(StepOnce(held: true).pressed,
                "after a reset the first held step must press again — stale prevHeld would swallow it");
        }

        [Test]
        public void LegacyIntent_WithoutAGunner_PushesNoWeaponCommands()
        {
            chooser.intent = new NavigationIntent
            {
                isValid = true,
                velocityReference = Vector2.zero,
                enableFiring = true,
            };
            commander.Step();
            Assert.IsEmpty(weapons.Commands,
                "legacy intents fire through the Gunner path only — the commander must not touch the actuator");
        }
    }
}
#endif
