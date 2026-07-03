using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Fields.Core
{
    /// <summary>
    /// Generation inputs for the baseline field, expressed as plain values so
    /// the core stays headless-testable. Built from the field + spawn settings
    /// assets by the MonoBehaviour tier.
    /// </summary>
    [Serializable]
    public struct FieldGenerationParams
    {
        public float CellSize;
        public float AverageAsteroidsPerCell;
        public float NoiseFrequency;
        public Vector2 DensityMultiplierRange;
        public float FieldRadius;

        // Noise profile. Struct defaults (0/false) are normalized by the
        // layout to the neutral profile: 1 octave, no ridging, contrast 1,
        // no floor, no warp — i.e. plain single-octave Perlin.
        public int NoiseOctaves;
        public float NoiseLacunarity;
        public float NoisePersistence;
        public bool RidgedNoise;
        public float NoiseContrast;
        public float DensityFloor;
        public float WarpStrength;
        public float WarpFrequency;

        public float[] MeshVolumes;
        public float MeshDensity;
        public Vector2 MassScaleRange;
        public Vector2 VelocityRange;
        public Vector2 SpinRange;
        public bool AmbientDrift;
    }

    /// <summary>
    /// Everything needed to spawn one field asteroid, in plane coordinates
    /// relative to the field origin. Pure data — the MonoBehaviour tier maps
    /// it to world space and spawner attributes.
    /// </summary>
    public struct FieldAsteroidSpec
    {
        public AsteroidId Id;
        public Vector2 PlanePosition;
        public Quaternion Rotation;
        public int MeshIndex;
        public float Mass;
        public float Scale;
        public Vector2 PlaneVelocity;
        public Vector3 AngularVelocity;
        public float HealthFraction;
    }

    /// <summary>
    /// The deterministic baseline layer: a procedural lookup table over hashed
    /// jittered cells with Perlin-modulated density. Never persisted — any
    /// cell regenerates the identical asteroid set (IDs, poses, attributes)
    /// from the seed alone.
    /// </summary>
    public class AsteroidFieldLayout
    {
        private const int MaxOctaves = 8;

        private readonly int seed;
        private readonly FieldGenerationParams p;
        private readonly Vector2[] octaveOffsets;
        private readonly Vector2 warpOffsetX;
        private readonly Vector2 warpOffsetY;
        private readonly int octaves;
        private readonly float lacunarity;
        private readonly float persistence;
        private readonly float contrast;

        public float CellSize => p.CellSize;
        public float FieldRadius => p.FieldRadius;

        public AsteroidFieldLayout(int seed, in FieldGenerationParams generationParams)
        {
            this.seed = seed;
            p = generationParams;

            // Normalize struct-default (zeroed) profile values to the neutral profile.
            octaves = Mathf.Clamp(p.NoiseOctaves < 1 ? 1 : p.NoiseOctaves, 1, MaxOctaves);
            lacunarity = p.NoiseLacunarity <= 0f ? 2f : p.NoiseLacunarity;
            persistence = p.NoisePersistence <= 0f ? 0.5f : p.NoisePersistence;
            contrast = p.NoiseContrast <= 0f ? 1f : p.NoiseContrast;

            // The first octave's offsets are drawn exactly like the original
            // single-octave field, so the neutral profile reproduces it.
            var offsetRng = new DeterministicRandom(DeterministicRandom.Hash(seed, 91, 17));
            octaveOffsets = new Vector2[octaves];
            for (var o = 0; o < octaves; o++)
                octaveOffsets[o] = new Vector2(offsetRng.Range(0f, 4096f), offsetRng.Range(0f, 4096f));
            var warpRng = new DeterministicRandom(DeterministicRandom.Hash(seed, 47, 101));
            warpOffsetX = new Vector2(warpRng.Range(0f, 4096f), warpRng.Range(0f, 4096f));
            warpOffsetY = new Vector2(warpRng.Range(0f, 4096f), warpRng.Range(0f, 4096f));
        }

        public Vector2Int CellOf(Vector2 planePos) => new(
            Mathf.FloorToInt(planePos.x / p.CellSize),
            Mathf.FloorToInt(planePos.y / p.CellSize));

        /// <summary>
        /// Density multiplier sampled at the cell. Shaped noise below the
        /// density floor produces exactly 0 (true void); everything else maps
        /// into the authored multiplier range.
        /// </summary>
        public float DensityMultiplier(int cellX, int cellY)
        {
            var shaped = SampleShapedNoise(cellX + 0.5f, cellY + 0.5f);
            if (shaped < 0f) return 0f; // below the coverage floor: true void
            return Mathf.Lerp(p.DensityMultiplierRange.x, p.DensityMultiplierRange.y, shaped);
        }

        /// <summary>
        /// The full noise pipeline in cell coordinates: domain warp → fBm
        /// octaves (optionally ridged) → contrast exponent → coverage floor.
        /// Returns 0..1, or -1 when below the floor (empty corridor).
        /// </summary>
        private float SampleShapedNoise(float x, float y)
        {
            if (p.WarpStrength > 0f && p.WarpFrequency > 0f)
            {
                // Two decorrelated noise fields bend the sample domain,
                // turning straight filaments into organic swirls.
                var wx = Mathf.PerlinNoise(warpOffsetX.x + x * p.WarpFrequency, warpOffsetX.y + y * p.WarpFrequency) - 0.5f;
                var wy = Mathf.PerlinNoise(warpOffsetY.x + x * p.WarpFrequency, warpOffsetY.y + y * p.WarpFrequency) - 0.5f;
                x += wx * 2f * p.WarpStrength;
                y += wy * 2f * p.WarpStrength;
            }

            var sum = 0f;
            var norm = 0f;
            var amplitude = 1f;
            var frequency = p.NoiseFrequency;
            for (var o = 0; o < octaves; o++)
            {
                var n = Mathf.Clamp01(Mathf.PerlinNoise(
                    octaveOffsets[o].x + x * frequency,
                    octaveOffsets[o].y + y * frequency));
                // Ridged: fold around the midline so noise ridges become
                // sharp bright filaments — the "tight spindle" shape.
                if (p.RidgedNoise) n = 1f - Mathf.Abs(2f * n - 1f);
                sum += n * amplitude;
                norm += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            var shaped = norm > 0f ? sum / norm : 0f;

            // Contrast >1 crushes midtones so only peaks keep density.
            shaped = Mathf.Pow(shaped, contrast);

            // Coverage floor: below it is a true void; above renormalizes to
            // keep the full multiplier range reachable.
            var floor = Mathf.Clamp01(p.DensityFloor);
            if (floor > 0f)
            {
                if (shaped <= floor) return -1f;
                shaped = (shaped - floor) / (1f - floor);
            }
            return Mathf.Clamp01(shaped);
        }

        /// <summary>Deterministic asteroid count for a cell, before field-radius culling.</summary>
        public int CountForCell(int cellX, int cellY)
        {
            var expected = p.AverageAsteroidsPerCell * DensityMultiplier(cellX, cellY);
            var whole = Mathf.FloorToInt(expected);
            var cellRng = new DeterministicRandom(DeterministicRandom.Hash(seed, cellX, cellY));
            // Stochastic-but-deterministic rounding keeps the fractional density.
            return whole + (cellRng.NextFloat() < expected - whole ? 1 : 0);
        }

        /// <summary>
        /// Appends the baseline asteroids of a cell. Asteroids whose home lies
        /// beyond the field radius are skipped (bounded sector; IDs stay
        /// stable because the per-cell count is decided first).
        /// </summary>
        public void GenerateCell(int cellX, int cellY, List<FieldAsteroidSpec> results)
        {
            var count = CountForCell(cellX, cellY);
            for (var i = 0; i < count; i++)
            {
                var spec = GenerateAsteroid(AsteroidId.Baseline(cellX, cellY, i));
                if (spec.PlanePosition.sqrMagnitude <= p.FieldRadius * p.FieldRadius)
                    results.Add(spec);
            }
        }

        /// <summary>
        /// Regenerates one baseline asteroid purely from its stable ID — the
        /// determinism keystone: no dependence on neighbours or load order.
        /// </summary>
        public FieldAsteroidSpec GenerateAsteroid(AsteroidId id)
        {
            var rng = new DeterministicRandom(DeterministicRandom.Hash(seed, id.CellX, id.CellY, id.Index));

            var cellMin = new Vector2(id.CellX * p.CellSize, id.CellY * p.CellSize);
            var position = cellMin + new Vector2(rng.Range(0f, p.CellSize), rng.Range(0f, p.CellSize));

            var meshCount = p.MeshVolumes?.Length ?? 0;
            var meshIndex = rng.RangeInt(meshCount);
            var baseVolume = meshCount > 0 ? p.MeshVolumes[meshIndex] : 0f;
            var baseMass = baseVolume * p.MeshDensity;
            var massFactor = rng.Range(p.MassScaleRange.x, p.MassScaleRange.y);
            var mass = baseMass * massFactor;
            var scale = Mathf.Pow(massFactor, 1f / 3f);

            var rotation = rng.RotationUniform();

            // Kinematics keep the old inverse-cube-root mass scaling. Drawn from
            // the stream even when drift is disabled so toggling AmbientDrift
            // never changes positions/orientations.
            var velocityScale = mass > 0f ? 1f / Mathf.Pow(mass, 1f / 3f) : 1f;
            var velocity = rng.Direction2() * (rng.Range(p.VelocityRange.x, p.VelocityRange.y) * velocityScale);
            var spin = new Vector3(
                rng.Range(p.SpinRange.x, p.SpinRange.y) * velocityScale,
                rng.Range(p.SpinRange.x, p.SpinRange.y) * velocityScale,
                rng.Range(p.SpinRange.x, p.SpinRange.y) * velocityScale);
            if (!p.AmbientDrift)
            {
                velocity = Vector2.zero;
                spin = Vector3.zero;
            }

            return new FieldAsteroidSpec
            {
                Id = id,
                PlanePosition = position,
                Rotation = rotation,
                MeshIndex = meshIndex,
                Mass = mass,
                Scale = scale,
                PlaneVelocity = velocity,
                AngularVelocity = spin,
                HealthFraction = 1f
            };
        }
    }
}
