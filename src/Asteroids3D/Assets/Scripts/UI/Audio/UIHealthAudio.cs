using Ships.Damage;
using UnityEngine;

namespace UI.Audio
{
    /// Plays audio cues driven by DamageController health/shield events.
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
        private IDamageEvents damage;
        private bool isAlarmPlaying;

        // Cache current values to avoid redundant calculations
        private float currentHealthPercentage = 1.0f;
        private float currentShieldPercentage = 1.0f;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop        = false;
        }

        public void Initialize(IDamageEvents damageEvents)
        {
            // Re-bindable: the persistent HUD re-Initializes when the hangar rebuilds the player.
            UnsubscribeFromEvents();
            damage = damageEvents;
            SubscribeToEvents();
            InitializeCurrentValues();
        }

        private void OnDisable()
        {
            StopAlarm();
            UnsubscribeFromEvents();
        }

        /* ----------------- Event Handlers ----------------- */
        private void OnHealthChanged(float current, float previous, float max)
        {
            currentHealthPercentage = max > 0f ? current / max : 0f;
            CheckAlarmCondition();
        }

        private void OnShieldChanged(float current, float previous, float max)
        {
            currentShieldPercentage = max > 0f ? current / max : 0f;
            CheckAlarmCondition();
        }

        private void OnPlayerDeath(Ships.ShipId _victimId, Damage.DamageInfo _killingBlow)
        {
            StopAlarm();
        }

        private void SubscribeToEvents()
        {
            if (damage == null) return;
            damage.Health.OnValueChanged += OnHealthChanged;
            damage.Shield.OnValueChanged += OnShieldChanged;
            damage.OnDeath += OnPlayerDeath;
        }

        private void InitializeCurrentValues()
        {
            if (damage == null) return;
            currentHealthPercentage = damage.Health.MaxValue > 0f
                ? damage.Health.CurrentValue / damage.Health.MaxValue
                : 0f;
            currentShieldPercentage = damage.Shield.MaxValue > 0f
                ? damage.Shield.CurrentValue / damage.Shield.MaxValue
                : 0f;
        }

        /* ----------------- Alarm Logic ----------------- */
        private void CheckAlarmCondition()
        {
            // Alarm triggers when BOTH health AND shield are below their critical thresholds
            var healthCritical = currentHealthPercentage <= criticalHealthThreshold && currentHealthPercentage > 0f;
            var shieldCritical = currentShieldPercentage <= criticalShieldThreshold;
            var shouldPlayAlarm = healthCritical && shieldCritical;

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
        private void PlayLowHealthAlarm()
        {
            if (!lowHealthAlarmClip) return;

            source.loop = true;
            source.clip = lowHealthAlarmClip;
            source.volume = volume;
            if (!source.isPlaying)
            {
                source.Play();
            }
            isAlarmPlaying = true;
        }

        private void StopAlarm()
        {
            source.loop = false;
            source.Stop();
            source.clip = null;
            isAlarmPlaying = false;
        }

        private void UnsubscribeFromEvents()
        {
            if (damage == null) return;
            damage.Health.OnValueChanged -= OnHealthChanged;
            damage.Shield.OnValueChanged -= OnShieldChanged;
            damage.OnDeath -= OnPlayerDeath;
        }
    }
} 
