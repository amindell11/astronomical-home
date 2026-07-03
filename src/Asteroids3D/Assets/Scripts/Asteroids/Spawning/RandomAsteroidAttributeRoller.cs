using UnityEngine;
using Random = UnityEngine.Random;

namespace Asteroids.Spawning
{
    /// <summary>
    /// UnityEngine.Random-based attribute roller for the fragment path only —
    /// fragments have no LUT home, and their rolled outcome is persisted in
    /// the override overlay, so global randomness is acceptable here. The
    /// baseline field draws everything from seeded per-asteroid streams
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
        public AsteroidAttributes RollForMass(float mass, Vector3 velocity, Vector3 angularVelocity)
        {
            var meshInfo = GetRandomMeshInfo(settings.meshInfos);
            var (finalMass, scale) = CalculateMassAndScale(meshInfo, mass);
            return new AsteroidAttributes(meshInfo, finalMass, scale, velocity, angularVelocity);
        }

        private static AsteroidSpawnSettings.MeshInfo GetRandomMeshInfo(AsteroidSpawnSettings.MeshInfo[] meshInfos)
        {
            if (meshInfos is not { Length: > 0 }) return default;
            var idx = Random.Range(0, meshInfos.Length);
            return meshInfos[idx];
        }

        private (float finalMass, float finalScale) CalculateMassAndScale(
            AsteroidSpawnSettings.MeshInfo meshInfo, float? mass = null)
        {
            var baseVolume = meshInfo.cachedVolume;
            var baseMass = baseVolume * settings.density;

            return mass.HasValue ? ScaleFromMass() : MassFromScale();

            (float finalMass, float finalScale) ScaleFromMass()
            {
                var factor = mass.Value / baseMass;
                var finalScale = Mathf.Pow(factor, 1f / 3f);
                return (mass.Value, finalScale);
            }

            (float finalMass, float finalScale) MassFromScale()
            {
                var factor = Random.Range(settings.massScaleRange.x, settings.massScaleRange.y);
                var finalScale = Mathf.Pow(factor, 1f / 3f);
                var finalMassComputed = baseMass * factor;
                return (finalMassComputed, finalScale);
            }
        }
    }
}
