using Cameras.Shake;
using Ships;
using Substrate;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons
{
    public sealed class WeaponShake : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Shake played on the ship at each shot, kicking against the shot.")]
        [SerializeField] private CameraShakeProfile profile;

        private WeaponComponent weapon;
        private Ship ship;

        private void Awake()
        {
            weapon = GetComponent<WeaponComponent>();
            ship = GetComponentInParent<Ship>();
        }

        private void OnEnable() => weapon.OnFire += Kick;

        private void OnDisable() => weapon.OnFire -= Kick;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void Kick()
        {
            if (!ship) return;
            ship.Shake.Raise(this, profile, -GamePlane.WorldDirToPlane(weapon.FireDirection));
        }
    }
}
