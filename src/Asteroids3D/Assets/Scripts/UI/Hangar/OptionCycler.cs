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

        public event Action<int> Stepped;

        public string Label
        {
            set => label.text = value;
        }

        private void Awake()
        {
            previousButton.onClick.AddListener(() => Stepped?.Invoke(-1));
            nextButton.onClick.AddListener(() => Stepped?.Invoke(1));
        }
    }
}
