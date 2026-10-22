using FMOD.Studio;
using FMODUnity;
using Ships.Damage;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace UI.Audio
{
    /// Plays audio cues driven by DamageController health/shield events.
    public class UIHealthAudio : MonoBehaviour
    {
        [Header("Events")]
        [Tooltip("Looping FMOD alarm held while health and shield are both critical.")]
        [SerializeField] private EventReference lowHealthEvent;
        [Tooltip("FMOD one-shot played when the player's shield reaches zero.")]
        [SerializeField] private EventReference shieldDepletedEvent;

        [Header("Settings")]
        [Tooltip("Health percentage threshold below which the alarm will play (0.0 to 1.0).")]
        [SerializeField, Range(0f, 1f)] private float criticalHealthThreshold = 0.25f;
        [Tooltip("Shield percentage threshold below which the alarm will play (0.0 to 1.0).")]
        [SerializeField, Range(0f, 1f)] private float criticalShieldThreshold = 0.1f;

        private IDamageEvents damage;
        private EventInstance alarm;

        private float currentHealthPercentage = 1.0f;
        private float currentShieldPercentage = 1.0f;

        public void Initialize(IDamageEvents damageEvents)
        {
            // Re-bindable: the persistent HUD re-Initializes when the hangar rebuilds the player.
            UnsubscribeFromEvents();
            StopAlarm();
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
            if (previous > 0f && current <= 0f)
                RuntimeManager.PlayOneShot(shieldDepletedEvent);
            CheckAlarmCondition();
        }

        private void OnPlayerDeath(Ships.Registry.ShipId _victimId, Damage.DamageInfo _killingBlow)
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

            if (shouldPlayAlarm && !alarm.isValid())
            {
                PlayLowHealthAlarm();
            }
            else if (!shouldPlayAlarm)
            {
                StopAlarm();
            }
        }

        /* ----------------- Audio Control ----------------- */
        private void PlayLowHealthAlarm()
        {
            alarm = RuntimeManager.CreateInstance(lowHealthEvent);
            alarm.start();
        }

        private void StopAlarm()
        {
            if (!alarm.isValid()) return;
            alarm.stop(STOP_MODE.ALLOWFADEOUT);
            alarm.release();
            alarm = default;
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
