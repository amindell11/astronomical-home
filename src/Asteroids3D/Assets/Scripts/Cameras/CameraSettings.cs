using UnityEngine;

namespace Cameras
{
    /// <summary>
    /// The player's camera preference, persisted in PlayerPrefs.
    /// Manual is wheel-controlled zoom and no enemy framing; off (the default) is the auto flight camera.
    /// </summary>
    public static class CameraSettings
    {
        private const string ManualCameraKey = "camera.manual";
        private static bool? manualCamera;

        public static bool ManualCamera
        {
            get => manualCamera ??= PlayerPrefs.GetInt(ManualCameraKey, 0) == 1;
            set
            {
                manualCamera = value;
                PlayerPrefs.SetInt(ManualCameraKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
