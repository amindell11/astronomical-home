using Asteroids.Fragnetics;
using Editor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Asteroids.Spawning
{
    public class AsteroidSpawner : MonoBehaviour
    {
        [SerializeField] private AsteroidSpawnSettings settings;
        public Registry Registry { get; private set; }
        private SpawnPool pool;
        
        private void Awake()
        {
            if (!settings)
            {
                enabled = false;
                return;
            }
            settings.ValidateSettings();
            Registry = new Registry();
            pool = new SpawnPool(settings, transform);
        }

        public void DespawnAll()
        {
            foreach(var a in Registry.ActiveAsteroids) Despawn(a);
        }
        
        public void Despawn(AsteroidController ast)
        {
            Registry.Unregister(ast);
            pool.ReleaseAsteroid(ast);
        }
        
        public AsteroidController SpawnRandom(Pose pose)
        {
            var ast = SpawnAtPose(pose);
            InitRandomAsteroid(ast);
            Registry.Register(ast);
            return ast;
        }
        
        public AsteroidController SpawnFragment(Frag frag)
        {
            var pose = new Pose(frag.Position, frag.Rotation);
            var ast = SpawnAtPose(pose);
            InitFragmentAsteroid(ast, frag.Mass, frag.Velocity, frag.Spin);
            Registry.Register(ast);
            return ast;
        }

        private AsteroidController SpawnAtPose(Pose pose)
        {
            var ast = pool.Get();
            ast.transform.SetParent(transform);
            ast.transform.SetPositionAndRotation(pose.position, pose.rotation);
            return ast;
        }

        private void InitRandomAsteroid(AsteroidController asteroid)
        {
            var meshInfo = GetRandomMeshInfo(settings.meshInfos);
            var (mass, scale) = CalculateMassAndScale(meshInfo);

            var velocity = RandomVelocity(mass, settings.velocityRange);
            var angularVelocity = RandomAngularVelocity(mass, settings.spinRange);

            asteroid.Initialize(this, meshInfo, mass, scale, velocity, angularVelocity);
        }

        private void InitFragmentAsteroid(
            AsteroidController asteroid,
            float mass,
            Vector3 velocity,
            Vector3 angularVelocity)
        {
            var meshInfo = GetRandomMeshInfo(settings.meshInfos);
            var (finalMass, scale) = CalculateMassAndScale(meshInfo, mass);
            asteroid.Initialize(this, meshInfo, finalMass, scale, velocity, angularVelocity);
        }

        private static AsteroidSpawnSettings.MeshInfo GetRandomMeshInfo(AsteroidSpawnSettings.MeshInfo[] meshInfos)
        {
            if (meshInfos is not { Length: > 0 }) return default;
            var idx = Random.Range(0, meshInfos.Length);
            return meshInfos[idx];
        }


        private static float VelocityScale(float mass)
        {
            return (mass > 0) ? 1f / Mathf.Pow(mass, 1f/3f) : 1f;
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
            AsteroidSpawnSettings.MeshInfo meshInfo, float? mass=null)
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
