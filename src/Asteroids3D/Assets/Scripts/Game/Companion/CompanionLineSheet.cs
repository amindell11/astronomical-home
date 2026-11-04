using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Companion
{
    [CreateAssetMenu(fileName = "CompanionLines", menuName = "Game/Companion Lines")]
    public sealed class CompanionLineSheet : ScriptableObject
    {
        [Tooltip("One line slot per moment the companion speaks for.")]
        [SerializeField] private List<LineSlot> slots = new();

        public IReadOnlyList<LineSlot> Slots => slots;

        public LineSlot Find(string moment) =>
            slots.FirstOrDefault(slot => slot.Moment == moment)
            ?? throw new InvalidOperationException(
                $"Companion line sheet '{name}' has no line slot for moment '{moment}'.");
    }

    [Serializable]
    public sealed class LineSlot
    {
        [Tooltip("The id of the moment this line slot speaks for.")]
        [SerializeField] private string moment;

        [Tooltip("Speaks without waiting out the nag budget's interval between lines.")]
        [SerializeField] private bool mustSay;

        [Tooltip("The companion says one of these, picked at random. A line quotes the moment's facts as " +
                 "{name}. An empty line slot stays silent.")]
        [SerializeField, TextArea] private string[] lines = Array.Empty<string>();

        public string Moment => moment;
        public bool MustSay => mustSay;
        public IReadOnlyList<string> Lines => lines;

        /// <summary>A random non-blank line, or null when there is none.</summary>
        public string Pick(Func<int, int> range)
        {
            var spoken = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            return spoken.Length == 0 ? null : spoken[range(spoken.Length)];
        }
    }
}
