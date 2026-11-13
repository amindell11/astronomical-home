using UnityEngine;
using UnityEngine.Rendering;

namespace UI.Projection
{
    internal interface ICanopySource
    {
        RenderTexture Ink { get; }
        RTHandle InkColor { get; }
        CanopySettings Settings { get; }
        CanopyScreen Screen { get; }
        bool EffectsEnabled { get; }
        float TransitionAge { get; }
        float HitAge { get; }
    }

    /// <summary>
    /// Where a canopy image shows: <see cref="Apparent"/> is the size its image takes on the
    /// viewer's screen and <see cref="Frame"/> that screen's size, both in screen pixels. The
    /// shader sizes its pixel effects from these, so a display drawn small on a monitor keeps the
    /// scanlines and aberration the full-screen HUD has.
    /// </summary>
    public readonly struct CanopyScreen
    {
        public readonly Vector2 Apparent;
        public readonly Vector2 Frame;

        public CanopyScreen(Vector2 apparent, Vector2 frame)
        {
            Apparent = apparent;
            Frame = frame;
        }

        // An image that fills the screen it is drawn on: the HUD, or an image left unmeasured.
        public static CanopyScreen Own(Vector2 size) => new(size, size);
    }
}
