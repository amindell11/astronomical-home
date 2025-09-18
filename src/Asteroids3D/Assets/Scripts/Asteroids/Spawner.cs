using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace Asteroids
{
    public class Spawner : MonoBehaviour
    {
        [Header("Asteroid Configuration")] [SerializeField]
        private Asteroid asteroidPrefab;

        [SerializeField] private SpawnSettings spawnSettings;

        private ObjectPool<Asteroid> asteroidPool;
        
        private void Awake()
        {

            if (!Registry.Instance) gameObject.AddComponent<Registry>();

            if (!spawnSettings)
            {
                RLog.AsteroidError("AsteroidSpawner requires a reference to AsteroidSpawnSettings.");
                enabled = false;
                return;
            }

            spawnSettings.ValidateSettings();

            var poolCapacity = spawnSettings.defaultPoolCapacity;
            var poolMaxSize = spawnSettings.maxPoolSize;
            asteroidPool = new ObjectPool<Asteroid>(
                CreatePooledAsteroid,
                OnAsteroidRetrieved,
                OnAsteroidReleased,
                OnAsteroidDestroyed,
                false,
                poolCapacity,
                poolMaxSize
            );

            // -------- Pre-warm the pool --------
            for (var i = 0; i < poolCapacity; ++i)
            {
                var obj = asteroidPool.Get();
                asteroidPool.Release(obj);
            }
        }

        public Asteroid SpawnAsteroid(SpawnRequest request)
        {
            var ast = asteroidPool.Get();
            ast.transform.SetParent(transform);
            ast.transform.SetPositionAndRotation(request.Pose.position, request.Pose.rotation);

            switch (request.Kind)
            {
                case SpawnRequest.SpawnKind.Random:
                    InitRandomAsteroid(ast);
                    break;

                case SpawnRequest.SpawnKind.Fragment:
                    InitFragmentAsteroid(
                        ast,
                        request.Mass!.Value,
                        request.Velocity!.Value,
                        request.AngularVelocity!.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Register with central registry (handles volume + active count).
            Registry.Instance?.Register(ast);
            return ast;
        }

        public void ReleaseAsteroid(Asteroid ast)
        {
            if (ast) Registry.Instance?.Unregister(ast);
            asteroidPool.Release(ast);
        }

        public void ReleaseAllAsteroids()
        {
            if (!Registry.Instance) return;

            var toRelease = new List<Asteroid>(Registry.Instance.ActiveAsteroids);
            foreach (var ast in toRelease.Where(ast => ast))
                ReleaseAsteroid(ast);
        }

        private void InitRandomAsteroid(Asteroid asteroid)
        {
            var meshInfo = spawnSettings.GetRandomMeshInfo();
            var (mass, scale) = CalculateMassAndScale(asteroid, meshInfo, null);

            var velocity = spawnSettings.GetRandomVelocity(mass);
            var angularVelocity = spawnSettings.GetRandomAngularVelocity(mass);

            asteroid.Initialize(this, meshInfo, mass, scale, velocity, angularVelocity);
        }

        private void InitFragmentAsteroid(
            Asteroid asteroid,
            float mass,
            Vector3 velocity,
            Vector3 angularVelocity)
        {
            // Use a random mesh but honour the supplied mass / kinematics
            var meshInfo = spawnSettings.GetRandomMeshInfo();
            var (finalMass, scale) = CalculateMassAndScale(asteroid, meshInfo, mass);

            asteroid.Initialize(this, meshInfo, finalMass, scale, velocity, angularVelocity);
        }

        // ----------------- Book-keeping -----------------
        // Tracking now handled by AsteroidRegistry – no local implementation needed.

        // --------- ObjectPool Callbacks ---------
        private Asteroid CreatePooledAsteroid()
        {
            return (Asteroid)Instantiate(asteroidPrefab, Vector3.zero, Quaternion.identity, transform.parent);
        }

        private static void OnAsteroidRetrieved(Asteroid ast)
        {
            ast.gameObject.SetActive(true);
        }

        private static void OnAsteroidReleased(Asteroid ast)
        {
            ast.gameObject.SetActive(false);
        }

        private static void OnAsteroidDestroyed(Asteroid ast)
        {
            Destroy(ast);
        }

        private (float finalMass, float finalScale) CalculateMassAndScale(Asteroid asteroid,
            SpawnSettings.MeshInfo meshInfo, float? mass)
        {
            var baseVolume = meshInfo.cachedVolume > 0f
                ? meshInfo.cachedVolume
                :
                meshInfo.mesh
                    ?
                    meshInfo.mesh.bounds.size.x * meshInfo.mesh.bounds.size.y * meshInfo.mesh.bounds.size.z
                    : 1f;
            var density = asteroid.Density;
            var baseMass = baseVolume * density;

            if (mass.HasValue)
            {
                var massScaleFactor = mass.Value / baseMass;
                var finalScale = Mathf.Pow(massScaleFactor, 1f / 3f);
                return (mass.Value, finalScale);
            }

            var currentMassScaleRange = spawnSettings.massScaleRange;
            var randomScaleFactor = Random.Range(currentMassScaleRange.x, currentMassScaleRange.y);
            var finalScaleComputed = Mathf.Pow(randomScaleFactor, 1f / 3f);
            var finalMassComputed = baseMass * randomScaleFactor;
            return (finalMassComputed, finalScaleComputed);
        }
    }
}