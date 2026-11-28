#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Mods;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Status;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons.Mods
{
    /// <summary>
    /// Turbolaser bolts fly through an enemy missile unless Point defence is held, ripper slugs always do,
    /// and a charge's blast sets off only its own shooter's charges. Steady ripper fire
    /// with Suppression slows a ship and locks its keystone. An EMP missile's hit jams its target.
    /// A Limpet charge rides the first ship it touches and goes off there.
    /// </summary>
    [Category("Weapons")]
    public class WeaponBehaviourModsPlayModeTests : PlayModeWorldFixture
    {
        private const string TurboLasersPath = "Assets/Prefabs/Weapons/TurboLasers.prefab";
        private const string RippersPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string MissilesPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string GrenadesPath = "Assets/Prefabs/Weapons/Grenades.prefab";
        private const string KeysPath = "Assets/Settings/Weapons/Mods/Behaviours/";

        private readonly List<GameObject> spawned = new();

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
        }

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        private T Mount<T>(string path, Vector3 at, Quaternion facing, string key = null) where T : WeaponComponent
        {
            var root = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(root);
            root.transform.SetPositionAndRotation(at, facing);
            root.AddComponent<StubShooter>();
            var weapon = Object.Instantiate(TestAssets.Load<T>(path), root.transform);
            if (key != null)
                weapon.Behaviours.Set(new[] { TestAssets.Load<WeaponBehaviourMod>($"{KeysPath}{key}.asset") });
            return weapon;
        }

        private Ship SpawnTarget(Vector3 at)
        {
            var ship = Object.Instantiate(TestAssets.LoadCrimsonPrefab(), at, GamePlane.Rotation);
            spawned.Add(ship.gameObject);
            ship.Initialize(0, 0, Projectiles);
            ship.Damage.AlwaysInvulnerable = true;
            Physics.SyncTransforms();
            return ship;
        }

        private (Missile missile, ProjectileBase bolt) HeadOn(bool pointDefence) =>
            HeadOn<Lasers>(TurboLasersPath, pointDefence ? "PointDefence" : null);

        private (Missile missile, ProjectileBase bolt) HeadOn<T>(string path, string key) where T : WeaponComponent
        {
            var guns = Mount<T>(path, Vector3.zero, GamePlane.Rotation, key);
            var ahead = guns.FireOrigin + guns.FireDirection * 12f;
            var launcher = Mount<Missiles>(MissilesPath, ahead,
                Quaternion.AngleAxis(180f, GamePlane.Normal) * GamePlane.Rotation);
            launcher.transform.parent.position += ahead - launcher.FireOrigin;
            Physics.SyncTransforms();

            var missile = (Missile)launcher.Fire(guns.FireOrigin, Projectiles);
            var bolt = guns.Fire(ahead, Projectiles);
            Assert.IsNotNull(bolt, "the guns fired");
            return (missile, bolt);
        }

        private static IEnumerator StepWhile(System.Func<bool> going, int steps)
        {
            for (var step = 0; step < steps && going(); step++)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator WithoutPointDefence_TheBoltFliesThroughAnEnemyMissile()
        {
            var (missile, bolt) = HeadOn(pointDefence: false);
            var toward = bolt.GetComponent<Rigidbody>().linearVelocity;

            yield return StepWhile(() => bolt.gameObject.activeSelf &&
                Vector3.Dot(bolt.transform.position - missile.transform.position, toward) < 2f, 40);

            Assert.IsTrue(missile.gameObject.activeSelf, "the missile flies on");
            Assert.Greater(Vector3.Dot(bolt.transform.position - missile.transform.position, toward), 0f,
                "the bolt passed through it");
        }

        [UnityTest]
        public IEnumerator PointDefence_TheBoltShootsDownAnEnemyMissile()
        {
            var (missile, bolt) = HeadOn(pointDefence: true);

            yield return StepWhile(() => missile.gameObject.activeSelf, 40);

            Assert.IsFalse(missile.gameObject.activeSelf, "the missile is shot down");
            Assert.IsFalse(bolt.gameObject.activeSelf, "the bolt spent itself on it");
        }

        [UnityTest]
        public IEnumerator RipperSlugs_FlyThroughAnEnemyMissile()
        {
            var (missile, slug) = HeadOn<Rippers>(RippersPath, null);
            var toward = slug.GetComponent<Rigidbody>().linearVelocity;

            yield return StepWhile(() => slug.gameObject.activeSelf &&
                Vector3.Dot(slug.transform.position - missile.transform.position, toward) < 2f, 40);

            Assert.IsTrue(missile.gameObject.activeSelf, "the missile flies on");
            Assert.Greater(Vector3.Dot(slug.transform.position - missile.transform.position, toward), 0f,
                "the slug passed through it");
        }

        [UnityTest]
        public IEnumerator AChargesBlast_SetsOffOnlyItsOwnShootersCharges([Values] bool own)
        {
            var dropper = Mount<Grenades>(GrenadesPath, Vector3.zero, GamePlane.Rotation);
            var neighbour = own ? dropper : Mount<Grenades>(GrenadesPath, Vector3.zero, GamePlane.Rotation);
            var origin = dropper.FireOrigin;

            var second = (Grenade)neighbour.Fire(origin + dropper.FireDirection * 20f, Projectiles);
            dropper.Reset();
            var first = (Grenade)dropper.Fire(origin - dropper.FireDirection * 20f, Projectiles);
            first.TakeDamage(default);

            yield return StepWhile(() => second.gameObject.activeSelf, 20);

            Assert.AreEqual(!own, second.gameObject.activeSelf,
                own ? "the blast chains its own shooter's charge" : "an enemy blast passes over the charge");
        }

        private IEnumerator HoldRippersOn(Rippers guns, Ship target, int steps)
        {
            for (var step = 0; step < steps; step++)
            {
                guns.HandleTrigger(new WeaponCommand { held = true, pressed = step == 0, targetPoint = target.transform.position },
                    Projectiles);
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator WithoutSuppression_SteadyRipperFireLeavesTheTargetFree()
        {
            var guns = Mount<Rippers>(RippersPath, Vector3.zero, GamePlane.Rotation);
            var target = SpawnTarget(guns.FireOrigin + guns.FireDirection * 8f);
            var hits = 0;
            target.Damage.OnBlocked += _ => hits++;

            yield return HoldRippersOn(guns, target, 60);

            Assert.Greater(hits, 5, "test premise: the slugs land");
            Assert.IsFalse(target.Status.Has(StatusEffect.Suppressed));
        }

        [UnityTest]
        public IEnumerator Suppression_SteadyRipperFireSlowsTheTarget_AndLocksItsKeystone()
        {
            var guns = Mount<Rippers>(RippersPath, Vector3.zero, GamePlane.Rotation, "Suppression");
            var target = SpawnTarget(guns.FireOrigin + guns.FireDirection * 8f);
            yield return new WaitForFixedUpdate();
            var cap = target.Rigidbody.maxLinearVelocity;

            yield return HoldRippersOn(guns, target, 60);
            Assert.IsTrue(target.Status.Has(StatusEffect.Suppressed), "steady fire suppresses the target");

            for (var step = 0; step < 10; step++)
            {
                target.Movement.Drive(new PilotCommand { boost = 1f });
                yield return HoldRippersOn(guns, target, 1);
            }

            Assert.AreEqual(0, target.Keystone.Activations, "no keystone while suppressed");
            Assert.Less(target.Rigidbody.maxLinearVelocity, cap, "its speed cap falls");
        }

        private static IEnumerator MissileHit(ProjectileBase missile)
        {
            yield return StepWhile(() => missile.gameObject.activeSelf, 150);
            Assert.IsFalse(missile.gameObject.activeSelf, "test premise: the missile hit");
        }

        [UnityTest]
        public IEnumerator WithoutEmp_AMissileHitLeavesTheTargetsWeaponsLive()
        {
            var launcher = Mount<Missiles>(MissilesPath, Vector3.zero, GamePlane.Rotation);
            var target = SpawnTarget(launcher.FireOrigin + launcher.FireDirection * 6f);

            yield return MissileHit(launcher.Fire(target.transform.position, Projectiles));

            Assert.IsTrue(target.Status.CanFire);
        }

        [UnityTest]
        public IEnumerator EmpWarhead_AMissileHitJamsTheTargetsWeaponsAndLocks()
        {
            var launcher = Mount<Missiles>(MissilesPath, Vector3.zero, GamePlane.Rotation, "EmpWarhead");
            var target = SpawnTarget(launcher.FireOrigin + launcher.FireDirection * 6f);

            yield return MissileHit(launcher.Fire(target.transform.position, Projectiles));

            Assert.IsTrue(target.Status.Has(StatusEffect.WeaponsJammed), "the hit jams it");
            Assert.IsFalse(target.Status.CanFire || target.Status.CanLock);
            Assert.IsTrue(target.Status.CanThrust && target.Status.CanTurn, "it keeps flying");
            Assert.That(target.Status.Remaining(StatusEffect.WeaponsJammed), Is.InRange(1.5f, 2f));
        }

        [UnityTest]
        public IEnumerator Limpet_TheChargeRidesTheShipItTouches_AndGoesOffWhereTheShipIs()
        {
            var launcher = Mount<Grenades>(GrenadesPath, Vector3.zero, GamePlane.Rotation, "Limpet");
            var target = SpawnTarget(launcher.FireOrigin + launcher.FireDirection * 8f);
            var across = Vector3.Cross(GamePlane.Normal, launcher.FireDirection).normalized;

            var charge = (Grenade)launcher.Fire(target.transform.position + launcher.FireDirection * 6f, Projectiles);
            yield return StepWhile(() => true, 15);
            var offset = charge.transform.position - target.transform.position;
            Assert.Less(offset.magnitude, 3f, "the charge stopped on the ship");

            for (var step = 0; step < 15; step++)
            {
                var to = target.Rigidbody.position + across * 0.3f;
                target.Rigidbody.position = to;
                target.transform.position = to;
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(charge.gameObject.activeSelf, "its fuse is still running");
            Assert.Less(Vector3.Distance(charge.transform.position - target.transform.position, offset), 0.1f,
                "the ship carries the charge");

            var shipAt = target.transform.position;
            ConcussionWave wave = null;
            charge.Spawned += (spawnedWave, _) => wave = (ConcussionWave)spawnedWave;
            yield return StepWhile(() => charge.gameObject.activeSelf, 60);

            Assert.IsNotNull(wave, "the charge went off");
            Assert.Less(Vector3.Distance(wave.transform.position, shipAt + offset), 0.5f, "on the ship that carried it");
        }
    }
}
#endif
