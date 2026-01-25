using UnityEngine;

namespace Asteroids.Spawning
{
    [CreateAssetMenu(fileName = "New Asteroid Spawn Settings", menuName = "Asteroid/Spawn Settings")]
    public class AsteroidSpawnSettings : ScriptableObject
    {
        [System.Serializable]
        public struct MeshInfo
        {
            public Mesh mesh;

            [Tooltip("Optional pre-cooked collider mesh. If null the mesh itself is used.")]
            public Mesh colliderMesh;

            public float cachedVolume;
        }
        
        [Header("Physical Properties")]
        [SerializeField] public float density = 1f;

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
        
        [Header("Asteroid Configuration")] [SerializeField]
        public AsteroidController asteroidPrefab;
        
        [Header("Pool Settings")]
        [Tooltip("Initial capacity of the asteroid object pool")]
        public int poolCapacity = 20;
    
        [Tooltip("Maximum size the asteroid object pool can grow to")]
        public int maxPoolSize = 100;
        public void ValidateSettings()
        {
            // Validation removed from production code
        }

        private void OnValidate()
        {
            ValidateSettings();
        }
    }
} 
