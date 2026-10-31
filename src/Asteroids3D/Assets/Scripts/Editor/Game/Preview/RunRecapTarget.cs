using System.Collections.Generic;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class RunRecapTarget : TuningTarget
    {
        public override string Name => "Run recap";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.RunRecap, PreviewEntry.Combat };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { game.recapFeel };
    }
}
