using UnityEngine;

namespace Diagnostics
{
    /// <summary>
    /// Diagnostic component that logs GameObject enable/disable state changes.
    /// Attach to any GameObject to track when it gets enabled/disabled and by what call stack.
    /// 
    /// USAGE:
    /// 1. Add this component to LockOnIndicator, ShieldUI, or weapon GameObjects in the scene
    /// 2. Enable "Log Enable/Disable" in the Inspector
    /// 3. Enable "Log Stack Trace" to see what code is causing the state change
    /// 4. Play the scene and watch the Console for diagnostic output
    /// 
    /// This helps identify:
    /// - When components disable unexpectedly
    /// - What code path triggered the disable
    /// - Whether parent or child deactivation is the cause
    /// </summary>
    public class ComponentStateDiagnostics : MonoBehaviour
    {
        [Header("Diagnostic Settings")]
        [Tooltip("Log when GameObject is enabled")]
        [SerializeField] private bool logEnable = true;

        [Tooltip("Log when GameObject is disabled")]
        [SerializeField] private bool logDisable = true;

        [Tooltip("Include stack trace in logs (helps identify caller)")]
        [SerializeField] private bool logStackTrace = true;

        [Tooltip("Log every frame while enabled (verbose)")]
        [SerializeField] private bool logUpdate = false;

        [Tooltip("Custom label for log messages (defaults to GameObject name)")]
        [SerializeField] private string customLabel = "";

        [Header("Runtime State")]
        [Tooltip("Number of times OnEnable was called")]
        [SerializeField] private int enableCount = 0;

        [Tooltip("Number of times OnDisable was called")]
        [SerializeField] private int disableCount = 0;

        [Tooltip("Is currently enabled")]
        [SerializeField] private bool isCurrentlyEnabled = false;

        private string Label => string.IsNullOrEmpty(customLabel) ? gameObject.name : customLabel;

        private void OnEnable()
        {
            enableCount++;
            isCurrentlyEnabled = true;

            if (logEnable)
            {
                var parentInfo = transform.parent ? $" (Parent: {transform.parent.name})" : " (No parent)";
                var message = $"[ComponentStateDiagnostics] {Label} OnEnable called (count: {enableCount}){parentInfo}";

                if (logStackTrace)
                {
                    Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace(), this);
                }
                else
                {
                    Debug.Log(message, this);
                }
            }
        }

        private void OnDisable()
        {
            disableCount++;
            isCurrentlyEnabled = false;

            if (logDisable)
            {
                var parentInfo = transform.parent ? $" (Parent: {transform.parent.name})" : " (No parent)";
                var message = $"[ComponentStateDiagnostics] {Label} OnDisable called (count: {disableCount}){parentInfo}";

                if (logStackTrace)
                {
                    Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace(), this);
                }
                else
                {
                    Debug.Log(message, this);
                }
            }
        }

        private void Update()
        {
            if (logUpdate)
            {
                Debug.Log($"[ComponentStateDiagnostics] {Label} Update - active: {gameObject.activeInHierarchy}, " +
                         $"self: {gameObject.activeSelf}", this);
            }
        }

        /// <summary>
        /// Call this from external code to log custom diagnostic messages.
        /// </summary>
        public void LogCustom(string message)
        {
            Debug.Log($"[ComponentStateDiagnostics] {Label} - {message}", this);
        }

        /// <summary>
        /// Reset diagnostic counters (useful when testing multiple scenarios).
        /// </summary>
        public void ResetCounters()
        {
            enableCount = 0;
            disableCount = 0;
            Debug.Log($"[ComponentStateDiagnostics] {Label} - Counters reset", this);
        }
    }
}
