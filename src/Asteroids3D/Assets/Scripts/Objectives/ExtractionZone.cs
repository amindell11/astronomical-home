using Game;
using UnityEngine;

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

        private readonly RigidbodyOccupancy occupancy = new();
        private Rigidbody playerBody;
        private Transform blocker;
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

        public void Arm(Transform blocker)
        {
            this.blocker = blocker;
            armed = true;
        }

        public void Disarm()
        {
            blocker = null;
            armed = false;
        }

        private void OnTriggerEnter(Collider other) => occupancy.Enter(other);

        private void OnTriggerExit(Collider other) => occupancy.Exit(other);
    }
}
