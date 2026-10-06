using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
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
    /// <summary>The AI fire path over the real semi-auto weapons: the Missiles dumbfire window, and one Gunner press per entry into a firing solution.</summary>
    [Category("Weapons")]
    public class SemiAutoGunneryPlayModeTests : PlayModeWorldFixture
    {
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";

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
            public Gunsight Sight(WeaponSlot slot) => sight;

            public void Fire(WeaponSlot slot, in WeaponCommand cmd)
            {
                Commands.Add(cmd);
                weapon.HandleTrigger(in cmd, projectiles);
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
    }
}
