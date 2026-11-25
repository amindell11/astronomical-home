using Cameras.Shake;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Shake
{
    [RequireComponent(typeof(Grenade))]
    public sealed class GrenadeShake : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Camera shake played where the charge detonates.")]
        [SerializeField] private CameraShakeProfile profile;

        private Grenade grenade;

        private void Awake()
        {
            grenade = GetComponent<Grenade>();
        }

        private void OnEnable() => grenade.OnDetonated += PlayDetonation;

        private void OnDisable() => grenade.OnDetonated -= PlayDetonation;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void PlayDetonation(Vector3 position) => ShakeListener.Hear(position, profile);
    }
}
