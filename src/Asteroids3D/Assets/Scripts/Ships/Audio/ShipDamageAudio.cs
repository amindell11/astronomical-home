using Damage;
using FMODUnity;
using Ships;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using Ships.Registry;

namespace Ships.Audio
{
    /// <summary>
    /// A ship's damage one-shots: shield hits, hull hits, and a death sound chosen by the killing
    /// blow's kind. Each plays at the ship's position, not attached: the ship deactivates in the same
    /// call that reports its death. The damage surface is injected via <see cref="Bind"/>
    /// (see <see cref="IShipVisual"/>).
    /// </summary>
    public sealed class ShipDamageAudio : MonoBehaviour, IShipVisual
    {
        [Tooltip("FMOD one-shot played at the ship each time its shield drops.")]
        [SerializeField] private EventReference shieldHitEvent;
        [Tooltip("FMOD one-shot played at the ship each time its hull health drops.")]
        [SerializeField] private EventReference hullHitEvent;
        [Tooltip("FMOD one-shot played where the ship dies, unless a collision killed it.")]
        [SerializeField] private EventReference deathEvent;
        [Tooltip("FMOD one-shot played where the ship dies when an asteroid collision killed it.")]
        [SerializeField] private EventReference collisionDeathEvent;

        private IDamageEvents damage;
        private bool subscribed;

        public void Bind(in ShipView view)
        {
            damage = view.Damage;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (damage != null) Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (subscribed || damage == null) return;
            damage.Shield.OnValueChanged += HandleShieldChanged;
            damage.Health.OnValueChanged += HandleHealthChanged;
            damage.OnDeath += HandleDeath;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || damage == null) return;
            damage.Shield.OnValueChanged -= HandleShieldChanged;
            damage.Health.OnValueChanged -= HandleHealthChanged;
            damage.OnDeath -= HandleDeath;
            subscribed = false;
        }

        private void HandleShieldChanged(float current, float previous, float max)
        {
            if (current < previous) RuntimeManager.PlayOneShot(shieldHitEvent, transform.position);
        }

        private void HandleHealthChanged(float current, float previous, float max)
        {
            if (current < previous) RuntimeManager.PlayOneShot(hullHitEvent, transform.position);
        }

        private void HandleDeath(ShipId _victimId, DamageInfo killingBlow)
        {
            var death = killingBlow.Kind == DamageKind.Collision ? collisionDeathEvent : deathEvent;
            RuntimeManager.PlayOneShot(death, transform.position);
        }
    }
}
