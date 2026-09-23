using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment
{
    public sealed class SkyboxPreviewWindow : EditorWindow
    {
        [SerializeField] private Material candidate;
        [SerializeField] private SceneAsset environment;
        private SkyboxPreviewSession preview;
        private bool showingCandidate;
        private string status;

        [MenuItem("Tools/Skybox Preview")]
        public static void Open() => GetWindow<SkyboxPreviewWindow>("Skybox Preview");

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += EndPreview;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            SkyboxImport.Imported += OnImported;
        }

        private void OnDisable()
        {
            EndPreview();
            AssemblyReloadEvents.beforeAssemblyReload -= EndPreview;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            SkyboxImport.Imported -= OnImported;
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                EndPreview();
            Repaint();
        }

        private void ActiveSceneChanged(Scene previous, Scene current) => EndPreview();

        private void OnImported(Material material)
        {
            if (!candidate)
                candidate = material;
            if (preview != null && candidate == material)
                preview.ShowCandidate(showingCandidate);
            Repaint();
        }

        public void BeginPreview(Material material)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode to preview against the game's lighting.");
            EndPreview();
            candidate = material;
            preview = new SkyboxPreviewSession(candidate);
            showingCandidate = true;
        }

        public void EndPreview()
        {
            var previous = preview;
            preview = null;
            previous?.Dispose();
            Repaint();
        }

        public static void ApplyToEnvironment(SceneAsset environment, Material material)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before applying a skybox.");
            if (!environment || !material)
                throw new ArgumentException("Choose an environment scene and skybox material.");
            if (!SkyboxImport.IsFinal(material))
                throw new ArgumentException("Send a final 8K sky from Blender before applying it.");
            var active = SceneManager.GetActiveScene();
            var path = AssetDatabase.GetAssetPath(environment);
            var target = SceneManager.GetSceneByPath(path);
            if (!target.isLoaded)
                target = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(target);
                Undo.RecordObject(Unsupported.GetRenderSettings(), "Apply Skybox to Environment");
                RenderSettings.skybox = material;
                EditorSceneManager.MarkSceneDirty(target);
            }
            finally
            {
                SceneManager.SetActiveScene(active);
            }
            DynamicGI.UpdateEnvironment();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Blender → Unity", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Send a draft from Blender, then preview it in Play Mode. " +
                                    "Use High Fidelity and fixed exposure/bloom when comparing.", MessageType.Info);
            EditorGUILayout.LabelField("Quality", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            EditorGUILayout.LabelField("Active environment", SceneManager.GetActiveScene().name);
            var reflection = ReflectionProbe.defaultTexture;
            EditorGUILayout.LabelField("Default reflection", reflection ? reflection.name : "None");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Imports"))
                    AssetDatabase.Refresh();
                if (GUILayout.Button("Choose Imported Sky"))
                {
                    var menu = new GenericMenu();
                    foreach (var guid in AssetDatabase.IsValidFolder(SkyboxImport.Folder)
                                 ? AssetDatabase.FindAssets("t:Material", new[] { SkyboxImport.Folder })
                                 : Array.Empty<string>())
                    {
                        var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                        menu.AddItem(new GUIContent(material.name), material == candidate, () =>
                        {
                            EndPreview();
                            candidate = material;
                        });
                    }
                    if (menu.GetItemCount() == 0)
                        menu.AddDisabledItem(new GUIContent("Send a sky from Blender first"));
                    menu.ShowAsContext();
                }
            }
            using (new EditorGUI.DisabledScope(preview != null))
                candidate = (Material)EditorGUILayout.ObjectField("Candidate", candidate, typeof(Material), false);
            using (new EditorGUI.DisabledScope(!candidate || !EditorApplication.isPlaying))
            {
                if (preview == null)
                {
                    if (GUILayout.Button("Preview Candidate"))
                        BeginPreview(candidate);
                }
                else
                {
                    var selected = GUILayout.Toolbar(showingCandidate ? 1 : 0, new[] { "Original", "Candidate" });
                    if ((selected == 1) != showingCandidate)
                    {
                        showingCandidate = selected == 1;
                        preview.ShowCandidate(showingCandidate);
                    }
                    if (GUILayout.Button("End Preview and Restore"))
                        EndPreview();
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Keep a Final Sky", EditorStyles.boldLabel);
            environment = (SceneAsset)EditorGUILayout.ObjectField("Environment Scene", environment, typeof(SceneAsset), false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                                               !environment || !SkyboxImport.IsFinal(candidate)))
            {
                if (GUILayout.Button("Apply Final to Environment (Undo)"))
                {
                    ApplyToEnvironment(environment, candidate);
                    status = "Environment updated. Save that scene to keep it; Undo restores its previous sky.";
                }
            }
            EditorGUILayout.HelpBox("Drafts are for preview. Send Final 8K from Blender to enable Apply. " +
                                    "Existing bloom, exposure and other lighting settings stay in place.", MessageType.None);
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, MessageType.Info);
        }
    }
}
