using UnityEngine;
using Random = UnityEngine.Random;

namespace Asteroids.Spawning
{
    /// <summary>
    /// UnityEngine.Random-based attribute provider, extracted verbatim from the
    /// old inline rolls in <see cref="AsteroidSpawner"/>. Plain class so the
    /// math is EditMode-testable without a scene.
    /// </summary>
    public class RandomAsteroidAttributeRoller : IAsteroidAttributeProvider
    {
        private readonly AsteroidSpawnSettings settings;

        public RandomAsteroidAttributeRoller(AsteroidSpawnSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>Roll a full random asteroid: mesh, mass/scale, velocity, spin.</summary>
        public AsteroidAttributes Roll()
        {
            var meshInfo = GetRandomMeshInfo(settings.meshInfos);
            var (mass, scale) = CalculateMassAndScale(meshInfo);

            var velocity = RandomVelocity(mass, settings.velocityRange);
            var angularVelocity = RandomAngularVelocity(mass, settings.spinRange);

            return new AsteroidAttributes(meshInfo, mass, scale, velocity, angularVelocity);
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

        private static float VelocityScale(float mass)
        {
            return (mass > 0) ? 1f / Mathf.Pow(mass, 1f / 3f) : 1f;
        }

        private static Vector3 RandomVelocity(float mass, Vector2 velocityRange)
        {
            var velocityScale = VelocityScale(mass);
            return Random.insideUnitCircle.normalized * (Random.Range(velocityRange.x, velocityRange.y) * velocityScale);
        }

        private static Vector3 RandomAngularVelocity(float mass, Vector2 spinRange)
        {
            var velocityScale = VelocityScale(mass);
            return new Vector3(
                Random.Range(spinRange.x, spinRange.y) * velocityScale,
                Random.Range(spinRange.x, spinRange.y) * velocityScale,
                Random.Range(spinRange.x, spinRange.y) * velocityScale
            );
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
