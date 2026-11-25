using UnityEngine;

namespace UI.Hangar.Hologram
{
    /// <summary>
    /// The cone from the lens that cuts the hologram image off; the projector cone turns along its
    /// axis. The projector builds one from the profile each frame.
    /// </summary>
    public readonly struct HangarReach
    {
        public Vector2 Axis { get; }
        public float HalfAngle { get; }
        public float Length { get; }
        public float Width { get; }
        public float Softness { get; }

        public HangarReach(Vector2 axis, float halfAngle, float length, float width, float softness)
        {
            Axis = axis;
            HalfAngle = halfAngle;
            Length = length;
            Width = width;
            Softness = softness;
        }
    }
}
