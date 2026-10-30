using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.UI.Projection
{
    [Category("UI")]
    public sealed class CanopyLegacyAssetsEditModeTests
    {
        [Test]
        public void HudAssetsDoNotReferenceDeletedGlowAssets()
        {
            var retired = new[] {
                "735aad0cefe7caa4eacb569c0d22a65b",
                "57eec28653e9aec46986b6d2be9c708e",
                "22f9ff3c8e452814a9bcd54193746e80",
                "0c6ae3f1d3680b34aa10e39998429f1c"
            };
            foreach (var guid in retired)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty);
            }
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/")) continue;
                var extension = Path.GetExtension(path);
                if (extension is not (".prefab" or ".mat" or ".asset" or ".unity")) continue;
                using var reader = new StreamReader(Path.Combine(Application.dataPath, path.Substring(7)));
                if (reader.ReadLine() != "%YAML 1.1") continue;
                var content = reader.ReadToEnd();
                foreach (var guid in retired)
                    Assert.That(content, Does.Not.Contain(guid), path);
            }
        }

        [Test]
        public void ShieldCanvasAndRingRenderWithTheWorld()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/_Shared/ShipBaseRig.prefab");
            var shield = rig.transform.Find("Shield");
            Assert.That(shield.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            var main = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cameras/Main Camera.prefab")
                .GetComponent<Camera>();
            var ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cameras/UI Cam.prefab")
                .GetComponent<Camera>();
            foreach (var child in shield.GetComponentsInChildren<Transform>(true))
            {
                var mask = 1 << child.gameObject.layer;
                Assert.That(main.cullingMask & mask, Is.Not.Zero, child.name);
                Assert.That(ui.cullingMask & mask, Is.Zero, child.name);
            }
        }
    }
}
