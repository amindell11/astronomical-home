using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Damage;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
#if UNITY_EDITOR
using UnityEditor;
using Substrate;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// The concussion charge flies straight at its target point (clamped to max range), brakes to
    /// rest there and detonates on arrival, on contact (never the owner's hull) or when shot — the
    /// owner's fire included. Its wave sweeps outward hitting everything once, the shooter included,
    /// with damage falling off toward the rim; its kick spins targets against their turn.
    /// </summary>
    [Category("Weapons")]
    public class ConcussionGrenadePlayModeTests : PlayModeWorldFixture
    {
        private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            // Detonation bursts (PooledVFX, untracked by design) outlive their test and would trip the phantom-burst zero-VFX assertion.
            foreach (var vfx in Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None))
                Object.DestroyImmediate(vfx.gameObject);

            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();

            base.TearDown();
        }

        private sealed class MovingShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity { get; set; }
            public Rigidbody Body => GetComponent<Rigidbody>();
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public float TotalDamage { get; private set; }

            public void TakeDamage(in DamageInfo hit)
            {
                TotalDamage += hit.Amount;
            }
        }

        private static DamageInfo Shot(Vector3 point) =>
            new(1f, DamageKind.Laser, Ships.Registry.ShipId.Invalid, 0.1f, Vector3.zero, point);

        private static Vector3 Plane(float x, float y) => GamePlane.PlaneDirToWorld(new Vector2(x, y));

        /// <summary>A shooter root at <paramref name="position"/> with the weapon mounted as a child, nose along the plane's forward axis.</summary>
        private T MountWeapon<T>(string prefabPath, Vector3 position, out MovingShooter shooter) where T : WeaponComponent
        {
            var ship = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.SetPositionAndRotation(position, GamePlane.Rotation);
            shooter = ship.AddComponent<MovingShooter>();
            return Mount<T>(prefabPath, ship);
        }

        private T Mount<T>(string prefabPath, GameObject ship) where T : WeaponComponent
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<T>(prefabPath);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {prefabPath}");
            var weapon = Object.Instantiate(prefab, ship.transform);
            spawned.Add(weapon.gameObject);
            return weapon;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }

        private Grenades MountWeapon(out MovingShooter shooter) =>
            MountWeapon<Grenades>(GrenadesPrefabPath, Vector3.zero, out shooter);

        private Grenades MountWeapon(Vector3 position) =>
            MountWeapon<Grenades>(GrenadesPrefabPath, position, out _);

        private DamageRecorder CreateTarget(Vector3 position, string name = "WaveTarget")
        {
            var go = new GameObject(name) { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = 0.5f;
            return go.AddComponent<DamageRecorder>();
        }

        private static Rigidbody AddBody(Component target)
        {
            var body = target.gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            return body;
        }

        private static ConcussionWave FindActiveWave()
        {
            var waves = Object.FindObjectsByType<ConcussionWave>(FindObjectsSortMode.None);
            return waves.Length > 0 ? waves[0] : null;
        }

        private static IEnumerator StepUntilGone(Component charge, int maxSteps)
        {
            for (var i = 0; i < maxSteps && charge.gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();
        }

        private static IEnumerator SweepFullWave()
        {
            var wave = FindActiveWave();
            Assert.IsNotNull(wave);
            var steps = Mathf.CeilToInt(wave.MaxRadius / 20f / Time.fixedDeltaTime) + 4;
            for (var i = 0; i < steps; i++)
                yield return new WaitForFixedUpdate();
        }

        [Test]
        public void EquippingTheWeapon_WarmsPoolsWithoutAPhantomBurst()
        {
            MountWeapon(out _);

            Assert.IsNull(FindActiveWave(), "Pool warmup must not leave an active wave.");
            Assert.AreEqual(0, Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None).Length,
                "Pool warmup must not fire the detonation burst (it activates the pooled wave once).");
        }

        [Test]
        public void Launch_HeadsStraightAtTheTargetPoint_WithoutTheShooterVelocity()
        {
            var weapon = MountWeapon(out var shooter);
            shooter.Velocity = Plane(0f, 10f);

            var grenade = weapon.Fire(weapon.firePoint.position + Plane(15f, 0f), Projectiles) as Grenade;

            Assert.IsNotNull(grenade, "Firing releases a charge.");
            var velocity = grenade.GetComponent<Rigidbody>().linearVelocity;
            Assert.Greater(Vector3.Dot(velocity, Plane(1f, 0f)), 1f, "The charge launches toward the target point, any direction.");
            Assert.AreEqual(0f, Vector3.Dot(velocity, Plane(0f, 1f)), 0.001f, "The shooter's velocity is not inherited.");
        }

        [UnityTest]
        public IEnumerator Charge_ComesToRestOnTheTargetPoint_AndDetonatesThere()
        {
            var weapon = MountWeapon(out _);
            var point = weapon.firePoint.position + Plane(-12f, 9f);
            var grenade = weapon.Fire(point, Projectiles) as Grenade;
            Vector3? detonatedAt = null;
            grenade.OnDetonated += at => detonatedAt = at;

            yield return StepUntilGone(grenade, 200);

            Assert.IsTrue(detonatedAt.HasValue, "The charge detonates on arrival — there is no fuse.");
            Assert.Less(Vector3.Distance(detonatedAt.Value, point), 0.1f, "The charge brakes to rest on its target point.");
            Assert.IsNotNull(FindActiveWave(), "Arrival spawns the wave.");
        }

        [Test]
        public void TargetPointPastMaxRange_IsClampedAlongTheLine_AndStillFires()
        {
            var weapon = MountWeapon(out _);
            var origin = weapon.firePoint.position;

            var grenade = weapon.Fire(origin + Plane(3f, 4f) * 100f, Projectiles) as Grenade;

            Assert.IsNotNull(grenade, "A shot past max range still fires.");
            var expected = origin + Plane(0.6f, 0.8f) * grenade.MaxDistance;
            Assert.Less(Vector3.Distance(grenade.TargetPoint, expected), 0.01f, "The point moves to max range along the same line.");
        }

        [UnityTest]
        public IEnumerator ContactBeforeArrival_Detonates()
        {
            var weapon = MountWeapon(out _);
            var origin = weapon.firePoint.position;
            var blocker = CreateTarget(origin + Plane(0f, 5f), "Blocker");

            var grenade = weapon.Fire(origin + Plane(0f, 20f), Projectiles) as Grenade;
            Vector3? detonatedAt = null;
            grenade.OnDetonated += at => detonatedAt = at;
            yield return StepUntilGone(grenade, 60);

            Assert.IsTrue(detonatedAt.HasValue, "Contact detonated the charge.");
            Assert.Less(Vector3.Distance(detonatedAt.Value, blocker.transform.position), 1.5f,
                "It blew on the blocker, short of its target point.");
        }

        [UnityTest]
        public IEnumerator OwnerHull_DoesNotDetonateTheCharge()
        {
            var weapon = MountWeapon(out var shooter);
            AddBody(shooter).isKinematic = true;
            shooter.gameObject.AddComponent<SphereCollider>().radius = 3f;
            shooter.gameObject.AddComponent<DamageRecorder>();

            var grenade = weapon.Fire(weapon.firePoint.position + Plane(0f, 20f), Projectiles) as Grenade;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(grenade.gameObject.activeSelf, "The charge leaves through its owner's hull without detonating.");
            Assert.IsNull(FindActiveWave());
        }

        [UnityTest]
        public IEnumerator OwnerFire_DetonatesTheCharge()
        {
            var weapon = MountWeapon(out var shooter);
            var lasers = Mount<Lasers>(LasersPrefabPath, shooter.gameObject);
            var heading = Plane(0f, 1f);

            var grenade = weapon.Fire(weapon.firePoint.position + heading * 20f, Projectiles) as Grenade;
            var bolt = lasers.Fire(weapon.firePoint.position + heading * 20f, Projectiles);
            Assert.IsNotNull(bolt, "The owner's laser fired.");

            // Park the owner's own bolt in the charge's path; a closing bolt would tunnel past it between steps.
            var boltBody = bolt.GetComponent<Rigidbody>();
            var boltSpot = grenade.transform.position + heading * 2f;
            bolt.transform.position = boltSpot;
            boltBody.position = boltSpot;
            boltBody.linearVelocity = Vector3.zero;

            yield return StepUntilGone(grenade, 10);

            Assert.IsFalse(grenade.gameObject.activeSelf, "The owner's own fire pops the charge early.");
            Assert.IsFalse(bolt.gameObject.activeSelf, "The bolt hit the charge rather than passing through it.");
            Assert.IsNotNull(FindActiveWave(), "Popping it still makes the full wave.");
        }

        [Test]
        public void Grenade_Shot_DetonatesImmediately()
        {
            var weapon = MountWeapon(out _);
            var grenade = weapon.Fire(weapon.firePoint.position + Plane(0f, 20f), Projectiles) as Grenade;

            grenade.TakeDamage(Shot(grenade.transform.position));

            Assert.IsFalse(grenade.gameObject.activeSelf, "A shot charge detonates on the spot.");
            Assert.IsNotNull(FindActiveWave(), "The full wave still happens.");
        }

        [UnityTest]
        public IEnumerator Wave_HitsEverythingOnce_ShooterIncluded_WithRimFalloff()
        {
            var weapon = MountWeapon(out var shooter);
            var shooterRecorder = shooter.gameObject.AddComponent<DamageRecorder>();
            shooter.gameObject.AddComponent<SphereCollider>().radius = 0.5f;

            var origin = weapon.firePoint.position;
            var near = CreateTarget(origin + Plane(0f, 3f), "NearTarget");
            var far = CreateTarget(origin + Plane(0f, 9f), "FarTarget");

            var grenade = weapon.Fire(origin + Plane(0f, -20f), Projectiles) as Grenade;
            grenade.TakeDamage(Shot(origin));
            var wave = FindActiveWave();
            yield return SweepFullWave();

            Assert.Greater(near.TotalDamage, 0f, "The wave reached the near target.");
            Assert.Greater(far.TotalDamage, 0f, "The wave reached the far target.");
            Assert.Greater(near.TotalDamage, far.TotalDamage,
                "Damage falls off toward the rim: nearer targets are hit by a younger, stronger frontier.");
            Assert.Greater(shooterRecorder.TotalDamage, 0f,
                "No friendly exemption — the shooter eats their own wave when they fail to outrun it.");
            Assert.IsFalse(wave.gameObject.activeSelf, "The spent wave returned to the pool.");
        }

        [UnityTest]
        public IEnumerator Wave_SpinsTargets_AgainstTheirTurn_AndStillOnesToo()
        {
            var weapon = MountWeapon(Plane(60f, 60f));
            var origin = weapon.firePoint.position;
            var turning = AddBody(CreateTarget(origin + Plane(1.5f, 0f), "TurningTarget"));
            var still = AddBody(CreateTarget(origin + Plane(-1.5f, 0f), "StillTarget"));
            turning.angularVelocity = GamePlane.Normal * 1f;

            var grenade = weapon.Fire(origin + Plane(0f, -20f), Projectiles) as Grenade;
            grenade.TakeDamage(Shot(origin));
            yield return SweepFullWave();

            Assert.Less(Vector3.Dot(turning.angularVelocity, GamePlane.Normal), 0f,
                "The kick throws a turning ship back through its turn.");
            Assert.Greater(Mathf.Abs(Vector3.Dot(still.angularVelocity, GamePlane.Normal)), 1f,
                "A near-centre hit spins a still ship — the spin does not depend on the offset.");
        }

        [UnityTest]
        public IEnumerator Wave_ChainDetonatesAnotherCharge()
        {
            var weapon = MountWeapon(Plane(-60f, 60f));
            var origin = weapon.firePoint.position;

            var second = weapon.Fire(origin + Plane(0f, 20f), Projectiles) as Grenade;
            weapon.Reset();
            var first = weapon.Fire(origin + Plane(0f, -20f), Projectiles) as Grenade;
            first.TakeDamage(Shot(origin));

            yield return StepUntilGone(second, 20);

            Assert.IsFalse(second.gameObject.activeSelf, "The first wave swept the second charge and set it off.");
            Assert.AreEqual(2, Object.FindObjectsByType<ConcussionWave>(FindObjectsSortMode.None).Length,
                "Both charges produced waves.");
        }

        [UnityTest]
        public IEnumerator Wave_SweepsMoreTargetsThanTheQueryBuffer()
        {
            // 70 targets pins the query-regrow path: swept inner colliders would crowd a fixed 64-slot buffer.
            var weapon = MountWeapon(Plane(60f, -60f));
            var origin = weapon.firePoint.position;

            const int targetCount = 70;
            var targets = new List<DamageRecorder>(targetCount);
            for (var i = 0; i < targetCount; i++)
            {
                var angle = i * Mathf.PI * 2f / targetCount;
                var ring = 2f + i % 8;
                targets.Add(CreateTarget(origin + Plane(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring), $"SwarmTarget{i}"));
            }

            var grenade = weapon.Fire(origin + Plane(0f, 20f), Projectiles) as Grenade;
            grenade.TakeDamage(Shot(origin));
            yield return SweepFullWave();

            for (var i = 0; i < targetCount; i++)
                Assert.Greater(targets[i].TotalDamage, 0f,
                    $"Target {i} was starved out of the sweep — every target inside the wave must be hit.");
        }

        [Test]
        public void Detonation_CascadesTheWaveIntoTheProjectileTracker_AndFlushReturnsIt()
        {
            var weapon = MountWeapon(out _);

            var grenade = weapon.Fire(weapon.firePoint.position + Plane(0f, 20f), Projectiles) as Grenade;
            Assert.AreEqual(1, Projectiles.ActiveCount, "the fired charge registers");

            grenade.TakeDamage(Shot(grenade.transform.position));
            var wave = FindActiveWave();
            Assert.IsNotNull(wave);
            Assert.AreEqual(1, Projectiles.ActiveCount,
                "the detonated charge deregisters and its announced wave registers in its place");

            Projectiles.ReturnAllToPool();
            Assert.AreEqual(0, Projectiles.ActiveCount);
            Assert.IsFalse(wave.gameObject.activeSelf, "the flush returned the mid-sweep wave to its pool");
        }
    }
}
