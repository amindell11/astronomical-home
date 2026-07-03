using UnityEngine;

namespace Asteroids.Fields
{
    [CreateAssetMenu(fileName = "AsteroidFieldSettings", menuName = "Asteroid/Field Settings")]
    public class AsteroidFieldSettings : ScriptableObject
    {
        [Header("Deterministic Layout")]
        [Tooltip("Cell = chunk = load/unload unit (units)")]
        public float chunkSize = 40f;
        [Tooltip("Perlin step per cell; lower = broader clumps and voids")]
        public float noiseFrequency = 0.35f;
        [Tooltip("Authored average asteroid count per cell before noise modulation")]
        public float averageAsteroidsPerCell = 6f;
        [Tooltip("Perlin maps into this density multiplier range (clusters vs corridors)")]
        public Vector2 densityMultiplierRange = new(0.3f, 1.7f);
        [Tooltip("Bounded sector: no baseline asteroids beyond this radius from the field origin")]
        public float fieldRadius = 400f;
        [Tooltip("Seeded ambient drift/spin for visual life; home positions stay the truth on reload")]
        public bool ambientDrift = true;

        [Header("Streaming")]
        [Tooltip("Chunks whose center enters this radius around the anchor are loaded")]
        public float loadRadius = 80f;
        [Tooltip("Unload only past loadRadius * this (hysteresis; keeps fragment freezes out of view)")]
        public float unloadRadiusMultiplier = 1.5f;
        [Tooltip("Load-work budget: maximum asteroids spawned per frame after the initial fill")]
        public int maxSpawnsPerFrame = 10;

        [Header("Editor Sanity")]
        [Tooltip("Editor-time warning threshold only. There is deliberately no runtime clamp — clipping content by count would reintroduce load-order nondeterminism.")]
        public int maxAsteroids = 300;

        public float UnloadRadius => loadRadius * unloadRadiusMultiplier;

        /// <summary>
        /// Computed worst-case simultaneously-loaded baseline count (full unload
        /// disc at maximum noise density). Used to pre-size the spawn pool.
        /// </summary>
        public int WorstCaseLoadedCount()
        {
            if (chunkSize <= 0f) return 0;
            var worstChunks = Mathf.PI * Mathf.Pow(UnloadRadius / chunkSize + 1f, 2f);
            return Mathf.CeilToInt(worstChunks * averageAsteroidsPerCell * densityMultiplierRange.y);
        }

        private void OnValidate()
        {
            var worst = WorstCaseLoadedCount();
            if (maxAsteroids > 0 && worst > maxAsteroids)
                Debug.LogWarning(
                    $"{name}: worst-case loaded asteroid count ({worst}) exceeds maxAsteroids ({maxAsteroids}). " +
                    "Reduce density/load radius or raise the warning threshold — there is no runtime clamp.",
                    this);
        }
    }
}
