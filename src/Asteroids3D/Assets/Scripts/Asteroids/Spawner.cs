using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Asteroids.Fragnetics;
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
        public Registry Registry {get;  private set;}
        private void Awake()
        {
            if (!spawnSettings)
            {
                RLog.AsteroidError("AsteroidSpawner requires a reference to AsteroidSpawnSettings.");
                enabled = false;
                return;
            }
    
            spawnSettings.ValidateSettings();
            Registry = new Registry();
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
            
            for (var i = 0; i < poolCapacity; ++i)
            {
                var obj = asteroidPool.Get();
                asteroidPool.Release(obj);
            }
        }

        public Asteroid SpawnRandom(Pose pose)
        {
            var ast = SpawnAtPose(pose);
            InitRandomAsteroid(ast);
            Registry.Register(ast);
            return ast;
        }

        public Asteroid SpawnFragment(Frag frag)
        {
            var pose = new Pose(frag.Position, frag.Rotation);
            var ast = SpawnAtPose(pose);
            InitFragmentAsteroid(ast, frag.Mass, frag.Velocity, frag.Spin);
            Registry.Register(ast);
            return ast;
        }

        private Asteroid SpawnAtPose(Pose pose)
        {
            var ast = asteroidPool.Get();
            ast.transform.SetParent(transform);
            ast.transform.SetPositionAndRotation(pose.position, pose.rotation);
            return ast;
        }

        public void ReleaseAsteroid(Asteroid ast)
        {
            if (ast) Registry.Unregister(ast);
            asteroidPool.Release(ast);
        }

        public void ReleaseAllAsteroids()
        {
            var toRelease = new List<Asteroid>(Registry.ActiveAsteroids);
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
            var meshInfo = spawnSettings.GetRandomMeshInfo();
            var (finalMass, scale) = CalculateMassAndScale(asteroid, meshInfo, mass);
            asteroid.Initialize(this, meshInfo, finalMass, scale, velocity, angularVelocity);
        }
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