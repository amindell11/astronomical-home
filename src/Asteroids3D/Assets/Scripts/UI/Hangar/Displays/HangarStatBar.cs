using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    public sealed class HangarStatBar : MonoBehaviour
    {
        private const float EaseSeconds = .33f;
        private static readonly Color Lit = new(1f, .80f, .50f, 1f);
        internal static readonly Color Unlit = new(.26f, .31f, .37f, 1f);

        [SerializeField] private Text number;
        [SerializeField] private Image[] segments;

        private float target;
        private float shown;

        internal float Fill => shown;
        internal float Target => target;
        internal string Number => number.text;

        private void Awake()
        {
            if (!number || segments == null || segments.Length == 0)
                throw new InvalidOperationException("HangarStatBar needs its number and segments.");
            Paint();
        }

        public void Show(string text, float fill)
        {
            number.text = text;
            target = fill;
        }

        private void Update()
        {
            if (Mathf.Approximately(shown, target)) return;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / EaseSeconds);
            Paint();
        }

        private void Paint()
        {
            var lit = shown * segments.Length;
            for (var i = 0; i < segments.Length; i++)
                segments[i].color = Color.Lerp(Unlit, Lit, Mathf.Clamp01(lit - i));
        }
    }
}
