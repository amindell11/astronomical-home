using System;
using Game;
using Player;
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
        [Header("Spawn")]
        [SerializeField] private float spawnRadius = 30f;

        public bool PlayerHasKey { get; private set; }

        public Vector3 KeyPosition => transform.position;

        public event Action OnKeyCollected;

        /// <summary>Move to a random in-plane position within <see cref="spawnRadius"/> of center and reset collected state.</summary>
        public void SpawnKey(Vector3 center)
        {
            PlayerHasKey = false;
            var offset2D = UnityEngine.Random.insideUnitCircle * spawnRadius;
            transform.position = center + GamePlane.PlaneDirToWorld(offset2D);
            gameObject.SetActive(true);
        }

        /// <summary>Reset collected state and re-show (for restart).</summary>
        public void ResetKey(Vector3 center)
        {
            SpawnKey(center);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (PlayerHasKey) return;
            if (!other.GetComponentInParent<PlayerMarker>()) return;

            PlayerHasKey = true;
            gameObject.SetActive(false);
            OnKeyCollected?.Invoke();
        }
    }
}
