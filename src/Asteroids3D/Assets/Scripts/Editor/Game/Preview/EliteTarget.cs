using System;
using System.Collections.Generic;
using Substrate.Sectors.Elements.Elites;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class EliteTarget : TuningTarget
    {
        private readonly string name;
        private readonly string fixture;

        public EliteTarget(string name, PreviewEntry entry, string fixture)
        {
            this.name = name;
            this.fixture = fixture;
            Entries = new[] { entry };
        }

        public override string Name => name;
        public override IReadOnlyList<PreviewEntry> Entries { get; }
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { Profile() };

        private EliteProfile Profile()
        {
            var profile = PreviewHostBootstrap.FixtureRow(fixture).elite;
            if (!profile)
                throw new InvalidOperationException($"The {fixture} fixture row must name an elite.");
            return profile;
        }
    }
}
