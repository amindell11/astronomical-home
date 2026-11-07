using System;
using System.Reflection;
using Capture.GameView.Recovery;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Capture.GameView.Views
{
    internal static class PresentationSettings
    {
        public static void Snapshot(CaptureRecoveryState state)
        {
            state.hasPresentation = true;
            state.qualityLevel = QualitySettings.GetQualityLevel();
            state.ambientMode = (int)RenderSettings.ambientMode;
            state.ambientLight = RenderSettings.ambientLight;
            state.fog = RenderSettings.fog;
            state.gameViewAsyncShaders = EditorSettings.asyncShaderCompilation;
            var overlay = FindOverlay();
            if (overlay == null) return;
            state.overlayFound = true;
            state.overlayValue = overlay.Value;
            state.overlayHasValue = overlay.HasValue;
        }

        public static void HideDebugOverlay()
        {
            var overlay = FindOverlay();
            if (overlay == null) return;
            overlay.Value = 0;
            overlay.HasValue = true;
        }

        public static void ApplyNeutralLighting()
        {
            var performant = Array.IndexOf(QualitySettings.names, "Performant");
            if (performant < 0) throw new InvalidOperationException("Neutral lineup requires the Performant quality profile.");
            QualitySettings.SetQualityLevel(performant, false);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.24f, .24f, .24f);
            RenderSettings.fog = false;
            EditorSettings.asyncShaderCompilation = false;
        }

        public static void Restore(CaptureRecoveryState state)
        {
            if (!state.hasPresentation) return;
            QualitySettings.SetQualityLevel(state.qualityLevel, false);
            RenderSettings.ambientMode = (AmbientMode)state.ambientMode;
            RenderSettings.ambientLight = state.ambientLight;
            RenderSettings.fog = state.fog;
            EditorSettings.asyncShaderCompilation = state.gameViewAsyncShaders;
            if (!state.overlayFound) return;
            var overlay = FindOverlay() ?? throw new InvalidOperationException("FMOD play-in-editor overlay disappeared before restoration.");
            overlay.Value = state.overlayValue;
            overlay.HasValue = state.overlayHasValue;
        }

        private static Overlay FindOverlay()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/FMOD/FMODStudioSettings.asset"))
            {
                if (!asset) continue;
                var identifier = Field(asset.GetType(), "identifier");
                if (identifier == null || (string)identifier.GetValue(asset) != "playInEditor") continue;
                var properties = Field(asset.GetType(), "Properties").GetValue(asset);
                var overlay = Field(properties.GetType(), "Overlay").GetValue(properties);
                return new Overlay(overlay);
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

        private sealed class Overlay
        {
            private readonly object storage;
            private readonly FieldInfo value;
            private readonly FieldInfo hasValue;

            public Overlay(object storage)
            {
                this.storage = storage;
                value = Field(storage.GetType(), "Value");
                hasValue = Field(storage.GetType(), "HasValue");
            }

            public int Value
            {
                get => Convert.ToInt32(value.GetValue(storage));
                set => this.value.SetValue(storage, Enum.ToObject(this.value.FieldType, value));
            }

            public bool HasValue
            {
                get => (bool)hasValue.GetValue(storage);
                set => hasValue.SetValue(storage, value);
            }
        }
    }
}
