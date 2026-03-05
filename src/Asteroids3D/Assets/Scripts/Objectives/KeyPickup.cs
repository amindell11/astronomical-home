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
    /// MonoBehaviour on the key prefab. Owns key visual state, pickup-by-distance
    /// check, and spawn logic. Runs its own Update — no external tick needed.
    /// </summary>
    public class KeyPickup : MonoBehaviour, IKeyTracker
    {
        [SerializeField] private float pickupDistance = 4f;

        private Transform player;
        private bool collected;

        public bool PlayerHasKey => collected;
        public Vector3 KeyPosition => transform.position;

        public event Action OnKeyCollected;

        public void Initialize(Transform playerTransform, float distance)
        {
            player = playerTransform;
            pickupDistance = distance;
            collected = false;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Move to a random position within radius of center and reset collected state.
        /// </summary>
        public void SpawnKey(Vector3 center, float radius)
        {
            collected = false;
            var offset2D = UnityEngine.Random.insideUnitCircle * radius;
            transform.position = new Vector3(center.x + offset2D.x, center.y, center.z + offset2D.y);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (collected || !player)
                return;

            var sqrDist = (player.position - transform.position).sqrMagnitude;
            if (sqrDist <= pickupDistance * pickupDistance)
            {
                collected = true;
                gameObject.SetActive(false);
                OnKeyCollected?.Invoke();
            }
        }

        /// <summary>Reset collected state and re-show (for restart).</summary>
        public void ResetKey(Vector3 center, float radius)
        {
            SpawnKey(center, radius);
        }
    }
}
