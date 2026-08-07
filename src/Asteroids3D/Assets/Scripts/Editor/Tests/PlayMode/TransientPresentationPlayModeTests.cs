using System.Collections;
using Combat.Projectile;
using Combat.Projectile.Audio;
using Combat.Projectile.Visual;
using Combat.Weapons;
using Game.Services;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// Session presentation policy on pooled transients: a headless session's ProjectileService darkens
    /// what it checks out, and the same pooled instance lights back up when a presenting session
    /// checks it out next (the pool is process-wide and crosses sessions).
    /// </summary>
    [Category("Presentation")]
    public class TransientPresentationPlayModeTests : PlayModeWorldFixture
    {
        private const string ConcussionWavePrefabPath = "Assets/Prefabs/Weapons/ConcussionWave.prefab";

        private ProjectileService headless;

        public override void SetUp()
        {
            base.SetUp();
            var host = new GameObject("[HeadlessHost]");
            headlessHost = host;
            headless = new ProjectileService(host.transform, presentationEnabled: false);
        }

        public override void TearDown()
        {
            DestroyTestObject(headlessHost);
            headless = null;
            base.TearDown();
        }

        private GameObject headlessHost;

        private static T LoadProjectilePrefab<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Missing prefab at {path}");
            return prefab.GetComponent<T>();
        }

        [UnityTest]
        public IEnumerator LaserCheckout_IsDarkHeadless_ThenRestoredByPresentingSession()
        {
            var prefab = LoadProjectilePrefab<Laser>("Assets/Prefabs/Weapons/Laser.prefab");
            var laser = SimplePool<Laser>.Get(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                headless.Register(laser, laser.ReturnToPoolImmediate);

                Assert.IsFalse(laser.GetComponent<LaserVisual>().enabled);
                foreach (var renderer in laser.GetComponentsInChildren<Renderer>(true))
                    Assert.IsFalse(renderer.enabled, $"renderer '{renderer.name}' still enabled headless");

                laser.ReturnToPoolImmediate();
                yield return null;

                var reused = SimplePool<Laser>.Get(prefab, Vector3.zero, Quaternion.identity);
                Assert.AreSame(laser, reused, "expected the pool to hand back the darkened instance");
                Projectiles.Register(reused, reused.ReturnToPoolImmediate);

                Assert.IsTrue(reused.GetComponent<LaserVisual>().enabled);
                foreach (var renderer in reused.GetComponentsInChildren<Renderer>(true))
                    Assert.IsTrue(renderer.enabled, $"renderer '{renderer.name}' not restored");

                reused.ReturnToPoolImmediate();
            }
            finally
            {
                SimplePool<Laser>.Clear();
            }
        }

        [UnityTest]
        public IEnumerator MissileCheckout_HeadlessStopsParticles_AndSilencesAudio()
        {
            var prefab = LoadProjectilePrefab<Missile>("Assets/Prefabs/Weapons/MissileProjectile.prefab");
            var missile = SimplePool<Missile>.Get(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                headless.Register(missile, missile.ReturnToPoolImmediate);
                yield return null;

                Assert.IsFalse(missile.GetComponent<MissileAudio>().enabled);
                foreach (var source in missile.GetComponentsInChildren<AudioSource>(true))
                    Assert.IsFalse(source.enabled, $"audio source '{source.name}' still enabled headless");
                foreach (var ps in missile.GetComponentsInChildren<ParticleSystem>(true))
                    Assert.IsFalse(ps.isPlaying, $"particle system '{ps.name}' still simulating headless");
                foreach (var renderer in missile.GetComponentsInChildren<Renderer>(true))
                    Assert.IsFalse(renderer.enabled, $"renderer '{renderer.name}' still enabled headless");

                missile.ReturnToPoolImmediate();
            }
            finally
            {
                SimplePool<Missile>.Clear();
            }
        }

        /// <summary>
        /// The seam's gate is the whole gate: a darkened part unsubscribes in OnDisable, so a live
        /// detonation reaches no handler and no burst VFX is pooled. Paired with the presenting case
        /// below — without that positive control this would also hold for a wave that never fires.
        /// </summary>
        [UnityTest]
        public IEnumerator ConcussionWaveCheckout_HeadlessSpawnsNoBurstVfx()
        {
            var prefab = LoadProjectilePrefab<ConcussionWave>(ConcussionWavePrefabPath);
            var wave = SimplePool<ConcussionWave>.Get(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                headless.Register(wave, wave.ReturnToPoolImmediate);
                Assert.IsFalse(wave.GetComponent<ConcussionWaveVisual>().enabled);

                var before = ActiveVfxCount();
                wave.Begin(new Ships.ShipId(1));
                yield return null;

                Assert.AreEqual(before, ActiveVfxCount(),
                    "a headless session must pool no burst VFX on detonation");
            }
            finally
            {
                SimplePool<ConcussionWave>.Clear();
                SimplePool<PooledVFX>.Clear();
            }
        }

        [UnityTest]
        public IEnumerator ConcussionWaveCheckout_PresentingSessionSpawnsBurstVfx()
        {
            var prefab = LoadProjectilePrefab<ConcussionWave>(ConcussionWavePrefabPath);
            var wave = SimplePool<ConcussionWave>.Get(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                Projectiles.Register(wave, wave.ReturnToPoolImmediate);
                Assert.IsTrue(wave.GetComponent<ConcussionWaveVisual>().enabled);

                var before = ActiveVfxCount();
                wave.Begin(new Ships.ShipId(1));
                yield return null;

                Assert.Greater(ActiveVfxCount(), before,
                    "test premise: a presenting session pools a burst VFX on detonation");
            }
            finally
            {
                SimplePool<ConcussionWave>.Clear();
                SimplePool<PooledVFX>.Clear();
            }
        }

        private static int ActiveVfxCount() =>
            Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator RailBeamVisual_SelfGates_WhenSessionPresentationIsOff()
        {
            var saved = GameSettings.PresentationEnabled;
            GameObject mount = null;
            try
            {
                GameSettings.SetPresentationEnabled(false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Railgun.prefab");
                Assert.IsNotNull(prefab, "Missing Railgun prefab");
                mount = Object.Instantiate(prefab);
                yield return null;

                var beam = mount.GetComponentInChildren<RailBeamVisual>(true);
                Assert.IsNotNull(beam, "Railgun prefab carries no RailBeamVisual");
                Assert.IsFalse(beam.enabled);
            }
            finally
            {
                GameSettings.SetPresentationEnabled(saved);
                DestroyTestObject(mount);
            }
        }
    }
}
