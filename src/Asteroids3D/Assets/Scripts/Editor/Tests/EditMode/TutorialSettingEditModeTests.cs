using Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("UI")]
    public class TutorialSettingEditModeTests
    {
        private static readonly string[] Keys = { "tutorial.prompts", "tutorial.retired" };
        private readonly int?[] saved = new int?[Keys.Length];

        [SetUp]
        public void SetUp()
        {
            for (var i = 0; i < Keys.Length; i++)
            {
                saved[i] = PlayerPrefs.HasKey(Keys[i]) ? PlayerPrefs.GetInt(Keys[i]) : null;
                PlayerPrefs.DeleteKey(Keys[i]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < Keys.Length; i++)
            {
                if (saved[i].HasValue) PlayerPrefs.SetInt(Keys[i], saved[i].Value);
                else PlayerPrefs.DeleteKey(Keys[i]);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void FreshSave_TeachesUntilTheFirstSectorOneClear()
        {
            Assert.IsTrue(TutorialSetting.On, "a fresh save teaches");
            TutorialSetting.NoteFirstSectorCleared();
            Assert.IsFalse(TutorialSetting.On, "the first sector 1 clear turns the prompts off");
        }

        [Test]
        public void TurnedBackOn_StaysOnThroughLaterClears()
        {
            TutorialSetting.NoteFirstSectorCleared();
            TutorialSetting.Set(true);
            TutorialSetting.NoteFirstSectorCleared();
            Assert.IsTrue(TutorialSetting.On, "only the first clear turns the prompts off");
        }

        [Test]
        public void TurnedOffBeforeTheFirstClear_StaysOff()
        {
            TutorialSetting.Set(false);
            TutorialSetting.NoteFirstSectorCleared();
            Assert.IsFalse(TutorialSetting.On);
            TutorialSetting.Set(true);
            Assert.IsTrue(TutorialSetting.On, "the player can turn the prompts back on");
        }
    }
}
