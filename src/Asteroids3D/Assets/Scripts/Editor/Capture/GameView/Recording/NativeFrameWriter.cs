using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capture.GameView.Recording
{
    internal sealed class NativeFrameWriter : IDisposable
    {
        private readonly RenderTexture target;
        private readonly Texture2D image;
        private readonly Color32[] upright;

        public NativeFrameWriter(int width, int height)
        {
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            upright = new Color32[width * height];
        }

        public void Write(string path)
        {
            var previous = RenderTexture.active;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    var pixels = image.GetPixels32();
                    for (var y = 0; y < target.height; y++)
                        Array.Copy(pixels, y * target.width, upright, (target.height - 1 - y) * target.width, target.width);
                    image.SetPixels32(upright);
                }
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose()
        {
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
