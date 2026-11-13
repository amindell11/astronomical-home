using System;
using UnityEngine;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// The selector knob's turn: an underdamped spring from the angle and speed it had when the
    /// detent was handed over, solved in closed form from the time since, so it lands with a small
    /// overshoot at any frame time. Its press travels through <see cref="HangarControl"/>.
    /// </summary>
    public sealed class HangarKnob : MonoBehaviour
    {
        private const float Omega = 30f;
        private const float Damping = .5f;
        private const float SettledDegrees = .01f;
        private static readonly float Ringing = Omega * Mathf.Sqrt(1f - Damping * Damping);

        private Quaternion rest;
        private Vector3 axis;
        private float angle;
        private float velocity;
        private float target;
        private float offset;
        private float swing;
        private float elapsed;

        public bool Settled => angle == target;
        internal float Angle => angle;

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

        public void Turn(float degrees)
        {
            target = degrees;
            offset = angle - target;
            swing = (velocity + Damping * Omega * offset) / Ringing;
            elapsed = 0f;
        }

        private void Update() => Step(Time.unscaledDeltaTime);

        internal void Step(float seconds)
        {
            if (Settled) return;
            elapsed += seconds;
            var envelope = Mathf.Exp(-Damping * Omega * elapsed);
            if (envelope * (Mathf.Abs(offset) + Mathf.Abs(swing)) < SettledDegrees)
            {
                angle = target;
                velocity = 0f;
            }
            else
            {
                var cos = Mathf.Cos(Ringing * elapsed);
                var sin = Mathf.Sin(Ringing * elapsed);
                angle = target + envelope * (offset * cos + swing * sin);
                velocity = envelope * ((Ringing * swing - Damping * Omega * offset) * cos
                                       - (Ringing * offset + Damping * Omega * swing) * sin);
            }
            Apply();
        }

        private void Apply() => transform.localRotation = rest * Quaternion.AngleAxis(angle, axis);
    }
}
