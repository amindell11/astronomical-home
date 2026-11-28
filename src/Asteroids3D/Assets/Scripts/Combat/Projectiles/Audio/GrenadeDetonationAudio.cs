using System;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Audio
{
    // Subscribes the detonation only: a contact detonation raises Hit first and would sound twice.
    public sealed class GrenadeDetonationAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD one-shot played where the charge detonates.")]
        [SerializeField] private EventReference detonateEvent;

        private IDetonating charge;

        private void Awake()
        {
            charge = GetComponent<IDetonating>();
            if (charge == null)
                throw new InvalidOperationException("GrenadeDetonationAudio requires a detonating charge.");
        }

        private void OnEnable() => charge.OnDetonated += PlayDetonation;

        private void OnDisable() => charge.OnDetonated -= PlayDetonation;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void PlayDetonation(Vector3 point) => RuntimeManager.PlayOneShot(detonateEvent, point);
    }
}
