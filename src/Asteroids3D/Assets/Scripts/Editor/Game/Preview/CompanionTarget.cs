using System.Collections.Generic;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class CompanionTarget : TuningTarget
    {
        public override string Name => "Companion";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) =>
            new ScriptableObject[] { game.companionFeel, game.companionLines };
    }
}
