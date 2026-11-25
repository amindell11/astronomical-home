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

        [Tooltip("Degrees the wings sweep past the thrust pose while the keystone kit is live. 0 leaves them alone.")]
        [SerializeField] private float liveSweepDegrees;

        private Func<PilotCommand> command;
        private Func<bool> keystoneLive;
        // x: share of the rest-to-thrust arc. y: share of the live sweep past it.
        private Vector2 current;
        private Vector2 from;
        private Vector2 target;
        private float elapsed;

        private void Awake()
        {
            if (joints == null || joints.Length == 0 || transitionSeconds <= 0f)
                throw new InvalidOperationException("Wing motion requires authored joints and a positive transition duration.");
            foreach (var joint in joints)
                if (!joint.bone)
                    throw new InvalidOperationException("Wing motion requires every authored bone reference.");
            Apply(Vector2.zero);
            // Hangar rigs remain idle until a flight view binds them.
            enabled = false;
        }

        public void Bind(in ShipView view)
        {
            command = view.Command ?? throw new ArgumentException("Wing motion requires the live pilot command.");
            keystoneLive = view.KeystoneLive;
            enabled = true;
        }

        private void OnEnable()
        {
            current = from = target = Vector2.zero;
            elapsed = transitionSeconds;
            Apply(Vector2.zero);
        }

        private void Update()
        {
            var thrust = command().thrust;
            var next = keystoneLive != null && keystoneLive() ? Vector2.one
                : new Vector2(thrust > .05f ? 1f : 0f, 0f);
            if (next != target)
            {
                from = current;
                target = next;
                elapsed = 0f;
            }
            elapsed = Mathf.Min(elapsed + Time.deltaTime, transitionSeconds);
            var t = elapsed / transitionSeconds;
            current = Vector2.Lerp(from, target, t * t * (3f - 2f * t));
            Apply(current);
        }

        private void Apply(Vector2 demand)
        {
            var sweep = demand.y * liveSweepDegrees;
            foreach (var joint in joints)
            {
                var pose = Quaternion.SlerpUnclamped(joint.restRotation, Quaternion.identity, demand.x);
                // SlerpUnclamped drifts past t = 1, so the sweep turns about the rest-to-thrust axis instead.
                if (sweep != 0f)
                {
                    Quaternion.Inverse(joint.restRotation).ToAngleAxis(out var arc, out var axis);
                    if (arc > 180f) axis = -axis;
                    pose *= Quaternion.AngleAxis(sweep, axis);
                }
                joint.bone.localRotation = pose;
            }
        }
    }
}
