using UnityEngine;

namespace Cameras.Shake
{
    [CreateAssetMenu(fileName = "Shake", menuName = "Cameras/Shake Profile")]
    public sealed class CameraShakeProfile : ScriptableObject
    {
        [Tooltip("Strength over seconds since the shake starts, from 0 to 1.")]
        public AnimationCurve envelope = AnimationCurve.EaseInOut(0f, 1f, .3f, 0f);

        [Tooltip("Seconds into the envelope where it holds while its source is held. Negative means no hold.")]
        [Min(-1f)] public float sustainAt = -1f;

        [Tooltip("Noise cycles per second.")]
        [Min(0f)] public float frequency = 12f;

        [Tooltip("Largest jitter offset at full envelope, as a share of the view's half-height.")]
        [Range(0f, .2f)] public float jitter = .02f;

        [Tooltip("Largest roll at full envelope, in degrees.")]
        [Range(0f, 10f)] public float roll = 1f;

        [Tooltip("Push along the source's direction at full envelope, as a share of the view's half-height. Negative pushes the other way.")]
        [Range(-.2f, .2f)] public float kick = 0f;

        public float Length => envelope.length == 0 ? 0f : envelope.keys[envelope.length - 1].time;
    }
}
