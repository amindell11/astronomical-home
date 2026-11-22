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

        [Tooltip("Strength by speed as a share of the ship's normal top speed, 1 being flat out without a kit. Read only by a source that scales its shake by speed.")]
        public AnimationCurve bySpeed = new(new Keyframe(1f, 0f), new Keyframe(1.5f, 1f));

        [Tooltip("Within this distance, in world units, a nearby blast shakes at full strength.")]
        [Min(0f)] public float innerRadius = 4f;

        [Tooltip("Beyond this distance, in world units, a nearby blast does not shake at all.")]
        [Min(0f)] public float outerRadius = 20f;

        [Tooltip("A blast this big shakes at full strength and radius; smaller ones scale down. 0 ignores size.")]
        [Min(0f)] public float fullSize = 0f;

        public float Length => envelope.length == 0 ? 0f : envelope[envelope.length - 1].time;
    }
}
