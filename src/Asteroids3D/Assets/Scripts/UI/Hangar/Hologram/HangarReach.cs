using UnityEngine;

namespace UI.Hangar.Hologram
{
    /// <summary>
    /// The cone the hologram surface and the projector cone both sample. The projector builds one
    /// from the profile each frame.
    /// </summary>
    public readonly struct HangarReach
    {
        public Vector2 Axis { get; }
        public float CosHalfAngle { get; }
        public float HalfAngle { get; }
        public float Length { get; }
        public float Width { get; }
        public float Softness { get; }

        public HangarReach(Vector2 axis, float halfAngle, float length, float width, float softness)
        {
            Axis = axis;
            HalfAngle = halfAngle;
            CosHalfAngle = Mathf.Cos(halfAngle);
            Length = length;
            Width = width;
            Softness = softness;
        }
    }
}
