using Objectives;
using UnityEngine;

namespace Diagnostics
{
    /// <summary>
    /// Editor-only diagnostic MonoBehaviour for the ObjectiveTracker state machine.
    ///
    /// USAGE:
    /// 1. Attach to any GameObject in the scene.
    /// 2. Call SetAdapter() with an IObjectiveTrackerAdapter (e.g. ObjectiveService) during setup.
    /// 3. State transitions are logged to the Console.
    /// 4. In Scene view, gizmos show the extraction zone radius (yellow sphere).
    ///
    /// This component does NOT affect gameplay and can be removed at any time.
    /// </summary>
    public class ObjectiveTrackerDiagnostics : MonoBehaviour
    {
        [Header("Diagnostics")]
        [Tooltip("Log state transitions to Console")]
        [SerializeField] private bool logTransitions = true;

        [Header("Gizmos — Scene View Only")]
        [Tooltip("Show extraction zone radius gizmo")]
        [SerializeField] private bool drawExtractionZone = true;

        [Tooltip("Extraction zone world-space center")]
        [SerializeField] private Vector3 extractionZoneCenter = Vector3.zero;

        [Tooltip("Extraction zone radius (should match ExtractionZone collider)")]
        [SerializeField] private float extractionZoneRadius = 10f;

        [Tooltip("Gizmo color for extraction zone")]
        [SerializeField] private Color extractionZoneColor = new Color(0f, 1f, 0.5f, 0.3f);

        // ── Runtime subscription ──────────────────────────────────────────────────

        private IObjectiveTrackerAdapter adapter;

        /// <summary>
        /// Wire an adapter at runtime (e.g. from CombatSectorManager after ObjectiveService is configured).
        /// Replaces any previously wired adapter.
        /// </summary>
        public void SetAdapter(IObjectiveTrackerAdapter newAdapter)
        {
            // Unsubscribe from previous
            if (adapter != null)
                adapter.OnStateChanged -= LogTransition;

            adapter = newAdapter;

            if (adapter != null && logTransitions && isActiveAndEnabled)
                adapter.OnStateChanged += LogTransition;
        }

        private void OnEnable()
        {
            if (adapter != null && logTransitions)
                adapter.OnStateChanged += LogTransition;
        }

        private void OnDisable()
        {
            if (adapter != null)
                adapter.OnStateChanged -= LogTransition;
        }

        private void LogTransition(ObjectiveType from, ObjectiveType to)
        {
            Debug.Log($"[ObjectiveTracker] {from} → {to} (t={Time.time:F2}s)", this);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (!drawExtractionZone) return;

            Gizmos.color = extractionZoneColor;
            Gizmos.DrawSphere(extractionZoneCenter, extractionZoneRadius);

            var outline = extractionZoneColor;
            outline.a = 1f;
            Gizmos.color = outline;
            Gizmos.DrawWireSphere(extractionZoneCenter, extractionZoneRadius);
        }
    }
}
