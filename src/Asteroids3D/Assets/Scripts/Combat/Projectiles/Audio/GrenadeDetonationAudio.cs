using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Audio
{
    // Subscribes the detonation only: a contact detonation raises Hit first and would sound twice.
    [RequireComponent(typeof(Grenade))]
    public sealed class GrenadeDetonationAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD one-shot played where the charge detonates.")]
        [SerializeField] private EventReference detonateEvent;

        private Grenade grenade;

        private void Awake()
        {
            grenade = GetComponent<Grenade>();
        }

        private void OnEnable() => grenade.OnDetonated += PlayDetonation;

        private void OnDisable() => grenade.OnDetonated -= PlayDetonation;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void PlayDetonation(Vector3 point) => RuntimeManager.PlayOneShot(detonateEvent, point);
    }
}
