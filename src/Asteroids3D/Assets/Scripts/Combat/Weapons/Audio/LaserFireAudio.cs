using Combat.Weapons.Arsenal;
using FMOD.Studio;
using UnityEngine;

namespace Combat.Weapons.Audio
{
    [RequireComponent(typeof(Lasers))]
    public sealed class LaserFireAudio : WeaponFireAudio
    {
        private const string HeatParameter = "Heat";

        private Lasers lasers;

        protected override void Awake()
        {
            base.Awake();
            lasers = GetComponent<Lasers>();
        }

        // Post-shot heat: the weapon's conditions process the shot before it raises OnFire.
        protected override void SetParameters(EventInstance instance) =>
            instance.setParameterByName(HeatParameter, lasers.Heat.HeatPct);
    }
}
