using System.Collections.Generic;
using AI;
using Combat.Projectiles;
using Combat.Weapons;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Substrate.Services.Projectiles;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the Gunner's trigger stream over a real sight: a weapon that declares no hold-through gets held equal to its firing solution step for step; a declared window bridges solution drops only; pressed marks only held's rising edges.</summary>
    [Category("AI")]
    public class GunnerTriggerStreamEditModeTests
    {
        private sealed class ScriptedWeapon : WeaponComponent
        {
            public bool[] solution;
            private int step;

            public override bool ShouldFire(TargetingContext context) => solution[step++];

            public override ProjectileBase Fire(IProjectileService projectiles) => null;
        }

        private sealed class PrimarySlotContext : IWeaponContext
        {
            private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
            public Gunsight sight;
            public float holdThroughSeconds;

            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => true;
            public float ProjectileSpeed(WeaponSlot slot) => 0f;
            public float HoldThroughSeconds(WeaponSlot slot) => holdThroughSeconds;
            public Gunsight Sight(WeaponSlot slot) => sight;
        }

        private sealed class CommandRecorder : IWeapons
        {
            public readonly List<WeaponCommand> Commands = new();
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) => Commands.Add(cmd);
        }

        /// <summary>A Gunner aimed at a target, over a weapon playing a scripted solution and declaring a hold-through window in steps.</summary>
        private sealed class Rig : System.IDisposable
        {
            private static readonly Vector2 target = new(0f, 10f);
            private readonly GameObject host = new("Gunner");
            private readonly CommandRecorder recorder = new();
            public readonly Gunner gunner;

            public Rig(bool[] solution, int windowSteps)
            {
                var weapon = host.AddComponent<ScriptedWeapon>();
                weapon.solution = solution;
                var context = new PrimarySlotContext
                {
                    sight = new Gunsight(weapon, Pose),
                    holdThroughSeconds = windowSteps * Time.fixedDeltaTime,
                };
                gunner = host.AddComponent<Gunner>();
                gunner.Initialize(context, recorder, Pose);
                Aim();
            }

            private static Kinematics Pose() => new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);

            public void Aim() => gunner.Aim(target, Vector2.zero);

            public WeaponCommand Step(bool engage = true)
            {
                gunner.Fire(engagePrimary: engage, engageSecondary: false);
                return recorder.Commands[^1];
            }

            public void Dispose() => Object.DestroyImmediate(host);
        }

        [Test]
        public void HeldIsTheFiringSolutionStepForStep_PressedOnlyOnItsRisingEdges()
        {
            bool[] solution = { false, true, true, true, false, true, false, false, true, true };
            var host = new GameObject("Gunner");
            try
            {
                var weapon = host.AddComponent<ScriptedWeapon>();
                weapon.solution = solution;
                Kinematics Pose() => new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
                var recorder = new CommandRecorder();
                var gunner = host.AddComponent<Gunner>();
                gunner.Initialize(new PrimarySlotContext { sight = new Gunsight(weapon, Pose) }, recorder, Pose);
                gunner.Aim(new Vector2(0f, 10f), Vector2.zero);

                for (var i = 0; i < solution.Length; i++)
                    gunner.Fire(engagePrimary: true, engageSecondary: false);

                Assert.AreEqual(solution.Length, recorder.Commands.Count);
                for (var i = 0; i < solution.Length; i++)
                {
                    var risingEdge = solution[i] && (i == 0 || !solution[i - 1]);
                    Assert.AreEqual(solution[i], recorder.Commands[i].held, $"step {i}: held is the firing solution");
                    Assert.AreEqual(risingEdge, recorder.Commands[i].pressed, $"step {i}: pressed only on a rising edge");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TwoStepWindow_BridgesDropsOfUpToTwoSteps_AndEverySolutionRearmsIt()
        {
            bool[] solution = { true, true, false, true, false, false, false, true };
            bool[] expectedHeld = { true, true, true, true, true, true, false, true };
            using var rig = new Rig(solution, windowSteps: 2);

            for (var i = 0; i < solution.Length; i++)
            {
                var cmd = rig.Step();
                Assert.AreEqual(expectedHeld[i], cmd.held, $"step {i}: held");
                Assert.AreEqual(i is 0 or 7, cmd.pressed, $"step {i}: pressed only where held rises");
            }
        }

        [Test]
        public void SolutionThatNeverComes_NeverStartsAHold()
        {
            bool[] solution = { false, false, false, false };
            using var rig = new Rig(solution, windowSteps: 2);

            for (var i = 0; i < solution.Length; i++)
                Assert.IsFalse(rig.Step().held, $"step {i}: only a solution puts the trigger down");
        }

        [Test]
        public void DisengageInsideTheWindow_ReleasesOnThatStep()
        {
            using var rig = new Rig(new[] { true, false }, windowSteps: 5);

            Assert.IsTrue(rig.Step().held);
            Assert.IsTrue(rig.Step().held, "The window bridges the dropped solution.");
            Assert.IsFalse(rig.Step(engage: false).held, "A disengage is the brain's call: no window bridges it.");
        }

        [Test]
        public void ResetStateInsideTheWindow_ClearsTheHold()
        {
            using var rig = new Rig(new[] { true, false, false }, windowSteps: 5);

            Assert.IsTrue(rig.Step().held);
            Assert.IsTrue(rig.Step().held, "The window bridges the dropped solution.");

            rig.gunner.ResetState();
            rig.Aim();

            Assert.IsFalse(rig.Step().held, "Still inside the old window, but the reset gunner has no hold to extend.");
        }
    }
}
