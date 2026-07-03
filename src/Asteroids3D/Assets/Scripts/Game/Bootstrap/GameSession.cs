using System;
using Game.Sectors;
using Game.Services;
using Player;
using Presentation;

namespace Game.Bootstrap
{
    /// <summary>
    /// Per-session state owned by the bootstrap lifecycle primitives on
    /// <see cref="MainGameManager"/>: the service container, the optional player/camera/UI rig,
    /// the presentation overlay, and the currently loaded sector. The primitives take this
    /// container explicitly instead of reading process-wide singletons, so a future multi-arena
    /// (RL) harness can own several sessions in one process without a signature-breaking retrofit.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Service registries owned by this session; cleared on session teardown.</summary>
        public GameServices Services { get; internal set; }

        /// <summary>Session-tier player/camera/UI/world rig; null for headless sessions.</summary>
        public PlayerRig Rig { get; internal set; }

        /// <summary>Game-tier visual overlay installer; null when presentation is disabled.</summary>
        public PresentationInstaller Presentation { get; internal set; }

        /// <summary>The currently loaded sector, if any.</summary>
        public Sector ActiveSector { get; internal set; }

        /// <summary>Completion handler subscribed at load time, so unload can detach the same delegate.</summary>
        internal Action<SectorResult> SectorCompleteHandler { get; set; }
    }
}
