using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class AsteroidsTarget : TuningTarget
    {
        public override string Name => "Asteroids";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => game.sessionProfile.run.Sectors
            .Select(row => row.field)
            .Distinct();
    }
}
