using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// A locale scene's sky root. Each child renderer is one sky layer on the <c>Sky</c> Unity
    /// layer, which only the flight and hangar cameras' culling masks include; the root enables its sky layers
    /// while its scene is active. A locale backdrop pair temporarily selects each sky for offscreen rendering.
    /// Locale lights follow the same visibility so preloaded locales cannot light the outgoing world.
    /// A sky layer's look lives on its material. Design: arc #678.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocaleSky : MonoBehaviour
    {
        private Renderer[] layers;
        private Light[] lights;
        private bool[] lightEnabled;

        private void Awake()
        {
            layers = GetComponentsInChildren<Renderer>(true);
            var sceneLights = new System.Collections.Generic.List<Light>();
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.gameObject.scene == gameObject.scene) sceneLights.Add(light);
            lights = sceneLights.ToArray();
            lightEnabled = new bool[lights.Length];
            for (var i = 0; i < lights.Length; i++) lightEnabled[i] = lights[i].enabled;
        }

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
            for (var i = 0; i < lights.Length; i++) lights[i].enabled = visible && lightEnabled[i];
        }

        private void Show(Scene active) => SetVisible(gameObject.scene == active);
    }
}
