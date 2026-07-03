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

        // Spawn-overlap rejection. Both 0 (struct default) disables rejection
        // entirely, reproducing the pre-rejection layout bit-for-bit.
        public float PackingMargin;
        public float MinSpacing;

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

        // Static authored clearings (field-relative plane space): an asteroid
        // whose home lies inside any volume is never generated — a permanent
        // hole, exactly parallel to the FieldRadius cull. Null/empty = none.
        public ExclusionVolume[] ExclusionVolumes;

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
    /// A circular no-spawn clearing in field-relative plane space. Must be
    /// static/authored (e.g. the sector's player start): baking it into the
    /// generation-time cull keeps the layout deterministic and persistent.
    /// Anything runtime-dependent must instead be handled at instantiation
    /// time, never here.
    /// </summary>
    [Serializable]
    public struct ExclusionVolume
    {
        public Vector2 Center;
        public float Radius;
    }

    /// <summary>
    /// The spatial footprint of a baseline asteroid: exactly the first four
    /// RNG draws of its stream (position ×2, mesh index, mass factor), enough
    /// to evaluate spawn overlap without generating the full spec.
    /// <see cref="AsteroidFieldLayout.GenerateAsteroid"/> continues the same
    /// stream after these draws, so the candidate a cell computes for a
    /// neighbour can never diverge from what that neighbour computes for itself.
    /// </summary>
    public struct AsteroidCandidate
    {
        public Vector2 Position;
        /// <summary>Volume-equivalent sphere radius × scale (no packing margin applied).</summary>
        public float Radius;
        public int MeshIndex;
        public float MassFactor;
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
        private readonly bool rejectOverlaps;

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
            rejectOverlaps = p.PackingMargin > 0f || p.MinSpacing > 0f;

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
        /// beyond the field radius or inside an authored exclusion volume are
        /// skipped, and (when a packing margin or spacing floor is authored)
        /// so are candidates overlapping a higher-priority neighbour (bounded
        /// sector / permanent clearings / non-interpenetrating spawns; IDs stay
        /// stable because the per-cell count is decided first and a rejected
        /// asteroid is a deterministic skip, not a renumber).
        /// </summary>
        public void GenerateCell(int cellX, int cellY, List<FieldAsteroidSpec> results)
        {
            var count = CountForCell(cellX, cellY);
            for (var i = 0; i < count; i++)
            {
                var id = AsteroidId.Baseline(cellX, cellY, i);
                if (rejectOverlaps && IsBlockedByHigherPriorityCandidate(id)) continue;
                var spec = GenerateAsteroid(id);
                if (spec.PlanePosition.sqrMagnitude <= p.FieldRadius * p.FieldRadius
                    && !InsideExclusionVolume(spec.PlanePosition))
                    results.Add(spec);
            }
        }

        /// <summary>
        /// Culled like the field radius: after overlap rejection, so an
        /// excluded candidate still blocks its neighbours — the layout around
        /// a clearing is independent of whether the clearing exists.
        /// </summary>
        private bool InsideExclusionVolume(Vector2 planePos)
        {
            var volumes = p.ExclusionVolumes;
            if (volumes == null) return false;
            for (var i = 0; i < volumes.Length; i++)
                if ((planePos - volumes[i].Center).sqrMagnitude <= volumes[i].Radius * volumes[i].Radius)
                    return true;
            return false;
        }

        /// <summary>
        /// Priority rejection: a candidate is rejected iff an overlapping
        /// candidate with a lexicographically-smaller key (cellX, cellY, index)
        /// exists. Max asteroid radius is far below the cell size, so the 3×3
        /// Moore neighbourhood provably contains every possible blocker.
        /// Blockers are raw candidates — a blocker that was itself rejected
        /// (or field-radius-culled) still blocks; asking whether it survived
        /// would recurse into an unbounded sweep over earlier cells. The rule
        /// is therefore fully order- and load-independent, at the cost of
        /// slightly compounded holes.
        /// </summary>
        private bool IsBlockedByHigherPriorityCandidate(AsteroidId id)
        {
            var candidate = GenerateCandidate(id);
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var cellX = id.CellX + dx;
                var cellY = id.CellY + dy;
                int limit;
                if (cellX < id.CellX || (cellX == id.CellX && cellY < id.CellY))
                    limit = CountForCell(cellX, cellY); // whole cell precedes ours
                else if (cellX == id.CellX && cellY == id.CellY)
                    limit = id.Index;                   // earlier indices in our own cell
                else
                    continue;                           // lexicographically after us

                for (var j = 0; j < limit; j++)
                {
                    var other = GenerateCandidate(AsteroidId.Baseline(cellX, cellY, j));
                    var required = RequiredSeparation(candidate.Radius, other.Radius);
                    if ((other.Position - candidate.Position).sqrMagnitude < required * required)
                        return true;
                }
            }
            return false;
        }

        private float RequiredSeparation(float radiusA, float radiusB)
        {
            var sum = radiusA + radiusB;
            return Mathf.Max(sum * p.PackingMargin, sum + p.MinSpacing);
        }

        /// <summary>
        /// The overlap footprint of a baseline asteroid, from its ID alone.
        /// Exposed for tests; the layout uses it for neighbourhood rejection.
        /// </summary>
        public AsteroidCandidate GenerateCandidate(AsteroidId id)
        {
            var rng = RngFor(id);
            return GenerateCandidate(id, ref rng);
        }

        /// <summary>
        /// Performs exactly the first four draws of the asteroid's stream and
        /// stops. <see cref="GenerateAsteroid"/> continues drawing from the
        /// same stream — one source of truth for the opening draw sequence.
        /// </summary>
        private AsteroidCandidate GenerateCandidate(AsteroidId id, ref DeterministicRandom rng)
        {
            var cellMin = new Vector2(id.CellX * p.CellSize, id.CellY * p.CellSize);
            var position = cellMin + new Vector2(rng.Range(0f, p.CellSize), rng.Range(0f, p.CellSize));

            var meshCount = p.MeshVolumes?.Length ?? 0;
            var meshIndex = rng.RangeInt(meshCount);
            var baseVolume = meshCount > 0 ? p.MeshVolumes[meshIndex] : 0f;
            var massFactor = rng.Range(p.MassScaleRange.x, p.MassScaleRange.y);
            var scale = Mathf.Pow(massFactor, 1f / 3f);

            return new AsteroidCandidate
            {
                Position = position,
                Radius = Mathf.Pow(3f * baseVolume / (4f * Mathf.PI), 1f / 3f) * scale,
                MeshIndex = meshIndex,
                MassFactor = massFactor
            };
        }

        private DeterministicRandom RngFor(AsteroidId id) =>
            new(DeterministicRandom.Hash(seed, id.CellX, id.CellY, id.Index));

        /// <summary>
        /// Regenerates one baseline asteroid purely from its stable ID — the
        /// determinism keystone: no dependence on neighbours or load order.
        /// (Overlap rejection lives in <see cref="GenerateCell"/>; this always
        /// returns the same pose for an ID regardless of neighbours.)
        /// </summary>
        public FieldAsteroidSpec GenerateAsteroid(AsteroidId id)
        {
            var rng = RngFor(id);
            var candidate = GenerateCandidate(id, ref rng);
            var position = candidate.Position;

            var meshCount = p.MeshVolumes?.Length ?? 0;
            var meshIndex = candidate.MeshIndex;
            var baseVolume = meshCount > 0 ? p.MeshVolumes[meshIndex] : 0f;
            var baseMass = baseVolume * p.MeshDensity;
            var massFactor = candidate.MassFactor;
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
