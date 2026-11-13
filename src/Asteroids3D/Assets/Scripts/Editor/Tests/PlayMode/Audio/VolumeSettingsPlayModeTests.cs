#if UNITY_EDITOR
using Game.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode.Audio
{
    public class VolumeSettingsPlayModeTests
    {
        private Pref master;
        private Pref music;
        private Pref effects;

        [SetUp]
        public void SetUp()
        {
            master = Read(VolumeSettings.Channel.Master);
            music = Read(VolumeSettings.Channel.Music);
            effects = Read(VolumeSettings.Channel.Effects);
        }

        [TearDown]
        public void TearDown()
        {
            Write(VolumeSettings.Channel.Master, master);
            Write(VolumeSettings.Channel.Music, music);
            Write(VolumeSettings.Channel.Effects, effects);
            PlayerPrefs.Save();
            VolumeSettings.Apply();
        }

        [Test]
        public void Set_PersistsAndClamps()
        {
            VolumeSettings.Set(VolumeSettings.Channel.Music, .3f);
            VolumeSettings.Save();

            Assert.AreEqual(.3f, PlayerPrefs.GetFloat(VolumeSettings.Key(VolumeSettings.Channel.Music)));
            Assert.AreEqual(.3f, VolumeSettings.Get(VolumeSettings.Channel.Music));

            PlayerPrefs.DeleteKey(VolumeSettings.Key(VolumeSettings.Channel.Effects));
            Assert.AreEqual(1f, VolumeSettings.Get(VolumeSettings.Channel.Effects), "an unset key reads full volume");

            VolumeSettings.Set(VolumeSettings.Channel.Master, 1.5f);
            Assert.AreEqual(1f, VolumeSettings.Get(VolumeSettings.Channel.Master));
            VolumeSettings.Set(VolumeSettings.Channel.Master, -.2f);
            Assert.AreEqual(0f, VolumeSettings.Get(VolumeSettings.Channel.Master));
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
