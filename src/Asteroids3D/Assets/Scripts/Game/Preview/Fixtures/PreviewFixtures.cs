using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Preview.Fixtures
{
    /// <summary>
    /// The preview fixture entries, looked up by layout name. A link indexes the game run;
    /// this asset holds no run.
    /// </summary>
    [CreateAssetMenu(fileName = "PreviewFixtures", menuName = "Game/Preview Fixtures")]
    public sealed class PreviewFixtures : ScriptableObject
    {
        [SerializeField] private PreviewFixture[] fixtures = Array.Empty<PreviewFixture>();

        public IReadOnlyList<PreviewFixture> Entries => fixtures;

        public PreviewFixture Fixture(string layout) =>
            fixtures.SingleOrDefault(fixture => fixture.layout && fixture.layout.name == layout)
            ?? throw new InvalidOperationException($"'{name}' has no row flying the {layout} layout.");
    }
}
