using System.Linq;
using NUnit.Framework;
using Substrate;
using Substrate.Sectors;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rendering.Locales
{
    [Category("Sectors")]
    public sealed class LocaleVarietyEditModeTests
    {
        [TestCase(4, "coral-sunset", "CoralSunset")]
        [TestCase(5, "cyan-aurora", "CyanAurora")]
        [TestCase(6, "violet-planet", "VerdantGiant")]
        [TestCase(7, "amber-planet", "BlueCrescent")]
        public void PreviewConfigSelectsItsCompleteLocaleWithIndependentPaletteMaterials(int number, string palette, string config)
        {
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/Locale_{number}.prefab");
            var sector = AssetDatabase.LoadAssetAtPath<SectorSettings>($"Assets/Settings/Game/Preview/Locales/{config}.asset");
            Assert.That(sector.Locale, Is.EqualTo(locale));
            var sky = locale.transform.Find("Sky");
            foreach (var renderer in sky.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.That(renderer.gameObject.layer, Is.EqualTo(LayerIds.Sky));
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                    Does.StartWith($"Assets/Visuals/Locales/Sky/Locales/Locale_{number}/"));
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
            Assert.That(planets.Length, Is.EqualTo(number >= 6 ? 1 : 0));
            if (planets.Length == 0) return;
            var planet = planets[0].sharedMaterial;
            var stars = sky.Find("StarField").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(planet.renderQueue, Is.GreaterThan(stars.renderQueue), "The planet must hide every star layer.");
            Assert.That(planet.renderQueue, Is.LessThan(sky.Find("CloseNebula").GetComponent<MeshRenderer>().sharedMaterial.renderQueue));
            var texture = planet.mainTexture;
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(texture.height));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }
    }
}
