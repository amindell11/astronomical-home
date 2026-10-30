using System;
using System.IO;
using Game.Preview;
using UnityEditor;
using UnityEngine;

namespace UI.Projection
{
    internal sealed class CanopyTunerImportWindow : EditorWindow
    {
        [SerializeField] private CanopyProjectionProfile asset;
        [SerializeField] private CanopyState target;
        private string error;

        [MenuItem("Astronomical/UI/Import Canopy Tuner Settings…")]
        private static void OpenMenu()
        {
            var window = GetWindow<CanopyTunerImportWindow>("Import canopy settings");
            window.asset = Selection.activeObject as CanopyProjectionProfile;
        }

        internal static void Open(CanopyProjectionProfile copy)
        {
            var window = GetWindow<CanopyTunerImportWindow>("Import canopy settings");
            window.asset = (CanopyProjectionProfile)FeelWorkingCopies.Current.AssetOf(copy);
        }

        private void OnGUI()
        {
            asset = (CanopyProjectionProfile)EditorGUILayout.ObjectField("Profile", asset,
                typeof(CanopyProjectionProfile), false);
            target = (CanopyState)EditorGUILayout.EnumPopup("Import into", target);
            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Import edits the preview working copy. Use Save in Preview Host to keep it.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!asset))
            {
                if (GUILayout.Button("Choose tuner JSON…")) Import();
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void Import()
        {
            var path = EditorUtility.OpenFilePanel("Import canopy tuner settings", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var imported = CanopyTunerSettings.Parse(File.ReadAllText(path));
                var destination = EditorApplication.isPlaying ? FeelWorkingCopies.Current.For(asset) : asset;
                Undo.RecordObject(destination, "Import canopy tuner settings");
                imported.Apply(destination, target);
                EditorUtility.SetDirty(destination);
                error = null;
                Close();
            }
            catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
            {
                error = exception.Message;
            }
        }
    }
}
