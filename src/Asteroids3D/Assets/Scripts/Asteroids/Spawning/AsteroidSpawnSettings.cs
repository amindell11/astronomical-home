using UnityEngine;

namespace Asteroids.Spawning
{
    [CreateAssetMenu(fileName = "New Asteroid Spawn Settings", menuName = "Asteroid/Spawn Settings")]
    public class AsteroidSpawnSettings : ScriptableObject
    {
        [System.Serializable]
        public struct MeshInfo
        {
            /// <summary>A single covering sphere ("lobe") in mesh-local space.</summary>
            [System.Serializable]
            public struct LobeSphere
            {
                public Vector3 center; // mesh-local (meshes are centroid-pivoted at import)
                public float radius;
            }

            public Mesh mesh;

            [Tooltip("Optional pre-cooked collider mesh. If null the mesh itself is used.")]
            public Mesh colliderMesh;

            public float cachedVolume;

            [Tooltip("Baked covering spheres along the mesh's principal axis (1..3). " +
                     "K=1 reproduces the single mean-vertex circle (center≈origin). " +
                     "Nothing reads these for gameplay yet.")]
            public LobeSphere[] cachedLobes;

            [Tooltip("λ1/λ2 — ratio of the two largest principal extents (debug/report).")]
            public float cachedLobeAspect;
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
#if UNITY_EDITOR
            RebakeLobes();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: (re)bake the covering-sphere lobes for every mesh. Cheap for the
        /// handful of shared asteroid meshes, so we just recompute unconditionally. Marks
        /// the asset dirty so the baked data serializes.
        /// </summary>
        internal void RebakeLobes()
        {
            if (meshInfos == null) return;
            bool changed = false;
            for (int i = 0; i < meshInfos.Length; i++)
            {
                if (meshInfos[i].mesh == null) continue;
                meshInfos[i].cachedLobes = AsteroidLobeBaker.Bake(meshInfos[i].mesh, out float aspect);
                meshInfos[i].cachedLobeAspect = aspect;
                changed = true;
            }
            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
