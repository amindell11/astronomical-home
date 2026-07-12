using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Sectors
{
    /// <summary>
    /// Serialized reference to a locale scene, authored as a <c>SceneAsset</c> in the editor and baked
    /// to a scene name that survives into builds. Unassigned means "no locale scene" — inherit the boot
    /// scene's lighting, which is also the headless path.
    /// </summary>
    [Serializable]
    public class SceneReference
    {
        [SerializeField] private string sceneName;
        [SerializeField] private string scenePath;
#if UNITY_EDITOR
        [SerializeField] private SceneAsset sceneAsset;
#endif

        public string SceneName => sceneName;
        public string ScenePath => scenePath;
        public bool IsAssigned => !string.IsNullOrEmpty(sceneName);
    }
}
