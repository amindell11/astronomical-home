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
        public Func<Vector3> CurrentAnchorPos { private get; set; }

        private float densityCheckTimer = 0f;

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
            base.Start();
            densityCheckTimer = densityCheckInterval;
            CullingBoundary.radius = updateMaxSpawnDistance * BoundaryMargin;
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
