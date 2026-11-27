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

            [Tooltip("Seconds after this layer starts when it is gone. 0 plays every frame.")]
            public float endsAt;

            [Tooltip("Seconds of fade before Ends At, or after the live window ends when Ends With Live is set.")]
            public float fadeOut;

            [Tooltip("What starts this layer: the kit's press, the kit going live, the kit's hit, or the kit's live window ending.")]
            public KeystoneFlipbookStart startsOn;

            [Tooltip("Ends when the kit stops being live, fading over Fade Out.")]
            public bool endsWithLive;

            [Tooltip("Repeats from Loop From after the last frame, until the live window ends. Needs Ends With Live.")]
            public bool loops;

            [Tooltip("First frame of the repeat. Frames before it play once, as an intro.")]
            public int loopFrom;

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

            [Tooltip("Left in the world where it started, instead of following the ship.")]
            public bool staysAtLaunch;

            [Tooltip("Left where the ship was on the frame before this layer started. Needs Stays At Launch.")]
            public bool startsBehind;

            [Header("Speed")]
            [Tooltip("Points along the ship's travel instead of its heading.")]
            public bool followsTravel;

            [Tooltip("Planar speed at or below which this layer is gone. Speed fading is off while Full At Speed is 0.")]
            public float goneAtSpeed;

            [Tooltip("Planar speed at or above which this layer is at full opacity. 0 turns speed fading off.")]
            public float fullAtSpeed;

            [Tooltip("Share of its length this layer keeps at rest, growing to full by Length Full At Speed. 0 keeps full length.")]
            [Range(0f, 1f)]
            public float lengthAtRest;

            [Tooltip("Forward speed at which this layer reaches its full length.")]
            public float lengthFullAtSpeed;

            [Tooltip("Seconds this layer's turn trails the ship's, pivoting at Anchor Ahead. 0 turns with the ship.")]
            public float turnLag;
        }
    }
}
