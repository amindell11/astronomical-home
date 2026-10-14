using Combat.Weapons.Arsenal;
using Damage;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons.Audio
{
    [RequireComponent(typeof(Railguns))]
    public sealed class RailgunImpactAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD one-shot played where the beam hits.")]
        [SerializeField] private EventReference impactEvent;

        private Railguns railguns;

        private void Awake()
        {
            railguns = GetComponent<Railguns>();
        }

        private void OnEnable() => railguns.Hit += PlayImpact;

        private void OnDisable() => railguns.Hit -= PlayImpact;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void PlayImpact(Vector3 point, IDamageable _) => RuntimeManager.PlayOneShot(impactEvent, point);
    }
}
