using System.Collections.Generic;
using Combat;
using Combat.Conditions;
using Combat.Projectile;
using Combat.Weapons;
using Damage;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// Weapons own their trigger semantics: full-auto fires on held, semi-auto on pressed,
    /// charge weapons accumulate while held and fire on release or at full charge. The AI
    /// "mashes" (press every step it wants fire) and aims each slot with that slot's ballistics
    /// — hitscan slots get no intercept lead.
    /// </summary>
    [Category("Weapons")]
    public class WeaponTriggerSemanticsPlayModeTests : PlayModeWorldFixture
    {
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string ChargeLasersPrefabPath = "Assets/Prefabs/Weapons/ChargeLasers.prefab";
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";

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

        [Test]
        public void AutoWeapon_FiresOnHeld_WithoutAPress()
        {
            var ripper = InstantiateWeapon<Rippers>(RippersPrefabPath);
            var fired = 0;
            ripper.OnFire += () => fired++;

            ripper.HandleTrigger(pressed: false, held: true, Projectiles);

            Assert.AreEqual(1, fired, "Full-auto fires from held state alone.");
        }

        [Test]
        public void SemiAutoWeapon_FiresOnPressOnly()
        {
            var missiles = InstantiateWeapon<Missiles>(MissilesPrefabPath);
            var fired = 0;
            missiles.OnFire += () => fired++;

            missiles.HandleTrigger(pressed: false, held: true, Projectiles);
            Assert.AreEqual(0, fired, "Semi-auto must not fire from held state alone.");

            missiles.HandleTrigger(pressed: true, held: true, Projectiles);
            Assert.AreEqual(1, fired, "Semi-auto fires on the press.");
        }

        [Test]
        public void ChargeWeapon_ChargesWhileHeld_AndAutoFiresAtFull()
        {
            var laser = InstantiateWeapon<ChargeLasers>(ChargeLasersPrefabPath);
            // 10 physics steps to full, independent of the project's fixed timestep.
            laser.Charge.Configure(chargeTime: Time.fixedDeltaTime * 10f, minChargeToFire: 0.3f, autoFireAtFull: true);
            var fired = 0;
            laser.OnFire += () => fired++;

            laser.HandleTrigger(pressed: true, held: true, Projectiles);
            Assert.AreEqual(0, fired, "Still charging — a press means nothing to a charge weapon.");

            for (var i = 0; i < 20 && fired == 0; i++)
                laser.HandleTrigger(pressed: false, held: true, Projectiles);

            Assert.AreEqual(1, fired, "Full charge while held auto-fires.");
            Assert.AreEqual(0f, laser.Charge.ChargePct, 0.0001f, "Firing consumed the charge.");
        }

        [Test]
        public void ChargeWeapon_ReleaseFires_WithChargeScaledDamage()
        {
            var laser = InstantiateWeapon<ChargeLasers>(ChargeLasersPrefabPath);
            laser.Charge.Configure(chargeTime: Time.fixedDeltaTime * 10f, minChargeToFire: 0.3f, autoFireAtFull: false);

            // Hold for half the charge time, then release.
            for (var i = 0; i < 5; i++)
                laser.HandleTrigger(pressed: false, held: true, Projectiles);
            Assert.AreEqual(0.5f, laser.Charge.ChargePct, 0.001f);

            var before = Object.FindObjectsByType<Laser>(FindObjectsSortMode.None).Length;
            laser.HandleTrigger(pressed: false, held: false, Projectiles);

            var bolts = Object.FindObjectsByType<Laser>(FindObjectsSortMode.None);
            Assert.AreEqual(before + 1, bolts.Length, "Release above the minimum fires the shot.");

            // Damage scales linearly from the min-charge to full-charge multiplier (0.4 → 1).
            var expected = Mathf.Lerp(0.4f, 1f, 0.5f);
            Assert.AreEqual(expected, bolts[bolts.Length - 1].DamageScale, 0.02f);
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public float TotalDamage { get; private set; }

            public void TakeDamage(in Damage.DamageInfo hit)
            {
                TotalDamage += hit.Amount;
            }
        }

        private sealed class TestShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => GetComponent<Rigidbody>();
            public Ships.ShipId Id => Ships.ShipId.Invalid;
        }

        private DamageRecorder CreateTarget(Vector3 position)
        {
            var go = new GameObject("BeamTarget") { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = Vector3.one;
            return go.AddComponent<DamageRecorder>();
        }

        [Test]
        public void Railgun_FullCharge_DamagesFirstThingTheBeamHits()
        {
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);
            var target = CreateTarget(railgun.transform.position + Vector3.up * 5f);

            railgun.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f, autoFireAtFull: true);
            var fired = 0;
            railgun.OnFire += () => fired++;

            railgun.HandleTrigger(pressed: false, held: true, Projectiles);

            Assert.AreEqual(1, fired, "One held step reaches full charge and auto-fires.");
            Assert.AreEqual(45f, target.TotalDamage, 0.001f, "Beam applies the railgun's damage.");
        }

        [Test]
        public void Railgun_PrefabAuthoredChargeTime_ReachesFullAndFires()
        {
            // Drives the prefab's serialized values with real fixed steps, guarding the pinned-just-below-full float case.
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);
            var target = CreateTarget(railgun.transform.position + Vector3.up * 5f);
            var fired = 0;
            railgun.OnFire += () => fired++;

            var steps = Mathf.CeilToInt(2f / Time.fixedDeltaTime);
            for (var i = 0; i < steps && fired == 0; i++)
                railgun.HandleTrigger(pressed: false, held: true, Projectiles);

            Assert.AreEqual(1, fired, "The prefab's authored charge time must reach full and auto-fire.");
            Assert.Greater(target.TotalDamage, 0f);
        }

        [Test]
        public void Railgun_BeamSkipsItsOwnShip()
        {
            // The weapon is mounted inside a ship whose collider surrounds the fire point; the
            // beam must pass through its own hull and hit the enemy beyond it.
            var ship = new GameObject("OwnShip") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.AddComponent<Rigidbody>().isKinematic = true;
            ship.AddComponent<BoxCollider>().size = Vector3.one * 2f;
            ship.AddComponent<TestShooter>();
            var ownRecorder = ship.AddComponent<DamageRecorder>();

#if UNITY_EDITOR
            // Instantiated as a child so Awake resolves the shooter from the parent hierarchy.
            var prefab = AssetDatabase.LoadAssetAtPath<Railguns>(RailgunPrefabPath);
            Assert.IsNotNull(prefab);
            var mounted = Object.Instantiate(prefab, ship.transform);
            spawned.Add(mounted.gameObject);
#else
            Railguns mounted = null;
            Assert.Ignore("Requires Unity Editor assets.");
#endif

            var enemy = CreateTarget(ship.transform.position + Vector3.up * 6f);

            mounted.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f, autoFireAtFull: true);
            mounted.HandleTrigger(pressed: false, held: true, Projectiles);

            Assert.AreEqual(0f, ownRecorder.TotalDamage, 0.001f, "Never hit the ship that fired.");
            Assert.AreEqual(45f, enemy.TotalDamage, 0.001f, "Beam continues past its own hull.");
        }

        private sealed class FakeWeaponContext : IWeaponContext
        {
            private readonly List<WeaponSlot> slots = new() { WeaponSlot.Primary, WeaponSlot.Secondary };
            public float PrimarySpeed = 20f;
            public float SecondarySpeed = 0f;

            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => true;
            public float ProjectileSpeed(WeaponSlot slot) => slot == WeaponSlot.Primary ? PrimarySpeed : SecondarySpeed;
            public Combat.Gunsight Sight(WeaponSlot slot) => null;
        }

        private sealed class CommandRecorder : IWeapons
        {
            public readonly List<(WeaponSlot slot, WeaponCommand cmd)> Commands = new();
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) => Commands.Add((slot, cmd));
        }

        [Test]
        public void Gunner_AimsEachSlotWithItsOwnBallistics_HitscanGetsNoLead()
        {
            var go = new GameObject("GunnerTest");
            spawned.Add(go);
            var gunner = go.AddComponent<AI.Gunner>();

            var context = new FakeWeaponContext();
            var recorder = new CommandRecorder();
            Kinematics Pose() => new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
            gunner.Initialize(context, recorder, Pose);

            var enemyPos = new Vector2(10f, 0f);
            var enemyVel = new Vector2(0f, 5f);
            gunner.ApplyIntent(new AI.States.NavigationIntent
            {
                isValid = true,
                enableFiring = true,
                hasTarget = true,
                target = new AI.Context.EnemyTarget
                {
                    kinematics = new Kinematics(enemyPos, enemyVel, 0f, 0f, 0f),
                },
            });

            // Hitscan slot (speed 0) aims at the target's present position.
            var hitscanAim = gunner.AimPointFor(WeaponSlot.Secondary);
            Assert.AreEqual(Game.GamePlane.PlanePointToWorld(enemyPos), hitscanAim);

            // Ballistic slot leads the moving target along its velocity.
            var ballisticAim = gunner.AimPointFor(WeaponSlot.Primary);
            var expectedLead = Combat.TargetingMath.PredictIntercept(Pose(), enemyPos, enemyVel, context.PrimarySpeed);
            Assert.AreEqual(Game.GamePlane.PlanePointToWorld(expectedLead), ballisticAim);
            Assert.AreNotEqual(hitscanAim, ballisticAim, "A moving target separates lead from no-lead aim.");

            // The AI mashes: pressed and held both reflect its per-step decision.
            gunner.Fire();
            Assert.AreEqual(2, recorder.Commands.Count);
            foreach (var (_, cmd) in recorder.Commands)
                Assert.AreEqual(cmd.held, cmd.pressed, "AI reports press and hold together.");
        }
    }
}
