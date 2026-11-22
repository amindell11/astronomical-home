using Cameras.Shake;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Projectiles.Shake
{
    [RequireComponent(typeof(Missile))]
    public sealed class MissileShake : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Camera shake played where the missile detonates.")]
        [SerializeField] private CameraShakeProfile profile;

        private Missile missile;

        private void Awake()
        {
            missile = GetComponent<Missile>();
        }

        private void OnEnable() => missile.OnDetonated += PlayDetonation;

        private void OnDisable() => missile.OnDetonated -= PlayDetonation;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void PlayDetonation(Vector3 position) => ShakeListener.Hear(position, profile);
    }
}
