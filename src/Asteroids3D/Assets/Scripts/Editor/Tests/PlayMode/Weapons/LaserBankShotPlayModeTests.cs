#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Mods;
using Damage;
using NUnit.Framework;
using Substrate;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    /// <summary>With Bank shot held, a laser bolt turns off the first asteroid it meets, unharmed, and dies on the next.</summary>
    [Category("Weapons")]
    public class LaserBankShotPlayModeTests : PlayModeWorldFixture
    {
        private const string LasersPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string BankShotPath = "Assets/Settings/Weapons/Mods/Behaviours/BankShot.asset";

        private readonly List<GameObject> spawned = new();

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public float Total { get; private set; }

            public float TakeDamage(in DamageInfo hit)
            {
                Total += hit.Amount;
                return 0f;
            }

            private void OnDestroy() => DamageableColliders.Unregister(this);
        }

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        private Lasers MountLasers(bool bankShot)
        {
            var ship = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.SetPositionAndRotation(Vector3.zero, GamePlane.Rotation);
            ship.AddComponent<StubShooter>();
            var lasers = Object.Instantiate(TestAssets.Load<Lasers>(LasersPath), ship.transform);
            if (bankShot)
                lasers.Behaviours.Set(new[] { TestAssets.Load<WeaponBehaviourMod>(BankShotPath) });
            return lasers;
        }

        private DamageRecorder CreateRock(Vector3 position)
        {
            var go = new GameObject("Rock") { layer = LayerIds.Asteroid };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = 1f;
            var rock = go.AddComponent<DamageRecorder>();
            DamageableColliderRegistration.Register(rock);
            return rock;
        }

        private static IEnumerator Fly(Component bolt, int steps)
        {
            for (var step = 0; step < steps && bolt.gameObject.activeSelf; step++)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator WithoutIt_TheBoltDiesOnTheRock()
        {
            var lasers = MountLasers(bankShot: false);
            var rock = CreateRock(lasers.FireOrigin + lasers.FireDirection * 6f);
            Physics.SyncTransforms();

            var bolt = lasers.Fire(rock.transform.position, Projectiles);
            yield return Fly(bolt, 60);

            Assert.IsFalse(bolt.gameObject.activeSelf, "the rock stops the bolt");
            Assert.Greater(rock.Total, 0f);
        }

        [UnityTest]
        public IEnumerator BankShot_TurnsTheBoltOffTheFirstRock_AndTheNextOneStopsIt()
        {
            var lasers = MountLasers(bankShot: true);
            var first = CreateRock(lasers.FireOrigin + lasers.FireDirection * 6f);
            Physics.SyncTransforms();

            var aim = lasers.FireDirection;
            var bolt = lasers.Fire(first.transform.position, Projectiles);
            var body = bolt.GetComponent<Rigidbody>();
            for (var step = 0; step < 60 && Vector3.Dot(body.linearVelocity, aim) > 0f; step++)
                yield return new WaitForFixedUpdate();

            Assert.IsTrue(bolt.gameObject.activeSelf, "the bolt survives the bank");
            Assert.Less(Vector3.Dot(body.linearVelocity, aim), 0f, "a head-on bank sends it back the way it came");
            Assert.Less(Vector3.Dot(bolt.transform.up, aim), 0f, "the bolt faces its new heading");
            Assert.AreEqual(0f, first.Total, "the bank does not hurt the rock");

            var second = CreateRock(lasers.FireOrigin - lasers.FireDirection * 6f);
            Physics.SyncTransforms();
            yield return Fly(bolt, 60);

            Assert.IsFalse(bolt.gameObject.activeSelf, "it banks only once");
            Assert.Greater(second.Total, 0f);
        }
    }
}
#endif
