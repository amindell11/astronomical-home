using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI;
using Combat.Targeting;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Ships.Registry;
using Substrate.Services.Projectiles;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
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
            public Vector2 AimPoint(WeaponSlot slot, in Kinematics shooter, Vector2 targetPos, Vector2 targetVel) => targetPos;
            public float? HoldRange(WeaponSlot slot) => null;
            public int KeepOutCircles(WeaponSlot slot, Span<AI.Scanning.DetectedObstacle.PlaneCircle> circles) => 0;
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
            gunner.Initialize(mount, mount, NoseUpAtOrigin, TestAimSettings.Perfect, new SeedScope(1),
                ShipId.Invalid, new StubShipRegistry());
            return gunner;
        }

        private static TargetingContext OnTheNose(float distance, bool lineOfSight) => new()
        {
            distanceToTarget = distance,
            angleToTarget = 0f,
            hasLineOfSight = lineOfSight,
        };

        private sealed class LockTarget : ITargetable
        {
            public LockTarget(Transform targetPoint) => TargetPoint = targetPoint;
            public Transform TargetPoint { get; }
            public LockChannel Lock { get; } = new();
        }

        private (Missiles missiles, LockOnSensor sensor) FreshLock()
        {
            var missiles = InstantiateWeapon<Missiles>(MissilesPrefabPath);
            var sensor = missiles.GetComponent<LockOnSensor>();
            var targetGo = new GameObject("LockTarget");
            spawned.Add(targetGo);

            var targetLock = sensor.targetLock;
            Assert.IsNotNull(targetLock, "test premise: the sensor has a lock");
            Assert.IsTrue(targetLock.TryStartLock(new LockTarget(targetGo.transform)), "test premise: the lock starts");
            for (var i = 0; i < 30 && targetLock.State != LockState.Locked; i++)
                targetLock.Update(1f, true);
            Assert.AreEqual(LockState.Locked, sensor.State, "test premise: the lock just completed");
            Assert.AreEqual(0f, sensor.LockedFor, "test premise: a just-completed lock has not been held");
            return (missiles, sensor);
        }

        [Test]
        public void Missiles_DumbfireWindow_IsPointBlankAndNeedsLineOfSight()
        {
            var missiles = InstantiateWeapon<Missiles>(MissilesPrefabPath);

            Assert.IsFalse(missiles.HoldTrigger(OnTheNose(8f, lineOfSight: true)),
                "8 u is outside the window: no unguided launch from the pilots' hold distance.");
            Assert.IsTrue(missiles.HoldTrigger(OnTheNose(5f, lineOfSight: true)),
                "5 u with a clear line is inside the window.");
            Assert.IsFalse(missiles.HoldTrigger(OnTheNose(5f, lineOfSight: false)),
                "A blocked line closes the window.");
        }

        [Test]
        public void HoldTrigger_HoldsAFreshLock_UntilTheWindUpPasses()
        {
            var (missiles, sensor) = FreshLock();
            var beyondFallback = OnTheNose(8f, lineOfSight: true);

            Assert.IsFalse(missiles.HoldTrigger(beyondFallback),
                "A lock that just completed has not wound up.");

            sensor.targetLock.Update(0.3f, true);
            Assert.GreaterOrEqual(sensor.LockedFor, 0.3f);
            Assert.IsTrue(missiles.HoldTrigger(beyondFallback),
                "Once the lock has been held for 0.3 s, the gunner launches.");
        }

        [Test]
        public void HoldTrigger_NoLaunch_WhenTheLockDropsDuringTheWindUp()
        {
            var (missiles, sensor) = FreshLock();
            var beyondFallback = OnTheNose(8f, lineOfSight: true);

            Assert.IsFalse(missiles.HoldTrigger(beyondFallback),
                "A lock that just completed has not wound up.");

            sensor.targetLock.Update(0.15f, true);
            Assert.Less(sensor.LockedFor, 0.3f, "test premise: the wind-up has not elapsed");
            Assert.IsFalse(missiles.HoldTrigger(beyondFallback),
                "A lock still inside the wind-up does not launch.");

            sensor.ResetLock();
            Assert.AreEqual(LockState.Idle, sensor.State, "test premise: the lock dropped");
            Assert.AreEqual(0f, sensor.LockedFor);
            Assert.IsFalse(missiles.HoldTrigger(beyondFallback),
                "A lock that drops during the wind-up does not launch, and 8 u is outside the dumbfire window.");

            sensor.targetLock.Update(0.3f, true);
            Assert.IsFalse(missiles.HoldTrigger(beyondFallback),
                "Time passing after the drop does not launch.");
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
