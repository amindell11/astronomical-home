#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Combat;
using Combat.Projectiles;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class BlinkPlayModeTests : PlayModeWorldFixture
    {
        private const string CrimsonPath = "Assets/Prefabs/Ships/Crimson.prefab";
        private const string MissilePath = "Assets/Prefabs/Weapons/Projectiles/MissileProjectile.prefab";
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";
        private const string FragSettingsPath = "Assets/Settings/Asteroids/FragSettings.asset";

        private readonly List<GameObject> spawned = new();
        private Ship crimson;
        private Blink blink;

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public global::Ships.Registry.ShipId Id => global::Ships.Registry.ShipId.Invalid;
        }

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(crimson);
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Blink_JumpsTowardTheTarget_ClampedToRange_KeepingVelocity()
        {
            yield return SpawnCrimson();
            var velocity = GamePlane.PlaneDirToWorld(new Vector2(0f, 10f));
            crimson.Rigidbody.linearVelocity = velocity;
            var start = Plane(crimson.Rigidbody.position);

            crimson.Movement.Drive(new PilotCommand { boost = 1f, abilityTarget = start + new Vector2(30f, 0f) });
            yield return new WaitForFixedUpdate();

            var jump = Plane(crimson.Rigidbody.position) - start;
            Assert.That(blink.Activations, Is.EqualTo(1));
            Assert.That(jump.x, Is.EqualTo(blink.Range).Within(0.5f), "the jump is clamped to range along the aim");
            Assert.That(Mathf.Abs(jump.y), Is.LessThan(0.5f), "only the one step of drift on top");
            Assert.That(Vector3.Distance(crimson.Rigidbody.linearVelocity, velocity), Is.LessThan(0.5f), "velocity is preserved");
        }

        [UnityTest]
        public IEnumerator Blink_IntoARock_ShortensToClearSpace()
        {
            yield return SpawnCrimson();
            var start = Plane(crimson.Rigidbody.position);
            var rock = SpawnRock(GamePlane.PlanePointToWorld(start + new Vector2(blink.Range, 0f)));
            // A rock's detailed collider switches on in its LateUpdate; give the field one frame, as in play.
            yield return null;
            yield return new WaitForFixedUpdate();

            crimson.Movement.Drive(new PilotCommand { boost = 1f, abilityTarget = start + new Vector2(30f, 0f) });
            yield return new WaitForFixedUpdate();

            var landed = Plane(crimson.Rigidbody.position);
            Assert.That(landed.x - start.x, Is.GreaterThan(1f), "it still jumps");
            Assert.That(landed.x - start.x, Is.LessThan(blink.Range - 1f), "but lands short of the rock");
            var overlapping = Physics.OverlapSphere(crimson.Rigidbody.position, crimson.ShipRadius, LayerMask.GetMask("Asteroid"));
            Assert.That(overlapping, Is.Empty, "and not inside it");
            Assert.That(rock.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator Blink_OutOfTheSeekerCone_DropsTheMissilesTrack_AndPays()
        {
            yield return SpawnCrimson();
            var start = Plane(crimson.Rigidbody.position);
            var missile = LaunchTrackingMissile(start - new Vector2(0f, 8f));
            yield return new WaitForFixedUpdate();
            Assert.That(crimson.Lock.Trackers, Has.Count.EqualTo(1), "the missile tracks the Crimson");

            crimson.Movement.Drive(new PilotCommand { boost = 1f, abilityTarget = start + new Vector2(30f, 0f) });
            yield return new WaitForFixedUpdate();
            Assert.That(blink.Activations, Is.EqualTo(1));
            for (var i = 0; i < 3; i++)
            {
                crimson.Movement.Drive(default);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(crimson.Lock.Trackers, Is.Empty, "a sideways jump far outside the ±35° seeker cone drops the track");
            Assert.That(missile.gameObject.activeSelf, Is.True, "the missile flies on, lost");
            Assert.That(blink.PaidActivations, Is.EqualTo(1), "breaking a missile's track pays");
        }

        private IEnumerator SpawnCrimson()
        {
            crimson = Object.Instantiate(TestAssets.Load<Ship>(CrimsonPath), Vector3.zero, GamePlane.Rotation);
            crimson.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            blink = crimson.Keystone as Blink;
            Assert.That(blink, Is.Not.Null, "Crimson carries Blink");
        }

        private static Vector2 Plane(Vector3 world) => GamePlane.WorldPointToPlane(world);

        private Missile LaunchTrackingMissile(Vector2 from)
        {
            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            spawned.Add(shooter.gameObject);
            var heading = GamePlane.PlaneDirToWorld(Vector2.up);
            var missile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Missile>(MissilePath),
                GamePlane.PlanePointToWorld(from), GamePlane.PlanePose(GamePlane.Normal, heading));
            spawned.Add(missile.gameObject);
            missile.Initialize(shooter);
            missile.Launch(heading, crimson.transform.position);
            missile.SetTarget(crimson);
            return missile;
        }

        private AsteroidController SpawnRock(Vector3 at)
        {
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);
            var holder = new GameObject("Asteroid spawner");
            spawned.Add(holder);
            holder.SetActive(false);
            SetReference(holder.AddComponent<Fragger>(), "asteroidFragAsteroidFragSettings",
                AssetDatabase.LoadAssetAtPath<AsteroidFragSettings>(FragSettingsPath));
            var spawner = holder.AddComponent<AsteroidSpawner>();
            SetReference(spawner, "settings", settings);
            holder.SetActive(true);
            var info = settings.meshInfos[0];
            return spawner.Spawn(new Pose(at, Quaternion.identity), new AsteroidAttributes(info, 0, 20000f, 2.5f, Vector3.zero, Vector3.zero), 713);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
