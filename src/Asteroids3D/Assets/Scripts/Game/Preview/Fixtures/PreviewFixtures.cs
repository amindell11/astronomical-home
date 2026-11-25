using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Preview.Fixtures
{
    /// <summary>Holds no run: each fixture's link indexes the game run.</summary>
    [CreateAssetMenu(fileName = "PreviewFixtures", menuName = "Game/Preview Fixtures")]
    public sealed class PreviewFixtures : ScriptableObject
    {
        [SerializeField] private PreviewFixture[] fixtures = Array.Empty<PreviewFixture>();

        public IReadOnlyList<PreviewFixture> Entries => fixtures;

        public PreviewFixture Fixture(string layout) =>
            fixtures.SingleOrDefault(fixture => fixture.layout && fixture.layout.name == layout)
            ?? throw new InvalidOperationException($"'{name}' has no fixture for the {layout} layout.");
    }
}
