using Game;
using UnityEngine;

namespace Objectives
{
    /// <summary>Consumed by ExploreState to check if the player has the key.</summary>
    public interface IKeyTracker
    {
        bool PlayerHasKey { get; }
    }

    /// <summary>Trigger-collision key pickup; its Collider must be a trigger and the toucher needs a Rigidbody to generate trigger events.</summary>
    [RequireComponent(typeof(Collider))]
    public class KeyPickup : MonoBehaviour, IKeyTracker
    {
        [SerializeField] private float spawnRadius = 30f;

        private Rigidbody playerBody;

        public bool PlayerHasKey { get; private set; }

        public void Initialize(Rigidbody playerBody) => this.playerBody = playerBody;

        /// <summary>Move to a random in-plane position within <see cref="spawnRadius"/> of center and reset collected state.</summary>
        public void SpawnKey(Vector3 center)
        {
            PlayerHasKey = false;
            var offset2D = Random.insideUnitCircle * spawnRadius;
            transform.position = center + GamePlane.PlaneDirToWorld(offset2D);
            gameObject.SetActive(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (PlayerHasKey) return;
            if (!playerBody || other.attachedRigidbody != playerBody) return;

            PlayerHasKey = true;
            gameObject.SetActive(false);
        }
    }
}
