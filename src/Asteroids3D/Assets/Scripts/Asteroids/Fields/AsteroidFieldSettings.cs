using UnityEngine;

namespace Asteroids.Fields
{
    [CreateAssetMenu(fileName = "AsteroidFieldSettings", menuName = "Asteroid/Field Settings")]
    public class AsteroidFieldSettings : ScriptableObject
    {
        [Header("Asteroid Population")]
        [Tooltip("Maximum number of asteroids that can exist in the field")]
        public int maxAsteroids = 200;

        [Header("Initial Spawn Zone")]
        [Tooltip("Minimum spawn distance from the field center on initialization")]
        public float initialMinSpawnDistance = 7.5f;
        [Tooltip("Maximum spawn distance from the field center on initialization")]
        public float initialMaxSpawnDistance = 80f;

        [Header("Volume Density Control")]
        [Tooltip("Target volume per square meter for the asteroid field (volume-based, not mass-based)")]
        public float targetVolumeDensity = 0.25f;
        [Tooltip("Radius used to calculate target volume")]
        public float densityCheckRadius = 80f;
        [Tooltip("Maximum number of asteroids to spawn per frame")]
        public int maxSpawnsPerFrame = 10;

        [Header("Update Spawn Zone (for UpdatingAsteroidField)")]
        [Tooltip("Minimum spawn distance used during ongoing updates")]
        public float updateMinSpawnDistance = 50f;
        [Tooltip("Maximum spawn distance used during ongoing updates")]
        public float updateMaxSpawnDistance = 80f;

        [Header("Update Timing (for UpdatingAsteroidField)")]
        [Tooltip("Time interval between density checks and spawn updates")]
        public float densityCheckInterval = 0.15f;
    }
}
