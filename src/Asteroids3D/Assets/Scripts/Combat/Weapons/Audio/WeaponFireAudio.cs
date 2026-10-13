using FMOD.Studio;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons.Audio
{
    /// <summary>
    /// A weapon family's fire sound, one-shot only by ruling: a sustained sound (charging, engines)
    /// is a sibling component that holds its own instance across state changes and stops it in
    /// <see cref="IPresentationPart.ApplyPresentation"/>.
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
