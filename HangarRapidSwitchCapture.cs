#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    [Category("UI"), Category("RequiresGraphics")]
    public sealed class HangarRapidSwitchCapture
    {
        [UnityTest]
        public IEnumerator FilmBackAndForth()
        {
            var offer = AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset");
            var valis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            var nightshade = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Nightshade.prefab");
            var stage = HangarPreviewStage.Create(null, 640, offer.ships);
            var camera = stage.GetComponentInChildren<Camera>();
            camera.backgroundColor = new Color(.035f, .052f, .09f, 1f);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/hangar-flight/spam-frames"));
            Directory.CreateDirectory(output);
            var captureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            var selected = valis;
            try
            {
                stage.Show(new ShipLoadout(valis, null, null, null, null));
                for (var frame = 0; frame < 420; frame++)
                {
                    var tap = frame >= 60 && frame < 150 && (frame - 60) % 9 == 0
                        || frame >= 210 && frame < 300 && (frame - 210) % 3 == 0;
                    if (tap)
                    {
                        selected = selected == valis ? nightshade : valis;
                        stage.Show(new ShipLoadout(selected, null, null, null, null));
                    }
                    yield return null;
                    Assert.That(stage.GetComponentsInChildren<HangarPreviewFlight>().Length, Is.LessThanOrEqualTo(2));
                    var previous = RenderTexture.active;
                    RenderTexture.active = (RenderTexture)stage.Texture;
                    pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    if (frame == 45 || frame == 195 || frame == 405)
                    {
                        var colors = pixels.GetPixels();
                        Assert.That(colors.Max(c => c.r) - colors.Min(c => c.r), Is.GreaterThan(.1f));
                    }
                    File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D4}.png"), pixels.EncodeToPNG());
                }
                var remaining = stage.GetComponentsInChildren<HangarPreviewFlight>();
                Assert.That(remaining, Has.Length.EqualTo(1));
                Assert.That(remaining[0].name, Is.EqualTo(selected.name + "_Preview"));
            }
            finally
            {
                Time.captureDeltaTime = captureDelta;
                Object.Destroy(stage.gameObject);
                Object.Destroy(pixels);
            }
        }
    }
}
#endif
