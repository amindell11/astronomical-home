using UnityEngine;

namespace Cameras
{
    /// <summary>
    /// The player's camera mode and screen-shake scale, persisted in PlayerPrefs; the canopy's shudder and glitch read the scale too.
    /// Manual is wheel-controlled zoom and no enemy framing; off (the default) is the auto flight camera.
    /// Heading-up turns the flight view so the ship points up; off (the default) keeps north up.
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

        private const string HeadingUpKey = "camera.headingUp";
        private static bool? headingUp;

        public static bool HeadingUp
        {
            get => headingUp ??= PlayerPrefs.GetInt(HeadingUpKey, 0) == 1;
            set
            {
                headingUp = value;
                PlayerPrefs.SetInt(HeadingUpKey, value ? 1 : 0);
            }
        }

        private const string ShakeScaleKey = "camera.shake";
        private static float? shakeScale;

        public static float ShakeScale
        {
            get => shakeScale ??= Mathf.Clamp01(PlayerPrefs.GetFloat(ShakeScaleKey, 1f));
            set
            {
                shakeScale = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(ShakeScaleKey, shakeScale.Value);
            }
        }
    }
}
