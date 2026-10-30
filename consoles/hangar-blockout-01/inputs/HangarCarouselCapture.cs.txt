#if UNITY_EDITOR
using System;
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
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Hangar
{
    // Temporary capture fixture for #1059's review clips; never committed to the feature branch.
    [Category("UI"), Category("RequiresGraphics")]
    public sealed class HangarCarouselCapture
    {
        private const int Fps = 30;
        private const int Width = 1280;
        private const int Height = 720;

        // Next at 2 s, Previous at 4 s.
        [UnityTest]
        public IEnumerator Single() => Film("single", 180, f => f == 60 ? 1 : f == 120 ? -1 : 0);

        // Alternating every 0.3 s (2–5 s), alternating at 10 Hz (7–10 s), Next at 10 Hz (12–15 s),
        // Previous at 10 Hz (17–20 s), settle to 23 s. Four offered ships, so each one-way burst wraps.
        [UnityTest]
        public IEnumerator Spam() => Film("spam", 690, f =>
        {
            if (f >= 60 && f < 150 && (f - 60) % 9 == 0) return (f - 60) / 9 % 2 == 0 ? 1 : -1;
            if (f >= 210 && f < 300 && (f - 210) % 3 == 0) return (f - 210) / 3 % 2 == 0 ? 1 : -1;
            if (f >= 360 && f < 450 && (f - 360) % 3 == 0) return 1;
            if (f >= 510 && f < 600 && (f - 510) % 3 == 0) return -1;
            return 0;
        });

        private static IEnumerator Film(string clip, int frames, Func<int, int> press)
        {
            var offer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset"));
            var valis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            var nightshade = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Nightshade.prefab");
            offer.ships = new[] { valis, nightshade }.Concat(offer.ships.Where(s => s != valis && s != nightshade)).ToArray();
            var root = new GameObject("CarouselCapture");
            var screen = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarScreen>("Assets/Prefabs/UI/HangarScreen.prefab"), root.transform);
            var loadout = new ShipLoadout(valis, null, null, null, null);
            screen.Show(offer, loadout, null);
            screen.enabled = false;
            var stage = root.GetComponentInChildren<HangarPreviewStage>();
            // The 1920×1080 layout: the framing square sits at the old 1200-unit preview rect.
            stage.Resize(new Vector2Int(Width, Height), new Rect(690f / 1920f, -30f / 1080f, 1200f / 1920f, 1200f / 1080f));
            var camera = stage.GetComponentInChildren<Camera>();
            camera.backgroundColor = new Color(.035f, .052f, .09f, 1f);
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, $"../../../results/hangar-carousel/{clip}-frames"));
            Directory.CreateDirectory(output);
            var labels = new System.Text.StringBuilder();
            var captureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / Fps;
            try
            {
                for (var frame = 0; frame < frames; frame++)
                {
                    var step = press(frame);
                    if (step != 0)
                        (step < 0 ? screen.shipRow.previousButton : screen.shipRow.nextButton).onClick.Invoke();
                    labels.AppendLine($"{frame}\t{step}\t{loadout.Ship.name}");
                    yield return null;
                    var previous = RenderTexture.active;
                    RenderTexture.active = (RenderTexture)stage.Texture;
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D4}.png"), pixels.EncodeToPNG());
                }
                File.WriteAllText(Path.Combine(output, "presses.tsv"), labels.ToString());
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
