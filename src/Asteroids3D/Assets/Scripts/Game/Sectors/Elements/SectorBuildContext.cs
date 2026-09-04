using AI.Scanning;
using Game.Services;
using Game.Sessions;
using Ships;

namespace Game.Sectors
{
    /// <summary>Build/teardown context for spawners and modules — no static lookups. The frame, the sector's obstacle field and the player are injected at runtime from the session (player null for headless/RL), the dependencies that cannot be dragged serialized references.</summary>
    public readonly struct SectorBuildContext
    {
        public readonly IGameServices Services;
        public readonly Sector Sector;
        public readonly SessionFrame Frame;
        /// <summary>The field AI ships spawned into this sector sense; null for a sector without rocks.</summary>
        public readonly IObstacleField Field;
        public readonly Ship Player;
        public readonly SectorEventBus Bus;

        public SectorBuildContext(IGameServices services, Sector sector, SessionFrame frame, IObstacleField field = null,
            Ship player = null, SectorEventBus bus = null)
        {
            Services = services;
            Sector = sector;
            Frame = frame;
            Field = field;
            Player = player;
            Bus = bus;
        }
    }
}
