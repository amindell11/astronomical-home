using Ships.Damage;
using UI;
using UnityEngine;

namespace Diagnostics
{
    /// <summary>
    /// Diagnostic component for UI elements (LockOnIndicator, ShieldUI).
    /// Monitors event subscriptions and state changes.
    /// 
    /// USAGE:
    /// 1. Add this component to GameObjects containing LockOnIndicator or ShieldUI
    /// 2. Enable "Monitor UI Components" in the Inspector
    /// 3. Play the scene and watch the Console for diagnostic output
    /// 
    /// This helps identify:
    /// - When UI components lose event subscriptions
    /// - When CanvasGroup alpha changes unexpectedly
    /// - Whether UI components are responding to game events
    /// </summary>
    public class UIDiagnostics : MonoBehaviour
    {
        [Header("Diagnostic Settings")]
        [Tooltip("Enable UI component monitoring")]
        [SerializeField] private bool monitorUIComponents = true;

        [Tooltip("Log shield value changes")]
        [SerializeField] private bool logShieldChanges = true;

        [Tooltip("Log lock progress updates")]
        [SerializeField] private bool logLockProgress = true;

        [Tooltip("Log CanvasGroup alpha changes")]
        [SerializeField] private bool logAlphaChanges = true;

        [Header("Runtime State")]
        [SerializeField] private int shieldChangeCount = 0;
        [SerializeField] private int lockProgressCount = 0;
        [SerializeField] private int lockAcquiredCount = 0;
        [SerializeField] private int lockReleasedCount = 0;
        [SerializeField] private float lastAlpha = -1f;

        private LockOnIndicator lockOnIndicator;
        private ShieldUI shieldUI;
        private DamageController damageController;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            lockOnIndicator = GetComponent<LockOnIndicator>();
            shieldUI = GetComponent<ShieldUI>();
            canvasGroup = GetComponent<CanvasGroup>();

            if (lockOnIndicator == null && shieldUI == null)
            {
                Debug.LogWarning("[UIDiagnostics] No LockOnIndicator or ShieldUI found on this GameObject. " +
                                "Add this component to a GameObject with UI components.", this);
            }
        }

        private void OnEnable()
        {
            if (!monitorUIComponents) return;

            Debug.Log($"[UIDiagnostics] {name} OnEnable - Setting up diagnostics", this);

            if (shieldUI != null)
            {
                // Try to find DamageController
                damageController = GetComponentInParent<DamageController>();
                if (damageController != null)
                {
                    damageController.Shield.OnValueChanged += HandleShieldChanged;
                    Debug.Log($"[UIDiagnostics] {name} - Subscribed to Shield.OnValueChanged", this);
                }
                else
                {
                    Debug.LogWarning($"[UIDiagnostics] {name} - DamageController not found for ShieldUI monitoring", this);
                }
            }

            // Note: We can't directly subscribe to LockOnIndicator events as they're internal to the component,
            // but we can log when the component enables/disables
            if (lockOnIndicator != null)
            {
                Debug.Log($"[UIDiagnostics] {name} - LockOnIndicator component is enabled", this);
            }

            if (canvasGroup != null)
            {
                lastAlpha = canvasGroup.alpha;
                Debug.Log($"[UIDiagnostics] {name} - CanvasGroup initial alpha: {lastAlpha}", this);
            }
        }

        private void OnDisable()
        {
            if (!monitorUIComponents) return;

            Debug.Log($"[UIDiagnostics] {name} OnDisable - Cleaning up diagnostics", this);

            if (damageController != null)
            {
                damageController.Shield.OnValueChanged -= HandleShieldChanged;
                Debug.Log($"[UIDiagnostics] {name} - Unsubscribed from Shield.OnValueChanged", this);
            }
        }

        private void Update()
        {
            if (!monitorUIComponents) return;

            // Monitor CanvasGroup alpha changes
            if (canvasGroup != null && logAlphaChanges)
            {
                if (!Mathf.Approximately(canvasGroup.alpha, lastAlpha))
                {
                    Debug.Log($"[UIDiagnostics] {name} - CanvasGroup alpha changed: {lastAlpha} -> {canvasGroup.alpha}", this);
                    lastAlpha = canvasGroup.alpha;
                }
            }

            // Check if components are still valid
            if (lockOnIndicator != null && !lockOnIndicator.enabled)
            {
                Debug.LogWarning($"[UIDiagnostics] {name} - LockOnIndicator component is DISABLED!", this);
            }

            if (shieldUI != null && !shieldUI.enabled)
            {
                Debug.LogWarning($"[UIDiagnostics] {name} - ShieldUI component is DISABLED!", this);
            }
        }

        private void HandleShieldChanged(float current, float previous, float max)
        {
            shieldChangeCount++;

            if (logShieldChanges)
            {
                Debug.Log($"[UIDiagnostics] {name} - Shield changed: {previous:F1} -> {current:F1} / {max:F1} " +
                         $"(count: {shieldChangeCount})", this);
            }

            // Verify ShieldUI is still enabled
            if (shieldUI != null && !shieldUI.enabled)
            {
                Debug.LogError($"[UIDiagnostics] {name} - ShieldUI received shield change event but component is DISABLED! " +
                              "This indicates event subscription survived component disable.", this);
            }
        }

        /// <summary>
        /// Manually log the current UI state.
        /// </summary>
        [ContextMenu("Log Current UI State")]
        public void LogCurrentState()
        {
            Debug.Log($"[UIDiagnostics] {name} State:\n" +
                     $"  GameObject active: {gameObject.activeInHierarchy}\n" +
                     $"  LockOnIndicator: {(lockOnIndicator != null ? (lockOnIndicator.enabled ? "Enabled" : "DISABLED") : "NULL")}\n" +
                     $"  ShieldUI: {(shieldUI != null ? (shieldUI.enabled ? "Enabled" : "DISABLED") : "NULL")}\n" +
                     $"  CanvasGroup alpha: {(canvasGroup != null ? canvasGroup.alpha.ToString("F2") : "N/A")}\n" +
                     $"  Shield changes: {shieldChangeCount}\n" +
                     $"  Lock events: Progress={lockProgressCount}, Acquired={lockAcquiredCount}, Released={lockReleasedCount}", this);
        }

        /// <summary>
        /// Reset diagnostic counters.
        /// </summary>
        [ContextMenu("Reset Counters")]
        public void ResetCounters()
        {
            shieldChangeCount = 0;
            lockProgressCount = 0;
            lockAcquiredCount = 0;
            lockReleasedCount = 0;
            Debug.Log($"[UIDiagnostics] {name} - Counters reset", this);
        }
    }
}
