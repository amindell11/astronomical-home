using System;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Capture.GameView
{
    internal static class UrpGizmoCaptureAdapter
    {
        public static bool CompatibilityMode => Settings.enableRenderCompatibilityMode;
        public static bool GlobalSettingsDirty => EditorUtility.IsDirty(GlobalSettings);

        public static void Prepare() => Restore(true);

        public static void Restore(bool compatibilityMode) =>
            Settings.enableRenderCompatibilityMode = compatibilityMode;

        public static void RestoreGlobalSettingsDirtyState(bool wasDirty)
        {
            if (wasDirty) EditorUtility.SetDirty(GlobalSettings);
            else EditorUtility.ClearDirty(GlobalSettings);
        }

        private static RenderGraphSettings Settings =>
            GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>() ??
            throw new InvalidOperationException("Native Game View capture requires URP RenderGraphSettings.");

        private static RenderPipelineGlobalSettings GlobalSettings =>
            GraphicsSettings.TryGetCurrentRenderPipelineGlobalSettings(out var settings)
                ? settings
                : throw new InvalidOperationException("Native Game View capture requires URP global settings.");
    }
}
