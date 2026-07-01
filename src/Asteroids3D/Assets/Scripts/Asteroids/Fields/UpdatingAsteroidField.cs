using System;
using UnityEngine;

namespace Asteroids.Fields
{
    /// <summary>
    /// Open-world asteroid field manager that centres spawning logic on the main
    /// player camera. All heavy logic lives in <see cref="AsteroidField"/>.
    /// </summary>
    public partial class UpdatingAsteroidField : AsteroidField
    {
        [SerializeField] private World.WorldFollow follower;

        public Func<Vector3> CurrentAnchorPos { private get; set; }

        private Transform playerAnchor;
        private float densityCheckTimer = 0f;

        /// <summary>
        /// Stash the injected player anchor (may be null for spectator/headless runs) and wire the
        /// follower to it. Called from <see cref="Game.Sectors.AsteroidFieldSpawner"/> during
        /// <c>Build</c>, before this component's own <c>Awake</c>/<c>Start</c> have run.
        /// </summary>
        public void SetPlayer(Transform player)
        {
            playerAnchor = player;
            if (follower) follower.SetTarget(player);
            CurrentAnchorPos = () => playerAnchor ? Game.GamePlane.ProjectOntoPlane(playerAnchor.position) : transform.position;
        }

        // Cached settings for update behavior
        private float updateMinSpawnDistance;
        private float updateMaxSpawnDistance;
        private float densityCheckInterval;

        protected override void CacheSettings()
        {
            base.CacheSettings();

            if (!settings) return;

            updateMinSpawnDistance = settings.updateMinSpawnDistance;
            updateMaxSpawnDistance = settings.updateMaxSpawnDistance;
            densityCheckInterval = settings.densityCheckInterval;
        }

        protected override void Start()
        {
            // Anchor the spawner BEFORE base.Start(): base.Start() spawns the initial field, and each
            // asteroid captures the spawner's world-anchor at spawn time (used for mesh-collider LOD).
            // Setting it afterwards leaves the initial batch with a null anchor -> mesh colliders never
            // enable. AsteroidSpawner already exists here (created in base Awake, which ran pre-Start).
            SetWorldAnchor(follower ? follower.transform : null);
            base.Start();
            densityCheckTimer = densityCheckInterval;
            if (CullingBoundary) CullingBoundary.radius = updateMaxSpawnDistance * BoundaryMargin;
            CurrentAnchorPos ??= () => transform.position;
        }

        private void Update()
        {
            densityCheckTimer -= Time.deltaTime;
            if (densityCheckTimer < 0f)
            {
                SpawnCenter = CurrentAnchorPos();
                ManageField(updateMinSpawnDistance, updateMaxSpawnDistance, maxSpawnsPerFrame);
                densityCheckTimer = densityCheckInterval;
            }
        }
    }
}
