using System.Collections.Generic;
using Combat;
using Combat.Weapons;
using Game;
using Movement;
using NUnit.Framework;
using Ships.Damage;
using UnityEngine;
using Utils;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Damage")]
    public class RegenResourceResetEditModeTests
    {
        [Test]
        public void Reset_RestoresFreshRegenPhase()
        {
            var fresh = new RegenResource(100f, 10f, 3f);
            var reused = new RegenResource(100f, 10f, 3f);
            reused.ApplyDamage(40f);
            reused.Update(1.7f);
            reused.Reset();

            AssertFreshRegenBehavior(fresh);
            AssertFreshRegenBehavior(reused);
            Assert.AreEqual(fresh.CurrentValue, reused.CurrentValue, 1e-4f,
                "A reset resource must regenerate exactly like a freshly constructed one");
        }

        private static void AssertFreshRegenBehavior(RegenResource resource)
        {
            Assert.AreEqual(resource.MaxValue, resource.CurrentValue, 1e-4f, "Reset must refill");
            resource.ApplyDamage(50f);
            resource.Update(2.9f);
            Assert.AreEqual(50f, resource.CurrentValue, 1e-4f, "No regen inside the post-damage delay");
            resource.Update(0.2f);
            Assert.Greater(resource.CurrentValue, 50f, "Regen must start once the delay elapses");
        }
    }

    [TestFixture]
    [Category("Targeting")]
    public class GunsightObservationEditModeTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in createdObjects)
                if (go) Object.DestroyImmediate(go);
            createdObjects.Clear();
        }

        private sealed class LosProbeWeapon : WeaponComponent
        {
            public bool LastFireLos { get; private set; }

            public override bool InEnvelope(in TargetingContext context) => context.hasLineOfSight;

            public override bool ShouldFire(TargetingContext context)
            {
                LastFireLos = context.hasLineOfSight;
                return context.hasLineOfSight;
            }

            public override Combat.Projectile.ProjectileBase Fire(Game.Services.IProjectileService projectiles) => null;
        }

        [Test]
        public void InEnvelope_DoesNotPerturbTheFiringPathsLosCache()
        {
            var shooterGo = new GameObject("Shooter");
            createdObjects.Add(shooterGo);
            shooterGo.transform.position = GamePlane.PlanePointToWorld(Vector2.zero);
            var weapon = shooterGo.AddComponent<LosProbeWeapon>();

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(wall);
            wall.layer = LayerIds.Asteroid;
            wall.transform.position = GamePlane.PlanePointToWorld(new Vector2(0f, 5f));
            wall.transform.localScale = new Vector3(4f, 4f, 4f);
            Physics.SyncTransforms();

            var sight = new Gunsight(weapon, () => new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f));
            var fireTarget = GamePlane.PlanePointToWorld(new Vector2(0f, 10f));

            sight.Evaluate(fireTarget);
            Assert.IsFalse(weapon.LastFireLos, "Wall must block the primed firing-path LOS");

            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();

            // Observation queries at a different point; with a shared cache these would invalidate the firing cache and flip the next Evaluate to a fresh (clear) raycast.
            var observedTarget = GamePlane.PlanePointToWorld(new Vector2(0.5f, 12f));
            for (var i = 0; i < 5; i++)
                sight.InEnvelope(observedTarget);

            sight.Evaluate(fireTarget);
            Assert.IsFalse(weapon.LastFireLos,
                "Within the cache window, Evaluate must return its own cached LOS — observation queries must not perturb the firing path");
        }
    }
}
