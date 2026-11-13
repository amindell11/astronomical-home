using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Display
{
    public static class DisplaySettings
    {
        // Pinned so a rename never drops a saved setting.
        private const string WidthKey = "display.width";
        private const string HeightKey = "display.height";
        private const string FullscreenKey = "display.fullscreen";

        public static IReadOnlyList<Vector2Int> Resolutions()
        {
            var found = new List<Vector2Int>();
            foreach (var resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!found.Contains(size)) found.Add(size);
            }

            found.Sort((a, b) =>
            {
                var byWidth = a.x.CompareTo(b.x);
                return byWidth != 0 ? byWidth : a.y.CompareTo(b.y);
            });

            // Batch mode can report none; the live size is still a choice.
            if (found.Count == 0) found.Add(new Vector2Int(Screen.width, Screen.height));
            return found;
        }

        public static bool Fullscreen =>
            PlayerPrefs.HasKey(FullscreenKey)
                ? PlayerPrefs.GetInt(FullscreenKey) != 0
                : Screen.fullScreenMode != FullScreenMode.Windowed;

        public static Vector2Int Resolution =>
            PlayerPrefs.HasKey(WidthKey) && PlayerPrefs.HasKey(HeightKey)
                ? new Vector2Int(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey))
                : new Vector2Int(Screen.width, Screen.height);

        public static void Set(Vector2Int resolution, bool fullscreen)
        {
            PlayerPrefs.SetInt(WidthKey, resolution.x);
            PlayerPrefs.SetInt(HeightKey, resolution.y);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            ApplyNow(resolution, fullscreen);
        }

        public static void Apply()
        {
            // Absent keys keep the display the machine already chose.
            if (!PlayerPrefs.HasKey(WidthKey) || !PlayerPrefs.HasKey(HeightKey) || !PlayerPrefs.HasKey(FullscreenKey))
                return;
            ApplyNow(Resolution, Fullscreen);
        }

        public static string Label(Vector2Int r) => $"{r.x} x {r.y}";

        public static int IndexOf(IReadOnlyList<Vector2Int> resolutions, Vector2Int resolution)
        {
            var nearest = 0;
            var nearestDelta = long.MaxValue;
            var target = (long)resolution.x * resolution.y;
            for (var i = 0; i < resolutions.Count; i++)
            {
                var size = resolutions[i];
                if (size == resolution) return i;
                var delta = Math.Abs((long)size.x * size.y - target);
                if (delta >= nearestDelta) continue;
                nearestDelta = delta;
                nearest = i;
            }

            return nearest;
        }

        private static void ApplyNow(Vector2Int resolution, bool fullscreen) =>
            Screen.SetResolution(resolution.x, resolution.y,
                fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }
}
