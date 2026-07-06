using System;
using System.Collections.Generic;
using Asteroids.Fragnetics;
using UnityEngine;

namespace Asteroids.Spawning
{
    [RequireComponent(typeof(Fragger))]
    public class AsteroidSpawner : MonoBehaviour
    {
        [SerializeField] private AsteroidSpawnSettings settings;

        public AsteroidSpawnSettings Settings => settings;

        /// <summary>Attribute roller for the fragment path (mass-constrained rolls).</summary>
        public RandomAsteroidAttributeRoller AttributeProvider { get; private set; }

        public int ActiveCount => registry.ActiveCount;
        public float TotalVolume => registry.TotalVolume;

        /// <summary>
        /// Live spawners, for consumers that query asteroid state directly instead of via
        /// physics (e.g. the MPC obstacle scan). Registered on enable, removed on disable;
        /// a spawner without settings disables itself in Awake and never appears here.
        /// </summary>
        public static IReadOnlyList<AsteroidSpawner> ActiveSpawners => activeSpawners;
        private static readonly List<AsteroidSpawner> activeSpawners = new();

        /// <summary>
        /// The live asteroid set (registry state: destroyed asteroids leave immediately,
        /// fragments enter on spawn). Null until Awake has run.
        /// </summary>
        public HashSet<AsteroidController> LiveAsteroids => registry?.LiveSet;

        /// <summary>
        /// Raised for fragments spawned via <see cref="Fragger"/> so the field
        /// can assign them authored IDs and track them in the override overlay.
        /// </summary>
        public event Action<AsteroidController> OnFragmentSpawned;

        private Registry registry;
        private SpawnPool pool;
        private int poolMaxSizeHint;
        private Transform worldAnchor;
        private Fragger fragger;

        // Created lazily so the field can pre-size it (from the computed
        // worst-case loaded count) before the first spawn, regardless of
        // sibling Awake ordering.
        private SpawnPool Pool => pool ??= new SpawnPool(settings, transform, poolMaxSizeHint);

        public void SetWorldAnchor(Transform anchor)
        {
            worldAnchor = anchor;
        }

        /// <summary>
        /// Raises the pool's retained-object cap to the given worst-case count.
        /// Must be called before the first spawn; later calls are ignored.
        /// </summary>
        public void PreSizePool(int worstCaseCount)
        {
            if (pool != null) return;
            poolMaxSizeHint = Mathf.Max(poolMaxSizeHint, worstCaseCount);
        }

        private void Awake()
        {
            if (!settings)
            {
                enabled = false;
                return;
            }
            settings.ValidateSettings();
            registry = new Registry();
            fragger = GetComponent<Fragger>();
            AttributeProvider = new RandomAsteroidAttributeRoller(settings);
        }

        private void OnEnable()
        {
            if (settings) activeSpawners.Add(this);
        }

        private void OnDisable()
        {
            activeSpawners.Remove(this);
        }

        public void DespawnAll()
        {
            // Snapshot first: Despawn -> Registry.Unregister mutates ActiveAsteroids mid-enumeration.
            foreach (var a in new List<AsteroidController>(registry.ActiveAsteroids)) Despawn(a);
        }

        public void Despawn(AsteroidController ast)
        {
            registry.Unregister(ast);
            Pool.ReleaseAsteroid(ast);
        }

        public AsteroidController Spawn(Pose pose, in AsteroidAttributes attrs)
        {
            var ast = SpawnAtPose(pose);
            ast.Initialize(this, fragger, attrs.MeshInfo, attrs.Mass, attrs.Scale, attrs.Velocity, attrs.AngularVelocity);
            registry.Register(ast);
            return ast;
        }

        public AsteroidController SpawnFragment(Frag frag)
        {
            var pose = new Pose(frag.Position, frag.Rotation);
            var attrs = AttributeProvider.RollForMass(frag.Mass, frag.Velocity, frag.Spin);
            var ast = Spawn(pose, attrs);
            OnFragmentSpawned?.Invoke(ast);
            return ast;
        }

        private AsteroidController SpawnAtPose(Pose pose)
        {
            var ast = Pool.Get();
            ast.transform.SetParent(transform);
            ast.transform.SetPositionAndRotation(pose.position, pose.rotation);
            ast.SetWorldAnchor(worldAnchor);
            return ast;
        }
    }
}
