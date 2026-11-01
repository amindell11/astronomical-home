using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// What the preview control panel tunes: the host entries that preview it and the feel profiles it
    /// edits. The host never sees a target. Replay entries rank above Playtest ones.
    /// </summary>
    internal abstract class TuningTarget
    {
        internal static readonly TuningTarget[] All =
        {
            new SectorJumpTarget(), new RunRecapTarget(), new HudTarget(), new FlightCameraTarget(), new AsteroidsTarget()
        };

        public abstract string Name { get; }
        public abstract IReadOnlyList<PreviewEntry> Entries { get; }
        public abstract IEnumerable<ScriptableObject> Profiles(GameHost game);
        public virtual void DrawControls(PreviewHost host) { }

        public PreviewEntry First => Entries.OrderBy(PreviewHost.IsPlaytest).First();
        public bool Offers(PreviewEntry entry) => Entries.Contains(entry);
        public PreviewEntry EntryAfterSwitch(PreviewEntry running) => Offers(running) ? running : First;

        internal static ScriptableObject[] ProfilesOfAll(GameHost game) =>
            All.SelectMany(target => target.Profiles(game)).Distinct().ToArray();
    }
}
