using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Capture.GameView
{
    internal static class UrpGizmoCaptureAdapter
    {
        public static bool CompatibilityMode => Settings.enableRenderCompatibilityMode;

        public static void Prepare() => Restore(true);

        public static void Restore(bool compatibilityMode) =>
            Settings.enableRenderCompatibilityMode = compatibilityMode;

        private static RenderGraphSettings Settings =>
            GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>() ??
            throw new InvalidOperationException("Native Game View capture requires URP RenderGraphSettings.");
    }
}
