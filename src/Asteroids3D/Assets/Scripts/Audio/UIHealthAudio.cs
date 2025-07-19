using UnityEngine;

/// Plays audio cues driven by the player Ship's OnHealthChanged events.
[RequireComponent(typeof(AudioSource))]
public class UIHealthAudio : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Looping alarm sound that plays when health drops below the critical threshold.")]
    [SerializeField] private AudioClip lowHealthAlarmClip;

    [Header("Settings")]
    [Tooltip("Health percentage threshold below which the alarm will play (0.0 to 1.0).")]
    [SerializeField, Range(0f, 1f)] private float criticalHealthThreshold = 0.25f;
    [Tooltip("Shield percentage threshold below which the alarm will play (0.0 to 1.0).")]
    [SerializeField, Range(0f, 1f)] private float criticalShieldThreshold = 0.1f;
    [Tooltip("Volume for health alarm sound effects.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    private AudioSource source;
    private Ship playerShip;
    private bool isAlarmPlaying = false;
    
    // Cache current values to avoid redundant calculations
    private float currentHealthPercentage = 1.0f;
    private float currentShieldPercentage = 1.0f;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop        = false;

        // Attempt to find the player ship at startup (may be null if player not spawned yet).
        TryAssignPlayerShip();
    }

    void OnDisable()
    {
        StopAlarm();
        UnsubscribeFromEvents();
    }

    void Update()
    {
        // Ensure we have a reference to the player Ship.
        if (playerShip == null)
        {
            TryAssignPlayerShip();
        }
    }

    /* ----------------- Event Handlers ----------------- */
    void OnHealthChanged(float current, float previous, float max)
    {
        currentHealthPercentage = max > 0f ? current / max : 0f;
        CheckAlarmCondition();
    }

    void OnShieldChanged(float current, float previous, float max)
    {
        currentShieldPercentage = max > 0f ? current / max : 0f;
        CheckAlarmCondition();
    }

    void OnPlayerDeath(Ship victim, Ship killer)
    {
        // Stop alarm when player dies
        StopAlarm();
    }

    /* ----------------- Alarm Logic ----------------- */
    void CheckAlarmCondition()
    {
        // Alarm triggers when BOTH health AND shield are below their critical thresholds
        bool healthCritical = currentHealthPercentage <= criticalHealthThreshold && currentHealthPercentage > 0f;
        bool shieldCritical = currentShieldPercentage <= criticalShieldThreshold;
        bool shouldPlayAlarm = healthCritical && shieldCritical;

        if (shouldPlayAlarm && !isAlarmPlaying)
        {
            PlayLowHealthAlarm();
        }
        else if (!shouldPlayAlarm && isAlarmPlaying)
        {
            StopAlarm();
        }
    }

    /* ----------------- Audio Control ----------------- */
    void PlayLowHealthAlarm()
    {
        if (lowHealthAlarmClip == null) return;

        source.loop = true;
        source.clip = lowHealthAlarmClip;
        source.volume = volume;
        if (!source.isPlaying)
        {
            source.Play();
        }
        isAlarmPlaying = true;
    }

    void StopAlarm()
    {
        source.loop = false;
        source.Stop();
        source.clip = null;
        isAlarmPlaying = false;
    }

    /* ----------------- Helper Methods ----------------- */
    void TryAssignPlayerShip()
    {
        var playerObj = GameObject.FindGameObjectWithTag(TagNames.Player);
        if (playerObj != null)
        {
            var newPlayerShip = playerObj.GetComponent<Ship>();
            if (newPlayerShip != null && newPlayerShip != playerShip)
            {
                // Unsubscribe from old ship if it exists
                UnsubscribeFromEvents();
                
                playerShip = newPlayerShip;
                
                // Subscribe to new ship events
                playerShip.OnHealthChanged += OnHealthChanged;
                playerShip.OnShieldChanged += OnShieldChanged;
                playerShip.OnDeath += OnPlayerDeath;
                
                // Initialize current values
                if (playerShip.damageHandler != null)
                {
                    currentHealthPercentage = playerShip.damageHandler.maxHealth > 0f ? 
                        playerShip.damageHandler.CurrentHealth / playerShip.damageHandler.maxHealth : 0f;
                    currentShieldPercentage = playerShip.damageHandler.maxShield > 0f ? 
                        playerShip.damageHandler.CurrentShield / playerShip.damageHandler.maxShield : 0f;
                }
            }
        }
    }

    void UnsubscribeFromEvents()
    {
        if (playerShip != null)
        {
            playerShip.OnHealthChanged -= OnHealthChanged;
            playerShip.OnShieldChanged -= OnShieldChanged;
            playerShip.OnDeath -= OnPlayerDeath;
        }
    }
} 