using UnityEngine;

namespace Objectives
{
    /// <summary>
    /// Interface for extraction zone checks. Testable without a real MonoBehaviour.
    /// </summary>
    public interface IExtractionZone
    {
        /// <summary>True when the player is inside the zone and not blocked.</summary>
        bool IsPlayerInZone { get; }
    }

    /// <summary>
    /// MonoBehaviour placed on the extraction zone GameObject.
    /// Detects player entry/exit via trigger collision. Tracks whether extraction
    /// is blocked by a nearby chaser using distance checks against a blocker transform.
    /// The collider radius defines the extraction zone size.
    /// Requires a Collider (set to isTrigger) on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExtractionZone : MonoBehaviour, IExtractionZone
    {
        [Header("Blocking")]
        [SerializeField] private float blockDistance = 20f;

        private Transform blocker;
        private float blockDistanceSqr;
        private bool playerInZone;
        private Transform playerTransform;
        private string playerTag = "Player";

        public bool IsPlayerInZone
        {
            get
            {
                if (!playerInZone) return false;

                if (blocker && playerTransform &&
                    (blocker.position - playerTransform.position).sqrMagnitude <= blockDistanceSqr)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Wire up the optional blocker reference. Call once after spawning chaser.
        /// </summary>
        /// <param name="blockerTransform">
        /// Optional chaser/blocker transform. Null means extraction is never blocked.
        /// </param>
        public void Initialize(Transform blockerTransform)
        {
            blocker = blockerTransform;
            blockDistanceSqr = blockDistance * blockDistance;
            playerInZone = false;
            playerTransform = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            playerInZone = true;
            playerTransform = other.transform;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            playerInZone = false;
        }
    }
}
