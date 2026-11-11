using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// One physical control on the console. A click or its key presses it: it raises
    /// <see cref="Pressed"/>, travels along its own axis and springs back with a small bounce.
    /// </summary>
    public sealed class HangarControl : MonoBehaviour, IPointerClickHandler
    {
        private const float InSeconds = .05f;
        private const float HoldSeconds = .06f;
        private const float ReturnOmega = 34f;
        private const float ReturnDamping = .45f;
        private const float SettledDepth = .002f;

        [Tooltip("Direction the control travels when pressed, in its own space.")]
        [SerializeField] private Vector3 pressAxis = Vector3.down;
        [Tooltip("How far it travels, in local units.")]
        [SerializeField] private float travel = .05f;

        private Vector3 rest;
        private float since = float.PositiveInfinity;
        private float depth;

        public event Action Pressed;

        public float Travel => travel;
        public Vector3 PressAxis => pressAxis;
        internal float Depth => depth;
        internal bool Moving => since < float.PositiveInfinity;

        private void Awake()
        {
            if (pressAxis == Vector3.zero || travel <= 0f)
                throw new InvalidOperationException($"HangarControl on '{name}' needs a press axis and travel.");
            rest = transform.localPosition;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        public void Press()
        {
            since = 0f;
            Pressed?.Invoke();
        }

        private void Update()
        {
            if (!Moving) return;
            since += Time.unscaledDeltaTime;
            depth = DepthAt(since);
            if (since > InSeconds + HoldSeconds && Mathf.Abs(depth) < SettledDepth)
            {
                depth = 0f;
                since = float.PositiveInfinity;
            }
            transform.localPosition = rest + pressAxis.normalized * (travel * depth);
        }

        // In fast, hold, then an underdamped return that lifts a hair past rest before settling.
        private static float DepthAt(float t)
        {
            if (t < InSeconds) return Mathf.Sin(t / InSeconds * Mathf.PI * .5f);
            t -= InSeconds;
            if (t < HoldSeconds) return 1f;
            t -= HoldSeconds;
            var damped = ReturnOmega * Mathf.Sqrt(1f - ReturnDamping * ReturnDamping);
            return Mathf.Exp(-ReturnDamping * ReturnOmega * t) *
                   (Mathf.Cos(damped * t) + ReturnDamping / Mathf.Sqrt(1f - ReturnDamping * ReturnDamping) * Mathf.Sin(damped * t));
        }
    }
}
