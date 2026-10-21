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
    /// <summary>Pins the Gunner's trigger stream over a real sight: held is the weapon's firing solution step for step, and pressed marks only its rising edges.</summary>
    [Category("AI")]
    public class GunnerTriggerStreamEditModeTests
    {
        private sealed class ScriptedWeapon : WeaponComponent
        {
            public bool[] solution;
            private int step;

            public override bool ShouldFire(TargetingContext context) => solution[step++];

            public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles) => null;
        }

        private sealed class PrimarySlotContext : IWeaponContext
        {
            private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
            public Gunsight sight;

            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => true;
            public float ProjectileSpeed(WeaponSlot slot) => 0f;
            public Gunsight Sight(WeaponSlot slot) => sight;
        }

        private sealed class CommandRecorder : IWeapons
        {
            public readonly List<WeaponCommand> Commands = new();
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) => Commands.Add(cmd);
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
    }
}
