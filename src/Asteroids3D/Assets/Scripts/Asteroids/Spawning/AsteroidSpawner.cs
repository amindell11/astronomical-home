using System.Collections.Generic;
using Asteroids.Fragnetics;
using UnityEngine;

namespace Asteroids.Spawning
{
    [RequireComponent(typeof(Fragger))]
    public class AsteroidSpawner : MonoBehaviour
    {
        [SerializeField] private AsteroidSpawnSettings settings;

        /// <summary>Attribute-decision seam; swapped for the deterministic provider in PR2.</summary>
        public RandomAsteroidAttributeRoller AttributeProvider { get; private set; }

        public int ActiveCount => registry.ActiveCount;
        public float TotalVolume => registry.TotalVolume;

        private Registry registry;
        private SpawnPool pool;
        private Transform worldAnchor;
        private Fragger fragger;

        public void SetWorldAnchor(Transform anchor)
        {
            worldAnchor = anchor;
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
            pool = new SpawnPool(settings, transform);
            fragger = GetComponent<Fragger>();
            AttributeProvider = new RandomAsteroidAttributeRoller(settings);
        }

        public void DespawnAll()
        {
            // Snapshot first: Despawn -> Registry.Unregister mutates ActiveAsteroids mid-enumeration.
            foreach (var a in new List<AsteroidController>(registry.ActiveAsteroids)) Despawn(a);
        }

        public void Despawn(AsteroidController ast)
        {
            registry.Unregister(ast);
            pool.ReleaseAsteroid(ast);
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
            return Spawn(pose, attrs);
        }

        private AsteroidController SpawnAtPose(Pose pose)
        {
            var ast = pool.Get();
            ast.transform.SetParent(transform);
            ast.transform.SetPositionAndRotation(pose.position, pose.rotation);
            ast.SetWorldAnchor(worldAnchor);
            return ast;
        }
    }
}
