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

        [Header("Spawn Overlap Rejection")]
        [Tooltip("Candidates whose volume-sphere radii (×this margin) overlap a higher-priority neighbour are never generated — kills spawn interpenetration (the 'unprogrammed drift'). 0 disables. ~1.15–1.25 approximates the convex hull's real reach. Rejection lowers effective density; compensate via averageAsteroidsPerCell (empirical).")]
        public float packingMargin = 1.2f;
        [Tooltip("Floor on the edge-to-edge spawn gap (units); guards tiny meshes where the margin alone is negligible")]
        public float minSpacing;

        [Header("Noise Profile")]
        [Tooltip("fBm harmonics: more octaves = clumps get multi-scale internal structure")]
        [Range(1, 8)] public int noiseOctaves = 3;
        [Tooltip("Frequency step per octave (~2 = each harmonic twice as fine)")]
        public float noiseLacunarity = 2f;
        [Tooltip("Amplitude falloff per octave (~0.5 = each harmonic half as strong)")]
        [Range(0.05f, 1f)] public float noisePersistence = 0.5f;
        [Tooltip("Fold octaves around the midline so noise ridges become tight filaments/spindles")]
        public bool ridgedNoise;
        [Tooltip("Exponent on the combined noise: >1 crushes midtones so only peaks keep density (higher contrast)")]
        [Range(0.2f, 6f)] public float noiseContrast = 1f;
        [Tooltip("Shaped noise below this is a TRUE void (zero asteroids) — carves wide-open corridors")]
        [Range(0f, 0.9f)] public float densityFloor;
        [Tooltip("Domain warp displacement in cells (0 = off): bends filaments into organic swirls")]
        public float warpStrength;
        [Tooltip("Frequency of the warp field, per cell")]
        public float warpFrequency = 0.12f;

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
        /// Bumped on every inspector edit (OnValidate is editor-only, so this
        /// stays 0 in builds). Live fields poll it and rebuild when it moves —
        /// the live-tuning loop for field shape/size.
        /// </summary>
        [System.NonSerialized] public int Version;

        /// <summary>
        /// The layout/noise inputs of the generation params, built in ONE place
        /// so the runtime field, the editor heatmap preview and tests can never
        /// drift apart. Attribute inputs (mesh volumes etc.) are filled in by
        /// the field from its spawn settings.
        /// </summary>
        public Core.FieldGenerationParams BuildGenerationParams()
        {
            return new Core.FieldGenerationParams
            {
                CellSize = chunkSize,
                AverageAsteroidsPerCell = averageAsteroidsPerCell,
                NoiseFrequency = noiseFrequency,
                DensityMultiplierRange = densityMultiplierRange,
                FieldRadius = fieldRadius,
                PackingMargin = packingMargin,
                MinSpacing = minSpacing,
                NoiseOctaves = noiseOctaves,
                NoiseLacunarity = noiseLacunarity,
                NoisePersistence = noisePersistence,
                RidgedNoise = ridgedNoise,
                NoiseContrast = noiseContrast,
                DensityFloor = densityFloor,
                WarpStrength = warpStrength,
                WarpFrequency = warpFrequency,
                AmbientDrift = ambientDrift
            };
        }

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
            Version++;
            var worst = WorstCaseLoadedCount();
            if (maxAsteroids > 0 && worst > maxAsteroids)
                Debug.LogWarning(
                    $"{name}: worst-case loaded asteroid count ({worst}) exceeds maxAsteroids ({maxAsteroids}). " +
                    "Reduce density/load radius or raise the warning threshold — there is no runtime clamp.",
                    this);
        }
    }
}
