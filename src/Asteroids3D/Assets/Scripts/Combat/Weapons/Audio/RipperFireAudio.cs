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
        private float previousShotTime = float.NegativeInfinity;

        protected override void Awake()
        {
            base.Awake();
            cooldown = GetComponent<Cooldown>();
        }

        // Held fire lands exactly one cooldown apart on fixed steps; anything later follows a gap.
        protected override void SetParameters(EventInstance instance)
        {
            var now = Time.fixedTime;
            var firstShot = now - previousShotTime > cooldown.SecondsBetweenShots + Time.fixedDeltaTime * 0.5f;
            previousShotTime = now;
            instance.setParameterByName(FirstShotParameter, firstShot ? 1f : 0f);
        }
    }
}
