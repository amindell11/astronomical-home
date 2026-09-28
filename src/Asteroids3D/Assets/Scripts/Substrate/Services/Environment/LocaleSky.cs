using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment
{
    /// <summary>
    /// A locale scene's sky root. Each child renderer is one sky layer on the <c>Sky</c> Unity
    /// layer, which only the flight camera's culling mask includes; the root enables its sky layers
    /// only while its scene is the active scene, so inactive loaded locales draw nothing.
    /// A sky layer's look lives on its material. Design: arc #678.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocaleSky : MonoBehaviour
    {
        private Renderer[] layers;

        private void Awake() => layers = GetComponentsInChildren<Renderer>(true);

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Show(SceneManager.GetActiveScene());
        }

        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        private void OnActiveSceneChanged(Scene previous, Scene active) => Show(active);

        private void Show(Scene active)
        {
            var visible = gameObject.scene == active;
            foreach (var layer in layers)
                layer.enabled = visible;
        }
    }
}
