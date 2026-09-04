using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Combat.Weapons;
using Damage;
using Game;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// The concussion grenade drops behind its shooter, detonates by fuse / armed contact /
    /// being shot, and its wave sweeps outward hitting everything once — the shooter included
    /// (no friendly exemption) — with damage falling off toward the rim.
    /// </summary>
    [Category("Weapons")]
    public class ConcussionGrenadePlayModeTests : PlayModeWorldFixture
    {
        private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";

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

        /// <summary>A shooter root with the weapon mounted as a child, nose along the plane's forward axis.</summary>
        private Grenades MountWeapon(out MovingShooter shooter)
        {
            var ship = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.rotation = GamePlane.Rotation;
            shooter = ship.AddComponent<MovingShooter>();

#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<Grenades>(GrenadesPrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {GrenadesPrefabPath}");
            var weapon = Object.Instantiate(prefab, ship.transform);
            spawned.Add(weapon.gameObject);
            return weapon;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }

        private DamageRecorder CreateTarget(Vector3 position, string name = "WaveTarget")
        {
            var go = new GameObject(name) { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = 0.5f;
            return go.AddComponent<DamageRecorder>();
        }

        private static ConcussionWave FindActiveWave()
        {
            var waves = Object.FindObjectsByType<ConcussionWave>(FindObjectsSortMode.None);
            return waves.Length > 0 ? waves[0] : null;
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
        public void Grenade_DropsBackward_FromTheShooterVelocity()
        {
            var weapon = MountWeapon(out var shooter);
            shooter.Velocity = GamePlane.PlaneDirToWorld(new Vector2(0f, 10f));

            var grenade = weapon.Fire(Projectiles) as Grenade;

            Assert.IsNotNull(grenade, "Firing releases a charge.");
            var velocity = grenade.GetComponent<Rigidbody>().linearVelocity;
            var expected = GamePlane.PlaneDirToWorld(new Vector2(0f, 10f - 3f));
            Assert.Less((velocity - expected).magnitude, 0.01f,
                "The charge inherits the shooter's velocity minus the backward push.");
        }

        [UnityTest]
        public IEnumerator Grenade_FuseExpiry_SpawnsTheWave()
        {
            var weapon = MountWeapon(out _);
            var grenade = weapon.Fire(Projectiles) as Grenade;
            grenade.Configure(fuseSeconds: Time.fixedDeltaTime * 2f, armingSeconds: 0f);

            for (var i = 0; i < 4; i++)
                yield return new WaitForFixedUpdate();

            Assert.IsFalse(grenade.gameObject.activeSelf, "The charge returned to the pool on detonation.");
            Assert.IsNotNull(FindActiveWave(), "Detonation spawned the concussion wave.");
        }

        [Test]
        public void Grenade_ShotBeforeTheFuse_DetonatesImmediately()
        {
            var weapon = MountWeapon(out _);
            var grenade = weapon.Fire(Projectiles) as Grenade;

            grenade.TakeDamage(Shot(grenade.transform.position));

            Assert.IsFalse(grenade.gameObject.activeSelf, "A shot charge detonates on the spot.");
            Assert.IsNotNull(FindActiveWave(), "The full wave still happens.");
        }

        [UnityTest]
        public IEnumerator Wave_HitsEverythingOnce_ShooterIncluded_WithRimFalloff()
        {
            var weapon = MountWeapon(out _);
            var shooterRecorder = weapon.transform.root.gameObject.AddComponent<DamageRecorder>();
            weapon.transform.root.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
            weapon.transform.root.position = new Vector3(2f, 0f, 0f);

            var origin = Vector3.zero;
            var near = CreateTarget(origin + GamePlane.PlaneDirToWorld(new Vector2(0f, 3f)), "NearTarget");
            var far = CreateTarget(origin + GamePlane.PlaneDirToWorld(new Vector2(0f, 9f)), "FarTarget");

            var grenade = weapon.Fire(Projectiles) as Grenade;
            grenade.transform.position = origin;
            grenade.TakeDamage(Shot(origin));

            var wave = FindActiveWave();
            Assert.IsNotNull(wave);
            var steps = Mathf.CeilToInt(wave.MaxRadius / 20f / Time.fixedDeltaTime) + 4;
            for (var i = 0; i < steps; i++)
                yield return new WaitForFixedUpdate();

            Assert.Greater(near.TotalDamage, 0f, "The wave reached the near target.");
            Assert.Greater(far.TotalDamage, 0f, "The wave reached the far target.");
            Assert.Greater(near.TotalDamage, far.TotalDamage,
                "Damage falls off toward the rim: nearer targets are hit by a younger, stronger frontier.");
            Assert.Greater(shooterRecorder.TotalDamage, 0f,
                "No friendly exemption — the shooter eats their own wave when they fail to outrun it.");
            Assert.IsFalse(wave.gameObject.activeSelf, "The spent wave returned to the pool.");
        }

        [UnityTest]
        public IEnumerator Wave_ChainDetonatesAnotherGrenade()
        {
            var weapon = MountWeapon(out _);
            weapon.transform.root.position = new Vector3(50f, 0f, 50f);

            var first = weapon.Fire(Projectiles) as Grenade;
            first.transform.position = Vector3.zero;
            weapon.Reset();
            var second = weapon.Fire(Projectiles) as Grenade;
            second.transform.position = GamePlane.PlanePointToWorld(new Vector2(0f, 4f));
            second.Configure(fuseSeconds: 999f, armingSeconds: 0f);

            first.TakeDamage(Shot(Vector3.zero));

            for (var i = 0; i < 20 && second.gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();

            Assert.IsFalse(second.gameObject.activeSelf,
                "The first wave swept the drifting second charge and set it off.");
            Assert.AreEqual(2, Object.FindObjectsByType<ConcussionWave>(FindObjectsSortMode.None).Length,
                "Both charges produced waves.");
        }

        [UnityTest]
        public IEnumerator Wave_SweepsMoreTargetsThanTheQueryBuffer()
        {
            // 70 targets pins the query-regrow path: swept inner colliders would crowd a fixed 64-slot buffer.
            var weapon = MountWeapon(out _);
            weapon.transform.root.position = new Vector3(80f, 0f, 80f);

            const int targetCount = 70;
            var targets = new List<DamageRecorder>(targetCount);
            for (var i = 0; i < targetCount; i++)
            {
                var angle = i * Mathf.PI * 2f / targetCount;
                var ring = 2f + i % 8;
                targets.Add(CreateTarget(
                    GamePlane.PlanePointToWorld(new Vector2(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring)), $"SwarmTarget{i}"));
            }

            var grenade = weapon.Fire(Projectiles) as Grenade;
            grenade.transform.position = Vector3.zero;
            grenade.TakeDamage(Shot(Vector3.zero));

            var wave = FindActiveWave();
            Assert.IsNotNull(wave);
            var steps = Mathf.CeilToInt(wave.MaxRadius / 20f / Time.fixedDeltaTime) + 4;
            for (var i = 0; i < steps; i++)
                yield return new WaitForFixedUpdate();

            for (var i = 0; i < targetCount; i++)
                Assert.Greater(targets[i].TotalDamage, 0f,
                    $"Target {i} was starved out of the sweep — every target inside the wave must be hit.");
        }

        [Test]
        public void Detonation_CascadesTheWaveIntoTheProjectileTracker_AndFlushReturnsIt()
        {
            var weapon = MountWeapon(out _);

            var grenade = weapon.Fire(Projectiles) as Grenade;
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

        [UnityTest]
        public IEnumerator Grenade_ContactBeforeArming_DoesNotDetonate()
        {
            var weapon = MountWeapon(out _);
            var grenade = weapon.Fire(Projectiles) as Grenade;
            grenade.Configure(fuseSeconds: 999f, armingSeconds: 999f);

            var bumper = CreateTarget(grenade.transform.position, "Bumper");
            bumper.gameObject.AddComponent<Rigidbody>().useGravity = false;

            for (var i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();

            Assert.IsTrue(grenade.gameObject.activeSelf, "An unarmed charge shrugs off contact.");
            Assert.IsNull(FindActiveWave(), "No wave before the fuse or arming window.");
        }
    }
}
