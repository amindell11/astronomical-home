using System.Collections;
using System.IO;
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
    public class HangarMotionPreviewCapture
    {
        [UnityTest]
        public IEnumerator FilmMotionForReview()
        {
            var offer = AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset");
            var valis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            var stage = HangarPreviewStage.Create(null, 640, offer.ships);
            var camera = stage.GetComponentInChildren<Camera>();
            camera.backgroundColor = new Color(.035f, .052f, .09f, 1f);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/hangar-flight/v2-frames"));
            Directory.CreateDirectory(output);
            var captureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                stage.Show(new ShipLoadout(valis, null, null, null, null));
                for (var frame = 0; frame < 540; frame++)
                {
                    if (frame == 240) stage.Show(new ShipLoadout(offer.ships[0], null, null, null, null));
                    if (frame == 360) stage.Show(new ShipLoadout(offer.ships[1], null, null, null, null));
                    if (frame == 369) stage.Show(new ShipLoadout(offer.ships[2], null, null, null, null));
                    if (frame == 378) stage.Show(new ShipLoadout(valis, null, null, null, null));
                    yield return null;
                    var previous = RenderTexture.active;
                    RenderTexture.active = (RenderTexture)stage.Texture;
                    pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D4}.png"), pixels.EncodeToPNG());
                }
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

