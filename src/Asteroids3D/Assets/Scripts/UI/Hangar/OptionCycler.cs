using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar
{
    /// <summary>
    /// One `‹ current ›` hangar row: shows a label and reports previous/next presses as a -1/+1 step.
    /// Holds no option state — <see cref="HangarScreen"/> owns the list and the pick.
    /// </summary>
    public sealed class OptionCycler : MonoBehaviour
    {
        [SerializeField] internal Button previousButton;
        [SerializeField] internal Button nextButton;
        [SerializeField] internal Text label;

        private const float DimmedAlpha = 0.35f;

        private Color labelColor;

        public event Action<int> Stepped;

        public string Label
        {
            set => label.text = value;
        }

        public bool Dimmed
        {
            set => label.color = value ? labelColor * new Color(1f, 1f, 1f, DimmedAlpha) : labelColor;
        }

        private void Awake()
        {
            labelColor = label.color;
            previousButton.onClick.AddListener(() => Stepped?.Invoke(-1));
            nextButton.onClick.AddListener(() => Stepped?.Invoke(1));
        }
    }
}
