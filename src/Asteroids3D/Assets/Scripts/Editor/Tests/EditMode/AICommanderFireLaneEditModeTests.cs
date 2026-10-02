#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using Combat;
using AI.Navigation.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using UnityEditor;
using UnityEngine;
using AI.Strategy;

namespace Tests.EditMode
{
    /// <summary>Pins the commander's fire-lane routing: the Gunner is the sole path from an AI ship to the weapon actuator — engaged or disengaged, every step pushes each slot's command through it.</summary>
    [Category("AI")]
    public class AICommanderFireLaneEditModeTests
    {
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_1.prefab";

        private sealed class StubPilot : IPilot
        {
            public void Drive(in PilotCommand cmd) { }
        }

        private sealed class SpyWeapons : IWeapons
        {
            public readonly List<(WeaponSlot slot, WeaponCommand cmd)> Commands = new();
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) => Commands.Add((slot, cmd));
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
            var status = new StubShipStatus { transform = host.transform, dynamics = ship.ResolveStats().Dynamics };
            commander.SetSensing(new StubShipRegistry(), null);
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

            foreach (var (slot, _) in weapons.Commands)
                Assert.AreEqual(WeaponSlot.Primary, slot);
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
