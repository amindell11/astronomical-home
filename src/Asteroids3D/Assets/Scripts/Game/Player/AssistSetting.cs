using UnityEngine;

namespace Game.Player
{
    public static class AssistSetting
    {
        // Pinned so a rename never drops a saved setting.
        private const string Key = "flight.assist";

        public static bool On => PlayerPrefs.GetInt(Key, 1) == 1;

        public static void Set(bool on) => PlayerPrefs.SetInt(Key, on ? 1 : 0);
    }
}
