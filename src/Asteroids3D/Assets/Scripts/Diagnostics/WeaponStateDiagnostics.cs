using Combat.Weapons;
using Ships.Weapons;
using UnityEngine;

namespace Diagnostics
{
    /// <summary>
    /// Diagnostic component specifically for WeaponsController and weapon mount state tracking.
    /// Monitors weapon references, GameObject state, and fire events.
    /// 
    /// USAGE:
    /// 1. Add this component to the Ship GameObject (it will find WeaponsController automatically)
    /// 2. Enable "Monitor Weapons" in the Inspector
    /// 3. Play the scene and watch the Console for diagnostic output
    /// 
    /// This helps identify:
    /// - When weapon references become null unexpectedly
    /// - When weapon GameObjects disable independently
    /// - Whether weapons can fire after ship death/reset
    /// </summary>
    [RequireComponent(typeof(WeaponsController))]
    public class WeaponStateDiagnostics : MonoBehaviour
    {
        [Header("Diagnostic Settings")]
        [Tooltip("Enable weapon state monitoring")]
        [SerializeField] private bool monitorWeapons = true;

        [Tooltip("Log weapon fire events")]
        [SerializeField] private bool logFireEvents = true;

        [Tooltip("Log detailed state every frame (very verbose)")]
        [SerializeField] private bool logEveryFrame = false;

        [Header("Runtime State")]
        [SerializeField] private int primaryFireCount = 0;
        [SerializeField] private int secondaryFireCount = 0;
        [SerializeField] private bool primaryWasNull = false;
        [SerializeField] private bool secondaryWasNull = false;

        private WeaponsController weaponsController;
        private bool wasEnabled = true;

        private void Awake()
        {
            weaponsController = GetComponent<WeaponsController>();
            if (!weaponsController)
            {
                Debug.LogError("[WeaponStateDiagnostics] WeaponsController not found!", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!monitorWeapons) return;

            Debug.Log($"[WeaponStateDiagnostics] Ship {name} OnEnable - Subscribing to weapon events", this);

            SubscribeToWeaponEvents();
            wasEnabled = true;
        }

        private void OnDisable()
        {
            if (!monitorWeapons) return;

            Debug.Log($"[WeaponStateDiagnostics] Ship {name} OnDisable - Unsubscribing from weapon events", this);

            UnsubscribeFromWeaponEvents();
            wasEnabled = false;
        }

        private void Update()
        {
            if (!monitorWeapons) return;

            // Check for unexpected null references
            if (weaponsController.Primary == null && !primaryWasNull)
            {
                Debug.LogWarning($"[WeaponStateDiagnostics] Ship {name} - Primary weapon became NULL!\n" +
                                StackTraceUtility.ExtractStackTrace(), this);
                primaryWasNull = true;
            }

            if (weaponsController.Secondary == null && !secondaryWasNull)
            {
                Debug.LogWarning($"[WeaponStateDiagnostics] Ship {name} - Secondary weapon became NULL!\n" +
                                StackTraceUtility.ExtractStackTrace(), this);
                secondaryWasNull = true;
            }

            // Check for unexpected disabled state
            if (weaponsController.Primary != null && !weaponsController.Primary.gameObject.activeInHierarchy)
            {
                if (gameObject.activeInHierarchy) // Ship is active but weapon is not
                {
                    Debug.LogWarning($"[WeaponStateDiagnostics] Ship {name} - Primary weapon GameObject is INACTIVE while ship is ACTIVE!\n" +
                                    $"Weapon self: {weaponsController.Primary.gameObject.activeSelf}, " +
                                    $"Weapon hierarchy: {weaponsController.Primary.gameObject.activeInHierarchy}", this);
                }
            }

            if (weaponsController.Secondary != null && !weaponsController.Secondary.gameObject.activeInHierarchy)
            {
                if (gameObject.activeInHierarchy) // Ship is active but weapon is not
                {
                    Debug.LogWarning($"[WeaponStateDiagnostics] Ship {name} - Secondary weapon GameObject is INACTIVE while ship is ACTIVE!\n" +
                                    $"Weapon self: {weaponsController.Secondary.gameObject.activeSelf}, " +
                                    $"Weapon hierarchy: {weaponsController.Secondary.gameObject.activeInHierarchy}", this);
                }
            }

            if (logEveryFrame)
            {
                LogCurrentState();
            }
        }

        private void SubscribeToWeaponEvents()
        {
            if (weaponsController.Primary != null)
            {
                weaponsController.Primary.OnFire += HandlePrimaryFire;
            }

            if (weaponsController.Secondary != null)
            {
                weaponsController.Secondary.OnFire += HandleSecondaryFire;
            }
        }

        private void UnsubscribeFromWeaponEvents()
        {
            if (weaponsController.Primary != null)
            {
                weaponsController.Primary.OnFire -= HandlePrimaryFire;
            }

            if (weaponsController.Secondary != null)
            {
                weaponsController.Secondary.OnFire -= HandleSecondaryFire;
            }
        }

        private void HandlePrimaryFire()
        {
            primaryFireCount++;
            if (logFireEvents)
            {
                Debug.Log($"[WeaponStateDiagnostics] Ship {name} - Primary weapon fired (total: {primaryFireCount})", this);
            }
        }

        private void HandleSecondaryFire()
        {
            secondaryFireCount++;
            if (logFireEvents)
            {
                Debug.Log($"[WeaponStateDiagnostics] Ship {name} - Secondary weapon fired (total: {secondaryFireCount})", this);
            }
        }

        private void LogCurrentState()
        {
            var primaryState = weaponsController.Primary != null
                ? $"Active: {weaponsController.Primary.gameObject.activeInHierarchy}"
                : "NULL";

            var secondaryState = weaponsController.Secondary != null
                ? $"Active: {weaponsController.Secondary.gameObject.activeInHierarchy}"
                : "NULL";

            Debug.Log($"[WeaponStateDiagnostics] Ship {name} State:\n" +
                     $"  Ship active: {gameObject.activeInHierarchy}\n" +
                     $"  Primary: {primaryState}, Fires: {primaryFireCount}\n" +
                     $"  Secondary: {secondaryState}, Fires: {secondaryFireCount}", this);
        }

        /// <summary>
        /// Manually log the current state (can be called from external code or inspector button).
        /// </summary>
        [ContextMenu("Log Current Weapon State")]
        public void LogState()
        {
            LogCurrentState();
        }

        /// <summary>
        /// Reset diagnostic counters.
        /// </summary>
        [ContextMenu("Reset Counters")]
        public void ResetCounters()
        {
            primaryFireCount = 0;
            secondaryFireCount = 0;
            primaryWasNull = false;
            secondaryWasNull = false;
            Debug.Log($"[WeaponStateDiagnostics] Ship {name} - Counters reset", this);
        }
    }
}
