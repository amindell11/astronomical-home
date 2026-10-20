using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Damage;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    [Category("Weapons")]
    public class MissileShieldBypassPlayModeTests : PlayModeWorldFixture
    {
        private const string MissilePrefabPath = "Assets/Prefabs/Weapons/Projectiles/MissileProjectile.prefab";

        private const float TargetAlong = 2f;
        private const float TargetRadius = 0.5f;
        private const float BystanderRadius = 0.3f;
        private const float BystanderClearance = 0.1f;
        private const int MaxSteps = 50;

        // The missile quad's half-length (0.452): its centre stops this far short of contact.
        private const float MissileHalfLength = 0.45f;

        private static readonly Vector3 WorldAim = GamePlane.PlaneDirToWorld(Vector2.up);
        private static readonly Vector3 WorldAcross = GamePlane.PlaneDirToWorld(Vector2.right);

        private readonly List<GameObject> spawned = new();

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public readonly List<DamageInfo> Hits = new();
            public void TakeDamage(in DamageInfo hit) => Hits.Add(hit);
        }

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator DirectHit_ArrivesFlagged_SplashOnABystander_Unflagged()
        {
            var missile = LaunchMissile();
            var target = CreateTarget("Target", WorldAim * TargetAlong, TargetRadius);

            // Beside the missile's contact pose, just clear of its flight path, well inside the blast.
            var contactAlong = TargetAlong - TargetRadius - MissileHalfLength;
            var bystanderAcross = missile.hitboxRadius + BystanderClearance + BystanderRadius;
            Assume.That(missile.explosionRadius, Is.GreaterThan(missile.hitboxRadius + BystanderClearance),
                "the bystander sits inside the blast");
            var bystander = CreateTarget("Bystander",
                WorldAim * contactAlong + WorldAcross * bystanderAcross, BystanderRadius);

            for (var i = 0; i < MaxSteps && target.Hits.Count == 0; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(target.Hits, Has.Count.EqualTo(1), "the missile hit its target once");
            Assert.That(target.Hits[0].BypassesShield, Is.True, "the direct hit skips shields");
            Assert.That(target.Hits[0].Amount, Is.EqualTo(missile.Damage), "the direct hit carries the missile's damage");
            Assert.That(bystander.Hits, Has.Count.EqualTo(1), "the splash reached the bystander once");
            Assert.That(bystander.Hits[0].BypassesShield, Is.False, "the splash goes shield-first");
        }

        private Missile LaunchMissile()
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<Missile>(MissilePrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load {MissilePrefabPath}");

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            spawned.Add(shooter.gameObject);
            shooter.transform.position = -WorldAim * 20f;

            var missile = Object.Instantiate(prefab, Vector3.zero, GamePlane.PlanePose(GamePlane.Normal, WorldAim));
            spawned.Add(missile.gameObject);
            missile.Initialize(shooter);
            missile.Launch(WorldAim, WorldAim * TargetAlong);
            return missile;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }

        private DamageRecorder CreateTarget(string name, Vector3 position, float radius)
        {
            var go = new GameObject(name) { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = radius;
            return go.AddComponent<DamageRecorder>();
        }
    }
}
