using UnityEngine;

namespace Objectives
{
    /// <summary>
    /// Plain C# class owning key state: position, collected flag, spawn logic, pickup-by-distance check.
    /// No MonoBehaviour — CombatSectorManager calls CheckPickup each frame and SpawnKey on restart.
    /// </summary>
    public class KeyPickup : IKeyTracker
    {
        private Vector3 keyPosition;
        private bool collected;

        public bool PlayerHasKey => collected;
        public Vector3 KeyPosition => keyPosition;

        /// <summary>
        /// Spawn the key at a random position within radius of center.
        /// Resets the collected flag.
        /// </summary>
        public void SpawnKey(Vector3 center, float radius)
        {
            collected = false;
            var offset2D = Random.insideUnitCircle * radius;
            keyPosition = new Vector3(center.x + offset2D.x, center.y, center.z + offset2D.y);
        }

        /// <summary>
        /// Check if the player is close enough to pick up the key.
        /// Call each frame from the sector manager.
        /// </summary>
        public bool CheckPickup(Vector3 playerPos, float pickupDistance)
        {
            if (collected)
                return false;

            if (Vector3.Distance(playerPos, keyPosition) <= pickupDistance)
            {
                collected = true;
                return true;
            }

            return false;
        }

        /// <summary>Reset collected state (for restart).</summary>
        public void Reset()
        {
            collected = false;
        }
    }
}
