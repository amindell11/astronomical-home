using System;
using UnityEngine;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// The selector knob's turn: an underdamped spring from the current angle to the detent it is
    /// handed, so it lands with a small overshoot. Its press travels through <see cref="HangarControl"/>.
    /// </summary>
    public sealed class HangarKnob : MonoBehaviour
    {
        private const float Omega = 30f;
        private const float Damping = .5f;
        private const float SettledDegrees = .01f;

        private Quaternion rest;
        private Vector3 axis;
        private float angle;
        private float target;
        private float velocity;

        public bool Settled => angle == target;

        private void Awake() => rest = transform.localRotation;

        public void Initialize(Vector3 axis)
        {
            if (axis == Vector3.zero) throw new ArgumentException("The knob needs an axis to turn about.", nameof(axis));
            this.axis = axis;
        }

        public void Snap(float degrees)
        {
            target = angle = degrees;
            velocity = 0f;
            Apply();
        }

        public void Turn(float degrees) => target = degrees;

        private void Update()
        {
            if (Settled) return;
            var dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            velocity += (Omega * Omega * (target - angle) - 2f * Damping * Omega * velocity) * dt;
            angle += velocity * dt;
            if (Mathf.Abs(target - angle) < SettledDegrees && Mathf.Abs(velocity) < SettledDegrees * 10f)
            {
                angle = target;
                velocity = 0f;
            }
            Apply();
        }

        private void Apply() => transform.localRotation = rest * Quaternion.AngleAxis(angle, axis);
    }
}
