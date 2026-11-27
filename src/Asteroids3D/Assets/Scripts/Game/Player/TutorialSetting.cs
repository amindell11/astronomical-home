using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Whether the game teaches as it goes: the guided first sector, and any other tutorial prompt. On for a fresh
    /// save; it turns itself off the first time the player clears sector 1, once, and the player can turn it back on.
    /// </summary>
    public static class TutorialSetting
    {
        // Pinned so a rename never drops a saved setting.
        private const string Key = "tutorial.prompts";
        private const string RetiredKey = "tutorial.retired";

        public static bool On => PlayerPrefs.GetInt(Key, 1) == 1;

        public static void Set(bool on) => PlayerPrefs.SetInt(Key, on ? 1 : 0);

        public static void NoteFirstSectorCleared()
        {
            if (PlayerPrefs.GetInt(RetiredKey, 0) == 1) return;
            PlayerPrefs.SetInt(RetiredKey, 1);
            Set(false);
            PlayerPrefs.Save();
        }
    }
}
