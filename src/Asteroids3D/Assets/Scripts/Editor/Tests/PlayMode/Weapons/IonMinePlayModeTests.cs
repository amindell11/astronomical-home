#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Ships;
using Ships.Status;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    [Category("Weapons")]
    public class IonMinePlayModeTests : PlayModeWorldFixture
    {
        private const string PrefabPath = "Assets/Prefabs/Weapons/IonMines.prefab";

        private readonly List<Ship> ships = new();

        public override void TearDown()
        {
            foreach (var ship in ships)
                if (ship) Object.DestroyImmediate(ship.gameObject);
            ships.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Drop_FromAMovingCarrier_SitsBehindTheHull_AtRest()
        {
            var (carrier, mines) = Arm(moving: true);
            var forward = mines.FireDirection;
            Assert.That(Vector3.Dot(forward, carrier.transform.up), Is.GreaterThan(0.99f));
            carrier.Rigidbody.linearVelocity = forward * 12f;
            var origin = carrier.transform.position;
            var mine = Fire(mines);
            var body = mine.GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            Assert.That(Vector3.Distance(mine.transform.position, Drop(carrier, mines, forward)), Is.LessThan(0.05f));
            Assert.That(Vector3.Distance(origin, mine.transform.position), Is.GreaterThan(carrier.ShipRadius));
            Assert.That(Vector3.Dot(mine.transform.position - origin, forward), Is.LessThan(0f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.01f));
            var dropped = mine.transform.position;
            for (var i = 0; i < 8; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(mine.transform.position, dropped), Is.LessThan(0.05f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(carrier.transform.position - origin, forward), Is.GreaterThan(0.2f));
        }

        [Test]
        public void Drop_WithTheMuzzleAimedBackward_StillSitsBehindTheHull()
        {
            var (carrier, mines) = Arm();
            var heading = carrier.transform.up;
            mines.transform.rotation = Quaternion.AngleAxis(180f, GamePlane.Normal) * mines.transform.rotation;
            Assert.That(Vector3.Dot(mines.FireDirection, heading), Is.LessThan(-0.99f));
            var mine = Fire(mines);
            Assert.That(Vector3.Distance(mine.transform.position, Drop(carrier, mines, heading)), Is.LessThan(0.05f));
        }

        [Test]
        public void DamageMods_DoNotFitTheMine()
        {
            var (_, mines) = Arm();
            Assert.IsFalse(mines.Supports(Combat.Weapons.Mods.WeaponModField.Damage));
        }

        [UnityTest]
        public IEnumerator SecondFire_WhileTheMineIsLive_SpendsNothing_AndFiresAgainOnceItIsGone()
        {
            var (_, mines) = Arm();
            var before = mines.Rounds.AmmoCount;
            var mine = Fire(mines);
            Assert.That(mines.Rounds.AmmoCount, Is.EqualTo(before - 1));
            Assert.IsNull(mines.Fire(Vector3.zero, Projectiles));
            Assert.That(mines.Rounds.AmmoCount, Is.EqualTo(before - 1));
            var cooldown = mines.GetComponent<Cooldown>();
            var budget = Mathf.CeilToInt((mine.FuseTime + cooldown.SecondsBetweenShots) / Time.fixedDeltaTime) + 5;
            yield return Until(() => !mine.gameObject.activeSelf, budget);
            Assert.IsFalse(mine.gameObject.activeSelf);
            yield return Until(() => mines.CanFire(), budget);
            Assert.IsNotNull(mines.Fire(Vector3.zero, Projectiles));
        }

        [UnityTest]
        public IEnumerator Proximity_WaitsUntilArmed_ThenStallsTheEnemy()
        {
            var (carrier, mines) = Arm();
            var forward = mines.FireDirection;
            var spec = mines.projectilePrefab;
            var enemy = Ship(1, Drop(carrier, mines, forward) + Beside(carrier, spec));
            Physics.SyncTransforms();
            var mine = Fire(mines);
            for (var i = 0; i < StepsBefore(spec.ArmingTime); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(mine.gameObject.activeSelf, "an enemy in proximity before arming does not set it off");
            Assert.IsFalse(enemy.Status.Has(StatusEffect.Stalled));
            for (var i = 0; i < 3 && mine.gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(mine.gameObject.activeSelf);
            yield return Until(() => enemy.Status.Has(StatusEffect.Stalled), 50);
            Assert.IsTrue(enemy.Status.Has(StatusEffect.Stalled), "the blast front reaches it");
        }

        [UnityTest]
        public IEnumerator Fuse_GoesOffWithNoEnemyNear()
        {
            var (_, mines) = Arm();
            var mine = Fire(mines);
            for (var i = 0; i < StepsBefore(mine.FuseTime); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(mine.gameObject.activeSelf, "it waits out the fuse");
            for (var i = 0; i < 3 && mine.gameObject.activeSelf; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(mine.gameObject.activeSelf, "it goes off at the fuse");
        }

        [UnityTest]
        public IEnumerator Blast_StallsEnemiesOnly_AndDealsNoDamage()
        {
            var (carrier, mines) = Arm();
            var forward = mines.FireDirection;
            var spec = mines.projectilePrefab;
            var drop = Drop(carrier, mines, forward);
            var beside = Beside(carrier, spec);
            Assert.That(Vector3.Distance(drop, carrier.transform.position), Is.LessThan(mines.BlastRadius));
            Assert.That(beside.magnitude, Is.LessThan(mines.BlastRadius));
            var enemy = Ship(1, drop + beside);
            var ally = Ship(0, drop - beside);
            Physics.SyncTransforms();
            var enemyHealth = enemy.Damage.Health.CurrentValue;
            var enemyShield = enemy.Damage.Shield.CurrentValue;
            var allyHealth = ally.Damage.Health.CurrentValue;
            IonBlast blast = null;
            var mine = Fire(mines);
            mine.Spawned += (spawned, _) => blast = spawned as IonBlast;
            yield return Until(() => blast && !blast.gameObject.activeSelf, 600);
            Assert.That(enemy.Status.Has(StatusEffect.Stalled), Is.True);
            Assert.That(carrier.Status.Has(StatusEffect.Stalled), Is.False);
            Assert.That(ally.Status.Has(StatusEffect.Stalled), Is.False);
            Assert.That(enemy.Damage.Health.CurrentValue, Is.EqualTo(enemyHealth));
            Assert.That(enemy.Damage.Shield.CurrentValue, Is.EqualTo(enemyShield));
            Assert.That(ally.Damage.Health.CurrentValue, Is.EqualTo(allyHealth));
        }

        [UnityTest]
        public IEnumerator StalledShield_DoesNotRegenerate_UntilTheStallEnds()
        {
            var (carrier, mines) = Arm();
            var enemy = Ship(1, Drop(carrier, mines, mines.FireDirection) + Beside(carrier, mines.projectilePrefab));
            Physics.SyncTransforms();
            var shield = enemy.Damage.Shield;
            Assert.That(shield.MaxValue, Is.GreaterThan(0f));
            Assert.That(enemy.Damage.shieldRegenRate, Is.GreaterThan(0f));
            shield.ApplyDamage(shield.MaxValue * 0.8f);
            var damaged = shield.CurrentValue;
            var cap = Mathf.CeilToInt((enemy.Damage.shieldRegenDelay + 1f) / Time.fixedDeltaTime) + 10;
            for (var i = 0; i < cap && shield.CurrentValue <= damaged + 0.01f; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(shield.CurrentValue, Is.GreaterThan(damaged));
            Fire(mines);
            for (var i = 0; i < cap && !enemy.Status.Has(StatusEffect.Stalled); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(enemy.Status.Has(StatusEffect.Stalled));
            var held = shield.CurrentValue;
            var stallSteps = Mathf.Max(1, Mathf.FloorToInt(enemy.Status.Remaining(StatusEffect.Stalled) / Time.fixedDeltaTime) - 2);
            for (var i = 0; i < stallSteps; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(shield.IsRegenerating, Is.False);
                Assert.That(shield.CurrentValue, Is.EqualTo(held).Within(0.001f));
            }
            for (var i = 0; i < cap && enemy.Status.Has(StatusEffect.Stalled); i++)
                yield return new WaitForFixedUpdate();
            Assert.IsFalse(enemy.Status.Has(StatusEffect.Stalled));
            var paused = shield.CurrentValue;
            for (var i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(shield.CurrentValue, Is.GreaterThan(paused));
        }

        [UnityTest]
        public IEnumerator RepeatStall_WithinTheWindow_IsShorter()
        {
            var (carrier, mines) = Arm();
            var spec = mines.projectilePrefab;
            var blast = spec.BlastPrefab;
            var cooldown = mines.GetComponent<Cooldown>().SecondsBetweenShots;
            Assert.That(Mathf.Max(blast.StallSeconds, cooldown) + spec.ArmingTime, Is.LessThan(blast.RepeatWindow));
            var enemy = Ship(1, Drop(carrier, mines, mines.FireDirection) + Beside(carrier, spec));
            Physics.SyncTransforms();
            Fire(mines);
            yield return Until(() => enemy.Status.Has(StatusEffect.Stalled), 300);
            Assert.That(enemy.Status.Remaining(StatusEffect.Stalled), Is.EqualTo(blast.StallSeconds).Within(0.15f));
            yield return Until(() => !enemy.Status.Has(StatusEffect.Stalled), 400);
            yield return Until(() => mines.CanFire(), 400);
            Fire(mines);
            yield return Until(() => enemy.Status.Has(StatusEffect.Stalled), 300);
            Assert.That(enemy.Status.Remaining(StatusEffect.Stalled),
                Is.EqualTo(blast.StallSeconds * blast.RepeatScale).Within(0.15f));
        }

        private (Ship carrier, IonMines mines) Arm(bool moving = false)
        {
            var carrier = Ship(0, Vector3.zero, moving);
            return (carrier, Mount(carrier));
        }

        private Ship Ship(int team, Vector3 at, bool moving = false)
        {
            var ship = ShipTestFactory.CreateShip(TestAssets.LoadCrimsonPrefab(), null, Projectiles, team, at, GamePlane.Rotation);
            ships.Add(ship);
            ship.Rigidbody.interpolation = RigidbodyInterpolation.None;
            ship.Movement.enabled = false;
            if (moving) return ship;
            ship.Rigidbody.linearVelocity = Vector3.zero;
            ship.Rigidbody.constraints |= RigidbodyConstraints.FreezePosition;
            return ship;
        }

        private static IonMines Mount(Ship carrier)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<IonMines>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var mines = Object.Instantiate(prefab, carrier.transform);
            mines.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(mines.projectilePrefab);
            Assert.IsNotNull(mines.projectilePrefab.BlastPrefab);
            Assert.IsNotNull(mines.Rounds);
            return mines;
        }

        private IonMine Fire(IonMines mines)
        {
            var mine = (IonMine)mines.Fire(Vector3.zero, Projectiles);
            Assert.IsNotNull(mine);
            return mine;
        }

        private static Vector3 Drop(Ship carrier, IonMines mines, Vector3 forward) =>
            GamePlane.WorldPointOnPlane(carrier.transform.position - forward * (carrier.ShipRadius + mines.projectilePrefab.DropGap));

        private static Vector3 Beside(Ship carrier, IonMine spec)
        {
            var gap = carrier.ShipRadius + 1f;
            var distance = gap < spec.ProximityRadius ? gap : spec.ProximityRadius * 0.5f;
            return GamePlane.Right * distance;
        }

        private static int StepsBefore(float seconds) =>
            Mathf.FloorToInt(seconds / Time.fixedDeltaTime - 0.001f);

        private static IEnumerator Until(Func<bool> done, int maxSteps)
        {
            for (var i = 0; i < maxSteps && !done(); i++)
                yield return new WaitForFixedUpdate();
        }
    }
}
#endif
