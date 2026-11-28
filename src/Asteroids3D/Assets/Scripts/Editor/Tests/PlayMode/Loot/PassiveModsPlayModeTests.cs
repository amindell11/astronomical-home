#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Damage;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Mods;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Loot
{
    /// <summary>The passives on shipped prefabs and keys: a Backstab holder's rear hit skips a live ship's shield, a rock a Shrapnel rocks holder breaks throws its fragments away from it, armed against other ships, a Turret holder's guns swing to the aim point while its hull turns at half rate, and return to the nose when the run's mods clear, a Lie in wait holder hides beside a heavy rock and shows when it thrusts, and a Duck and load holder's guns cool faster while nothing sights it.</summary>
    [Category("Ships")]
    public class PassiveModsPlayModeTests : PlayModeWorldFixture
    {
        private const string BackstabPath = "Assets/Settings/Ships/Passives/Backstab.asset";
        private const string ShrapnelPath = "Assets/Settings/Ships/Passives/ShrapnelRocks.asset";
        private const string TurretPath = "Assets/Settings/Ships/Passives/Turret.asset";
        private const string LieInWaitPath = "Assets/Settings/Ships/Passives/LieInWait.asset";
        private const string DuckAndLoadPath = "Assets/Settings/Ships/Passives/DuckAndLoad.asset";
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";
        private const string FragSettingsPath = "Assets/Settings/Asteroids/FragSettings.asset";
        private const float Scale = 2f;

        private readonly List<Object> made = new();

        public override void TearDown()
        {
            foreach (var item in made) if (item) Object.DestroyImmediate(item);
            made.Clear();
            base.TearDown();
        }

        private Ship SpawnCrimson(Vector3 position, params LullMod[] picks)
        {
            var ship = Object.Instantiate(TestAssets.LoadCrimsonPrefab(), position, GamePlane.Rotation);
            made.Add(ship.gameObject);
            ship.Initialize(0, 0, Projectiles);
            var stack = new LullModStack();
            ship.BindMods(stack);
            foreach (var pick in picks) stack.Add(pick);
            return ship;
        }

        [Test]
        public void ABackstabbersRearHit_SkipsTheShield_AndPopsItsWord_AndAFrontHitDoesNot()
        {
            var attacker = SpawnCrimson(new Vector3(0f, -40f, 0f), TestAssets.Load<PassiveMod>(BackstabPath));
            var victim = SpawnCrimson(Vector3.zero);
            var shield = victim.Damage.Shield.CurrentValue;
            var hull = victim.Damage.Health.CurrentValue;
            Assert.IsTrue(attacker.Backstab.Held, "the shipped key reaches the prefab's Backstab");
            var words = new List<string>();
            attacker.Words.Raised += (word, _) => words.Add(word.Text);

            DamageInfo From(Vector3 point) =>
                new(10f, DamageKind.Laser, attacker.Id, 0.1f, Vector3.zero, point, attacker: attacker);

            victim.Damage.TakeDamage(From(victim.transform.position - victim.transform.up * 2f));
            Assert.AreEqual((shield, hull - 10f), (victim.Damage.Shield.CurrentValue, victim.Damage.Health.CurrentValue),
                "a rear hit goes straight to the hull");
            Assert.That(words, Is.EqualTo(new[] { "BACKSTAB" }), "a rear hit pops the word on the attacker");

            victim.Damage.TakeDamage(From(victim.transform.position + victim.transform.up * 2f));
            Assert.AreEqual((shield - 10f, hull - 10f), (victim.Damage.Shield.CurrentValue, victim.Damage.Health.CurrentValue),
                "a front hit meets the shield");
            Assert.That(words, Has.Count.EqualTo(1), "a front hit pops nothing");
        }

        [UnityTest]
        public IEnumerator ARockAShrapnelHolderBreaks_ThrowsItsFragmentsAwayFromIt()
        {
            var thrower = SpawnCrimson(new Vector3(-60f, 0f, 0f), TestAssets.Load<PassiveMod>(ShrapnelPath));
            var plain = SpawnCrimson(new Vector3(-60f, 80f, 0f));
            var enemy = SpawnCrimson(new Vector3(0f, -80f, 0f));
            Assert.IsTrue(thrower.ShrapnelRocks.Held && !plain.ShrapnelRocks.Held);

            var spawner = NewSpawner(out var settings);
            var info = settings.meshInfos[0];
            var attributes = new AsteroidAttributes(info, 0, info.cachedVolume * Scale * Scale * Scale * settings.density,
                Scale, Vector3.zero, Vector3.zero);
            var thrown = spawner.Spawn(new Pose(Vector3.zero, Quaternion.identity), attributes, 713);
            var ordinary = spawner.Spawn(new Pose(new Vector3(0f, 80f, 0f), Quaternion.identity), attributes, 713);
            yield return null;

            var thrownFragments = Shatter(spawner, thrown, thrower);
            var ordinaryFragments = Shatter(spawner, ordinary, plain);
            for (var frame = 0; frame < 20; frame++) yield return null;

            Assert.IsNotEmpty(thrownFragments, "the rock broke into fragments");
            var thrownAway = thrownFragments.Average(fragment => fragment.Rb.linearVelocity.x);
            var ordinaryAway = ordinaryFragments.Average(fragment => fragment.Rb.linearVelocity.x);
            Debug.Log($"[Shrapnel] mean speed away from the breaker: thrown {thrownAway:F2} m/s, ordinary {ordinaryAway:F2} m/s");
            Assert.Greater(thrownAway, ordinaryAway + 6f, "shrapnel flies away from its thrower");

            var fragmentDamage = thrownFragments[0].Damage;
            Assert.Greater(fragmentDamage.DamageTo(enemy.Damage, 10f), fragmentDamage.DamageTo(thrower.Damage, 10f),
                "a thrown fragment hits an enemy harder than its thrower");
            Assert.AreEqual(ordinaryFragments[0].Damage.DamageTo(enemy.Damage, 10f), fragmentDamage.DamageTo(thrower.Damage, 10f),
                1e-4f, "an ordinary fragment hits like a thrown one hits its thrower");
        }

        [UnityTest]
        public IEnumerator ATurretHolder_AimsItsGunsAtTheAimPoint_AndTurnsAtHalfRate()
        {
            var turret = SpawnCrimson(new Vector3(0f, -40f, 0f), TestAssets.Load<PassiveMod>(TurretPath));
            var plain = SpawnCrimson(new Vector3(0f, 40f, 0f));
            Assert.IsTrue(turret.Turret.Held, "the shipped key reaches the prefab's Turret");
            Assert.AreEqual(plain.MaxYawRate * 0.5f, turret.MaxYawRate, 1e-3f, "the hull turns at half rate");
            Assert.AreEqual(plain.Stats.reverseForce * 2.5f, turret.Stats.reverseForce, 1e-2f, "reverse thrust is stronger");

            var plainFacing = plain.Weapons.Primary.FireDirection;
            var turretNose = turret.Weapons.Primary.FireDirection;
            var aside = GamePlane.Right * 30f;
            Aim(turret, turret.transform.position + aside);
            Aim(plain, plain.transform.position + aside);
            yield return null;

            var gun = turret.Weapons.Primary;
            var toAim = GamePlane.WorldDirOnPlane(turret.transform.position + aside - gun.FireOrigin);
            Assert.Less(Vector3.Angle(gun.FireDirection, toAim), 1f, "the Turret's guns point at the aim point");
            Assert.Less(Vector3.Angle(plain.Weapons.Primary.FireDirection, plainFacing), 1f, "a plain ship's guns keep to the nose");

            turret.Mods.Clear();
            Aim(turret, turret.transform.position + aside);
            yield return null;
            Assert.Less(Vector3.Angle(turret.Weapons.Primary.FireDirection, turretNose), 1f,
                "once the run's mods clear, the guns return to the nose");
            Assert.AreEqual(plain.MaxYawRate, turret.MaxYawRate, 1e-3f, "and the hull turns at its own rate");
        }

        private void Aim(Ship ship, Vector3 point) =>
            ship.Weapons.Arm(Projectiles).Fire(WeaponSlot.Primary, new WeaponCommand { targetPoint = point });

        [UnityTest]
        public IEnumerator ALieInWaitHolder_HidesBesideAHeavyRock_AndShowsWhenItThrusts()
        {
            var ship = SpawnCrimson(Vector3.zero, TestAssets.Load<PassiveMod>(LieInWaitPath));
            Assert.IsTrue(ship.LieInWait.Held, "the shipped key reaches the prefab's Lie in wait");
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            made.Add(rock);
            rock.layer = LayerIds.Asteroid;
            rock.transform.localScale = Vector3.one * 4f;
            rock.transform.position = ship.transform.position + GamePlane.Right * (ship.ShipRadius + 3.5f);
            var body = rock.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = 10f;
            Physics.SyncTransforms();

            yield return new WaitForSeconds(1.3f);
            Assert.IsFalse(ship.Lock.IsConcealed, "a light rock hides nothing");

            body.mass = ship.Body.mass * 100f;
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(ship.Lock.IsConcealed, "the ship hides only once it has held still long enough");
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(ship.Lock.IsConcealed, "held still beside a heavy rock, the ship hides");

            ship.Movement.Drive(new PilotCommand { thrust = 1f });
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(ship.Lock.IsConcealed, "thrusting shows the ship");
        }

        [UnityTest]
        public IEnumerator ADuckAndLoadHolder_CoolsFaster_WhileNothingSightsIt()
        {
            var ship = SpawnCrimson(Vector3.zero, TestAssets.Load<PassiveMod>(DuckAndLoadPath));
            Assert.IsTrue(ship.DuckAndLoad.Held, "the shipped key reaches the prefab's Duck and load");
            var heat = ship.Weapons.Primary.GetComponent<Heat>();
            Assert.IsTrue(heat, "the Crimson's primary runs on heat");

            for (var shot = 0; shot < 3; shot++) heat.ProcessFire();
            var start = Time.time;
            while (Time.time - start < 0.8f) yield return null;
            var unseen = heat.CurrentHeat;

            heat.Reset();
            for (var shot = 0; shot < 3; shot++) heat.ProcessFire();
            start = Time.time;
            while (Time.time - start < 0.8f)
            {
                ship.Lock.NoteSighted(Time.time);
                yield return null;
            }
            var seen = heat.CurrentHeat;

            Debug.Log($"[DuckAndLoad] heat after 0.8 s: unseen {unseen:F1}, sighted {seen:F1}");
            Assert.Greater(seen, 0f, "sighted, the gun cools at its own rate");
            Assert.Less(unseen, seen - 15f, "unseen, the gun cools much faster");
        }

        private static List<AsteroidController> Shatter(AsteroidSpawner spawner, AsteroidController rock, Ship breaker)
        {
            var fragments = new List<AsteroidController>();
            spawner.OnFragmentSpawned += fragments.Add;
            rock.Damage.TakeDamage(new DamageInfo(rock.Damage.MaxHealth + 1, DamageKind.Laser, breaker.Id, 2f,
                new Vector3(0f, 4f, 0f), rock.transform.position, attacker: breaker));
            spawner.OnFragmentSpawned -= fragments.Add;
            return fragments;
        }

        private AsteroidSpawner NewSpawner(out AsteroidSpawnSettings settings)
        {
            settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);
            var holder = new GameObject("Asteroid spawner");
            made.Add(holder);
            holder.SetActive(false);
            SetReference(holder.AddComponent<Fragger>(), "asteroidFragAsteroidFragSettings",
                AssetDatabase.LoadAssetAtPath<AsteroidFragSettings>(FragSettingsPath));
            var spawner = holder.AddComponent<AsteroidSpawner>();
            SetReference(spawner, "settings", settings);
            holder.SetActive(true);
            spawner.SetCollisionDamageLaw(new CollisionDamageLaw(graceDeltaV: 5f, damagePerDeltaV: 3f, lethality: 1f));
            return spawner;
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
