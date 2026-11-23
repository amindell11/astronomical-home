using System;
using Substrate;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Visual
{
    [RequireComponent(typeof(Grenade))]
    public sealed class ConcussionMineFlightVisual : MonoBehaviour, IPresentationPart
    {
        [Tooltip("The model's braking nozzles point along this pivot's local negative Y.")]
        [SerializeField] private Transform modelPivot;
        [SerializeField] private MeshRenderer brakeExhaust;

        private Grenade charge;
        private Quaternion brakingPose = Quaternion.identity;

        private void Awake()
        {
            if (!modelPivot || !brakeExhaust)
                throw new InvalidOperationException("The concussion mine requires its model pivot and braking exhaust.");
            charge = GetComponent<Grenade>();
        }

        private void OnEnable() => charge.Launched += BeginFlight;
        private void OnDisable() => charge.Launched -= BeginFlight;
        public void ApplyPresentation(bool visible) => enabled = visible;

        private void BeginFlight()
        {
            brakingPose = GamePlane.PlanePose(GamePlane.Normal, -charge.rb.linearVelocity.normalized);
            Pose(1f);
        }

        private void LateUpdate() => Pose(Mathf.Clamp01(charge.rb.linearVelocity.magnitude / charge.LaunchSpeed));

        private void Pose(float speedFraction)
        {
            float braking = 1f - speedFraction;
            float spin = 1f - speedFraction * speedFraction;
            float tilt = Mathf.SmoothStep(96f, 0f, Mathf.InverseLerp(0.1f, 0.64f, braking));

            modelPivot.rotation = brakingPose
                * Quaternion.AngleAxis(290f * spin, Vector3.down)
                * Quaternion.AngleAxis(tilt, Vector3.right)
                * Quaternion.AngleAxis(400f * spin, Vector3.down);
            brakeExhaust.enabled = tilt < 22f && speedFraction > 0f;
        }
    }
}
