using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Capture.GameView
{
    /// <summary>
    /// The FMOD play-in-editor overlay is re-read from the platform every editor Update,
    /// so clearing its override is what hides the panel.
    /// </summary>
    internal static class CaptureEditorSettings
    {
        private const string SettingsPath = "Assets/Resources/FMOD/FMODStudioSettings.asset";
        private const string PlayInEditorId = "playInEditor";

        public static void Snapshot(CaptureRecoveryState state)
        {
            state.hasEditorExtras = true;
            state.qualityLevel = QualitySettings.GetQualityLevel();
            state.ambientMode = (int)RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            state.ambientR = ambient.r;
            state.ambientG = ambient.g;
            state.ambientB = ambient.b;
            state.ambientA = ambient.a;
            if (!TryReadOverlay(out var value, out var hasValue)) return;
            state.overlayFound = true;
            state.overlayValue = value;
            state.overlayHasValue = hasValue;
        }

        public static void HidePlayInEditorOverlay()
        {
            using var overlay = OpenOverlay() ?? throw new InvalidOperationException(
                "[Capture] FMOD play-in-editor overlay setting was not found.");
            overlay.Value = TriStateDisabled;
            overlay.HasValue = true;
        }

        public static void Restore(CaptureRecoveryState state)
        {
            if (state == null || !state.hasEditorExtras) return;
            if (QualitySettings.GetQualityLevel() != state.qualityLevel)
                QualitySettings.SetQualityLevel(state.qualityLevel, false);
            RenderSettings.ambientMode = (AmbientMode)state.ambientMode;
            RenderSettings.ambientLight = new Color(state.ambientR, state.ambientG, state.ambientB, state.ambientA);
            if (!state.overlayFound) return;
            using var overlay = OpenOverlay() ?? throw new InvalidOperationException(
                "[Capture] FMOD play-in-editor overlay setting disappeared before restore.");
            overlay.Value = state.overlayValue;
            overlay.HasValue = state.overlayHasValue;
        }

        internal static bool TryReadOverlay(out int value, out bool hasValue)
        {
            using var overlay = OpenOverlay();
            if (overlay == null)
            {
                value = 0;
                hasValue = false;
                return false;
            }
            value = overlay.Value;
            hasValue = overlay.HasValue;
            return true;
        }

        // FMODUnity.TriStateBool.Disabled. The capture assembly does not reference FMOD.
        private const int TriStateDisabled = 0;

        private static OverlayAccess OpenOverlay()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(SettingsPath);
            if (assets == null) return null;
            foreach (var asset in assets)
            {
                if (!asset) continue;
                var identifier = Field(asset.GetType(), "identifier");
                if (identifier == null || (string)identifier.GetValue(asset) != PlayInEditorId) continue;
                var properties = Field(asset.GetType(), "Properties");
                var storage = properties?.GetValue(asset);
                var overlayField = storage == null ? null : Field(storage.GetType(), "Overlay");
                var overlay = overlayField?.GetValue(storage);
                var value = overlay == null ? null : Field(overlay.GetType(), "Value");
                var hasValue = overlay == null ? null : Field(overlay.GetType(), "HasValue");
                if (overlay == null || value == null || hasValue == null)
                    throw new InvalidOperationException(
                        "[Capture] FMOD play-in-editor platform has no Overlay property.");
                return new OverlayAccess(overlay, value, hasValue);
            }
            return null;
        }

        private static FieldInfo Field(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            while (type != null)
            {
                var field = type.GetField(name, flags);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private sealed class OverlayAccess : IDisposable
        {
            private readonly object overlay;
            private readonly FieldInfo value;
            private readonly FieldInfo hasValue;

            public OverlayAccess(object overlay, FieldInfo value, FieldInfo hasValue)
            {
                this.overlay = overlay;
                this.value = value;
                this.hasValue = hasValue;
            }

            public int Value
            {
                get => Convert.ToInt32(value.GetValue(overlay));
                set => this.value.SetValue(overlay, Enum.ToObject(this.value.FieldType, value));
            }

            public bool HasValue
            {
                get => (bool)hasValue.GetValue(overlay);
                set => hasValue.SetValue(overlay, value);
            }

            public void Dispose() { }
        }
    }
}
