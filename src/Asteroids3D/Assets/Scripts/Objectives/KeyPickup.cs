using System;
using UnityEngine;

namespace Objectives
{
    /// <summary>
    /// Interface consumed by ExploreState to check if the player has the key.
    /// </summary>
    public interface IKeyTracker
    {
        bool PlayerHasKey { get; }
    }

    /// <summary>
    /// MonoBehaviour on the key prefab. Detects pickup via trigger collision.
    /// Requires a Collider (set to isTrigger) on the same GameObject.
    /// The player must have a Collider and Rigidbody to generate trigger events.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KeyPickup : MonoBehaviour, IKeyTracker
    {
        [Header("Spawn")]
        [SerializeField] private float spawnRadius = 30f;

        private bool collected;
        private string playerTag = "Player";

        public bool PlayerHasKey => collected;
        public Vector3 KeyPosition => transform.position;
        public float SpawnRadius => spawnRadius;

        public event Action OnKeyCollected;

        /// <summary>
        /// Move to a random position within <see cref="spawnRadius"/> of center
        /// and reset collected state.
        /// </summary>
        public void SpawnKey(Vector3 center)
        {
            collected = false;
            var offset2D = UnityEngine.Random.insideUnitCircle * spawnRadius;
            transform.position = new Vector3(center.x + offset2D.x, center.y, center.z + offset2D.y);
            gameObject.SetActive(true);
        }

        /// <summary>Reset collected state and re-show (for restart).</summary>
        public void ResetKey(Vector3 center)
        {
            SpawnKey(center);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected) return;
            if (!other.CompareTag(playerTag)) return;

            collected = true;
            gameObject.SetActive(false);
            OnKeyCollected?.Invoke();
        }
    }
}
