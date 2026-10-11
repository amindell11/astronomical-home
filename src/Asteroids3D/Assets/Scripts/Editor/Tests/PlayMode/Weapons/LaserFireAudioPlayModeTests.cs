#if UNITY_EDITOR
using System.Collections;
using Combat.Weapons.Arsenal;
using FMOD.Studio;
using FMODUnity;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    /// <summary>
    /// FMOD trial evidence: one real laser shot starts event:/Weapons/Laser/Fire with Heat fed from
    /// the weapon, and the log line records which output FMOD initialised with in this run.
    /// </summary>
    [Category("Audio")]
    public class LaserFireAudioPlayModeTests : PlayModeWorldFixture
    {
        private const string FireEventPath = "event:/Weapons/Laser/Fire";

        private sealed class IdleCommander : Commander
        {
            public override void Initialize(in ShipControl control) { }
        }

        private Ship ship;
        private Commander commanderPrefab;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            commanderPrefab = new GameObject("IdleCommanderPrefab").AddComponent<IdleCommander>();
            ship = Factory.CreateShip(TestAssets.LoadCrimsonPrefab(), commanderPrefab, team: 0, decisionSeed: 0,
                                      projectiles: Projectiles, position: Vector3.zero, rotation: Quaternion.identity);
        }

        [TearDown]
        public override void TearDown()
        {
            DestroyTestObject(commanderPrefab.gameObject);
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LaserShot_StartsFireEvent_WithWeaponHeat()
        {
            var lasers = ship.GetComponentInChildren<Lasers>();
            Assert.IsNotNull(lasers, "Crimson carries no Lasers mount");

            var description = RuntimeManager.GetEventDescription(FireEventPath);
            // Earlier tests' shots may still be playing.
            description.releaseAllInstances();
            RuntimeManager.StudioSystem.flushCommands();

            Assert.IsNotNull(lasers.Fire(lasers.firePoint.position + lasers.firePoint.up * 10f, Projectiles), "shot refused");
            RuntimeManager.StudioSystem.flushCommands();
            yield return null;

            RuntimeManager.CoreSystem.getOutput(out var output);
            description.getInstanceList(out var instances);
            Debug.Log($"[FMOD-TRIAL] batchmode={Application.isBatchMode} output={output} instances={instances.Length}");
            Assert.AreEqual(1, instances.Length, "fire event instances after one shot");
            instances[0].getParameterByName("Heat", out var heat);
            instances[0].getPlaybackState(out var state);
            Debug.Log($"[FMOD-TRIAL] heat={heat} weaponHeat={lasers.Heat.HeatPct} state={state}");

            Assert.AreEqual(lasers.Heat.HeatPct, heat, 1e-4f);
            Assert.That(state, Is.EqualTo(PLAYBACK_STATE.PLAYING).Or.EqualTo(PLAYBACK_STATE.STARTING));
        }
    }
}
#endif
