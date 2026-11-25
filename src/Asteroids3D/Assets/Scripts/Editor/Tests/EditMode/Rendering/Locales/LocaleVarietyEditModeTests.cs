using System.Linq;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rendering.Locales
{
    [Category("Sectors")]
    public sealed class LocaleVarietyEditModeTests
    {
        private static readonly string[] NewLocales = { "CoralSunset", "CyanAurora", "VerdantGiant", "BlueCrescent" };

        [TestCase("CoralSunset", "coral-sunset", false)]
        [TestCase("CyanAurora", "cyan-aurora", false)]
        [TestCase("VerdantGiant", "violet-planet", true, Category = "RequiresLfsPng")]
        [TestCase("BlueCrescent", "amber-planet", true, Category = "RequiresLfsPng")]
        public void LocaleIsCompleteWithIndependentPaletteMaterials(string name, string palette, bool hasPlanet)
        {
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/{name}.prefab");
            var sky = locale.transform.Find("Sky");
            foreach (var renderer in sky.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.That(renderer.gameObject.layer, Is.EqualTo(LayerIds.Sky));
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                    Does.StartWith($"Assets/Visuals/Locales/Sky/Locales/{name}/"));
            }
            foreach (var layer in new[] { "Background", "FarNebula", "CloseNebula" })
            {
                var material = sky.Find(layer).GetComponent<MeshRenderer>().sharedMaterial;
                var suffix = layer == "Background" ? "" : "." + layer;
                Assert.That(AssetDatabase.GetAssetPath(material.parent),
                    Is.EqualTo($"{FlatBackgroundImport.Folder}/{palette}-final{suffix}.mat"));
            }
            var planets = sky.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.sharedMaterial.shader.name == "Locales/Planet").ToArray();
            Assert.That(planets.Length, Is.EqualTo(hasPlanet ? 1 : 0));
            if (planets.Length == 0) return;
            var planet = planets[0].sharedMaterial;
            var stars = sky.Find("StarField").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(planet.renderQueue, Is.GreaterThan(stars.renderQueue), "The planet must hide every star layer.");
            Assert.That(planet.renderQueue, Is.LessThan(sky.Find("CloseNebula").GetComponent<MeshRenderer>().sharedMaterial.renderQueue));
            var texture = planet.mainTexture;
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(texture.height * 2));
            Assert.That(texture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(texture.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [Test]
        public void NewLocalesShareNeutralShipLightingAndReflections()
        {
            Color[] previousReflection = null;
            Light[] previousLights = null;
            foreach (var name in NewLocales)
            {
                var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/{name}.prefab");
                var lighting = new SerializedObject(locale).FindProperty("lighting");
                Assert.That(lighting.FindPropertyRelative("ambientSkyColor").colorValue,
                    Is.EqualTo(new Color(.24f, .24f, .24f)));
                Assert.That(lighting.FindPropertyRelative("ambientIntensity").floatValue, Is.EqualTo(1));
                Assert.That(lighting.FindPropertyRelative("reflectionIntensity").floatValue, Is.EqualTo(.35f));
                var reflection = (Cubemap)lighting.FindPropertyRelative("reflection").objectReferenceValue;
                var colors = Enumerable.Range(0, 6).SelectMany(face => reflection.GetPixels((CubemapFace)face)).ToArray();
                Assert.That(colors.All(c => c.r == c.g && c.g == c.b), Is.True,
                    "Reflection light must not tint a ship to match its sky.");
                if (previousReflection != null) Assert.That(colors, Is.EqualTo(previousReflection));
                previousReflection = colors;
                var lights = locale.GetComponentsInChildren<Light>(true).OrderBy(light => light.name).ToArray();
                Assert.That(lights, Has.Length.EqualTo(2));
                foreach (var light in lights)
                {
                    Assert.That(light.color, Is.EqualTo(Color.white));
                    Assert.That(light.useColorTemperature, Is.False);
                }
                if (previousLights != null)
                    for (var i = 0; i < lights.Length; i++)
                    {
                        Assert.That(lights[i].intensity, Is.EqualTo(previousLights[i].intensity));
                        Assert.That(lights[i].transform.rotation, Is.EqualTo(previousLights[i].transform.rotation));
                    }
                previousLights = lights;
            }
        }
    }
}
