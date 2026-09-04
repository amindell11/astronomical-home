using System;
using Damage;
using Game.Sectors;
using Game.Services;
using Player;
using Ships;
using Ships.Registry;

namespace Game.Session
{
    /// <summary>
    /// Per-session state owned by the session lifecycle primitives on
    /// <see cref="SessionHost"/>: the composition profile, the service container, the optional
    /// player/camera/UI rig, and the currently loaded sector. The primitives take this container
    /// explicitly rather than reading process-wide singletons, so one process can hold several
    /// sessions at once.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Driver-supplied composition inputs (what to build/load); set before ComposeSession, consumed by the primitives.</summary>
        public SessionProfile Profile { get; internal set; }

        /// <summary>Service registries owned by this session; cleared on session teardown.</summary>
        public GameServices Services { get; internal set; }

        /// <summary>The world ships currently spawn into: the loaded sector's handle, or the no-rocks handle between sectors; null once torn down.</summary>
        public WorldHandle World { get; internal set; }

        /// <summary>Session-tier player/camera/UI/world rig; null for headless sessions.</summary>
        public SessionRig Rig { get; internal set; }

        /// <summary>The currently loaded sector, if any.</summary>
        public Sector ActiveSector { get; internal set; }

        /// <summary>
        /// "Episode/sector ended" policy hook, set once by the driver before the first load
        /// (gameplay wires restart; an RL driver wires its terminal condition, or leaves it null and
        /// polls). LoadSector subscribes it to the sector and UnloadSector detaches it, so do not
        /// reassign while a sector is loaded.
        /// </summary>
        public Action<SectorResult> OnSectorComplete { get; set; }

        /// <summary>
        /// "Player died" policy hook, set once by the driver before compose (gameplay wires
        /// respawn/restart; an RL driver wires its terminal condition, or leaves it null). The host
        /// injects it onto the player at Build — the rig wires it onto <c>Ship.Damage.OnDeath</c>
        /// synchronously at spawn and re-wires it across a player rebuild; mechanism-only below.
        /// </summary>
        public Action<ShipId, DamageInfo> OnPlayerDeath { get; set; }
    }
}
