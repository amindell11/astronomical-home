using Game.Services;
using Ships;

namespace Game.Sectors
{
    /// <summary>Build/teardown context for spawners and modules — no static lookups. Player and world are injected at runtime from the session tier (player null for headless/RL), the dependencies that cannot be dragged serialized references.</summary>
    public readonly struct SectorBuildContext
    {
        public readonly IGameServices Services;
        public readonly Sector Sector;
        public readonly WorldHandle World;
        public readonly Ship Player;
        public readonly SectorEventBus Bus;

        public SectorBuildContext(IGameServices services, Sector sector, WorldHandle world, Ship player = null, SectorEventBus bus = null)
        {
            Services = services;
            Sector = sector;
            World = world;
            Player = player;
            Bus = bus;
        }
    }
}
