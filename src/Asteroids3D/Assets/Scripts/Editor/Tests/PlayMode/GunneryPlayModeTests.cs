using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Substrate.Services.Projectiles;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>The AI fire path over the real weapons: the Missiles dumbfire window, one Gunner press per entry into a firing solution, and a charge kept through a dropped solution by hold-through.</summary>
    [Category("Weapons")]
    public class GunneryPlayModeTests : PlayModeWorldFixture
    {
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";
        private const string ChargeLasersPrefabPath = "Assets/Prefabs/Weapons/ChargeLasers.prefab";

        protected override bool AccelerateTime => true;

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();

            base.TearDown();
        }

        private T InstantiateWeapon<T>(string path) where T : WeaponComponent
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {path}");
            var weapon = Object.Instantiate(prefab);
            spawned.Add(weapon.gameObject);
            return weapon;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }

        /// <summary>One real weapon in the primary slot: the Gunner's read view and a recording actuator that fires it.</summary>
        private sealed class PrimaryMount : IWeaponContext, IWeapons
        {
            private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
            private readonly WeaponComponent weapon;
            private readonly Gunsight sight;
            private readonly IProjectileService projectiles;

            public readonly List<WeaponCommand> Commands = new();

            public PrimaryMount(WeaponComponent weapon, Func<Kinematics> pose, IProjectileService projectiles)
            {
                this.weapon = weapon;
                this.projectiles = projectiles;
                sight = new Gunsight(weapon, pose);
            }

            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => weapon.CanFire();
            public float ProjectileSpeed(WeaponSlot slot) => 0f;
            public float HoldThroughSeconds(WeaponSlot slot) => weapon.HoldThroughSeconds;
            public Gunsight Sight(WeaponSlot slot) => sight;

            public void Fire(WeaponSlot slot, in WeaponCommand cmd)
            {
                Commands.Add(cmd);
                weapon.HandleTrigger(cmd.pressed, cmd.held, projectiles);
            }
        }

        private static Kinematics NoseUpAtOrigin() => new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);

        private Gunner MountGunner(WeaponComponent weapon, out PrimaryMount mount)
        {
            var go = new GameObject("Gunner");
            spawned.Add(go);
            var gunner = go.AddComponent<Gunner>();
            mount = new PrimaryMount(weapon, NoseUpAtOrigin, Projectiles);
            gunner.Initialize(mount, mount, NoseUpAtOrigin);
            return gunner;
        }

        private static TargetingContext OnTheNose(float distance, bool lineOfSight) => new()
        {
            distanceToTarget = distance,
            angleToTarget = 0f,
            hasLineOfSight = lineOfSight,
        };

        [Test]
        public void Missiles_DumbfireWindow_IsPointBlankAndNeedsLineOfSight()
        {
            var missiles = InstantiateWeapon<Missiles>(MissilesPrefabPath);

            Assert.IsFalse(missiles.ShouldFire(OnTheNose(8f, lineOfSight: true)),
                "8 u is outside the window: no unguided launch from the pilots' hold distance.");
            Assert.IsTrue(missiles.ShouldFire(OnTheNose(5f, lineOfSight: true)),
                "5 u with a clear line is inside the window.");
            Assert.IsFalse(missiles.ShouldFire(OnTheNose(5f, lineOfSight: false)),
                "A blocked line closes the window.");
        }

        [UnityTest]
        public IEnumerator Missiles_TargetHeldInTheWindow_LaunchesBothRoundsACooldownApart()
        {
            var missiles = InstantiateWeapon<Missiles>(MissilesPrefabPath);
            var cooldown = missiles.GetComponent<Cooldown>().SecondsBetweenShots;
            var gunner = MountGunner(missiles, out _);
            var step = 0;
            var launchSteps = new List<int>();
            missiles.OnFire += () => launchSteps.Add(step);

            gunner.Aim(new Vector2(0f, 5f), Vector2.zero);
            var steps = Mathf.CeilToInt(2f * cooldown / Time.fixedDeltaTime);
            for (; step < steps; step++)
            {
                gunner.Fire(engagePrimary: true, engageSecondary: false);
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(2, launchSteps.Count,
                "The solution drops while the weapon cools down, so the press re-arms for the second round.");
            Assert.AreEqual(cooldown, (launchSteps[1] - launchSteps[0]) * Time.fixedDeltaTime, 2f * Time.fixedDeltaTime);
        }

        [UnityTest]
        public IEnumerator Grenades_TargetHeldInTheDropEnvelope_GetsOnePressPerEntry()
        {
            var grenades = InstantiateWeapon<Grenades>(GrenadesPrefabPath);
            var cooldown = grenades.GetComponent<Cooldown>().SecondsBetweenShots;
            var gunner = MountGunner(grenades, out var mount);
            var drops = 0;
            grenades.OnFire += () => drops++;

            var behind = new Vector2(0f, -8f);
            var ahead = new Vector2(0f, 8f);

            // Held past the cooldown: an every-step press would drop a second charge inside the hold.
            var holdSteps = Mathf.CeilToInt(1.5f * cooldown / Time.fixedDeltaTime);
            gunner.Aim(behind, Vector2.zero);
            for (var i = 0; i < holdSteps; i++)
            {
                gunner.Fire(engagePrimary: true, engageSecondary: false);
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(holdSteps, mount.Commands.Count(c => c.held), "The trigger stays down for the whole hold.");
            Assert.AreEqual(1, mount.Commands.Count(c => c.pressed), "One press for one entry into the envelope.");
            Assert.AreEqual(1, drops);

            gunner.Aim(ahead, Vector2.zero);
            gunner.Fire(engagePrimary: true, engageSecondary: false);
            gunner.Aim(behind, Vector2.zero);
            gunner.Fire(engagePrimary: true, engageSecondary: false);

            Assert.AreEqual(2, mount.Commands.Count(c => c.pressed), "Leaving and re-entering presses again.");
            Assert.AreEqual(2, drops);
        }

        private static int HoldThroughSteps(WeaponComponent weapon) =>
            Mathf.RoundToInt(weapon.HoldThroughSeconds / Time.fixedDeltaTime);

        [Test]
        public void ChargeLasers_OneStepEnvelopeExit_KeepsTheCharge()
        {
            var lasers = InstantiateWeapon<ChargeLasers>(ChargeLasersPrefabPath);
            Assert.GreaterOrEqual(HoldThroughSteps(lasers), 1, "The prefab declares a hold-through window of at least one step.");
            var gunner = MountGunner(lasers, out _);
            var shots = 0;
            lasers.OnFire += () => shots++;
            var chargeReports = new List<float>();
            lasers.Charge.OnChargeChanged += chargeReports.Add;

            var ahead = new Vector2(0f, 10f);
            var behind = new Vector2(0f, -10f);

            gunner.Aim(ahead, Vector2.zero);
            for (var i = 0; i < 5; i++)
                gunner.Fire(engagePrimary: true, engageSecondary: false);
            var chargeBeforeExit = lasers.Charge.ChargePct;
            Assert.Greater(chargeBeforeExit, 0f);

            gunner.Aim(behind, Vector2.zero);
            gunner.Fire(engagePrimary: true, engageSecondary: false);
            gunner.Aim(ahead, Vector2.zero);
            gunner.Fire(engagePrimary: true, engageSecondary: false);

            Assert.Greater(lasers.Charge.ChargePct, chargeBeforeExit, "The charge kept building across the exit.");
            Assert.AreEqual(0, shots);
            Assert.That(chargeReports, Has.None.EqualTo(0f), "The charge was never dropped.");
        }

        [Test]
        public void ChargeLasers_ExitLongerThanTheWindow_DropsTheCharge()
        {
            var lasers = InstantiateWeapon<ChargeLasers>(ChargeLasersPrefabPath);
            var windowSteps = HoldThroughSteps(lasers);
            const int solutionSteps = 3;
            // Stretched so the window ends below the minimum charge whatever window the prefab declares.
            lasers.Charge.Configure(chargeTime: 10f * (solutionSteps + windowSteps) * Time.fixedDeltaTime, minChargeToFire: 1f);
            var gunner = MountGunner(lasers, out var mount);
            var shots = 0;
            lasers.OnFire += () => shots++;

            gunner.Aim(new Vector2(0f, 10f), Vector2.zero);
            for (var i = 0; i < solutionSteps; i++)
                gunner.Fire(engagePrimary: true, engageSecondary: false);

            gunner.Aim(new Vector2(0f, -10f), Vector2.zero);
            for (var i = 0; i < windowSteps; i++)
                gunner.Fire(engagePrimary: true, engageSecondary: false);

            Assert.IsTrue(mount.Commands.All(c => c.held), "The trigger stays down through the whole window.");
            Assert.Greater(lasers.Charge.ChargePct, 0f);

            gunner.Fire(engagePrimary: true, engageSecondary: false);

            Assert.IsFalse(mount.Commands[^1].held, "One step past the window the trigger comes up.");
            Assert.AreEqual(0f, lasers.Charge.ChargePct, "A release below the minimum charge drops it.");
            Assert.AreEqual(0, shots);
        }
    }
}
