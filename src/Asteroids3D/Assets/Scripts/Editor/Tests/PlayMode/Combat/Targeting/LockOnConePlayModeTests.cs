using System.Collections;
using Combat.Targeting;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Registry;
using Ships.Status;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [Category("Targeting")]
    public class LockOnConePlayModeTests : PlayModeWorldFixture
    {
        private const string VanguardPrefabPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const float TargetRange = 3f;
        private const float TargetBearingDeg = 25f;

        private ShipRegistry registry;
        private Ship shooter;
        private Ship target;

        public override void SetUp()
        {
            base.SetUp();
            registry = new ShipRegistry();
        }

        public override void TearDown()
        {
            DestroyTestObject(shooter);
            DestroyTestObject(target);
            registry.Dispose();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TargetInConeFromShipCentre_StartsLock_ThoughOutsideItFromMount()
        {
            var sensor = SpawnInCone(shooterTeam: 0, targetTeam: 1);
            var halfCone = sensor.lockOnConeAngle / 2f;
            Assert.Greater(Vector3.Angle(shooter.transform.up, target.transform.position - sensor.GetComponent<WeaponComponent>().FireOrigin), halfCone,
                "test premise: measured from the missile's fire point, the target sits outside the lock-on cone");

            sensor.SetRegistry(registry);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(LockState.Locking, sensor.State,
                "a target inside the cone measured from the ship's centre starts a lock and holds it");
        }

        [UnityTest]
        public IEnumerator SameTeamShipInCone_NeverStartsLock()
        {
            var sensor = SpawnInCone(shooterTeam: 1, targetTeam: 1);

            sensor.SetRegistry(registry);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(LockState.Idle, sensor.State, "a wingmate in cone and range is never a lock candidate");
        }

        [UnityTest]
        public IEnumerator AJamOrStun_DropsALockInProgress_AndHoldsOffANewOne()
        {
            var sensor = SpawnInCone(shooterTeam: 0, targetTeam: 1);
            sensor.SetRegistry(registry);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(LockState.Locking, sensor.State, "test premise: a lock is in progress");

            shooter.Status.Apply(StatusEffect.WeaponsJammed, 5f);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(LockState.Idle, sensor.State, "the jam drops the lock in progress");

            for (var i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(LockState.Idle, sensor.State, "no new lock starts while jammed");
        }

        [UnityTest]
        public IEnumerator AStun_DropsAnAcquiredLock()
        {
            var sensor = SpawnInCone(shooterTeam: 0, targetTeam: 1);
            sensor.SetRegistry(registry);
            for (var i = 0; i < 200 && sensor.State != LockState.Locked; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(LockState.Locked, sensor.State, "test premise: the lock is acquired");

            shooter.Status.Apply(StatusEffect.Stunned, 5f);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(LockState.Idle, sensor.State, "the stun drops the acquired lock");
            Assert.IsNull(sensor.CurrentTarget);
        }

        [UnityTest]
        public IEnumerator PlayerLock_HoldsThroughLeavingTheCone_ForItsGrace()
        {
            var sensor = SpawnPlayerInCone();
            sensor.SetRegistry(registry);
            yield return UntilLocked(sensor);
            Assert.AreEqual(LockState.Locked, sensor.State, "test premise: the lock is acquired");

            PlaceTargetOutsideTheCone(sensor);
            yield return Advance(0.5f);
            Assert.AreEqual(LockState.Locked, sensor.State, "a player lock holds through the first half-second outside the cone");

            yield return Advance(0.7f);
            Assert.AreEqual(LockState.Idle, sensor.State, "a player lock drops once its grace has run out");
        }

        [UnityTest]
        public IEnumerator EnemyLock_DropsAsSoonAsTheTargetLeavesTheCone()
        {
            var sensor = SpawnInCone(shooterTeam: 0, targetTeam: 1);
            sensor.SetRegistry(registry);
            yield return UntilLocked(sensor);
            Assert.AreEqual(LockState.Locked, sensor.State, "test premise: the lock is acquired");

            PlaceTargetOutsideTheCone(sensor);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(LockState.Idle, sensor.State, "an enemy lock drops the moment the target leaves the cone");
        }

        [UnityTest]
        public IEnumerator PlayerLock_StealthStillBreaksItInsideTheGrace()
        {
            var sensor = SpawnPlayerInCone();
            sensor.SetRegistry(registry);
            yield return UntilLocked(sensor);
            Assert.AreEqual(LockState.Locked, sensor.State, "test premise: the lock is acquired");

            target.Lock.Conceal();
            Assert.AreEqual(LockState.Idle, sensor.State, "stealth breaks a player lock inside its grace");
        }

        private LockOnSensor SpawnPlayerInCone()
        {
            var sensor = SpawnInCone(0, 1, TestAssets.LoadPlayerPilot());
            Assert.IsNotNull(shooter.Commander, "test premise: the shooter has a commander");
            Assert.IsTrue(shooter.Commander.IsPlayer, "test premise: the shooter is player-commanded");
            // Its FixedUpdate reads input, and batch mode leaves that state null.
            shooter.Commander.enabled = false;
            return sensor;
        }

        private static IEnumerator UntilLocked(LockOnSensor sensor)
        {
            for (var i = 0; i < 200 && sensor.State != LockState.Locked; i++)
                yield return new WaitForFixedUpdate();
        }

        private void PlaceTargetOutsideTheCone(LockOnSensor sensor)
        {
            var outside = shooter.transform.position - shooter.transform.up * TargetRange;
            target.Rigidbody.position = outside;
            target.transform.position = outside;
            Physics.SyncTransforms();
            Assert.Greater(Vector3.Angle(shooter.transform.up, target.transform.position - shooter.transform.position),
                sensor.lockOnConeAngle / 2f, "test premise: the target sits outside the lock cone");
        }

        private static IEnumerator Advance(float seconds)
        {
            for (var t = 0f; t < seconds; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
        }

        private LockOnSensor SpawnInCone(int shooterTeam, int targetTeam) =>
            SpawnInCone(shooterTeam, targetTeam, null);

        private LockOnSensor SpawnInCone(int shooterTeam, int targetTeam, Commander commander)
        {
            var prefab = TestAssets.Load<Ship>(VanguardPrefabPath);
            Assert.IsNotNull(prefab, "Vanguard prefab loads");

            shooter = Factory.CreateShip(prefab, commander, team: shooterTeam, decisionSeed: 0, Projectiles, Vector3.zero, Quaternion.identity);
            var bearing = TargetBearingDeg * Mathf.Deg2Rad;
            var targetPos = shooter.transform.position
                + shooter.transform.up * (TargetRange * Mathf.Cos(bearing))
                + shooter.transform.right * (TargetRange * Mathf.Sin(bearing));
            target = Factory.CreateShip(prefab, null, team: targetTeam, decisionSeed: 0, Projectiles, targetPos, Quaternion.identity);
            registry.ActiveShips.Add(shooter);
            registry.ActiveShips.Add(target);

            var sensor = shooter.Targeting;
            Assert.IsNotNull(sensor, "Vanguard mounts missiles");
            return sensor;
        }
    }
}
