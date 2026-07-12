using Game.Services;
using Ships;

namespace Game.Sectors
{
    /// <summary>
    /// Context handed to each spawner/module during build/teardown. Lets it reach the game services
    /// (to spawn ships, query the world), its owning sector, and the runtime player ship without any
    /// static lookup. <see cref="Player"/> is injected via <see cref="Sector.Initialize"/> from the
    /// session-tier <c>SessionRig</c> (null for headless/RL runs); it is the only dependency that cannot
    /// be a dragged serialized reference because it is built at runtime a tier up.
    /// </summary>
    public readonly struct SectorBuildContext
    {
        public readonly IGameServices Services;
        public readonly Sector Sector;
        public readonly Ship Player;

        public SectorBuildContext(IGameServices services, Sector sector, Ship player = null)
        {
            Services = services;
            Sector = sector;
            Player = player;
        }
    }
}
