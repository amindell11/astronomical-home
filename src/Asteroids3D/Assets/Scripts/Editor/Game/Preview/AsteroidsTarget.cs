using System.Collections.Generic;
using System.Linq;
using Asteroids.Fields;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class AsteroidsTarget : TuningTarget
    {
        public override string Name => "Asteroids";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => game.sessionProfile.sectors
            .Select(sector => sector.prefab.GetComponentInChildren<UpdatingAsteroidField>(true).settings)
            .Distinct();
    }
}
