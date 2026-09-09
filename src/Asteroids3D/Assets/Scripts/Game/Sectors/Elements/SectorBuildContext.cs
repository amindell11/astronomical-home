using AI.Scanning;
using Cameras;
using Game.Services;
using Game.Sessions;
using Ships;
using Game.Sectors.Activation;

namespace Game.Sectors.Elements
{
    /// <summary>
    /// Build/teardown context for spawners and modules — no static lookups. The frame, the sector's
    /// obstacle field, the player and the observer camera are injected at runtime from the host
    /// through the session (all null for headless/RL), the dependencies that cannot be dragged as
    /// serialized references. <see cref="Observer"/> is an interim seam: one module in one prefab
    /// nothing loads reads it, and it leaves with #540.
    /// </summary>
    public readonly struct SectorBuildContext
    {
        public readonly IGameServices Services;
        public readonly Sector Sector;
        public readonly SessionFrame Frame;
        /// <summary>The field AI ships spawned into this sector sense; null for a sector without rocks.</summary>
        public readonly IObstacleField Field;
        public readonly Ship Player;
        public readonly ObserverCam Observer;
        public readonly SectorEventBus Bus;

        public SectorBuildContext(IGameServices services, Sector sector, SessionFrame frame, IObstacleField field = null,
            Ship player = null, ObserverCam observer = null, SectorEventBus bus = null)
        {
            Services = services;
            Sector = sector;
            Frame = frame;
            Field = field;
            Player = player;
            Observer = observer;
            Bus = bus;
        }
    }
}
