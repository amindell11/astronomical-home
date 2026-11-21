using System;
using UnityEngine;

namespace Ships.Keystones.Tells
{
    [CreateAssetMenu(fileName = "KeystoneFlipbookFeel", menuName = "Game/Ships/Keystone Flipbook Feel")]
    public sealed class KeystoneFlipbookFeel : ScriptableObject
    {
        [Tooltip("Size of the whole effect.")]
        [SerializeField, Min(0.1f)] private float scale = 1f;

        [Tooltip("Playback rate of the whole effect. 1 is the baked timing.")]
        [SerializeField, Min(0.1f)] private float speed = 1f;

        [Tooltip("How much the effect narrows as the ship banks. 1 matches the hull; higher exaggerates.")]
        [SerializeField, Range(0f, 4f)] private float bankNarrowing = 1f;

        [Tooltip("Rate the sheets were baked at.")]
        [SerializeField] private float framesPerSecond = 60f;

        [SerializeField] private Layer[] layers;

        public float Scale => scale;
        public float Speed => speed;
        public float BankNarrowing => bankNarrowing;
        public float FramesPerSecond => framesPerSecond;
        public Layer[] Layers => layers;

        [Serializable]
        public struct Layer
        {
            public string name;

            [Tooltip("Draw this layer.")]
            public bool show;

            [Tooltip("Size of this layer, about its anchor.")]
            public float size;

            [Range(0f, 1f)]
            public float opacity;

            [Tooltip("Playback rate of this layer.")]
            public float speed;

            [Tooltip("Seconds after the kit fires when this layer is gone. 0 plays every frame.")]
            public float endsAt;

            [Tooltip("Seconds of fade before Ends At.")]
            public float fadeOut;

            [Header("Baked with the sheet")]
            public Material material;
            public int columns;
            public int rows;
            public int frames;

            [Tooltip("Ship lengths across the ship.")]
            public float width;

            [Tooltip("Ship lengths along the ship.")]
            public float length;

            [Tooltip("Ship lengths the sheet's centre sits ahead of the ship's centre. Negative is behind.")]
            public float centreAhead;

            [Tooltip("Ship lengths ahead of the ship's centre that Size scales about.")]
            public float anchorAhead;

            [Tooltip("Local Z offset in world units.")]
            public float depth;

            [Tooltip("Left in the world where the kit fired, instead of following the ship.")]
            public bool staysAtLaunch;
        }
    }
}
