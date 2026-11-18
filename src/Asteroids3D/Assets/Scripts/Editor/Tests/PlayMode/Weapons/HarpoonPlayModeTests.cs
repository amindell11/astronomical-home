#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Damage;
using Substrate;
using NUnit.Framework;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    [Category("Weapons")]
    public class HarpoonPlayModeTests : PlayModeWorldFixture
    {
        private const string PrefabPath = "Assets/Prefabs/Weapons/Harpoons.prefab";
        private const float ShooterMass = 1000f;
        private const float TargetMass = 60f;

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        private sealed class MovingShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity { get; set; }
            public Rigidbody Body { get; private set; }
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;

            private void Awake() => Body = GetComponent<Rigidbody>();
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public float TotalDamage { get; private set; }

            public float TakeDamage(in DamageInfo hit)
            {
                TotalDamage += hit.Amount;
                return 0f;
            }

            private void OnDestroy() => DamageableColliders.Unregister(this);
        }

        private Harpoons Mount(out MovingShooter shooter, out Rigidbody shooterBody)
        {
            var ship = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.SetPositionAndRotation(Vector3.zero, GamePlane.Rotation);
            shooterBody = Body(ship, ShooterMass, kinematic: false);
            shooter = ship.AddComponent<MovingShooter>();

            var prefab = TestAssets.Load<Harpoons>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var weapon = Object.Instantiate(prefab, ship.transform);
            spawned.Add(weapon.gameObject);
            Assert.IsTrue(weapon.CanFire(), "the harpoon starts able to fire");
            return weapon;
        }

        private DamageRecorder CreateTarget(string name, int layer, Vector3 position, float mass, float radius, bool trigger)
        {
            var go = new GameObject(name) { layer = layer };
            spawned.Add(go);
            go.transform.position = position;
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = radius;
            collider.isTrigger = trigger;
            Body(go, mass, kinematic: trigger);
            var recorder = go.AddComponent<DamageRecorder>();
            DamageableColliderRegistration.Register(recorder);
            Physics.SyncTransforms();
            return recorder;
        }

        private void PlaceAsteroid(Vector3 position, float radius)
        {
            var go = new GameObject("Between") { layer = LayerIds.Asteroid };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = radius;
            Physics.SyncTransforms();
        }

        private static Rigidbody Body(GameObject go, float mass, bool kinematic)
        {
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = mass;
            body.isKinematic = kinematic;
            body.constraints = RigidbodyConstraints.FreezeRotation | GamePlane.PositionConstraint;
            return body;
        }

        private static Vector3 Ahead(Harpoons weapon, float distance) =>
            weapon.FireOrigin + weapon.FireDirection * distance;

        private void Pull(Harpoons weapon, Vector3 aim, bool held, bool pressed) =>
            weapon.HandleTrigger(new WeaponCommand { held = held, pressed = pressed, targetPoint = aim }, Projectiles);

        private IEnumerator Hold(Harpoons weapon, Vector3 aim, int steps) =>
            Until(() => false, weapon, aim, steps);

        private IEnumerator Until(Func<bool> done, Harpoons weapon, Vector3 aim, int maxSteps)
        {
            for (var step = 0; step < maxSteps && !done(); step++)
            {
                Pull(weapon, aim, held: true, pressed: false);
                yield return new WaitForFixedUpdate();
            }
        }

        private IEnumerator Hook(Harpoons weapon, Vector3 aim)
        {
            Pull(weapon, aim, held: true, pressed: true);
            yield return Until(() => weapon.Tether != null, weapon, aim, 80);
        }

        private static int Steps(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime);

        private static float Tuned(Object component, string field)
        {
            var property = new SerializedObject(component).FindProperty(field);
            Assert.IsNotNull(property, field);
            return property.floatValue;
        }

        [UnityTest]
        public IEnumerator HeldTrigger_HooksTheShipAhead_AndDealsNoDamage()
        {
            var weapon = Mount(out _, out _);
            var target = CreateTarget("Ship", LayerIds.Ship, Ahead(weapon, 12f), TargetMass, 1f, trigger: false);

            yield return Hook(weapon, target.transform.position);

            Assert.IsNotNull(weapon.Tether, "a held trigger hooks the ship in front of the nose");
            Assert.AreEqual(0f, target.TotalDamage, "hooking deals no damage");
        }

        [UnityTest]
        public IEnumerator HeldTether_PullsTheLighterBodyFarther()
        {
            var weapon = Mount(out _, out var shooterBody);
            var target = CreateTarget("Ship", LayerIds.Ship, Ahead(weapon, 12f), TargetMass, 1f, trigger: false);
            var aim = target.transform.position;
            yield return Hook(weapon, aim);
            Assert.IsNotNull(weapon.Tether);

            var targetBody = target.GetComponent<Rigidbody>();
            var axis = (targetBody.position - shooterBody.position).normalized;
            var shooterStart = shooterBody.position;
            var targetStart = targetBody.position;
            yield return Hold(weapon, aim, Steps(1f));

            var shooterShift = Vector3.Dot(shooterBody.position - shooterStart, axis);
            var targetShift = Vector3.Dot(targetBody.position - targetStart, axis);
            Assert.Greater(shooterShift, 0.05f, "the heavy shooter is pulled toward the target");
            Assert.Less(targetShift, -0.05f, "the light target is pulled toward the shooter");
            Assert.Greater(-targetShift, shooterShift, "the lighter body moves farther along the tether");
        }

        [UnityTest]
        public IEnumerator HeldTether_ReelsInToMinLength()
        {
            var weapon = Mount(out _, out _);
            var minLength = Tuned(weapon, "minLength");
            var target = CreateTarget("Ship", LayerIds.Ship, Ahead(weapon, minLength + 8f), TargetMass, 1f, trigger: false);
            var aim = target.transform.position;
            yield return Hook(weapon, aim);
            Assert.IsNotNull(weapon.Tether);
            Assert.Greater(weapon.Tether.Length, minLength + 1f);

            var shrunk = false;
            var previous = weapon.Tether.Length;
            var limit = Steps(Tuned(weapon, "maxSeconds"));
            for (var step = 0; step < limit && weapon.Tether != null; step++)
            {
                Pull(weapon, aim, held: true, pressed: false);
                yield return new WaitForFixedUpdate();
                if (weapon.Tether == null) break;
                if (weapon.Tether.Length < previous - 0.001f) shrunk = true;
                previous = weapon.Tether.Length;
                if (Mathf.Abs(weapon.Tether.Length - minLength) < 0.02f) break;
            }

            Assert.IsTrue(shrunk, "the tether shortens while held");
            Assert.IsNotNull(weapon.Tether, "it is held when it reaches minimum length");
            Assert.AreEqual(minLength, weapon.Tether.Length, 0.05f);
            yield return Hold(weapon, aim, Steps(0.4f));
            Assert.IsNotNull(weapon.Tether);
            Assert.AreEqual(minLength, weapon.Tether.Length, 0.05f, "it does not shorten past the minimum");
        }

        [UnityTest]
        public IEnumerator Release_EndsTheTether()
        {
            var weapon = Mount(out _, out _);
            var aim = Ahead(weapon, 12f);
            CreateTarget("Ship", LayerIds.Ship, aim, TargetMass, 1f, trigger: false);
            yield return Hook(weapon, aim);
            Assert.IsNotNull(weapon.Tether);

            Pull(weapon, aim, held: false, pressed: false);

            Assert.IsNull(weapon.Tether);
        }

        [UnityTest]
        public IEnumerator ReleaseBeforeImpact_ReturnsTheHarpoon_AndFormsNoTether()
        {
            var weapon = Mount(out _, out _);
            var aim = Ahead(weapon, 20f);
            CreateTarget("Ship", LayerIds.Ship, aim, TargetMass, 1f, trigger: false);

            Pull(weapon, aim, held: true, pressed: true);
            Assert.IsNotNull(weapon.InFlight, "the press launched a harpoon");
            Pull(weapon, aim, held: false, pressed: false);

            Assert.IsNull(weapon.InFlight, "releasing before it lands returns the harpoon");
            Assert.IsNull(weapon.Tether);
            for (var step = 0; step < 80; step++)
                yield return new WaitForFixedUpdate();
            Assert.IsNull(weapon.Tether, "the cancelled shot never hooks");
            Assert.IsNull(weapon.InFlight);
        }

        [UnityTest]
        public IEnumerator TimeCap_EndsAHeldTether()
        {
            var weapon = Mount(out _, out _);
            var maxSeconds = Tuned(weapon, "maxSeconds");
            var aim = Ahead(weapon, 12f);
            CreateTarget("Ship", LayerIds.Ship, aim, TargetMass, 1f, trigger: false);
            yield return Hook(weapon, aim);
            Assert.IsNotNull(weapon.Tether);

            var steps = 0;
            var limit = Steps(maxSeconds) + 3;
            while (weapon.Tether != null && steps < limit)
            {
                Pull(weapon, aim, held: true, pressed: false);
                yield return new WaitForFixedUpdate();
                steps++;
            }

            Assert.IsNull(weapon.Tether, "the time cap ends a tether that is still held");
            Assert.That(steps * Time.fixedDeltaTime,
                Is.InRange(maxSeconds - 2f * Time.fixedDeltaTime, maxSeconds + Time.fixedDeltaTime));
        }

        [UnityTest]
        public IEnumerator AsteroidBetweenTheEnds_CutsTheTether()
        {
            var weapon = Mount(out _, out _);
            var aim = Ahead(weapon, 14f);
            CreateTarget("Ship", LayerIds.Ship, aim, TargetMass, 1f, trigger: false);
            yield return Hook(weapon, aim);
            Assert.IsNotNull(weapon.Tether);

            PlaceAsteroid((weapon.Tether.NosePoint + weapon.Tether.HookPoint) * 0.5f, 1.5f);
            yield return Until(() => weapon.Tether == null, weapon, aim, 5);

            Assert.IsNull(weapon.Tether, "an asteroid between the ends cuts the tether");
        }

        [UnityTest]
        public IEnumerator DrivingTheTargetAway_SnapsTheTether()
        {
            var weapon = Mount(out var shooter, out _);
            var maxSeconds = Tuned(weapon, "maxSeconds");
            var aim = Ahead(weapon, 12f);
            var target = CreateTarget("Ship", LayerIds.Ship, aim, TargetMass, 1f, trigger: false);
            var targetBody = target.GetComponent<Rigidbody>();
            yield return Hook(weapon, aim);
            Assert.IsNotNull(weapon.Tether);

            var steps = 0;
            var limit = Steps(maxSeconds) + 3;
            while (weapon.Tether != null && steps < limit)
            {
                var away = targetBody.position - shooter.Body.position;
                targetBody.AddForce(away.normalized * 500000f, ForceMode.Force);
                Pull(weapon, aim, held: true, pressed: false);
                yield return new WaitForFixedUpdate();
                steps++;
            }

            Assert.IsNull(weapon.Tether, "yanking the target apart snaps the tether");
            Assert.Less(steps * Time.fixedDeltaTime, maxSeconds - Time.fixedDeltaTime);
        }

        [UnityTest]
        public IEnumerator NonHookableTrigger_IsIgnored_AndTheShipBehindIsHooked()
        {
            var weapon = Mount(out _, out _);
            var aim = Ahead(weapon, 16f);
            var decoy = CreateTarget("Decoy", LayerIds.Projectile, Ahead(weapon, 6f), 1f, 2f, trigger: true);
            var ship = CreateTarget("Ship", LayerIds.Ship, aim, TargetMass, 1f, trigger: false);

            yield return Hook(weapon, aim);

            Assert.IsNotNull(weapon.Tether, "the harpoon flies through the non-hookable trigger and hooks the ship");
            Assert.AreEqual(0f, decoy.TotalDamage, "the trigger is not a hit");
            Assert.AreEqual(0f, ship.TotalDamage, "the hook deals no damage");
        }
    }
}
#endif
