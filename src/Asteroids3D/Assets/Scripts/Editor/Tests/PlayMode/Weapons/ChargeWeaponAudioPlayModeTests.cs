#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using FMOD.Studio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    /// <summary>
    /// The sustained charge sound on both charge weapons: live while the trigger is held with
    /// Charge on the gauge, playing on past the shot with Charge at the spent charge, and stopped
    /// after a release below the minimum.
    /// </summary>
    [Category("Audio")]
    public class ChargeWeaponAudioPlayModeTests : WeaponAudioFixture
    {
        private const string ChargeLasersPrefabPath = "Assets/Prefabs/Weapons/ChargeLasers.prefab";
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";
        private const string ChargeLaserFire = "event:/Weapons/ChargeLaser/Fire";
        private const string RailgunFire = "event:/Weapons/Railgun/Fire";

        [UnityTest]
        public IEnumerator ChargeLasers_SoundFollowsTheChargeUp_KeysOffOnTheShot_AndStopsOnADrop()
        {
            var weapon = Mount<ChargeLasers>(ChargeLasersPrefabPath);
            yield return ChargeSound(weapon, weapon.Charge, ChargeLaserFire);
        }

        [UnityTest]
        public IEnumerator Railgun_SoundFollowsTheChargeUp_KeysOffOnTheShot_AndStopsOnADrop()
        {
            var weapon = Mount<Railguns>(RailgunPrefabPath);
            yield return ChargeSound(weapon, weapon.Charge, RailgunFire);
        }

        private IEnumerator ChargeSound(WeaponComponent weapon, ChargeTime charge, string eventPath)
        {
            // 10 physics steps to full, independent of the project's fixed timestep.
            charge.Configure(chargeTime: Time.fixedDeltaTime * 10f, minChargeToFire: 0.3f);
            var description = Fresh(eventPath);
            var fired = 0;
            weapon.OnFire += () => fired++;

            Hold(weapon, 5);
            Assert.AreEqual(0.5f, charge.ChargePct, 1e-3f);
            yield return Flush();
            var instances = Instances(description);
            Assert.AreEqual(1, instances.Length, "one sustained instance while held");
            var heldState = State(instances[0]);
            var heldCharge = Parameter(instances[0], "Charge");
            Debug.Log($"[FMOD] {eventPath} held: state={heldState} charge={heldCharge} gauge={charge.ChargePct}");
            Assert.That(heldState, Is.EqualTo(PLAYBACK_STATE.PLAYING).Or.EqualTo(PLAYBACK_STATE.STARTING).Or.EqualTo(PLAYBACK_STATE.SUSTAINING),
                "live while held; SUSTAINING once the cursor parks at the sustain point");
            Assert.AreEqual(charge.ChargePct, heldCharge, 1e-4f, "Charge follows the gauge");

            Release(weapon);
            Assert.AreEqual(1, fired, "a release above the minimum fires");
            Assert.AreEqual(0f, charge.ChargePct, 1e-4f, "the shot consumed the charge");
            Assert.AreEqual(0.5f, charge.SpentCharge, 1e-3f);
            yield return Flush();
            instances = Instances(description);
            Assert.AreEqual(1, instances.Length, "the keyed-off instance plays on into its release");
            var spentCharge = Parameter(instances[0], "Charge");
            Debug.Log($"[FMOD] {eventPath} shot: state={State(instances[0])} charge={spentCharge} spent={charge.SpentCharge}");
            AssertPlaying(instances[0], "keyed off: past the sustain point, not stopped");
            Assert.AreEqual(charge.SpentCharge, spentCharge, 1e-4f, "Charge holds the spent charge through the release");

            description.releaseAllInstances();
            yield return Flush();
            Hold(weapon, 2);
            yield return Flush();
            Assert.AreEqual(1, Instances(description).Length, "a fresh instance for the second charge-up");
            Release(weapon);
            Assert.AreEqual(1, fired, "a release below the minimum does not fire");
            yield return Flush();
            instances = Instances(description);
            var dropState = instances.Length == 0 ? PLAYBACK_STATE.STOPPED : State(instances[0]);
            Debug.Log($"[FMOD] {eventPath} drop: instances={instances.Length} state={dropState}");
            Assert.That(dropState, Is.EqualTo(PLAYBACK_STATE.STOPPED).Or.EqualTo(PLAYBACK_STATE.STOPPING), "an unfired release stops the sound");
        }
    }
}
#endif
