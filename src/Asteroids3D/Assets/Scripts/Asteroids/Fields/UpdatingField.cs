using System;
using UnityEditor;
using UnityEngine;

namespace Asteroids.Fields
{
    /// <summary>
    /// Open-world asteroid field manager that centres spawning logic on the main
    /// player camera. All heavy logic lives in <see cref="BaseFieldManager"/>.
    /// </summary>
    public class UpdatingField : Field
    {
        [Header("Update Spawn Zone")]
        [Tooltip("Min spawn distance used during ongoing updates (InvokeRepeating calls)")]
        [SerializeField] protected float updateMinSpawnDistance = 30f;
        [Tooltip("Max spawn distance used during ongoing updates (InvokeRepeating calls)")]
        [SerializeField] protected float updateMaxSpawnDistance = 50f;
    
        [Header("Update Timing")]
        [SerializeField] protected float densityCheckInterval = 0.25f;

        public Func<Vector3> CurrentAnchorPos { private get; set; }
        private float densityCheckTimer = 0f;
        
        protected override void Start()
        {
            base.Start();
            densityCheckTimer = densityCheckInterval;
        }

        private void Update()
        {
            densityCheckTimer -= Time.deltaTime;
            if (!(densityCheckTimer <= 0f)) return;
            
            CurrentAnchorPos ??= () => transform.position;
            SpawnCenter = CurrentAnchorPos();
            ManageField(updateMinSpawnDistance, updateMaxSpawnDistance, maxSpawnsPerFrame);
            
            densityCheckTimer = densityCheckInterval;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            // Draw the base class gizmos first (initial spawn zone and density check)
            base.OnDrawGizmosSelected();
        
            // Now draw our update spawn zone

            var center = SpawnCenter;
            center.y = 0f;

            // Draw update spawn zone with different colors
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(center, updateMinSpawnDistance);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, updateMaxSpawnDistance);
        
            // Add labels to distinguish the zones
            Handles.color = Color.white;
            Handles.Label(center + Vector3.forward * (minSpawnDistance + 2f), "Initial Min");
            Handles.Label(center + Vector3.forward * (maxSpawnDistance + 2f), "Initial Max");
            Handles.Label(center + Vector3.forward * (updateMinSpawnDistance + 2f), "Update Min");
            Handles.Label(center + Vector3.forward * (updateMaxSpawnDistance + 2f), "Update Max");
        }
#endif
    }
} 