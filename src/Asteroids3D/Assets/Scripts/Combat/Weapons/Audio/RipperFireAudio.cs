using Combat.Weapons.Conditions;
using FMOD.Studio;
using UnityEngine;

namespace Combat.Weapons.Audio
{
    [RequireComponent(typeof(Cooldown))]
    public sealed class RipperFireAudio : WeaponFireAudio
    {
        private const string FirstShotParameter = "FirstShot";

        private Cooldown cooldown;

        protected override void Awake()
        {
            base.Awake();
            cooldown = GetComponent<Cooldown>();
        }

        protected override void SetParameters(EventInstance instance) =>
            instance.setParameterByName(FirstShotParameter, cooldown.StartsNewCadence ? 1f : 0f);
    }
}
