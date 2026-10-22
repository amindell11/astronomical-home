#if UNITY_EDITOR
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Damage;
using NUnit.Framework;
using Ships.Damage;
using Ships.Loadout;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Weapons
{
    [Category("Weapons")]
    public class RailgunBeamPlayModeTests : PlayModeWorldFixture
    {
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";

        private static readonly Vector3 Aim = GamePlane.PlaneDirToWorld(Vector2.up);
        private static readonly Vector3 Across = GamePlane.PlaneDirToWorld(Vector2.right);

        private readonly List<GameObject> spawned = new();
        private readonly List<IDamageable> struck = new();
        private readonly List<Vector3> hitPoints = new();
        private Railguns railgun;
        private Vector3 beamStart;
        private Vector3 beamEnd;

        /// <summary>Soaks <see cref="Toughness"/> damage, then dies and passes the rest on.</summary>
        private sealed class Soak : MonoBehaviour, IDamageable
        {
            public float Toughness = float.PositiveInfinity;
            public readonly List<DamageInfo> Hits = new();

            public float TakeDamage(in DamageInfo hit)
            {
                Hits.Add(hit);
                var overkill = Mathf.Max(0f, hit.Amount - Toughness);
                Toughness = Mathf.Max(0f, Toughness - hit.Amount);
                return overkill;
            }
        }

        public override void SetUp()
        {
            base.SetUp();
            var prefab = AssetDatabase.LoadAssetAtPath<Railguns>(RailgunPrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {RailgunPrefabPath}");
            railgun = Object.Instantiate(prefab, Vector3.zero, GamePlane.Rotation);
            spawned.Add(railgun.gameObject);
            railgun.OnBeamFired += (start, end) => (beamStart, beamEnd) = (start, end);
            railgun.Hit += (point, target) =>
            {
                hitPoints.Add(point);
                struck.Add(target);
            };
        }

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            struck.Clear();
            hitPoints.Clear();
            base.TearDown();
        }

        private Soak CreateSoak(Vector3 position, float toughness = float.PositiveInfinity, float size = 1f)
        {
            var go = new GameObject("Soak") { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = Vector3.one * size;
            var soak = go.AddComponent<Soak>();
            soak.Toughness = toughness;
            return soak;
        }

        private DamageController CreateShip(Vector3 position, float health, float shield)
        {
            var go = new GameObject("Ship") { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = Vector3.one;
            var damage = go.AddComponent<DamageController>();
            damage.PopulateSettings(new ResolvedShipStats { maxHealth = health, maxShield = shield });
            return damage;
        }

        private void Fire()
        {
            Physics.SyncTransforms();
            var fired = 0;
            railgun.OnFire += () => fired++;
            railgun.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f);
            railgun.HandleTrigger(new WeaponCommand { pressed = false, held = true }, Projectiles);
            Assert.AreEqual(1, fired, "one held step reaches full charge and fires");
        }

        private static float Amount(Soak soak) => soak.Hits[0].Amount;

        [Test]
        public void KillingBlow_CarriesTheOverkillToTheTargetBehind()
        {
            var weak = CreateShip(Aim * 5f, health: 10f, shield: 5f);
            var behind = CreateSoak(Aim * 10f);

            Fire();

            Assert.AreEqual(0f, weak.Health.CurrentValue, "the beam kills the weak ship");
            Assert.That(behind.Hits, Has.Count.EqualTo(1), "the beam pierces to the target behind");
            Assert.AreEqual(railgun.Damage - 15f, Amount(behind), 0.001f, "it arrives with damage less the weak ship's shield + hull");
            CollectionAssert.AreEqual(new IDamageable[] { weak, behind }, struck, "one Hit per target, nearest first");
        }

        [Test]
        public void KillingTheLastTarget_DrawsTheBeamToFullRange()
        {
            CreateSoak(Aim * 5f, toughness: 10f);

            Fire();

            Assert.That(struck, Has.Count.EqualTo(1));
            Assert.Less(Vector3.Distance(beamEnd, Aim * railgun.BeamRange), 0.001f, "leftover damage and nothing else in range");
        }

        [Test]
        public void Survivor_BlocksTheTargetBehind_AndEndsTheBeam()
        {
            var survivor = CreateSoak(Aim * 5f);
            var behind = CreateSoak(Aim * 10f);

            Fire();

            Assert.AreEqual(railgun.Damage, Amount(survivor), 0.001f);
            Assert.That(behind.Hits, Is.Empty, "a survivor stops the beam");
            Assert.Less(Vector3.Distance(beamEnd, Aim * 4.5f), 0.01f, "the beam ends on the survivor's near face");
        }

        [Test]
        public void InvulnerableShip_BlocksTheTargetBehind()
        {
            var ship = CreateShip(Aim * 5f, health: 10f, shield: 0f);
            ship.SetInvulnerability(10f);
            var behind = CreateSoak(Aim * 10f);

            Fire();

            Assert.AreEqual(10f, ship.Health.CurrentValue, "invulnerable: no damage taken");
            CollectionAssert.AreEqual(new IDamageable[] { ship }, struck, "the beam strikes the ship and stops there");
            Assert.That(behind.Hits, Is.Empty);
        }

        [Test]
        public void MultiColliderTarget_CountsOnce()
        {
            var root = new GameObject("TwoHullShip") { layer = LayerIds.Ship };
            spawned.Add(root);
            root.AddComponent<Rigidbody>().isKinematic = true;
            foreach (var along in new[] { 5f, 7f })
            {
                var part = new GameObject("Hull") { layer = LayerIds.Ship };
                part.transform.SetParent(root.transform, false);
                part.transform.position = Aim * along;
                part.AddComponent<BoxCollider>().size = Vector3.one;
            }
            var ship = root.AddComponent<Soak>();
            ship.Toughness = 10f;
            var behind = CreateSoak(Aim * 10f);

            Fire();

            Assert.That(ship.Hits, Has.Count.EqualTo(1), "two colliders on one target take one hit");
            Assert.AreEqual(railgun.Damage - 10f, Amount(behind), 0.001f);
        }

        [Test]
        public void TargetBesideTheLine_IsHitWithinBeamRadius_MissedBeyondIt()
        {
            const float size = 0.2f;
            var radius = railgun.BeamRadius;
            Assume.That(radius, Is.GreaterThan(size), "the beam is wider than the probe targets");
            var beyond = CreateSoak(Aim * 3f - Across * (radius + size / 2f + 0.05f), size: size);
            var within = CreateSoak(Aim * 6f + Across * (radius + size / 2f - 0.05f), size: size);

            Fire();

            Assert.That(beyond.Hits, Is.Empty, "a near face 0.05 outside the beam radius is missed");
            Assert.That(within.Hits, Has.Count.EqualTo(1), "a near face 0.05 inside the beam radius is hit");
        }

        [Test]
        public void FirePointTiltedOutOfPlane_StillHitsAnInPlaneTarget()
        {
            railgun.transform.SetPositionAndRotation(GamePlane.Normal * 0.8f,
                Quaternion.AngleAxis(30f, Across) * GamePlane.Rotation);
            Assume.That(Vector3.Angle(railgun.firePoint.up, Aim), Is.EqualTo(30f).Within(0.01f), "the fire point tilts out of the plane");
            var target = CreateSoak(Aim * 10f);

            Fire();

            Assert.That(target.Hits, Has.Count.EqualTo(1), "the beam is cast in the plane");
            Assert.Less(Vector3.Angle(target.Hits[0].HitVelocity, Aim), 0.01f, "the impulse runs along the planar beam");
            Assert.AreEqual(0f, Vector3.Dot(beamStart - GamePlane.Origin, GamePlane.Normal), 0.001f, "beam starts in the plane");
            Assert.AreEqual(0f, Vector3.Dot(beamEnd - GamePlane.Origin, GamePlane.Normal), 0.001f, "beam ends in the plane");
        }

        [Test]
        public void TargetOverlappingTheMuzzle_IsHitAtItsClosestPoint()
        {
            var muzzle = Across * 10f;
            railgun.transform.position = muzzle;
            var target = CreateSoak(muzzle + Aim * 0.3f, size: 0.2f);

            Fire();

            Assert.That(target.Hits, Has.Count.EqualTo(1), "a collider inside the beam's starting sphere is struck");
            Assert.Less(Vector3.Distance(hitPoints[0], muzzle + Aim * 0.2f), 0.001f,
                "the impact sits on the target's near face, not at the zero point the cast reports");
        }
    }
}
#endif
