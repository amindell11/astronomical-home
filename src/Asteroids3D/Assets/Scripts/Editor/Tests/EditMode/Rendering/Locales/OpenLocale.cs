using System;
using System.Linq;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.EditMode.Rendering.Locales
{
    /// <summary>
    /// A locale's <see cref="LocaleSky"/> root and its <c>Sky</c> transform, loaded from a locale prefab or from the
    /// one active locale instance in a scene (the edit scene). <see cref="Name"/> is the root's name, which for a
    /// scene instance names the locale prefab it came from. Disposing unloads what was loaded.
    /// </summary>
    internal sealed class OpenLocale : IDisposable
    {
        private readonly Scene scene;
        private readonly GameObject contents;

        public OpenLocale(string path)
        {
            if (path.EndsWith(".prefab"))
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                Root = contents.GetComponent<LocaleSky>();
            }
            else
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    Root = scene.GetRootGameObjects().Single(g => g.activeSelf && g.GetComponent<LocaleSky>())
                        .GetComponent<LocaleSky>();
                }
                catch
                {
                    EditorSceneManager.CloseScene(scene, true);
                    throw;
                }
            }
            Name = Root.name;
            Sky = Root.transform.Find("Sky");
        }

        public string Name { get; }
        public LocaleSky Root { get; }
        public Transform Sky { get; }

        public void Dispose()
        {
            if (contents) PrefabUtility.UnloadPrefabContents(contents);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
