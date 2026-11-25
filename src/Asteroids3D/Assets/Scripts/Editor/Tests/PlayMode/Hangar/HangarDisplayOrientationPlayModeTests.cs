#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Hangar
{
    // The glass image is what the player sees; the readback is where a click on that glass lands.
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class HangarDisplayOrientationPlayModeTests
    {
        private const string ConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";

        [UnityTest]
        public IEnumerator MainGlass_ShowsTheCanvasRightWayUp()
        {
            var console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            Texture2D shot = null;
            try
            {
                var display = console.MainDisplay;
                var block = new GameObject("Corner", typeof(RectTransform), typeof(Image));
                block.transform.SetParent(display.Canvas.transform, false);
                var rect = block.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, .6f);
                rect.anchorMax = new Vector2(.4f, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = block.GetComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = false;

                for (var i = 0; i < 5; i++) yield return null;

                var glass = console.MainScreenGlass;
                var surface = new MeshSurface(glass.GetComponent<MeshFilter>().sharedMesh);
                shot = Read(console);
                // Inset from the corner: the canopy effect shifts pixels that sit on the edge.
                var bright = ColorAt(shot, console, glass, surface, new Vector2(.2f, .8f));
                var dark = ColorAt(shot, console, glass, surface, new Vector2(.8f, .2f));
                Assert.That(bright.grayscale, Is.GreaterThan(dark.grayscale + .15f),
                    $"corner {bright} should beat the opposite corner {dark}");
            }
            finally
            {
                if (shot) Object.Destroy(shot);
                if (console) Object.Destroy(console.gameObject);
            }
        }

        private static Color ColorAt(Texture2D shot, HangarConsole console, MeshRenderer glass, MeshSurface surface, Vector2 uv)
        {
            var world = glass.transform.TransformPoint(surface.LocalPoint(uv));
            var viewport = console.HardwareCamera.WorldToViewportPoint(world);
            var x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * shot.width), 0, shot.width - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * shot.height), 0, shot.height - 1);
            return shot.GetPixel(x, y);
        }

        private static Texture2D Read(HangarConsole console)
        {
            var source = (RenderTexture)console.Texture;
            var readable = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, readable);
            var previous = RenderTexture.active;
            RenderTexture.active = readable;
            var shot = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(readable);
            return shot;
        }
    }
}
#endif
