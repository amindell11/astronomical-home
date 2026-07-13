using Game.Services;
using Ships;

namespace Game.Sectors
{
    /// <summary>Build/teardown context for spawners and modules — no static lookups. Player is injected at runtime from the session tier (null for headless/RL), the one dependency that cannot be a dragged serialized reference.</summary>
    public readonly struct SectorBuildContext
    {
        public readonly IGameServices Services;
        public readonly Sector Sector;
        public readonly Ship Player;
        public readonly SectorEventBus Bus;

        public SectorBuildContext(IGameServices services, Sector sector, Ship player = null, SectorEventBus bus = null)
        {
            Services = services;
            Sector = sector;
            Player = player;
            Bus = bus;
        }
    }
}
