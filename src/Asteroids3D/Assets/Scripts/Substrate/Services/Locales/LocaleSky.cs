using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// A locale scene's sky root. Each child renderer is one sky layer on the <c>Sky</c> Unity
    /// layer, which only the flight and hangar cameras' culling masks include; the root enables its sky layers
    /// while its scene is active. A locale backdrop pair temporarily selects each sky for offscreen rendering.
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

        internal void SetVisible(bool visible)
        {
            foreach (var layer in layers)
                layer.enabled = visible;
        }

        private void Show(Scene active) => SetVisible(gameObject.scene == active);
    }
}
