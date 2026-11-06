using System.Collections.Generic;
using AI;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class TacticsTarget : TuningTarget
    {
        public override string Name => "Tactics";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.CrowdedFight };

        // No shipped prefab reads the asset, so this tunes the one the situation overlay loads.
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) =>
            new ScriptableObject[] { SituationGizmos.Settings };
    }
}
