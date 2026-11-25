using System;
using UnityEngine;

namespace UI.Screens.Windows
{
    /// <summary>
    /// The typefaces the pick and recap windows are set in. Those screens are built in code with no
    /// prefab to carry a font, so they load this from Resources.
    /// </summary>
    public sealed class ScreenFonts : ScriptableObject
    {
        [Tooltip("Names, headlines and stat values.")]
        [SerializeField] private Font heading;

        [Tooltip("Labels, window titles and table numbers.")]
        [SerializeField] private Font label;

        [Tooltip("The centred screen title.")]
        [SerializeField] private Font title;

        public Font Heading => heading;
        public Font Label => label;
        public Font Title => title;

        public static ScreenFonts Load()
        {
            var fonts = Resources.Load<ScreenFonts>("Screens/Fonts");
            if (!fonts || !fonts.heading || !fonts.label || !fonts.title)
                throw new InvalidOperationException("The windowed screens require Screens/Fonts with all three fonts set.");
            return fonts;
        }
    }
}
