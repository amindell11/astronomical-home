using UnityEngine;

namespace Asteroids
{
    [CreateAssetMenu(fileName = "New Asteroid Spawn Settings", menuName = "Asteroid/Spawn Settings")]
    public class SpawnSettings : ScriptableObject
    {
        [System.Serializable]
        public struct MeshInfo
        {
            public Mesh mesh;

            [Tooltip("Optional pre-cooked collider mesh. If null the mesh itself is used.")]
            public Mesh colliderMesh;

            public float cachedVolume;
        }
        
        [Header("Asteroid Configuration")] [SerializeField]
        public Asteroid asteroidPrefab;

        [Header("Mesh Assets")]
        [Tooltip("Array of asteroid meshes with optional collider overrides and pre-cached volume")]
        public MeshInfo[] meshInfos;
        
        [Header("Randomization Ranges")]
        [Tooltip("The range of random mass scaling applied to newly spawned asteroids")]
        public Vector2 massScaleRange = new Vector2(0.5f, 2f);
    
        [Tooltip("The base velocity range, which gets scaled by mass")]
        public Vector2 velocityRange = new Vector2(0.5f, 2f);
    
        [Tooltip("The base spin range, which gets scaled by mass")]
        public Vector2 spinRange = new Vector2(-30f, 30f);
    
        [Header("Pool Settings")]
        [Tooltip("Initial capacity of the asteroid object pool")]
        public int defaultPoolCapacity = 20;
    
        [Tooltip("Maximum size the asteroid object pool can grow to")]
        public int maxPoolSize = 100;

        [Header("Physical Properties")]
        [Tooltip("Default density for asteroids (used for mass calculations)")]
        public float defaultDensity = 1000f;

        public void ValidateSettings()
        {
            if (meshInfos == null || meshInfos.Length == 0)
                Debug.LogWarning($"AsteroidSpawnSettings '{name}': No asteroid meshes assigned!");
            if (massScaleRange.x <= 0 || massScaleRange.y <= 0)
                Debug.LogWarning($"AsteroidSpawnSettings '{name}': Mass scale range contains non-positive values!");
            if (defaultPoolCapacity <= 0)
                Debug.LogWarning($"AsteroidSpawnSettings '{name}': Default pool capacity should be greater than 0!");
            if (maxPoolSize < defaultPoolCapacity)
                Debug.LogWarning($"AsteroidSpawnSettings '{name}': Max pool size should be >= default pool capacity!");
            if (defaultDensity <= 0)
                Debug.LogWarning($"AsteroidSpawnSettings '{name}': Default density should be greater than 0!");
            
        }

        private void OnValidate()
        {
            ValidateSettings();
        }
    }
} 