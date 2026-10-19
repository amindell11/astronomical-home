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
    public sealed class HangarDirectionalSwitchCapture
    {
        [UnityTest]
        public IEnumerator FilmActualPreviousAndNextButtons()
        {
            var offer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset"));
            var valis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            var nightshade = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Nightshade.prefab");
            offer.ships = new[] { valis, nightshade }.Concat(offer.ships.Where(s => s != valis && s != nightshade)).ToArray();
            var root = new GameObject("DirectionalCapture");
            var screen = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarScreen>("Assets/Prefabs/UI/HangarScreen.prefab"), root.transform);
            var loadout = new ShipLoadout(valis, null, null, null, null);
            screen.Show(offer, loadout, null);
            screen.enabled = false;
            var stage = root.GetComponentInChildren<HangarPreviewStage>();
            stage.Resize(640);
            var camera = stage.GetComponentInChildren<Camera>();
            camera.backgroundColor = new Color(.035f, .052f, .09f, 1f);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/hangar-flight/directional-frames"));
            Directory.CreateDirectory(output);
            var captureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            var backwards = false;
            try
            {
                for (var frame = 0; frame < 510; frame++)
                {
                    var tap = frame == 60 || frame == 120
                        || frame >= 180 && frame < 270 && (frame - 180) % 9 == 0
                        || frame >= 330 && frame < 420 && (frame - 330) % 3 == 0;
                    if (tap)
                    {
                        var button = backwards ? screen.shipRow.previousButton : screen.shipRow.nextButton;
                        button.onClick.Invoke();
                        backwards = !backwards;
                    }
                    yield return null;
                    Assert.That(stage.GetComponentsInChildren<HangarPreviewFlight>().Length, Is.LessThanOrEqualTo(2));
                    var previous = RenderTexture.active;
                    RenderTexture.active = (RenderTexture)stage.Texture;
                    pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    if (frame == 90 || frame == 150 || frame == 495)
                    {
                        var colors = pixels.GetPixels();
                        Assert.That(colors.Max(c => c.r) - colors.Min(c => c.r), Is.GreaterThan(.1f));
                    }
                    File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D4}.png"), pixels.EncodeToPNG());
                }
                var remaining = stage.GetComponentsInChildren<HangarPreviewFlight>();
                Assert.That(remaining, Has.Length.EqualTo(1));
                Assert.That(remaining[0].name, Is.EqualTo(loadout.Ship.name + "_Preview"));
            }
            finally
            {
                Time.captureDeltaTime = captureDelta;
                Object.Destroy(root);
                Object.Destroy(offer);
                Object.Destroy(pixels);
            }
        }
    }
}
#endif
