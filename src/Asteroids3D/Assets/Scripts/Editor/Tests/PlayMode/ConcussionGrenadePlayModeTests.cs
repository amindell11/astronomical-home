using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectile;
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
            foreach (var wave in Object.FindObjectsByType<ConcussionWave>(FindObjectsSortMode.None))
                Object.DestroyImmediate(wave.gameObject);

            foreach (var proj in Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None))
                Object.DestroyImmediate(proj.gameObject);

            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();

            base.TearDown();
        }

        private sealed class MovingShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity { get; set; }
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public float TotalDamage { get; private set; }
            public GameObject LastAttacker { get; private set; }

            public void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker)
            {
                TotalDamage += damage;
                LastAttacker = attacker;
            }
        }

        /// <summary>A shooter root with the weapon mounted as a child, nose along world +Z (in-plane).</summary>
        private Grenades MountWeapon(out MovingShooter shooter)
        {
            var ship = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
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

        // ── Pool warmup ──

        [Test]
        public void EquippingTheWeapon_WarmsPoolsWithoutAPhantomBurst()
        {
            MountWeapon(out _);

            Assert.IsNull(FindActiveWave(), "Pool warmup must not leave an active wave.");
            Assert.AreEqual(0, Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None).Length,
                "Pool warmup must not fire the detonation burst (it activates the pooled wave once).");
        }

        // ── Drop ──

        [Test]
        public void Grenade_DropsBackward_FromTheShooterVelocity()
        {
            var weapon = MountWeapon(out var shooter);
            shooter.Velocity = new Vector3(0f, 0f, 10f);

            var grenade = weapon.Fire() as Grenade;

            Assert.IsNotNull(grenade, "Firing releases a charge.");
            var velocity = grenade.GetComponent<Rigidbody>().linearVelocity;
            var expected = new Vector3(0f, 0f, 10f - 3f);
            Assert.Less((velocity - expected).magnitude, 0.01f,
                "The charge inherits the shooter's velocity minus the backward push.");
        }

        // ── Detonation paths ──

        [UnityTest]
        public IEnumerator Grenade_FuseExpiry_SpawnsTheWave()
        {
            var weapon = MountWeapon(out _);
            var grenade = weapon.Fire() as Grenade;
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
            var grenade = weapon.Fire() as Grenade;

            grenade.TakeDamage(1f, 0.1f, Vector3.zero, grenade.transform.position, null);

            Assert.IsFalse(grenade.gameObject.activeSelf, "A shot charge detonates on the spot.");
            Assert.IsNotNull(FindActiveWave(), "The full wave still happens.");
        }

        // ── Wave behavior ──

        [UnityTest]
        public IEnumerator Wave_HitsEverythingOnce_ShooterIncluded_WithRimFalloff()
        {
            var weapon = MountWeapon(out _);
            var shooterRecorder = weapon.transform.root.gameObject.AddComponent<DamageRecorder>();
            weapon.transform.root.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
            weapon.transform.root.position = new Vector3(2f, 0f, 0f);

            var origin = Vector3.zero;
            var near = CreateTarget(origin + new Vector3(0f, 0f, 3f), "NearTarget");
            var far = CreateTarget(origin + new Vector3(0f, 0f, 9f), "FarTarget");

            var grenade = weapon.Fire() as Grenade;
            grenade.transform.position = origin;
            grenade.TakeDamage(1f, 0.1f, Vector3.zero, origin, null);

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

            var first = weapon.Fire() as Grenade;
            first.transform.position = Vector3.zero;
            weapon.Reset();
            var second = weapon.Fire() as Grenade;
            second.transform.position = new Vector3(0f, 0f, 4f);
            second.Configure(fuseSeconds: 999f, armingSeconds: 0f);

            first.TakeDamage(1f, 0.1f, Vector3.zero, Vector3.zero, null);

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
            // Swept inner colliders stay inside the growing sphere; with a fixed 64-slot query
            // they'd crowd out newly reached outer targets. 70 targets pins the regrow path.
            var weapon = MountWeapon(out _);
            weapon.transform.root.position = new Vector3(80f, 0f, 80f);

            const int targetCount = 70;
            var targets = new List<DamageRecorder>(targetCount);
            for (var i = 0; i < targetCount; i++)
            {
                var angle = i * Mathf.PI * 2f / targetCount;
                var ring = 2f + i % 8;
                targets.Add(CreateTarget(
                    new Vector3(Mathf.Cos(angle) * ring, 0f, Mathf.Sin(angle) * ring), $"SwarmTarget{i}"));
            }

            var grenade = weapon.Fire() as Grenade;
            grenade.transform.position = Vector3.zero;
            grenade.TakeDamage(1f, 0.1f, Vector3.zero, Vector3.zero, null);

            var wave = FindActiveWave();
            Assert.IsNotNull(wave);
            var steps = Mathf.CeilToInt(wave.MaxRadius / 20f / Time.fixedDeltaTime) + 4;
            for (var i = 0; i < steps; i++)
                yield return new WaitForFixedUpdate();

            for (var i = 0; i < targetCount; i++)
                Assert.Greater(targets[i].TotalDamage, 0f,
                    $"Target {i} was starved out of the sweep — every target inside the wave must be hit.");
        }

        [UnityTest]
        public IEnumerator Grenade_ContactBeforeArming_DoesNotDetonate()
        {
            var weapon = MountWeapon(out _);
            var grenade = weapon.Fire() as Grenade;
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
