#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Damage;
using NUnit.Framework;
using Ships.Registry;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class AsteroidBreakAudioPlayModeTests : FmodEvidenceFixture
    {
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";
        private const string FragSettingsPath = "Assets/Settings/Asteroids/FragSettings.asset";
        private const string BreakEvent = "event:/World/Asteroid/Break";
        private const float Scale = 1.65f;

        private GameObject holder;

        [TearDown]
        public override void TearDown()
        {
            DestroyTestObject(holder);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator DestroyedAsteroids_PlayOneBreakEach_WhereEachBroke()
        {
            var breaks = Fresh(BreakEvent);
            var spawner = NewSpawner(out var settings);
            var info = settings.meshInfos[0];
            var attributes = new AsteroidAttributes(info, 0, info.cachedVolume * Scale * Scale * Scale * settings.density,
                Scale, Vector3.zero, Vector3.zero);
            var first = spawner.Spawn(new Pose(new Vector3(-20f, 0f, 0f), Quaternion.identity), attributes, 713);
            var second = spawner.Spawn(new Pose(new Vector3(20f, 0f, 0f), Quaternion.identity), attributes, 714);
            yield return Flush();
            Assert.AreEqual(0, Instances(breaks).Length, "spawning breaks nothing");

            foreach (var rock in new[] { first, second })
            {
                breaks = Fresh(BreakEvent);
                var position = rock.transform.position;
                Shatter(rock);
                yield return Flush();

                var instances = Instances(breaks);
                Debug.Log($"[FMOD] {BreakEvent} instances={instances.Length} after destroying the asteroid at {position}");
                Assert.AreEqual(1, instances.Length, "one break per destroyed asteroid; its fragments play none");
                Assert.Less(Vector3.Distance(Position(instances[0]), position), 0.01f, "played where the asteroid broke");
                AssertPlaying(instances[0], "the break still sounds a frame later");
            }
        }

        // The game's spawner and fragger, as the field wires them, so the asteroid is the authored one.
        private AsteroidSpawner NewSpawner(out AsteroidSpawnSettings settings)
        {
            settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);
            holder = new GameObject("Asteroid spawner");
            holder.SetActive(false);
            SetReference(holder.AddComponent<Fragger>(), "asteroidFragAsteroidFragSettings",
                AssetDatabase.LoadAssetAtPath<AsteroidFragSettings>(FragSettingsPath));
            var spawner = holder.AddComponent<AsteroidSpawner>();
            SetReference(spawner, "settings", settings);
            holder.SetActive(true);
            return spawner;
        }

        private static void Shatter(AsteroidController rock) =>
            rock.Damage.TakeDamage(new DamageInfo(rock.Damage.MaxHealth + 1, DamageKind.Collision,
                ShipId.Invalid, 2, new Vector3(4, 1, 0), rock.transform.position));

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
