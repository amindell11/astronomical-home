#if UNITY_EDITOR
using Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode
{
    public class AssistSettingPlayModeTests
    {
        private Pref stored;

        [SetUp]
        public void SetUp() => stored = Read();

        [TearDown]
        public void TearDown()
        {
            Write(stored);
            PlayerPrefs.Save();
        }

        [Test]
        public void DefaultsOn_AndSetReadsBack()
        {
            PlayerPrefs.DeleteKey("flight.assist");
            Assert.IsTrue(AssistSetting.On);

            AssistSetting.Set(false);
            Assert.IsFalse(AssistSetting.On);
            Assert.AreEqual(0, PlayerPrefs.GetInt("flight.assist"));

            AssistSetting.Set(true);
            Assert.IsTrue(AssistSetting.On);
            Assert.AreEqual(1, PlayerPrefs.GetInt("flight.assist"));
        }

        private static Pref Read()
        {
            const string key = "flight.assist";
            return PlayerPrefs.HasKey(key) ? new Pref { had = true, value = PlayerPrefs.GetInt(key) } : new Pref();
        }

        private static void Write(Pref pref)
        {
            const string key = "flight.assist";
            if (pref.had) PlayerPrefs.SetInt(key, pref.value);
            else PlayerPrefs.DeleteKey(key);
        }

        private struct Pref
        {
            public bool had;
            public int value;
        }
    }
}
#endif
