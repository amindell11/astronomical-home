using System;
using Ships.Command;
using Ships.Presentation;
using UnityEngine;

namespace Ships.Visuals.Wings
{
    public sealed class WingVisuals : MonoBehaviour, IShipVisual
    {
        [Serializable]
        private struct Joint
        {
            public Transform bone;
            public Quaternion restRotation;
        }

        [SerializeField] private Joint[] joints;
        [SerializeField, Min(.01f)] private float transitionSeconds = .5f;
        private Func<PilotCommand> command;
        private float current;
        private float from;
        private float target;
        private float elapsed;

        private void Awake()
        {
            if (joints == null || joints.Length == 0 || transitionSeconds <= 0f)
                throw new InvalidOperationException("Wing motion requires authored joints and a positive transition duration.");
            foreach (var joint in joints)
                if (!joint.bone)
                    throw new InvalidOperationException("Wing motion requires every authored bone reference.");
            Apply(0f);
            // Hangar rigs remain idle until a flight view binds them.
            enabled = false;
        }

        public void Bind(in ShipView view)
        {
            command = view.Command ?? throw new ArgumentException("Wing motion requires the live pilot command.");
            enabled = true;
        }

        private void OnEnable()
        {
            current = from = target = 0f;
            elapsed = transitionSeconds;
            Apply(0f);
        }

        private void Update()
        {
            var thrust = command().thrust;
            var next = thrust > .05f ? 1f : 0f;
            if (next != target)
            {
                from = current;
                target = next;
                elapsed = 0f;
            }
            elapsed = Mathf.Min(elapsed + Time.deltaTime, transitionSeconds);
            var t = elapsed / transitionSeconds;
            current = Mathf.Lerp(from, target, t * t * (3f - 2f * t));
            Apply(current);
        }

        private void Apply(float demand)
        {
            foreach (var joint in joints)
                joint.bone.localRotation = Quaternion.Slerp(joint.restRotation, Quaternion.identity, demand);
        }
    }
}
