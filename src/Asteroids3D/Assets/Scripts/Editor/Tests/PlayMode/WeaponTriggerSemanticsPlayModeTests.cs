using System.Collections;
using System.Collections.Generic;
using AI;
using Combat;
using Combat.Projectiles;
using Combat.Weapons;
using Damage;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Tests.PlayMode.Common;
using Substrate;
using Substrate.Services.Projectiles;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// Weapons own their trigger semantics: full-auto fires on held, semi-auto on pressed,
    /// charge weapons accumulate while held and fire on release or at full charge. The AI
    /// "mashes" (press every step it wants fire) and aims each slot with that slot's ballistics
    /// — hitscan slots get no intercept lead. Holding the trigger on a shipped prefab delivers
    /// the sustained DPS its own hold cycle mode states.
    /// </summary>
    [Category("Weapons")]
    public class WeaponTriggerSemanticsPlayModeTests : PlayModeWorldFixture
    {
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string ChargeLasersPrefabPath = "Assets/Prefabs/Weapons/ChargeLasers.prefab";
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";

        private static readonly string[] ShippedWeaponPrefabPaths =
        {
            "Assets/Prefabs/Weapons/Lasers.prefab",
            RippersPrefabPath,
            ChargeLasersPrefabPath,
            RailgunPrefabPath,
            MissilesPrefabPath,
            "Assets/Prefabs/Weapons/Grenades.prefab",
        };

        private readonly List<GameObject> spawned = new();
        private float savedCaptureDelta;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            savedCaptureDelta = Time.captureDeltaTime;
        }

        [TearDown]
        public override void TearDown()
        {
            Time.captureDeltaTime = savedCaptureDelta;

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
            laser.Charge.Configure(chargeTime: Time.fixedDeltaTime * 10f, minChargeToFire: 0.3f);
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
            laser.Charge.Configure(chargeTime: Time.fixedDeltaTime * 10f, minChargeToFire: 0.3f);

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

            public void TakeDamage(in DamageInfo hit)
            {
                TotalDamage += hit.Amount;
            }
        }

        private sealed class TestShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => GetComponent<Rigidbody>();
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
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

            railgun.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f);
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

            mounted.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f);
            mounted.HandleTrigger(pressed: false, held: true, Projectiles);

            Assert.AreEqual(0f, ownRecorder.TotalDamage, 0.001f, "Never hit the ship that fired.");
            Assert.AreEqual(45f, enemy.TotalDamage, 0.001f, "Beam continues past its own hull.");
        }

        private sealed class LaunchRecorder : IProjectileService
        {
            private readonly IProjectileService inner;
            private readonly List<ProjectileBase> launched = new();

            public LaunchRecorder(IProjectileService inner) => this.inner = inner;

            public int ActiveCount => inner.ActiveCount;

            public void Register(MonoBehaviour instance, System.Action returnToPool)
            {
                inner.Register(instance, returnToPool);
                if (instance is ProjectileBase projectile)
                    launched.Add(projectile);
            }

            public void ReturnAllToPool() => inner.ReturnAllToPool();

            public void ForEachLive(System.Action<MonoBehaviour> visit) => inner.ForEachLive(visit);

            /// <summary>Damage carried by the projectiles launched since the last call; a grenade carries its blast.</summary>
            public float TakeLaunchedDamage()
            {
                var damage = 0f;
                foreach (var projectile in launched)
                    damage += projectile is Grenade grenade
                        ? grenade.WavePrefab.MaxDamage
                        : projectile.Damage * projectile.DamageScale;
                launched.Clear();
                return damage;
            }
        }

        // Reads authored values: a retune passes, a cycle model that stops matching the game fails.
        [UnityTest]
        public IEnumerator HeldTrigger_DeliversTheHoldModesSustainedDps(
            [ValueSource(nameof(ShippedWeaponPrefabPaths))] string path)
        {
            const int cycles = 3;
            // One Update per fixed step, so heat and reload tick in step with the trigger.
            Time.captureDeltaTime = Time.fixedDeltaTime;

            var weapon = InstantiateWeapon<WeaponComponent>(path);
            var hold = weapon.CycleModes[0];
            var recorder = new LaunchRecorder(Projectiles);
            var beamTarget = weapon is Railguns ? CreateTarget(weapon.transform.position + Vector3.up * 5f) : null;

            var damage = 0f;
            var firstShotTime = -1f;
            var measuredSeconds = 0f;
            var deadline = Time.fixedTime + (cycles + 1) * hold.CycleSeconds * 1.5f;
            while (measuredSeconds <= 0f && Time.fixedTime < deadline)
            {
                yield return new WaitForFixedUpdate();

                var beamBefore = beamTarget ? beamTarget.TotalDamage : 0f;
                // Pressed and held together, as the AI mashes: semi-auto refires as soon as it can.
                weapon.HandleTrigger(pressed: true, held: true, recorder);
                var shot = recorder.TakeLaunchedDamage() + (beamTarget ? beamTarget.TotalDamage - beamBefore : 0f);
                if (shot <= 0f) continue;

                if (firstShotTime < 0f) firstShotTime = Time.fixedTime;
                // The shot that opens the next cycle closes the measurement.
                if (damage >= cycles * hold.MagazineDamage - 0.001f)
                    measuredSeconds = Time.fixedTime - firstShotTime;
                else
                    damage += shot;
            }

            var measuredDps = measuredSeconds > 0f ? damage / measuredSeconds : 0f;
            var report = $"{weapon.DisplayName} / {hold.Label}: model {hold.SustainedDps:0.###} DPS " +
                         $"({hold.MagazineDamage:0.###} per {hold.CycleSeconds:0.###} s), " +
                         $"game {measuredDps:0.###} DPS ({damage:0.###} over {measuredSeconds:0.###} s)";
            TestContext.WriteLine(report);
            Assert.AreEqual(hold.SustainedDps, measuredDps, hold.SustainedDps * 0.05f, report);
        }

        private sealed class FakeWeaponContext : IWeaponContext
        {
            private readonly List<WeaponSlot> slots = new() { WeaponSlot.Primary, WeaponSlot.Secondary };
            public float PrimarySpeed = 20f;
            public float SecondarySpeed = 0f;

            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => true;
            public float ProjectileSpeed(WeaponSlot slot) => slot == WeaponSlot.Primary ? PrimarySpeed : SecondarySpeed;
            public Combat.Weapons.Gunsight Sight(WeaponSlot slot) => null;
        }

        private sealed class CommandRecorder : IWeapons
        {
            public readonly List<(WeaponSlot slot, WeaponCommand cmd)> Commands = new();
            public void Fire(WeaponSlot slot, in WeaponCommand cmd) => Commands.Add((slot, cmd));
        }

        [Test]
        public void Gunner_ValidTargetAtWorldOrigin_HasTarget()
        {
            var go = new GameObject("GunnerOriginTest");
            spawned.Add(go);
            var gunner = go.AddComponent<AI.Gunner>();

            Kinematics Pose() => new(new Vector2(10f, 0f), Vector2.zero, 0f, 0f, 0f);
            gunner.Initialize(new FakeWeaponContext(), new CommandRecorder(), Pose);
            gunner.Aim(new AI.Context.EnemyTarget
            {
                kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f),
            });

            Assert.AreEqual(Vector3.zero, gunner.Target);
            Assert.IsTrue(gunner.HasTarget, "World origin is a valid aim point, not a no-target sentinel.");
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
            gunner.Aim(new AI.Context.EnemyTarget
            {
                kinematics = new Kinematics(enemyPos, enemyVel, 0f, 0f, 0f),
            });

            // Hitscan slot (speed 0) aims at the target's present position.
            var hitscanAim = gunner.AimPointFor(WeaponSlot.Secondary);
            Assert.AreEqual(Substrate.GamePlane.PlanePointToWorld(enemyPos), hitscanAim);

            // Ballistic slot leads the moving target along its velocity.
            var ballisticAim = gunner.AimPointFor(WeaponSlot.Primary);
            var expectedLead = Combat.Targeting.TargetingMath.PredictIntercept(Pose(), enemyPos, enemyVel, context.PrimarySpeed);
            Assert.AreEqual(Substrate.GamePlane.PlanePointToWorld(expectedLead), ballisticAim);
            Assert.AreNotEqual(hitscanAim, ballisticAim, "A moving target separates lead from no-lead aim.");

            // The AI mashes: pressed and held both reflect its per-step decision.
            gunner.Fire(engagePrimary: true, engageSecondary: true);
            Assert.AreEqual(2, recorder.Commands.Count);
            foreach (var (_, cmd) in recorder.Commands)
                Assert.AreEqual(cmd.held, cmd.pressed, "AI reports press and hold together.");

            // Disengaging releases the trigger rather than going silent.
            recorder.Commands.Clear();
            gunner.Fire(engagePrimary: false, engageSecondary: false);
            Assert.AreEqual(2, recorder.Commands.Count);
            foreach (var (_, cmd) in recorder.Commands)
                Assert.IsFalse(cmd.held || cmd.pressed, "a disengaged slot pushes a released trigger");
        }
    }
}
