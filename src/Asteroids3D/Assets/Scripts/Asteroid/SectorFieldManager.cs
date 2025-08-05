using UnityEngine;

namespace Asteroid
{
    /// <summary>
    /// Field manager variant for ML-Agents training arenas.  The spawn anchor is provided
    /// via the inspector or through <see cref="SetAnchor"/>, enabling multiple
    /// independent arenas to coexist in a single scene or head-less batch process.
    /// </summary>
    public class SectorFieldManager : BaseFieldManager
    {
        [Tooltip("Transform that defines the sector centre for density checks and spawning.")]
        [SerializeField] private Transform anchorTransform;

        /// <summary>
        /// Provide / override the anchor transform at runtime (e.g., from an ArenaReset).
        /// </summary>
        public void SetAnchor(Transform t)
        {
            anchorTransform = t;
        }

        protected override Transform AcquireAnchor()
        {
            return anchorTransform != null ? anchorTransform : transform;
        }

        protected override void Awake()
        {
            base.Awake();
            // Nothing else for now – base handles spawn logic once Start() is called.
        }
        public void RespawnAsteroids()
        {
            if (Spawner == null) return;
            Spawner.ReleaseAllAsteroids();
            ManageAsteroidField();
        }


    }
} 