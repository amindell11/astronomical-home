using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Damage;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class GuardDashPlayModeTests : PlayModeWorldFixture
    {
        private const string VanguardPrefabPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const string MissilePrefabPath = "Assets/Prefabs/Weapons/Projectiles/MissileProjectile.prefab";
        private const float TargetAlong = 4f;
        private const int MaxSteps = 60;

        private static readonly Vector3 WorldAim = GamePlane.PlaneDirToWorld(Vector2.up);

        private readonly List<GameObject> spawned = new();
        private Ship ship;

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public global::Ships.Registry.ShipId Id => global::Ships.Registry.ShipId.Invalid;
        }

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator MissileInsideTheWindow_AppliesNothing_AndIsReportedBlocked()
        {
            yield return SpawnVanguard();
            var guard = (GuardDash)ship.Keystone;
            var blocked = new List<DamageInfo>();
            ship.Damage.OnBlocked += blocked.Add;
            var health = ship.Damage.Health.CurrentValue;
            var shield = ship.Damage.Shield.CurrentValue;

            yield return Dash();
            Assert.That(guard.Guarding, Is.True);
            Hold();
            var missile = LaunchMissileAtShip();

            for (var i = 0; i < MaxSteps && blocked.Count == 0 && missile; i++)
            {
                Hold();
                yield return new WaitForFixedUpdate();
            }

            Assert.That(guard.Guarding, Is.True, "the missile detonated inside the window");
            Assert.That(blocked, Is.Not.Empty, "the detonation reached the ship and was dropped");
            Assert.That(ship.Damage.Health.CurrentValue, Is.EqualTo(health), "no hull damage inside the window");
            Assert.That(ship.Damage.Shield.CurrentValue, Is.EqualTo(shield), "no shield damage inside the window");
        }

        [UnityTest]
        public IEnumerator MissileAfterTheWindow_AppliesFully()
        {
            yield return SpawnVanguard();
            var guard = (GuardDash)ship.Keystone;
            var applied = new List<AppliedDamage>();
            ship.Damage.OnDamaged += applied.Add;

            yield return Dash();
            while (guard.Guarding)
                yield return new WaitForFixedUpdate();
            Hold();
            var health = ship.Damage.Health.CurrentValue;
            var missile = LaunchMissileAtShip();
            var direct = missile.Damage;

            for (var i = 0; i < MaxSteps && applied.Count == 0 && missile; i++)
            {
                Hold();
                yield return new WaitForFixedUpdate();
            }

            Assert.That(applied, Is.Not.Empty, "the detonation reached the ship");
            Assert.That(health - ship.Damage.Health.CurrentValue, Is.EqualTo(direct).Within(0.01f),
                "the direct hit lands in full, past the shield");
        }

        [UnityTest]
        public IEnumerator Dash_HoldsEveryHullSurfaceWhiteForTheWindow_ThenRestoresIt()
        {
            yield return SpawnVanguard();
            yield return null;
            var surfaces = ship.VisualRig.Silhouette;
            Assert.That(surfaces, Is.Not.Empty);
            var block = new MaterialPropertyBlock();
            var baseColors = new Color[surfaces.Count];
            for (var i = 0; i < surfaces.Count; i++)
            {
                surfaces[i].GetPropertyBlock(block);
                baseColors[i] = block.HasColor(BaseColor) ? block.GetColor(BaseColor) : surfaces[i].sharedMaterial.GetColor(BaseColor);
            }

            yield return Dash();
            yield return null;
            foreach (var surface in surfaces)
            {
                surface.GetPropertyBlock(block);
                Assert.That(block.GetFloat(DamageFlash), Is.EqualTo(1f), $"{surface.name}: full flash inside the window");
                Assert.That(block.GetColor(BaseColor), Is.EqualTo(Color.white), $"{surface.name}: the guard's flash is white");
            }

            var guard = (GuardDash)ship.Keystone;
            while (guard.Guarding)
                yield return new WaitForFixedUpdate();
            yield return null;

            for (var i = 0; i < surfaces.Count; i++)
            {
                surfaces[i].GetPropertyBlock(block);
                Assert.That(block.GetFloat(DamageFlash), Is.EqualTo(0f), $"{surfaces[i].name}: no flash after the window");
                Assert.That(block.GetColor(BaseColor), Is.EqualTo(baseColors[i]), $"{surfaces[i].name}: back to its base colour");
            }
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int DamageFlash = Shader.PropertyToID("_DamageFlash");

        private IEnumerator SpawnVanguard()
        {
            var prefab = TestAssets.Load<Ship>(VanguardPrefabPath);
            Assert.IsNotNull(prefab, VanguardPrefabPath);
            // Nose toward the missile's launch point, so the dash closes on it rather than outrunning it.
            ship = Object.Instantiate(prefab, WorldAim * TargetAlong, GamePlane.PlanePose(GamePlane.Normal, -WorldAim));
            ship.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            Assert.That(ship.Keystone, Is.InstanceOf<GuardDash>(), "Vanguard carries the Guard Dash");
        }

        private IEnumerator Dash()
        {
            ship.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(ship.Keystone.Activations, Is.EqualTo(1));
        }

        // Pins the ship at its spot so the missile's straight flight meets it.
        private void Hold()
        {
            ship.Movement.Drive(default);
            ship.Rigidbody.linearVelocity = Vector3.zero;
            ship.Rigidbody.position = WorldAim * TargetAlong;
        }

        private Missile LaunchMissileAtShip()
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<Missile>(MissilePrefabPath);
            Assert.IsNotNull(prefab, MissilePrefabPath);
            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            spawned.Add(shooter.gameObject);
            shooter.transform.position = -WorldAim * 20f;
            // The missile starts slow; launching half a unit off the hull keeps its flight inside the window.
            var shipCenter = WorldAim * TargetAlong;
            Assert.That(Physics.Raycast(shipCenter - WorldAim * 10f, WorldAim, out var surface, 20f, 1 << LayerIds.Ship),
                Is.True, "found the hull surface");
            var start = surface.point - WorldAim * 0.5f;
            var missile = Object.Instantiate(prefab, start, GamePlane.PlanePose(GamePlane.Normal, WorldAim));
            spawned.Add(missile.gameObject);
            missile.Initialize(shooter);
            missile.Launch(WorldAim, shipCenter);
            return missile;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }
    }
}
