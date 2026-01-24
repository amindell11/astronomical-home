using UnityEngine;

namespace Ships
{
    [CreateAssetMenu(fileName = "SpawnerSettings", menuName = "Ship/SpawnerSettings")]
    public class ShipSpawnerSettings : ScriptableObject
    {
        [Header("Game Flow Settings")]
        [Tooltip("Delay before restarting the game after player death")]
        public float restartDelay = 3f;
        
        [Tooltip("Whether to restart the entire game when the player dies")]
        public bool restartOnPlayerDeath;

        [Header("Enemy Respawn Settings")]
        [Tooltip("Delay before respawning an enemy ship after death")]
        public float enemyRespawnDelay = 3f;

        [Tooltip("Distance from camera to spawn ships offscreen")]
        public float offscreenDistance = 25f;
    }
}
