using System.Linq;
using Asteroids.Spawning;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Locales;
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
        }
    }
}
