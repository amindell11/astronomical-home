#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    [Category("Weapons")]
    public class ScattergunPlayModeTests : PlayModeWorldFixture
    {
        private const string PrefabPath = "Assets/Prefabs/Weapons/Scattergun.prefab";

        private readonly List<GameObject> spawned = new();
        private float savedTimeScale;
        private float savedCaptureDelta;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            savedTimeScale = Time.timeScale;
            savedCaptureDelta = Time.captureDeltaTime;
        }

        [TearDown]
        public override void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.captureDeltaTime = savedCaptureDelta;
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [Test]
        public void OnePull_LaunchesEveryPelletInsideTheCone()
        {
            var gun = Spawn();
            var fires = 0;
            gun.OnFire += () => fires++;
            var before = gun.Rounds.AmmoCount;

            var first = gun.Fire(Ahead(gun), Projectiles);

            Assert.IsNotNull(first);
            Assert.AreEqual(1, fires);
            Assert.AreEqual(before - 1, gun.Rounds.AmmoCount);
            Assert.AreEqual(gun.Pellets, Projectiles.ActiveCount);

            var half = gun.SpreadDegrees * 0.5f;
            var directions = new List<Vector3>();
            Projectiles.ForEachLive(live =>
            {
                var body = live.GetComponent<Rigidbody>();
                Assert.IsNotNull(body, live.name);
                Assert.Greater(body.linearVelocity.sqrMagnitude, 0f, live.name);
                Assert.LessOrEqual(Vector3.Angle(body.linearVelocity, gun.FireDirection), half + 0.01f, live.name);
                directions.Add(body.linearVelocity);
            });

            Assert.AreEqual(gun.Pellets, directions.Count);
            if (directions.Count < 2) return;

            var widest = 0f;
            for (var i = 0; i < directions.Count; i++)
            for (var j = i + 1; j < directions.Count; j++)
                widest = Mathf.Max(widest, Vector3.Angle(directions[i], directions[j]));
            Assert.AreEqual(gun.SpreadDegrees, widest, 0.1f);
        }

        [UnityTest]
        public IEnumerator Pellets_ReturnToThePoolAtTheirRange()
        {
            var gun = Spawn();
            Assert.Greater(gun.ProjectileSpeed, 0f);
            Assert.Greater(gun.Range, 0f);
            Assert.IsNotNull(gun.Fire(Ahead(gun), Projectiles));
            Assert.AreEqual(gun.Pellets, Projectiles.ActiveCount);

            var halfFlight = 0.5f * gun.Range / gun.ProjectileSpeed;
            var bound = Mathf.CeilToInt(gun.Range / gun.ProjectileSpeed / Time.fixedDeltaTime) + 5;
            var liveShortOfRange = -1;
            for (var step = 1; step <= bound; step++)
            {
                yield return new WaitForFixedUpdate();
                if (liveShortOfRange >= 0 || step * Time.fixedDeltaTime < halfFlight) continue;

                liveShortOfRange = Projectiles.ActiveCount;
                Projectiles.ForEachLive(live =>
                {
                    var shot = live as ProjectileBase;
                    Assert.IsNotNull(shot, live.name);
                    Assert.Less(shot.DistanceTraveled, gun.Range * 0.75f, live.name);
                });
            }

            Assert.AreEqual(gun.Pellets, liveShortOfRange, "still in flight well short of range");
            Assert.AreEqual(0, Projectiles.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Magazine_EmptiesThenReloads()
        {
            // One Update per fixed step, so the magazine timer and the cooldown advance together.
            Time.timeScale = 1f;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            var gun = Spawn();
            var rounds = gun.Rounds;
            var cooldown = gun.GetComponent<Cooldown>();
            Assert.IsNotNull(rounds);
            Assert.IsNotNull(cooldown);
            Assert.Greater(rounds.MaxAmmo, 0);
            Assert.Greater(rounds.ReloadTime, 0f);
            Assert.AreEqual(Rounds.RefillMode.Magazine, rounds.Refill);
            var gapSteps = Mathf.CeilToInt(cooldown.SecondsBetweenShots / Time.fixedDeltaTime) + 1;

            for (var shot = 0; shot < rounds.MaxAmmo; shot++)
            {
                if (shot > 0)
                    yield return Until(() => gun.CanFire(), gapSteps);
                Assert.IsTrue(gun.CanFire(), $"pull {shot}");
                Assert.IsNotNull(gun.Fire(Ahead(gun), Projectiles), $"pull {shot}");
            }

            Assert.AreEqual(0, rounds.AmmoCount);
            Assert.IsFalse(rounds.CanFire());
            Assert.IsTrue(rounds.IsReloading, "an empty magazine starts its own reload");
            Assert.IsNull(gun.Fire(Ahead(gun), Projectiles));
            Assert.AreEqual(0, rounds.AmmoCount);

            var reloadSteps = Mathf.CeilToInt((rounds.ReloadTime + cooldown.SecondsBetweenShots) / Time.fixedDeltaTime) + 3;
            yield return Until(() => rounds.AmmoCount == rounds.MaxAmmo && gun.CanFire(), reloadSteps);
            Assert.IsFalse(rounds.IsReloading);
            Assert.AreEqual(rounds.MaxAmmo, rounds.AmmoCount);
            Assert.IsTrue(gun.CanFire());
            Assert.IsNotNull(gun.Fire(Ahead(gun), Projectiles));
        }

        [Test]
        public void AiShip_MountsAndFiresIt()
        {
            var prefab = TestAssets.Load<Scatterguns>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var ship = Object.Instantiate(TestAssets.LoadCrimsonPrefab(), Vector3.zero, GamePlane.Rotation);
            spawned.Add(ship.gameObject);
            ship.Initialize(0, 0, Projectiles);
            ship.Reequip(prefab, null);

            var primary = (Scatterguns)ship.Weapons.Primary;
            var sight = ship.Weapons.Context.Sight(WeaponSlot.Primary);
            Assert.IsNotNull(sight);
            var near = primary.FireOrigin + primary.FireDirection * 8f;
            var far = primary.FireOrigin + primary.FireDirection * (primary.FireRange + 5f);
            Assert.IsTrue(sight.Evaluate(near), "inside fire distance and angle");
            Assert.IsFalse(sight.Evaluate(far), "past fire distance");
            Assert.AreEqual(prefab.HoldRange, ship.Weapons.Context.HoldRange(WeaponSlot.Primary));

            ship.Weapons.Arm(Projectiles).Fire(WeaponSlot.Primary, new WeaponCommand { held = true, targetPoint = near });
            Assert.AreEqual(primary.Pellets, Projectiles.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Pellets_KnockTheirTargetBackAlongTheShot()
        {
            var shooter = Object.Instantiate(TestAssets.LoadCrimsonPrefab(), Vector3.zero, GamePlane.Rotation);
            spawned.Add(shooter.gameObject);
            shooter.Initialize(0, 0, Projectiles);
            shooter.Reequip(TestAssets.Load<Scatterguns>(PrefabPath), null);
            var gun = (Scatterguns)shooter.Weapons.Primary;
            Assert.Greater(gun.Knockback, 0f);

            var target = Object.Instantiate(TestAssets.LoadCrimsonPrefab(),
                gun.FireOrigin + gun.FireDirection * 6f, GamePlane.Rotation);
            spawned.Add(target.gameObject);
            target.Initialize(1, 0, Projectiles);
            var shieldAndHull = target.ShieldPct + target.HealthPct;

            Assert.IsNotNull(gun.Fire(target.transform.position, Projectiles));
            for (var step = 0; step < 30 && target.ShieldPct + target.HealthPct >= shieldAndHull; step++)
                yield return new WaitForFixedUpdate();

            Assert.Less(target.ShieldPct + target.HealthPct, shieldAndHull, "a pellet hit the target");
            Assert.Greater(Vector3.Dot(target.Rigidbody.linearVelocity, gun.FireDirection), 0f,
                "the hit pushed the target along the shot");
        }

        private Scatterguns Spawn()
        {
            var prefab = TestAssets.Load<Scatterguns>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var gun = Object.Instantiate(prefab, Vector3.zero, GamePlane.Rotation);
            spawned.Add(gun.gameObject);
            return gun;
        }

        private static Vector3 Ahead(WeaponComponent gun) => gun.FireOrigin + gun.FireDirection * 10f;

        private static IEnumerator Until(Func<bool> ready, int maxSteps)
        {
            for (var step = 0; step < maxSteps && !ready(); step++)
                yield return new WaitForFixedUpdate();
        }
    }
}
#endif
