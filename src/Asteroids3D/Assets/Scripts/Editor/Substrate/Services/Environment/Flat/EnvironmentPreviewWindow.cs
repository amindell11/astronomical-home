using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment.Flat
{
    public sealed class EnvironmentPreviewWindow : EditorWindow
    {
        [SerializeField] private FlatBackgroundAsset candidate;
        [SerializeField] private SceneAsset environmentScene;
        private EnvironmentPreviewSession preview;
        private bool showingCandidate;

        [MenuItem("Tools/Environment Preview")]
        public static void Open() => GetWindow<EnvironmentPreviewWindow>("Environment Preview");

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += EndPreview;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            SceneManager.activeSceneChanged += SceneChanged;
            EditorApplication.projectChanged += ImportsChanged;
        }

        private void OnDisable()
        {
            EndPreview();
            AssemblyReloadEvents.beforeAssemblyReload -= EndPreview;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            SceneManager.activeSceneChanged -= SceneChanged;
            EditorApplication.projectChanged -= ImportsChanged;
        }

        private void PlayModeChanged(PlayModeStateChange state) => EndPreview();
        private void SceneChanged(Scene previous, Scene next) => EndPreview();
        private void ImportsChanged()
        {
            if (preview != null) preview.Show(showingCandidate ? candidate : null);
            Repaint();
        }

        public void BeginPreview(FlatBackgroundAsset source)
        {
            EndPreview();
            candidate = source;
            preview = new EnvironmentPreviewSession(source);
            showingCandidate = true;
        }

        public void EndPreview()
        {
            preview?.Dispose();
            preview = null;
            Repaint();
        }

        public static EnvironmentAuthoring FindEnvironment(Scene scene)
        {
            var roots = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnvironmentAuthoring>(true)).ToArray();
            if (roots.Length > 1)
                throw new InvalidOperationException($"Scene '{scene.name}' has multiple Environment authoring roots.");
            return roots.SingleOrDefault();
        }

        public static Shader BackgroundShader()
        {
            var shader = Shader.Find("Environment/Flat Background");
            if (!shader) throw new InvalidOperationException("The Environment/Flat Background shader is missing.");
            return shader;
        }

        public static EnvironmentAuthoring Apply(SceneAsset sceneAsset, FlatBackgroundAsset source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before applying an environment.");
            if (!sceneAsset || !source || !source.IsFinal)
                throw new ArgumentException("Choose a locale scene and a final imported background.");
            var path = AssetDatabase.GetAssetPath(sceneAsset);
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var root = FindEnvironment(scene);
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Final Environment");
            if (!root)
            {
                var go = new GameObject("Environment");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Create Environment");
                root = Undo.AddComponent<EnvironmentAuthoring>(go);
                var initial = new SerializedObject(root);
                var lights = scene.GetRootGameObjects().SelectMany(obj => obj.GetComponentsInChildren<Light>(true)).ToArray();
                var property = initial.FindProperty("nativeLights");
                property.arraySize = lights.Length;
                for (var i = 0; i < lights.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                initial.ApplyModifiedProperties();
            }
            var serialized = new SerializedObject(root);
            serialized.FindProperty("background").objectReferenceValue = source;
            serialized.FindProperty("backgroundShader").objectReferenceValue = BackgroundShader();
            serialized.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            return root;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Send a flat draft from Blender, then compare in Play Mode. Native lights, exposure and bloom stay fixed.", MessageType.Info);
            EditorGUILayout.LabelField("Active locale", SceneManager.GetActiveScene().name);
            using (new EditorGUI.DisabledScope(preview != null))
                candidate = (FlatBackgroundAsset)EditorGUILayout.ObjectField("Candidate", candidate, typeof(FlatBackgroundAsset), false);
            if (GUILayout.Button("Refresh Imports")) AssetDatabase.Refresh();
            using (new EditorGUI.DisabledScope(!candidate || !EditorApplication.isPlaying))
            {
                if (preview == null)
                {
                    if (GUILayout.Button("Preview Candidate")) BeginPreview(candidate);
                }
                else
                {
                    var next = GUILayout.Toolbar(showingCandidate ? 1 : 0, new[] { "Original", "Candidate" }) == 1;
                    if (next != showingCandidate)
                    {
                        showingCandidate = next;
                        preview.Show(next ? candidate : null);
                    }
                    if (GUILayout.Button("End Preview and Restore")) EndPreview();
                }
            }
            environmentScene = (SceneAsset)EditorGUILayout.ObjectField("Locale Scene", environmentScene, typeof(SceneAsset), false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !environmentScene || !candidate || !candidate.IsFinal))
            {
                if (GUILayout.Button("Apply Final (Undo)"))
                    Selection.activeObject = Apply(environmentScene, candidate);
            }
            EditorGUILayout.HelpBox("Apply marks the selected locale scene dirty. Save it normally to keep the result. Reimport never saves or assigns scenes. Palette overrides live on its Environment root; changing the baked colors requires regeneration.", MessageType.None);
        }
    }
}
