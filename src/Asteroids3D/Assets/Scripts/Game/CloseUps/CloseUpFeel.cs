using System;
using UnityEngine;

namespace Game.CloseUps
{
    [Serializable]
    public sealed class CloseUpFeel
    {
        [Tooltip("Fraction of normal speed the world runs at while the camera holds on the rival.")]
        [Range(0.01f, 1f)] public float slowScale = .15f;

        [Tooltip("Real seconds the camera takes to pan and zoom onto the rival as time slows.")]
        [Min(0.01f)] public float easeInSeconds = .6f;

        [Tooltip("Real seconds the camera holds on the rival.")]
        [Min(0f)] public float holdSeconds = 1.8f;

        [Tooltip("Real seconds the camera takes to pan back to the player as time speeds up.")]
        [Min(0.01f)] public float easeOutSeconds = .6f;

        [Tooltip("Orthographic size the camera frames the rival at. The flight camera's base framing is 15.")]
        [Min(1f)] public float closeSize = 7f;
    }
}
