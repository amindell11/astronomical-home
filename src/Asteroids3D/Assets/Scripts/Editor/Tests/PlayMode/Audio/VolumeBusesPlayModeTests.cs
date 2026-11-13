#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using FMODUnity;
using Game.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class VolumeBusesPlayModeTests : FmodEvidenceFixture
    {
        private Pref master;
        private Pref music;
        private Pref effects;

        public override void SetUp()
        {
            base.SetUp();
            master = Read(VolumeSettings.Channel.Master);
            music = Read(VolumeSettings.Channel.Music);
            effects = Read(VolumeSettings.Channel.Effects);
        }

        public override void TearDown()
        {
            Write(VolumeSettings.Channel.Master, master);
            Write(VolumeSettings.Channel.Music, music);
            Write(VolumeSettings.Channel.Effects, effects);
            PlayerPrefs.Save();
            MixBuses.SetVolume(MixBuses.Master, 1f);
            MixBuses.SetVolume(MixBuses.Music, 1f);
            MixBuses.SetVolume(MixBuses.World, 1f);
            MixBuses.SetVolume(MixBuses.Hud, 1f);
            RuntimeManager.StudioSystem.flushCommands();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Set_DrivesEachChannelBus()
        {
            VolumeSettings.Set(VolumeSettings.Channel.Master, .25f);
            VolumeSettings.Set(VolumeSettings.Channel.Music, .5f);
            VolumeSettings.Set(VolumeSettings.Channel.Effects, .75f);
            RuntimeManager.StudioSystem.flushCommands();

            AssertVolume(MixBuses.Master, .25f);
            AssertVolume(MixBuses.Music, .5f);
            AssertVolume(MixBuses.World, .75f);
            AssertVolume(MixBuses.Hud, .75f);
            yield break;
        }

        [UnityTest]
        public IEnumerator Apply_FollowsStoredPrefs()
        {
            PlayerPrefs.SetFloat(VolumeSettings.Key(VolumeSettings.Channel.Master), .2f);
            PlayerPrefs.SetFloat(VolumeSettings.Key(VolumeSettings.Channel.Music), .4f);
            PlayerPrefs.SetFloat(VolumeSettings.Key(VolumeSettings.Channel.Effects), .6f);
            VolumeSettings.Apply();
            RuntimeManager.StudioSystem.flushCommands();

            AssertVolume(MixBuses.Master, .2f);
            AssertVolume(MixBuses.Music, .4f);
            AssertVolume(MixBuses.World, .6f);
            AssertVolume(MixBuses.Hud, .6f);
            yield break;
        }

        private static void AssertVolume(string path, float expected)
        {
            var result = RuntimeManager.GetBus(path).getVolume(out var volume);
            Assert.AreEqual(FMOD.RESULT.OK, result, path);
            Assert.AreEqual(expected, volume, 1e-3f, path);
        }

        private static Pref Read(VolumeSettings.Channel channel)
        {
            var key = VolumeSettings.Key(channel);
            return PlayerPrefs.HasKey(key)
                ? new Pref { had = true, value = PlayerPrefs.GetFloat(key) }
                : new Pref();
        }

        private static void Write(VolumeSettings.Channel channel, Pref pref)
        {
            var key = VolumeSettings.Key(channel);
            if (pref.had) PlayerPrefs.SetFloat(key, pref.value);
            else PlayerPrefs.DeleteKey(key);
        }

        private struct Pref
        {
            public bool had;
            public float value;
        }
    }
}
#endif
