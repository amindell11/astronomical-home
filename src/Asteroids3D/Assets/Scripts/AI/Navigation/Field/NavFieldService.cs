using System.Collections.Generic;
using AI.Scanning;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC.Field
{
    /// <summary>
    /// Session-root sibling holding one shared cost-to-go <see cref="NavField"/> per chase target so
    /// every pursuer of that target reuses one solve; the per-field solve/rebuild machinery lives in
    /// <see cref="FieldBaker"/>. Reached per-arena via <see cref="Game.Services.ArenaContext.NavField"/>.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public partial class NavFieldService : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Cells per side. Extent = gridSize * cellSize, centered on the target.")]
        [SerializeField] private int gridSize = 64;
        [Tooltip("Cell size in plane units (~ship length to half a typical gap).")]
        [SerializeField] private float cellSize = 3f;

        [Header("Obstacle inflation")]
        [Tooltip("Added to every asteroid radius before stamping (ship radius + margin).")]
        [SerializeField] private float shipRadiusBuffer = 2f;

        [Header("Rebuild policy")]
        [Tooltip("Minimum interval between rebuilds of one target's field.")]
        [SerializeField] private float minRebuildInterval = 0.15f;
        [Tooltip("Rebuild when the live obstacle count changed by at least this much.")]
        [SerializeField] private int registryDeltaThreshold = 3;
        [Tooltip("Rebuild at least this often regardless of motion (drifting rocks).")]
        [SerializeField] private float maxStaleness = 1f;

        private readonly Dictionary<Transform, FieldBaker> fields = new();
        private readonly List<Transform> stale = new();

        private void OnDestroy()
        {
            foreach (var baker in fields.Values) baker.Dispose();
            fields.Clear();
        }

        /// <summary>
        /// Sampling view of the target's field. Returns false while nothing is solved yet
        /// (first frames) — the caller's terminal hook then contributes 0. Kicks the rebuild
        /// machinery as a side effect. <paramref name="field"/> is B2's live obstacle source
        /// (may be null between sectors, in which case the field bakes with no obstacles).
        /// </summary>
        public bool TryGetData(Transform target, float2 targetPlanePos, float nominalSpeed,
            IObstacleField field, out TerminalFieldData data)
        {
            data = default;
            if (!target) return false;

            if (!fields.TryGetValue(target, out var baker))
            {
                baker = new FieldBaker(gridSize, cellSize, shipRadiusBuffer, new FieldBaker.Policy
                {
                    minRebuildInterval = minRebuildInterval,
                    registryDeltaThreshold = registryDeltaThreshold,
                    maxStaleness = maxStaleness,
                });
                fields[target] = baker;
            }

            baker.RequestBake(targetPlanePos, field);
            return baker.TryGetData(nominalSpeed, out data);
        }

        private void Update()
        {
            stale.Clear();
            foreach (var kvp in fields)
            {
                kvp.Value.Pump();
                if (!kvp.Key) stale.Add(kvp.Key);
            }
            foreach (var key in stale)
            {
                fields[key].Dispose();
                fields.Remove(key);
            }
        }
    }
}
