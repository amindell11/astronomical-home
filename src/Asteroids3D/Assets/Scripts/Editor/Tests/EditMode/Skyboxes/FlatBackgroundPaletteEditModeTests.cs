using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Substrate.Services.Environment;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Skyboxes
{
    [Category("Sectors")]
    public class FlatBackgroundPaletteEditModeTests
    {
        private static readonly Color Primary = new(0.35f, 0.008f, 0.52f);
        private static readonly Color Secondary = new(0.018f, 0.18f, 0.72f);
        private static readonly Color Accent = new(0.75f, 0.025f, 0.16f);

        private string folder;
        private string sidecar;

        [SetUp]
        public void SetUp()
        {
            folder = FlatBackgroundImport.Folder + "/test-" + Guid.NewGuid().ToString("N");
            sidecar = folder + "/clouds-final.json";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(folder);

        [Test]
        public void SidecarImport_WritesPaletteParents_OverridingOnlyTheMappedColours()
        {
            Publish(Primary, Secondary, Accent);

            var far = PaletteParent("FarNebula");
            var close = PaletteParent("CloseNebula");
            Assert.AreSame(Load("Assets/Visuals/Environment/Sky/NebulaMaterial.mat"), far.parent);
            Assert.AreSame(Load("Assets/Visuals/Environment/Sky/ForegroundNebulaMaterial.mat"), close.parent);
            AssertColour(Secondary.gamma, far.GetColor("_NebulaCool"));
            AssertColour(Primary.gamma, far.GetColor("_NebulaWarm"));
            AssertColour(Secondary.gamma, close.GetColor("_NebulaCool"));
            AssertColour(Accent.gamma, close.GetColor("_NebulaWarm"));
            foreach (var parent in new[] { far, close })
            {
                Assert.That(parent.renderQueue, Is.EqualTo(parent.parent.renderQueue));
                for (var i = 0; i < parent.shader.GetPropertyCount(); i++)
                {
                    var property = parent.shader.GetPropertyName(i);
                    Assert.That(parent.IsPropertyOverriden(property),
                        Is.EqualTo(property is "_NebulaCool" or "_NebulaWarm"), $"{parent.name}.{property}");
                }
            }
        }

        [Test]
        public void Reimport_RefreshesInheritedColours_KeepsLocaleOverrides_AndRevertReturnsToPalette()
        {
            Publish(Primary, Secondary, Accent);
            var far = PaletteParent("FarNebula");
            var locale = new Material(far) { parent = far };
            locale.RevertAllPropertyOverrides();
            AssetDatabase.CreateAsset(locale, folder + "/Locale.mat");
            locale.SetColor("_NebulaWarm", Color.green);
            AssetDatabase.SaveAssetIfDirty(locale);

            var primary = new Color(0.62f, 0.05f, 0.015f);
            var secondary = new Color(0.8f, 0.26f, 0.03f);
            Publish(primary, secondary, Accent);

            AssertColour(secondary.gamma, far.GetColor("_NebulaCool"));
            AssertColour(primary.gamma, far.GetColor("_NebulaWarm"));
            AssertColour(secondary.gamma, locale.GetColor("_NebulaCool"));
            AssertColour(Color.green, locale.GetColor("_NebulaWarm"));
            locale.RevertPropertyOverride("_NebulaWarm");
            AssertColour(primary.gamma, locale.GetColor("_NebulaWarm"));
        }

        [Test]
        public void Parse_RejectsAnUnknownSchemaVersion()
        {
            Assert.Throws<FormatException>(() =>
                FlatBackgroundSidecar.Parse(Json(2, Primary, Secondary, Accent), "test"));
        }

        private void Publish(Color primary, Color secondary, Color accent)
        {
            File.WriteAllText(sidecar, Json(FlatBackgroundSidecar.SchemaVersion, primary, secondary, accent));
            AssetDatabase.ImportAsset(sidecar, ImportAssetOptions.ForceUpdate);
        }

        private Material PaletteParent(string layer)
        {
            var material = Load(PaletteParents.PathFor(sidecar, layer));
            Assert.IsTrue(material, $"No {layer} palette parent was written beside {sidecar}.");
            return material;
        }

        private static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        private static string Json(int schema, Color primary, Color secondary, Color accent) =>
            $"{{\"schema_version\": {schema}, \"palette\": {{\"base\": [0, 0, 0], \"primary\": {Rgb(primary)}, " +
            $"\"secondary\": {Rgb(secondary)}, \"accent\": {Rgb(accent)}}}}}";

        private static string Rgb(Color colour) =>
            string.Format(CultureInfo.InvariantCulture, "[{0}, {1}, {2}]", colour.r, colour.g, colour.b);

        private static void AssertColour(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f));
        }
    }
}
