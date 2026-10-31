using System.Collections.Generic;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class SectorJumpTarget : TuningTarget
    {
        public override string Name => "Sector jump";
        public override IReadOnlyList<PreviewEntry> Entries { get; } =
            new[] { PreviewEntry.SectorJump, PreviewEntry.CrowdedJumpGate };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { game.jumpFeel };
    }
}
