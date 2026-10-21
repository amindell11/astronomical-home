#if UNITY_EDITOR
using System;
using System.Reflection;
using Capture;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Rendering
{
    [Category("Camera")]
    public sealed class CaptureLayersEditModeTests
    {
        [Test]
        public void CaptureRig_ExcludesMinimapMeshesFromTheWorldView()
        {
            var type = Type.GetType("Capture.GameView.GameViewEpisodeCapture, Capture.GameView.Editor", true);
            var capture = ScriptableObject.CreateInstance(type);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                type.GetField("config", flags).SetValue(capture, new CaptureConfig());
                type.GetMethod("CreateRig", flags).Invoke(capture, null);
                var camera = (Camera)type.GetField("captureCamera", flags).GetValue(capture);
                foreach (var layer in new[] { "Minimap", "Minimap_Ship", "Minimap_Enemy" })
                    Assert.That(camera.cullingMask & LayerMask.GetMask(layer), Is.Zero, layer);
                Assert.That(camera.cullingMask & LayerMask.GetMask("Asteroid"), Is.Not.Zero);
            }
            finally
            {
                type.GetMethod("End").Invoke(capture, null);
                Object.DestroyImmediate(capture);
            }
        }
    }
}
#endif
