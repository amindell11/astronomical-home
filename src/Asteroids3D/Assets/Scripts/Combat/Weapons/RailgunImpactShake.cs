using Cameras.Shake;
using Combat.Weapons.Arsenal;
using Damage;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons
{
    [RequireComponent(typeof(Railguns))]
    public sealed class RailgunImpactShake : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Camera shake played where the beam hits.")]
        [SerializeField] private CameraShakeProfile profile;

        private Railguns railguns;

        private void Awake()
        {
            railguns = GetComponent<Railguns>();
        }

        private void OnEnable() => railguns.Hit += PlayImpact;

        private void OnDisable() => railguns.Hit -= PlayImpact;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void PlayImpact(Vector3 point, IDamageable _) => ShakeListener.Hear(point, profile);
    }
}
