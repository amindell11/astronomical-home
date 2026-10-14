#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Asteroids.Visual;
using Damage;
using NUnit.Framework;
using Ships.Registry;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode.Presentation
{
    [Category("Asteroids")]
    public sealed class DrawnAsteroidAppearancePlayModeTests : PlayModeWorldFixture
    {
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";
        private const string FragSettingsPath = "Assets/Settings/Asteroids/FragSettings.asset";
        private const string ExplosionName = "LayeredAsteroidExplosion";
        private const uint BreakSeed = 713;

        private GameObject holder;
        private PooledVFX explosion;

        public override void TearDown()
        {
            DestroyTestObject(holder);
            DestroyTestObject(explosion);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LethalDamage_HidesLayers_FragmentsTakeTheirShape_ReuseRestoresLayers()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);
            var shapes = ReadShapes(settings.asteroidPrefab.GetComponent<DrawnAsteroidAppearance>());

            holder = new GameObject("Asteroid spawner");
            holder.SetActive(false);
            SetReference(holder.AddComponent<Fragger>(), "asteroidFragAsteroidFragSettings",
                AssetDatabase.LoadAssetAtPath<AsteroidFragSettings>(FragSettingsPath));
            var spawner = holder.AddComponent<AsteroidSpawner>();
            SetReference(spawner, "settings", settings);
            var fragments = new List<(AsteroidController rock, Mesh surface, Material paint, Mesh drawing, Mesh contour)>();
            spawner.OnFragmentSpawned += rock => fragments.Add((rock, rock.CurrentMesh, rock.Renderer.sharedMaterial,
                Layer(rock, "Crease drawing").GetComponent<MeshFilter>().sharedMesh,
                Layer(rock, "Outer contour").GetComponent<MeshFilter>().sharedMesh));
            holder.SetActive(true);

            var info = settings.meshInfos[0];
            const float scale = 1.65f;
            var attributes = new AsteroidAttributes(info, 0, info.cachedVolume * scale * scale * scale * settings.density,
                scale, Vector3.zero, Vector3.zero);
            var parent = spawner.Spawn(new Pose(Vector3.zero, Quaternion.identity), attributes, BreakSeed);
            var priorExplosions = ActiveExplosions().ToList();

            parent.Damage.TakeDamage(new DamageInfo(parent.Damage.MaxHealth + 1, DamageKind.Collision,
                ShipId.Invalid, 2, new Vector3(4, 1, 0), parent.transform.position));

            Assert.That(Layer(parent, "Crease drawing").gameObject.activeSelf, Is.False);
            Assert.That(Layer(parent, "Outer contour").gameObject.activeSelf, Is.False);
            var spawned = ActiveExplosions().Except(priorExplosions).ToArray();
            Assert.That(spawned, Has.Length.EqualTo(1), "Asteroid destruction must spawn its assigned explosion prefab.");
            explosion = spawned[0];

            Assert.That(fragments, Is.Not.Empty, "Lethal damage must fragment the asteroid.");
            foreach (var fragment in fragments)
            {
                var shape = shapes[fragment.rock.MeshIndex];
                Assert.That(fragment.surface, Is.SameAs(shape.surface), "surface");
                Assert.That(fragment.paint, Is.SameAs(shape.paint), "paint");
                Assert.That(fragment.drawing, Is.SameAs(shape.drawing), "drawing");
                Assert.That(fragment.contour, Is.SameAs(shape.surface), "contour");
            }

            for (var frame = 0; frame < 300 && parent.gameObject.activeSelf; frame++)
                yield return null;
            Assert.That(parent.gameObject.activeSelf, Is.False, "The destroyed asteroid must return to the pool.");

            var reused = spawner.Spawn(new Pose(Vector3.zero, Quaternion.identity), attributes, BreakSeed);
            Assert.That(reused, Is.SameAs(parent));
            Assert.That(Layer(reused, "Crease drawing").gameObject.activeSelf, Is.True);
            Assert.That(Layer(reused, "Outer contour").gameObject.activeSelf, Is.True);
        }

        private static (Mesh surface, Mesh drawing, Material paint)[] ReadShapes(DrawnAsteroidAppearance appearance)
        {
            var shapes = new SerializedObject(appearance).FindProperty("shapes");
            var result = new (Mesh surface, Mesh drawing, Material paint)[shapes.arraySize];
            for (var i = 0; i < result.Length; i++)
            {
                var shape = shapes.GetArrayElementAtIndex(i);
                result[i] = ((Mesh)shape.FindPropertyRelative("surface").objectReferenceValue,
                    (Mesh)shape.FindPropertyRelative("drawing").objectReferenceValue,
                    (Material)shape.FindPropertyRelative("paint").objectReferenceValue);
            }
            return result;
        }

        private static IEnumerable<PooledVFX> ActiveExplosions() =>
            Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None)
                .Where(v => v.name.StartsWith(ExplosionName) && v.gameObject.activeInHierarchy);

        private static Transform Layer(AsteroidController rock, string name) => rock.transform.Find(name);

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
