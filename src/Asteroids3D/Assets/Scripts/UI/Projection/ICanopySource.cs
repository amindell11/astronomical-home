using UnityEngine;
using UnityEngine.Rendering;

namespace UI.Projection
{
    internal interface ICanopySource
    {
        RenderTexture Ink { get; }
        RTHandle InkColor { get; }
        CanopySettings Settings { get; }
        bool EffectsEnabled { get; }
        float TransitionAge { get; }
        float HitAge { get; }
    }
}
