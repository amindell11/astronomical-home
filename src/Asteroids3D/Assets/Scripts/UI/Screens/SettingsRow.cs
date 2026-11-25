using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Screens
{
    public sealed class SettingsRow
    {
        private enum Kind { Slider, Toggle, Choice }

        private readonly Kind kind;
        private readonly string label;
        private readonly Action<float> onSlider;
        private readonly Action<bool> onToggle;
        private readonly Action<int> onChoice;
        private readonly IReadOnlyList<string> options;
        private float sliderValue;
        private bool toggleOn;
        private int choiceIndex;

        private SettingsRow(string label, float value, Action<float> changed)
        {
            kind = Kind.Slider;
            this.label = label;
            sliderValue = value;
            onSlider = changed;
        }

        private SettingsRow(string label, bool value, Action<bool> changed)
        {
            kind = Kind.Toggle;
            this.label = label;
            toggleOn = value;
            onToggle = changed;
        }

        private SettingsRow(string label, IReadOnlyList<string> options, int index, Action<int> changed)
        {
            kind = Kind.Choice;
            this.label = label;
            this.options = options;
            choiceIndex = index;
            onChoice = changed;
        }

        public static SettingsRow Slider(string label, float value, Action<float> changed)
        {
            if (changed == null) throw new ArgumentException("A slider needs a callback.", nameof(changed));
            return new SettingsRow(label, value, changed);
        }

        public static SettingsRow Toggle(string label, bool value, Action<bool> changed)
        {
            if (changed == null) throw new ArgumentException("A toggle needs a callback.", nameof(changed));
            return new SettingsRow(label, value, changed);
        }

        public static SettingsRow Choice(string label, IReadOnlyList<string> options, int index, Action<int> changed)
        {
            if (changed == null) throw new ArgumentException("A choice needs a callback.", nameof(changed));
            if (options == null || options.Count == 0)
                throw new ArgumentException("A choice needs at least one option.", nameof(options));
            if (index < 0 || index >= options.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new SettingsRow(label, options, index, changed);
        }

        internal string Label => label;

        internal bool IsSlider => kind == Kind.Slider;

        internal float SliderValue => sliderValue;

        internal string ValueText => kind switch
        {
            Kind.Slider => Mathf.RoundToInt(sliderValue * 100f) + "%",
            Kind.Toggle => toggleOn ? "ON" : "OFF",
            _ => options[choiceIndex]
        };

        internal void OnSlider(float value)
        {
            sliderValue = value;
            onSlider(value);
        }

        internal void Click()
        {
            switch (kind)
            {
                case Kind.Toggle:
                    Flip();
                    break;
                case Kind.Choice:
                    var next = (choiceIndex + 1) % options.Count;
                    if (next == choiceIndex) return;
                    choiceIndex = next;
                    onChoice(choiceIndex);
                    break;
            }
        }

        internal void Step(int direction)
        {
            if (kind == Kind.Toggle)
            {
                if (direction != 0) Flip();
                return;
            }

            if (kind != Kind.Choice) return;
            var next = choiceIndex + direction;
            if (next < 0 || next >= options.Count) return;
            choiceIndex = next;
            onChoice(choiceIndex);
        }

        private void Flip()
        {
            toggleOn = !toggleOn;
            onToggle(toggleOn);
        }
    }
}
