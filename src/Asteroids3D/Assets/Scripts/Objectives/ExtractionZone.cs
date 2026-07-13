using Game;
using UnityEngine;

namespace Objectives
{
    /// <summary>Extraction zone check, testable without a MonoBehaviour.</summary>
    public interface IExtractionZone
    {
        /// <summary>True when the player is inside the zone and not blocked.</summary>
        bool IsPlayerInZone { get; }
    }

    /// <summary>Trigger extraction zone; occupancy is a per-rigidbody level, so player identity and the blocker can arrive while the player is already parked inside.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExtractionZone : MonoBehaviour, IExtractionZone
    {
        [SerializeField] private float blockDistance = 20f;

        private readonly RigidbodyOccupancy occupancy = new();
        private Rigidbody playerBody;
        private Transform blocker;

        public bool IsPlayerInZone
        {
            get
            {
                if (!occupancy.Contains(playerBody)) return false;
                return !blocker ||
                       (blocker.position - playerBody.position).sqrMagnitude > blockDistance * blockDistance;
            }
        }

        /// <summary>Null blocker means extraction is never blocked.</summary>
        public void Initialize(Rigidbody playerBody, Transform blocker = null)
        {
            this.playerBody = playerBody;
            this.blocker = blocker;
        }

        private void OnTriggerEnter(Collider other) => occupancy.Enter(other.attachedRigidbody);

        private void OnTriggerExit(Collider other) => occupancy.Exit(other.attachedRigidbody);
    }
}
