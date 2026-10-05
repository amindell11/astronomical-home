using UnityEngine;
using Utils;

namespace Asteroids.Spawning
{
    /// <summary>
    /// Attribute roller for the fragment path: the mesh pick draws from the
    /// parent rock's break stream, so a fragment replays with its lineage, never
    /// with global UnityEngine.Random order. Fragments have no LUT home; their
    /// rolled outcome persists in the override overlay. The baseline field
    /// draws from seeded per-asteroid streams
    /// (<see cref="Fields.Core.AsteroidFieldLayout"/>) instead.
    /// </summary>
    public class RandomAsteroidAttributeRoller
    {
        private readonly AsteroidSpawnSettings settings;

        public RandomAsteroidAttributeRoller(AsteroidSpawnSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Roll a fragment: random mesh, scale derived from the given mass,
        /// kinematics supplied by the fragmentation solver.
        /// </summary>
        public AsteroidAttributes RollForMass(float mass, Vector3 velocity, Vector3 angularVelocity, ref DeterministicRandom rng)
        {
            var meshInfos = settings.meshInfos;
            var meshIndex = meshInfos is { Length: > 0 } ? rng.RangeInt(meshInfos.Length) : -1;
            var meshInfo = meshIndex >= 0 ? meshInfos[meshIndex] : default;
            var baseMass = meshInfo.cachedVolume * settings.density;
            var scale = Mathf.Pow(mass / baseMass, 1f / 3f);
            return new AsteroidAttributes(meshInfo, meshIndex, mass, scale, velocity, angularVelocity);
        }
    }
}
