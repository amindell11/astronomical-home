using UnityEngine;
using UnityEngine.Rendering;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// A locale prefab's root: the locale's lighting, and the sky layers, lights and volumes beneath it. Each renderer
    /// below is one sky layer on the <c>Sky</c> Unity layer, which only the flight and hangar cameras' culling masks
    /// include. The locale service shows and hides each instance by explicit call; a hidden locale draws no sky layer
    /// and enables no light or volume, so a preloaded locale cannot light or grade the outgoing world. A locale
    /// backdrop pair temporarily shows each locale for offscreen rendering. A sky layer's look lives on its material.
    /// Design: arc #1274.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocaleSky : MonoBehaviour
    {
        [SerializeField] private LocaleLighting lighting;

        private Renderer[] layers;
        private Behaviour[] followers;
        private bool[] followerEnabled;

        internal bool Shown { get; private set; }

        private void Awake()
        {
            layers = GetComponentsInChildren<Renderer>(true);
            var lights = GetComponentsInChildren<Light>(true);
            var volumes = GetComponentsInChildren<Volume>(true);
            followers = new Behaviour[lights.Length + volumes.Length];
            lights.CopyTo(followers, 0);
            volumes.CopyTo(followers, lights.Length);
            followerEnabled = new bool[followers.Length];
            for (var i = 0; i < followers.Length; i++) followerEnabled[i] = followers[i].enabled;
        }

        internal void SetVisible(bool visible)
        {
            foreach (var layer in layers)
                layer.enabled = visible;
            for (var i = 0; i < followers.Length; i++) followers[i].enabled = visible && followerEnabled[i];
            Shown = visible;
        }

        internal void ApplyLighting() => lighting.Apply();
    }
}
