#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Combat;
using Combat.Projectiles;
using Damage;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Ships.Keystones.Kits;
using Ships.Mods;
using Ships.Registry;
using Ships.Status;
using Substrate;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private const string RecallPath = "Assets/Settings/Ships/Keystones/Mods/Recall.asset";
        private const string ShieldBashPath = "Assets/Settings/Ships/Keystones/Mods/ShieldBash.asset";

        private readonly List<GameObject> spawned = new();
        private Ship crimson;
        private Ship rival;
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
            ShipTestFactory.DestroyShip(rival);
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
        public IEnumerator Recall_AFreshPressOnCooldown_JumpsBackToTheStart_KeepingVelocity()
        {
            yield return SpawnCrimson();
            blink.Behaviours.Set(new BehaviourMod[] { TestAssets.Load<KeystoneMod>(RecallPath) });
            var velocity = GamePlane.PlaneDirToWorld(new Vector2(0f, 10f));
            crimson.Rigidbody.linearVelocity = velocity;
            var start = Plane(crimson.Rigidbody.position);

            crimson.Movement.Drive(new PilotCommand { boost = 1f, abilityTarget = start + new Vector2(30f, 0f) });
            yield return new WaitForFixedUpdate();
            Assert.That(blink.Marker.HasValue && Vector2.Distance(blink.Marker.Value, start) < 0.5f, Is.True,
                "the blink marks where it started");
            for (var i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Plane(crimson.Rigidbody.position).x - start.x, Is.GreaterThan(blink.Range - 1f),
                "the press that blinked does not also recall");
            Assert.That(blink.Ready, Is.False, "the blink is on cooldown");
            Assert.That(blink.MarkerLeft, Is.InRange(0.5f, 1f), "the marker fades over the recall window");

            var before = crimson.Rigidbody.linearVelocity;
            crimson.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();

            Assert.That(Mathf.Abs(Plane(crimson.Rigidbody.position).x - start.x), Is.LessThan(0.5f), "back at the marker");
            Assert.That(Vector3.Distance(crimson.Rigidbody.linearVelocity, before), Is.LessThan(0.5f), "keeping its velocity");
            Assert.That(blink.Activations, Is.EqualTo(1), "a recall is not a second blink");
            Assert.That(blink.TelledPresses, Is.EqualTo(2), "but the recall plays the blink's tell");
            Assert.That(blink.Marker, Is.Null, "the marker is spent");
        }

        [UnityTest]
        public IEnumerator Recall_ReachesTheKit_FromThePlayersSpaceBar_WhileTheBlinkIsOnCooldown()
        {
            var input = new PlayModeInputTestFixture();
            input.Setup();
            try
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                yield return SpawnCrimson();
                blink.Behaviours.Set(new BehaviourMod[] { TestAssets.Load<KeystoneMod>(RecallPath) });
                crimson.AddCommander(TestAssets.LoadPlayerPilot());
                var start = Plane(crimson.Rigidbody.position);

                yield return Tap(input, keyboard, () => blink.Activations == 1, "the first press blinks");
                for (var i = 0; i < 3; i++)
                    yield return new WaitForFixedUpdate();
                Assert.That(Vector2.Distance(Plane(crimson.Rigidbody.position), start), Is.GreaterThan(blink.Range - 1f));
                Assert.That(blink.Ready, Is.False, "test premise: the blink is on cooldown");

                yield return Tap(input, keyboard, () => !blink.Marker.HasValue, "the second press recalls");
                Assert.That(Vector2.Distance(Plane(crimson.Rigidbody.position), start), Is.LessThan(1f), "back at the start");
                Assert.That(blink.Activations, Is.EqualTo(1));
                Assert.That(crimson.KeystoneTakesPress, Is.False, "with the marker spent, a press waits for the cooldown");
            }
            finally
            {
                input.TearDown();
            }
        }

        private static IEnumerator Tap(PlayModeInputTestFixture input, Keyboard keyboard, System.Func<bool> done, string what)
        {
            input.Press(keyboard.spaceKey);
            for (var i = 0; i < 30 && !done(); i++)
                yield return new WaitForFixedUpdate();
            input.Release(keyboard.spaceKey);
            Assert.That(done(), Is.True, what);
        }

        [UnityTest]
        public IEnumerator Recall_OutsideItsWindow_OrWithoutTheMod_DoesNothing([Values] bool held)
        {
            yield return SpawnCrimson();
            if (held) blink.Behaviours.Set(new BehaviourMod[] { TestAssets.Load<KeystoneMod>(RecallPath) });
            var start = Plane(crimson.Rigidbody.position);
            crimson.Movement.Drive(new PilotCommand { boost = 1f, abilityTarget = start + new Vector2(30f, 0f) });
            yield return new WaitForFixedUpdate();
            var window = new SerializedObject(blink).FindProperty("recallWindow").floatValue;
            var wait = held ? Mathf.CeilToInt(window / Time.fixedDeltaTime) + 2 : 3;
            for (var i = 0; i < wait; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(blink.Ready, Is.False, "the press comes on cooldown");
            var landed = Plane(crimson.Rigidbody.position);

            crimson.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();

            Assert.That(Vector2.Distance(Plane(crimson.Rigidbody.position), landed), Is.LessThan(0.5f), "no jump back");
        }

        [UnityTest]
        public IEnumerator ShieldBash_StunsAnEnemyTheBlinkPassesThrough_AndTheRecallBack([Values] bool held)
        {
            yield return SpawnCrimson();
            var mods = new List<BehaviourMod> { TestAssets.Load<KeystoneMod>(RecallPath) };
            if (held) mods.Add(TestAssets.Load<KeystoneMod>(ShieldBashPath));
            blink.Behaviours.Set(mods.ToArray());
            var start = Plane(crimson.Rigidbody.position);
            rival = Object.Instantiate(TestAssets.LoadCrimsonPrefab(), GamePlane.PlanePointToWorld(start + new Vector2(6f, 0f)),
                GamePlane.Rotation);
            rival.Initialize(1, 0, Projectiles);
            yield return new WaitForFixedUpdate();

            crimson.Movement.Drive(new PilotCommand { boost = 1f, abilityTarget = start + new Vector2(30f, 0f) });
            yield return new WaitForFixedUpdate();
            Assert.That(Plane(crimson.Rigidbody.position).x - start.x, Is.GreaterThan(blink.Range - 1f),
                "test premise: the blink jumps clean past the enemy");
            Assert.That(rival.Status.Has(StatusEffect.Stunned), Is.EqualTo(held),
                held ? "the blink stuns the enemy it passes through" : "without Shield bash it flies on");
            var stun = new SerializedObject(blink).FindProperty("shieldBashStun").floatValue;
            if (held)
                Assert.That(rival.Status.Remaining(StatusEffect.Stunned), Is.EqualTo(stun).Within(0.05f), "for the stun's length");

            for (var i = 0; i < Mathf.CeilToInt(stun / Time.fixedDeltaTime) + 2; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(rival.Status.Has(StatusEffect.Stunned), Is.False, "the stun wears off");

            crimson.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(Plane(crimson.Rigidbody.position), start), Is.LessThan(1f), "test premise: recalled");
            Assert.That(rival.Status.Has(StatusEffect.Stunned), Is.EqualTo(held), "the recall back through it stuns it again");
        }

        [UnityTest]
        public IEnumerator AsteroidImpact_DamagesTheShip()
        {
            yield return SpawnCrimson();
            var hits = new List<AppliedDamage>();
            crimson.Damage.OnDamaged += hits.Add;
            var rock = SpawnRock(GamePlane.Forward * 8f);
            // The mesh collider switches on in LateUpdate.
            yield return null;
            yield return new WaitForFixedUpdate();

            for (var i = 0; i < 60 && hits.Count == 0; i++)
            {
                crimson.Rigidbody.linearVelocity = GamePlane.Forward * 20f;
                yield return new WaitForFixedUpdate();
            }

            Assert.That(hits, Is.Not.Empty, "the asteroid impact damaged the ship");
            Assert.That(hits[0].Info.Kind, Is.EqualTo(DamageKind.Collision));
            Assert.That(hits[0].Info.AttackerId, Is.EqualTo(ShipId.Invalid));
            Assert.That(hits[0].Info.HitMass, Is.EqualTo(rock.Mass));
            Assert.That(hits[0].Info.Amount, Is.GreaterThan(0f));
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
            var overlapping = Physics.OverlapSphereNonAlloc(crimson.Rigidbody.position, crimson.ShipRadius, new Collider[4], LayerMask.GetMask("Asteroid"));
            Assert.That(overlapping, Is.Zero, "and not inside it");
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
            missile.SetTarget(crimson, holdFor: 0f);
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
            spawner.SetCollisionDamageLaw(new CollisionDamageLaw(graceDeltaV: 5f, damagePerDeltaV: 3f, lethality: 1f));
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
