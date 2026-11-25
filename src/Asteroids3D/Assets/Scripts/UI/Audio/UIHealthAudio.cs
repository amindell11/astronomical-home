using FMOD.Studio;
using FMODUnity;
using Ships.Damage;
using UI.PlayerState.Alerts;
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

        private IDamageEvents damage;
        private EventInstance alarm;

        public void Initialize(IDamageEvents damageEvents)
        {
            // Re-bindable: the persistent HUD re-Initializes when the hangar rebuilds the player.
            UnsubscribeFromEvents();
            StopAlarm();
            damage = damageEvents;
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            StopAlarm();
            UnsubscribeFromEvents();
        }

        /* ----------------- Event Handlers ----------------- */
        private void OnHealthChanged(float current, float previous, float max) => CheckAlarmCondition();

        private void OnShieldChanged(float current, float previous, float max)
        {
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

        /* ----------------- Alarm Logic ----------------- */
        private void CheckAlarmCondition()
        {
            var shouldPlayAlarm = HullCritical.Holds(damage);

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
