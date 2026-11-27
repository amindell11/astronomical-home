#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Damage;
using Loot;
using NUnit.Framework;
using Ships;
using Ships.Mods;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Loot
{
    /// <summary>The pitch passives on shipped prefabs and keys: a Backstab holder's rear hit skips a live ship's shield, and a rock a Shrapnel rocks holder breaks throws its fragments away from it, armed against other ships.</summary>
    [Category("Ships")]
    public class PassiveModsPlayModeTests : PlayModeWorldFixture
    {
        private const string BackstabPath = "Assets/Settings/Ships/Passives/Backstab.asset";
        private const string ShrapnelPath = "Assets/Settings/Ships/Passives/ShrapnelRocks.asset";
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
        public void ABackstabbersRearHit_SkipsTheShield_AndAFrontHitDoesNot()
        {
            var attacker = SpawnCrimson(new Vector3(0f, -40f, 0f), TestAssets.Load<PassiveMod>(BackstabPath));
            var victim = SpawnCrimson(Vector3.zero);
            var shield = victim.Damage.Shield.CurrentValue;
            var hull = victim.Damage.Health.CurrentValue;
            Assert.IsTrue(attacker.Backstab.Held, "the shipped key reaches the prefab's Backstab");

            DamageInfo From(Vector3 point) =>
                new(10f, DamageKind.Laser, attacker.Id, 0.1f, Vector3.zero, point, attacker: attacker);

            victim.Damage.TakeDamage(From(victim.transform.position - victim.transform.up * 2f));
            Assert.AreEqual((shield, hull - 10f), (victim.Damage.Shield.CurrentValue, victim.Damage.Health.CurrentValue),
                "a rear hit goes straight to the hull");

            victim.Damage.TakeDamage(From(victim.transform.position + victim.transform.up * 2f));
            Assert.AreEqual((shield - 10f, hull - 10f), (victim.Damage.Shield.CurrentValue, victim.Damage.Health.CurrentValue),
                "a front hit meets the shield");
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
