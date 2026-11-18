using AI;
using Combat.Projectiles;
using Combat.Weapons;
using Movement;
using NUnit.Framework;
using Ships.Registry;
using Substrate;
using Substrate.Services.Projectiles;
using Tests.Common;
using UnityEngine;
using Utils;

namespace Tests.EditMode
{
    /// <summary>Pins the Gunner's firing-lane veto: a friendly ship nearest on the line to the aim holds the trigger; anything else leaves it to the weapon.</summary>
    [Category("AI")]
    public class GunnerFiringLaneEditModeTests
    {
        private const int ShooterTeam = 0;
        private const int OtherTeam = 1;
        private static readonly ShipId Shooter = new(1);
        private static readonly ShipId Bystander = new(2);
        private static readonly Vector2 AimPlane = new(0f, 10f);

        private sealed class EagerWeapon : WeaponComponent
        {
            public override bool HoldTrigger(TargetingContext context) => true;

            public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles) => null;
        }

        [Test]
        public void FriendlyBetweenFirePointAndAim_HoldsTheTrigger() =>
            Assert.IsFalse(HeldWithBystander(5f, ShooterTeam), "a friendly in the firing lane holds fire");

        [Test]
        public void HostileBetweenFirePointAndAim_LeavesItToTheWeapon() =>
            Assert.IsTrue(HeldWithBystander(5f, OtherTeam), "a hostile in the firing lane leaves the shot to the weapon");

        [Test]
        public void FriendlyBeyondTheAim_LeavesItToTheWeapon() =>
            Assert.IsTrue(HeldWithBystander(15f, ShooterTeam), "a friendly past the aim point is out of the firing lane");

        [Test]
        public void EmptyLane_LeavesItToTheWeapon() =>
            Assert.IsTrue(Held(new StubShipRegistry()), "an empty firing lane leaves the shot to the weapon");

        private static bool HeldWithBystander(float range, int team)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cube.layer = LayerIds.Ship;
                cube.transform.position = GamePlane.PlanePointToWorld(new Vector2(0f, range));
                var registry = new StubShipRegistry();
                registry.ColliderIds[cube.GetComponent<Collider>()] = Bystander;
                registry.Teams[Bystander] = team;
                return Held(registry);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        private static bool Held(StubShipRegistry registry)
        {
            registry.Teams[Shooter] = ShooterTeam;
            var host = new GameObject("Gunner");
            try
            {
                host.transform.position = GamePlane.PlanePointToWorld(Vector2.zero);
                Physics.SyncTransforms();
                var weapon = host.AddComponent<EagerWeapon>();
                Kinematics Pose() => new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
                var recorder = new GunnerTriggerStreamEditModeTests.CommandRecorder();
                var gunner = host.AddComponent<Gunner>();
                gunner.Initialize(
                    new GunnerTriggerStreamEditModeTests.PrimarySlotContext { sight = new Gunsight(weapon, Pose) },
                    recorder, Pose, TestAimSettings.Perfect, new SeedScope(1), Shooter, registry);
                gunner.Aim(AimPlane, Vector2.zero);

                gunner.Fire(engagePrimary: true, engageSecondary: false);

                return recorder.Commands[0].held;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
