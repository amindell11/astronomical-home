using System.Linq;
using Asteroids.Spawning;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Locales;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Presentation")]
    public sealed class IllustratedSceneWiringEditModeTests
    {
        [Test]
        public void Contours_DrawAfterEveryActiveIllustratedSkyLayerAndBeforeExplosions()
        {
            var shader = Shader.Find("Astronomical/Comparison/Drawn Contour");
            Assert.That(shader, Is.Not.Null);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Locales/Locale_3.unity", OpenSceneMode.Additive);
            try
            {
                var sky = scene.GetRootGameObjects().Where(g => g.activeSelf)
                    .SelectMany(g => g.GetComponentsInChildren<LocaleSky>()).Single();
                var materials = sky.GetComponentsInChildren<MeshRenderer>()
                    .Where(r => r.enabled && r.gameObject.layer == LayerIds.Sky)
                    .SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                Assert.That(materials, Is.Not.Empty);
                foreach (var material in materials)
                    Assert.That(shader.renderQueue, Is.GreaterThan(material.renderQueue),
                        $"{material.name} would overwrite contours without depth writes.");
                Assert.That(shader.renderQueue, Is.LessThan(3000), "Explosion particles must draw after contour ink.");
                foreach (var path in new[]
                {
                    "Assets/Prefabs/Ships/Ship_1.prefab",
                    "Assets/Prefabs/Ships/Ship_2.prefab",
                    "Assets/Visuals/Vfx/LayeredExplosion/Prefabs/FragmentingDrawnAsteroid.prefab"
                })
                {
                    var prefab = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                    Assert.That(prefab, Is.Not.Null);
                    var contours = prefab.GetComponentsInChildren<MeshRenderer>(true)
                        .SelectMany(r => r.sharedMaterials).Where(m => m && m.shader == shader).Distinct().ToArray();
                    Assert.That(contours, Is.Not.Empty, path);
                    foreach (var contour in contours)
                    {
                        Assert.That(contour.renderQueue, Is.GreaterThan(materials.Max(m => m.renderQueue)), path);
                        Assert.That(contour.renderQueue, Is.LessThan(3000), path);
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ProductionAsteroidSettings_SpawnTheSavedFragmentingDrawnPrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.asteroidPrefab, Is.Not.Null);
            Assert.That(settings.asteroidPrefab.GetComponents<Asteroids.Visual.DrawnAsteroidAppearance>(), Has.Length.EqualTo(1));
            Assert.That(settings.asteroidPrefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Crease drawing"), Is.EqualTo(1));
            Assert.That(settings.asteroidPrefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Outer contour"), Is.EqualTo(1));
            Assert.That(settings.asteroidPrefab.transform.Find("LowLODMESH").GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(settings.asteroidPrefab),
                Is.EqualTo("Assets/Visuals/Vfx/LayeredExplosion/Prefabs/FragmentingDrawnAsteroid.prefab"));
            var explosion = new SerializedObject(settings.asteroidPrefab.GetComponent<Asteroids.Visual.AsteroidVisual>())
                .FindProperty("explosionPrefab").objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(explosion),
                Is.EqualTo("Assets/Visuals/Vfx/LayeredExplosion/Prefabs/LayeredAsteroidExplosion.prefab"));
        }

        [Test]
        public void SavedDrawnSurfaces_PreserveEachSpawnShapeSilhouette()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
            var shapes = new SerializedObject(settings.asteroidPrefab.GetComponent<Asteroids.Visual.DrawnAsteroidAppearance>())
                .FindProperty("shapes");
            Assert.That(shapes.arraySize, Is.EqualTo(settings.meshInfos.Length));
            for (var i = 0; i < settings.meshInfos.Length; i++)
            {
                var source = settings.meshInfos[i].mesh;
                var surface = (Mesh)shapes.GetArrayElementAtIndex(i).FindPropertyRelative("surface").objectReferenceValue;
                using var sourceData = MeshUtility.AcquireReadOnlyMeshData(source);
                using var vertices = new NativeArray<Vector3>(sourceData[0].vertexCount, Allocator.Temp);
                sourceData[0].GetVertices(vertices);
                Assert.That(vertices.Length, Is.GreaterThan(0), $"Shape {i + 1} source mesh must be readable.");
                var converted = surface.vertices;
                var worst = 0f;
                for (var vertex = 0; vertex < vertices.Length; vertex += 13)
                {
                    var nearest = float.MaxValue;
                    foreach (var point in converted) nearest = Mathf.Min(nearest, (point - vertices[vertex]).sqrMagnitude);
                    worst = Mathf.Max(worst, Mathf.Sqrt(nearest));
                }
                Assert.That(worst, Is.LessThan(source.bounds.size.magnitude * .025f),
                    $"Shape {i + 1} must preserve source coordinates and silhouette.");
            }
        }
    }
}
