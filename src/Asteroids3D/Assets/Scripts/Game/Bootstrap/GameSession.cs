using System;
using Game.Sectors;
using Game.Services;
using Player;

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

        /// <summary>The currently loaded sector, if any.</summary>
        public Sector ActiveSector { get; internal set; }

        /// <summary>
        /// "Episode/sector ended" policy hook, set once by the driver before the first load
        /// (gameplay wires restart; an RL driver wires its terminal condition, or leaves it null and
        /// polls). LoadSector subscribes it to the sector and UnloadSector detaches it, so do not
        /// reassign while a sector is loaded.
        /// </summary>
        public Action<SectorResult> OnSectorComplete { get; set; }
    }
}
