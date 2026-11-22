using UnityEngine;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// One frame of light from a console source. <see cref="Color"/> is linear, with intensity included.
    /// </summary>
    public readonly struct HangarLight
    {
        public Vector3 Position { get; }
        public Color Color { get; }
        public float Range { get; }

        public HangarLight(Vector3 position, Color color, float range)
        {
            Position = position;
            Color = color;
            Range = range;
        }
    }
}
