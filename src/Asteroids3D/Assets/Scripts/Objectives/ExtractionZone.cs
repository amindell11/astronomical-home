using Utils;
using UnityEngine;
using Utils.Physics;

namespace Objectives
{
    public interface IExtractionZone
    {
        bool IsPlayerInZone { get; }
    }

    /// <summary>Trigger extraction zone; occupancy is a per-rigidbody level, so player identity and the blocker can arrive while the player is already parked inside.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExtractionZone : MonoBehaviour, IExtractionZone
    {
        [SerializeField] private float blockDistance = 20f;

        [Tooltip("Optional hostile whose proximity blocks extraction while the zone is armed (observation-only binding).")]
        [SerializeField] private Transform blocker;

        private readonly RigidbodyOccupancy occupancy = new();
        private Rigidbody playerBody;
        private bool armed;

        // Unarmed reads as not-in-zone: broken challenge wiring must never complete extraction silently.
        public bool IsPlayerInZone
        {
            get
            {
                if (!armed || !occupancy.Contains(playerBody)) return false;
                return !blocker ||
                       (blocker.position - playerBody.position).sqrMagnitude > blockDistance * blockDistance;
            }
        }

        public void BindPlayer(Rigidbody playerBody) => this.playerBody = playerBody;

        public void Arm() => armed = true;

        public void Disarm() => armed = false;

#if UNITY_EDITOR
        internal void Bind(Transform blocker) => this.blocker = blocker;
#endif

        private void OnTriggerEnter(Collider other) => occupancy.Enter(other);

        private void OnTriggerExit(Collider other) => occupancy.Exit(other);
    }
}
