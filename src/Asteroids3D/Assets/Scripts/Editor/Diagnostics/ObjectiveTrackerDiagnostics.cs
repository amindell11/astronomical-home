using Game.Services;
using Objectives;
using UnityEngine;

namespace Diagnostics
{
    /// <summary>Editor-only spine-transition logger; attach next to ObjectiveService, safe to remove.</summary>
    [RequireComponent(typeof(ObjectiveService))]
    public class ObjectiveTrackerDiagnostics : MonoBehaviour
    {
        [Tooltip("Log state transitions to Console")]
        [SerializeField] private bool logTransitions = true;

        private ObjectiveService objectiveService;

        private void Awake()
        {
            objectiveService = GetComponent<ObjectiveService>();
        }

        private void OnEnable()
        {
            if (objectiveService != null && logTransitions)
                objectiveService.OnSpineStateChanged += LogTransition;
        }

        private void OnDisable()
        {
            if (objectiveService != null)
                objectiveService.OnSpineStateChanged -= LogTransition;
        }

        private void LogTransition(ObjectiveType from, ObjectiveType to)
        {
            Debug.Log($"[ObjectiveTracker] {from} → {to} (t={Time.time:F2}s)", this);
        }
    }
}
