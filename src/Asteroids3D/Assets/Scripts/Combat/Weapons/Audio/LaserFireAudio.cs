using Combat.Weapons.Arsenal;
using FMODUnity;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons.Audio
{
    [RequireComponent(typeof(Lasers))]
    public sealed class LaserFireAudio : MonoBehaviour, IPresentationPart
    {
        private const string HeatParameter = "Heat";

        [Tooltip("FMOD event played at the fire point on every shot; its Heat parameter receives the weapon's heat fraction after the shot.")]
        [SerializeField] private EventReference fireEvent;

        private Lasers lasers;

        private void Awake()
        {
            lasers = GetComponent<Lasers>();
        }

        private void OnEnable() => lasers.OnFire += PlayFire;

        private void OnDisable() => lasers.OnFire -= PlayFire;

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void PlayFire()
        {
            var instance = RuntimeManager.CreateInstance(fireEvent);
            instance.set3DAttributes(lasers.firePoint.position.To3DAttributes());
            instance.setParameterByName(HeatParameter, lasers.Heat.HeatPct);
            instance.start();
            instance.release();
        }
    }
}
