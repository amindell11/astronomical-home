using AI.Scanning;
using Game.Services.Units;
using Game.Services.Objectives;
using Game.Sessions;
using Ships;
using Game.Sectors.Activation;

namespace Game.Sectors.Elements
{
    /// <summary>
    /// Build/teardown context for spawners and modules — no static lookups. The frame, the sector's
    /// obstacle field and the player are injected at runtime from the host through the session
    /// (all null for headless/RL), the dependencies that cannot be dragged as serialized references.
    /// </summary>
    public readonly struct SectorBuildContext
    {
        public readonly IUnitService Units;
        public readonly IObjectiveService Objectives;
        /// <summary>This session's presentation policy — spawn seams apply it to what they instantiate.</summary>
        public readonly bool PresentationEnabled;
        public readonly Sector Sector;
        public readonly SessionFrame Frame;
        /// <summary>The field AI ships spawned into this sector sense; null for a sector without rocks.</summary>
        public readonly IObstacleField Field;
        public readonly Ship Player;
        public readonly SectorEventBus Bus;

        public SectorBuildContext(IUnitService units, IObjectiveService objectives, bool presentationEnabled,
            Sector sector, SessionFrame frame, IObstacleField field = null,
            Ship player = null, SectorEventBus bus = null)
        {
            Units = units;
            Objectives = objectives;
            PresentationEnabled = presentationEnabled;
            Sector = sector;
            Frame = frame;
            Field = field;
            Player = player;
            Bus = bus;
        }
    }
}
