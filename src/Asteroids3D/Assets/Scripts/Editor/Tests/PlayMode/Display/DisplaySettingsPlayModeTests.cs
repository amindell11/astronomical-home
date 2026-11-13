#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Display;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode.Display
{
    public class DisplaySettingsPlayModeTests
    {
        private Pref width;
        private Pref height;
        private Pref fullscreen;

        [SetUp]
        public void SetUp()
        {
            width = Read("display.width");
            height = Read("display.height");
            fullscreen = Read("display.fullscreen");
        }

        [TearDown]
        public void TearDown()
        {
            Write("display.width", width);
            Write("display.height", height);
            Write("display.fullscreen", fullscreen);
            PlayerPrefs.Save();
        }

        [Test]
        public void Resolutions_AreNonEmptyDistinctAndAscending()
        {
            var resolutions = DisplaySettings.Resolutions();
            Assert.Greater(resolutions.Count, 0);
            for (var i = 1; i < resolutions.Count; i++)
            {
                var previous = resolutions[i - 1];
                var current = resolutions[i];
                var ordered = previous.x < current.x || (previous.x == current.x && previous.y < current.y);
                Assert.IsTrue(ordered, $"{previous.x} x {previous.y} then {current.x} x {current.y}");
            }
        }

        [Test]
        public void Set_StoresTheKeys_AndReadsThemBack()
        {
            DisplaySettings.Set(new Vector2Int(1280, 720), false);
            Assert.AreEqual(1280, PlayerPrefs.GetInt("display.width"));
            Assert.AreEqual(720, PlayerPrefs.GetInt("display.height"));
            Assert.AreEqual(0, PlayerPrefs.GetInt("display.fullscreen"));
            Assert.IsFalse(DisplaySettings.Fullscreen);
            Assert.AreEqual(new Vector2Int(1280, 720), DisplaySettings.Resolution);

            DisplaySettings.Set(new Vector2Int(1920, 1080), true);
            Assert.AreEqual(1920, PlayerPrefs.GetInt("display.width"));
            Assert.AreEqual(1080, PlayerPrefs.GetInt("display.height"));
            Assert.AreEqual(1, PlayerPrefs.GetInt("display.fullscreen"));
            Assert.IsTrue(DisplaySettings.Fullscreen);
            Assert.AreEqual(new Vector2Int(1920, 1080), DisplaySettings.Resolution);
        }

        [Test]
        public void Unset_ReadsTheLiveDisplay()
        {
            PlayerPrefs.DeleteKey("display.width");
            PlayerPrefs.DeleteKey("display.height");
            PlayerPrefs.DeleteKey("display.fullscreen");

            var live = new Vector2Int(Screen.width, Screen.height);
            Assert.AreEqual(live, DisplaySettings.Resolution);
            Assert.AreEqual(Screen.fullScreenMode != FullScreenMode.Windowed, DisplaySettings.Fullscreen);
        }

        [Test]
        public void Apply_WithoutKeys_LeavesThemAbsent()
        {
            PlayerPrefs.DeleteKey("display.width");
            PlayerPrefs.DeleteKey("display.height");
            PlayerPrefs.DeleteKey("display.fullscreen");

            DisplaySettings.Apply();

            Assert.IsFalse(PlayerPrefs.HasKey("display.width"));
            Assert.IsFalse(PlayerPrefs.HasKey("display.height"));
            Assert.IsFalse(PlayerPrefs.HasKey("display.fullscreen"));
        }

        [Test]
        public void Label_JoinsWidthAndHeight()
        {
            Assert.AreEqual("2560 x 1440", DisplaySettings.Label(new Vector2Int(2560, 1440)));
        }

        [Test]
        public void IndexOf_ReturnsTheExactSize_OrTheNearestPixelCount()
        {
            IReadOnlyList<Vector2Int> resolutions = new[]
            {
                new Vector2Int(800, 600),
                new Vector2Int(1920, 1080),
                new Vector2Int(1280, 720)
            };
            Assert.AreEqual(2, DisplaySettings.IndexOf(resolutions, new Vector2Int(1280, 720)));
            Assert.AreEqual(1, DisplaySettings.IndexOf(resolutions, new Vector2Int(2000, 1000)),
                "2000 x 1000 is nearest 1920 x 1080 by pixel count");
        }

        private static Pref Read(string key) =>
            PlayerPrefs.HasKey(key) ? new Pref { had = true, value = PlayerPrefs.GetInt(key) } : new Pref();

        private static void Write(string key, Pref pref)
        {
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
