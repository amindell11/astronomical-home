using System.Collections.Generic;
using Combat;
using Combat.Weapons;
using Movement;
using NUnit.Framework;
using UnityEngine;
using Substrate;

namespace Tests.EditMode
{
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

            public override Combat.Projectiles.ProjectileBase Fire(UnityEngine.Vector3 targetPoint, Substrate.Services.Projectiles.IProjectileService projectiles) => null;
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
