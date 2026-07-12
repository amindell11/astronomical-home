using Game.Services;
using Objectives;
using UnityEngine;

namespace Diagnostics
{
    /// <summary>
    /// Editor-only diagnostic MonoBehaviour for the ObjectiveTracker state machine.
    ///
    /// USAGE:
    /// 1. Attach to the same GameObject as SessionHost (which RequireComponents ObjectiveService).
    /// 2. State transitions are logged to the Console automatically.
    /// 3. In Scene view, gizmos show the extraction zone radius (yellow sphere).
    ///
    /// This component does NOT affect gameplay and can be removed at any time.
    /// </summary>
    [RequireComponent(typeof(ObjectiveService))]
    public class ObjectiveTrackerDiagnostics : MonoBehaviour
    {
        [Header("Diagnostics")]
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
                objectiveService.OnStateChanged += LogTransition;
        }

        private void OnDisable()
        {
            if (objectiveService != null)
                objectiveService.OnStateChanged -= LogTransition;
        }

        private void LogTransition(ObjectiveType from, ObjectiveType to)
        {
            Debug.Log($"[ObjectiveTracker] {from} → {to} (t={Time.time:F2}s)", this);
        }


    }
}
