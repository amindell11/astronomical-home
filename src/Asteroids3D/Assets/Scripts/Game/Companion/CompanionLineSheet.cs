using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Companion
{
    [CreateAssetMenu(fileName = "CompanionLines", menuName = "Game/Companion Lines")]
    public sealed class CompanionLineSheet : ScriptableObject
    {
        [Tooltip("The line file: the canonical CSV of every line.")]
        [SerializeField] private TextAsset lineFile;

        [Tooltip("One line slot per moment the companion speaks for.")]
        [SerializeField] private List<LineSlot> slots = new();

        public IReadOnlyList<LineSlot> Slots => slots;

        public LineSlot Find(string moment) =>
            slots.FirstOrDefault(slot => slot.Moment == moment)
            ?? throw new InvalidOperationException(
                $"Companion line sheet '{name}' has no line slot for moment '{moment}'.");

        public LineTable LoadLines()
        {
            if (!lineFile) throw new InvalidOperationException($"Companion line sheet '{name}' has no line file.");
            return LineTable.Parse(lineFile.text, slots);
        }
    }

    [Serializable]
    public sealed class LineSlot
    {
        [Tooltip("The id of the moment this line slot speaks for.")]
        [SerializeField] private string moment;

        [Tooltip("Who says this slot's lines; blank is the companion.")]
        [SerializeField] private string speaker;

        [Tooltip("Speaks without waiting out the nag budget's interval between lines.")]
        [SerializeField] private bool mustSay;

        public string Moment => moment;
        public string Speaker => speaker;
        public string SpokenBy => string.IsNullOrWhiteSpace(speaker) ? LineTable.CompanionSpeaker : speaker;
        public bool MustSay => mustSay;
    }
}
