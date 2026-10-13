using FMOD.Studio;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons.Audio
{
    /// <summary>
    /// One-shot fire sound for a weapon family: on every shot it starts <c>fireEvent</c> at the
    /// fire point, lets the family set the event's parameters, and releases the instance. One-shots
    /// only, by ruling: a sustained sound (charging, engines) is a sibling component that holds its
    /// own instance across state changes and stops it in <see cref="ApplyPresentation"/>.
    /// </summary>
    public abstract class WeaponFireAudio : MonoBehaviour, IPresentationPart
    {
        [Tooltip("FMOD event started at the fire point on every shot.")]
        [SerializeField] private EventReference fireEvent;

        private WeaponComponent weapon;

        protected virtual void Awake()
        {
            weapon = GetComponent<WeaponComponent>();
        }

        private void OnEnable() => weapon.OnFire += PlayFire;

        private void OnDisable() => weapon.OnFire -= PlayFire;

        public void ApplyPresentation(bool visible) => enabled = visible;

        protected abstract void SetParameters(EventInstance instance);

        private void PlayFire()
        {
            var instance = RuntimeManager.CreateInstance(fireEvent);
            instance.set3DAttributes(weapon.firePoint.position.To3DAttributes());
            SetParameters(instance);
            instance.start();
            instance.release();
        }
    }
}
