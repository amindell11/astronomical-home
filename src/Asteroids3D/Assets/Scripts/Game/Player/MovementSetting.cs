using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Which frame the movement keys push in. Off (the default) is along the ship: W thrusts down the nose.
    /// Screen-relative makes W push up the screen and A/D left and right, wherever the nose points.
    /// </summary>
    public static class MovementSetting
    {
        // Pinned so a rename never drops a saved setting.
        private const string Key = "flight.screenRelative";
        private static bool? screenRelative;

        public static bool ScreenRelative
        {
            get => screenRelative ??= PlayerPrefs.GetInt(Key, 0) == 1;
            set
            {
                screenRelative = value;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
            }
        }
    }
}
