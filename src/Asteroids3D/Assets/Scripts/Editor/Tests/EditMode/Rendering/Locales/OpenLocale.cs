using System;
using System.IO;
using System.Linq;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.EditMode.Rendering.Locales
{
    /// <summary>
    /// A locale's <see cref="LocaleSky"/> root and its <c>Sky</c> transform, loaded from a locale prefab or from a
    /// scene that authors a sky root in place (the edit scene, whose root is the <c>Sky</c> transform itself).
    /// Disposing unloads what was loaded.
    /// </summary>
    internal sealed class OpenLocale : IDisposable
    {
        private readonly Scene scene;
        private readonly GameObject contents;

        public OpenLocale(string path)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            if (path.EndsWith(".prefab"))
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                Root = contents.GetComponent<LocaleSky>();
                Sky = contents.transform.Find("Sky");
                return;
            }
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Root = scene.GetRootGameObjects().Single(g => g.activeSelf && g.GetComponent<LocaleSky>())
                    .GetComponent<LocaleSky>();
                Sky = Root.transform;
            }
            catch
            {
                EditorSceneManager.CloseScene(scene, true);
                throw;
            }
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
