using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// A locale scene's sky root. Each child renderer is one sky layer on the <c>Sky</c> Unity
    /// layer, which only the flight and hangar cameras' culling masks include; the root enables its sky layers
    /// while its scene is active. A locale backdrop pair temporarily selects each sky for offscreen rendering.
    /// Locale lights and post-processing volumes follow the same visibility so a preloaded locale cannot light
    /// or grade the outgoing world. A sky layer's look lives on its material. Design: arc #678.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocaleSky : MonoBehaviour
    {
        private Renderer[] layers;
        private Behaviour[] followers;
        private bool[] followerEnabled;

        private void Awake()
        {
            layers = GetComponentsInChildren<Renderer>(true);
            var sceneFollowers = new List<Behaviour>(InScene<Light>());
            sceneFollowers.AddRange(InScene<Volume>());
            followers = sceneFollowers.ToArray();
            followerEnabled = new bool[followers.Length];
            for (var i = 0; i < followers.Length; i++) followerEnabled[i] = followers[i].enabled;
        }

        private IEnumerable<T> InScene<T>() where T : Behaviour
        {
            foreach (var behaviour in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour.gameObject.scene == gameObject.scene) yield return behaviour;
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
            for (var i = 0; i < followers.Length; i++) followers[i].enabled = visible && followerEnabled[i];
        }

        private void Show(Scene active) => SetVisible(gameObject.scene == active);
    }
}
