using System;
using Game.Services.Units;
using Game.Services.Projectiles;
using Game.Services.Objectives;

namespace Game.Services
{
    public class GameServices : IGameServices
    {
        public IUnitService UnitService { get; }
        public IProjectileService Projectiles { get; }
        public IObjectiveService ObjectiveService { get; }
        public bool PresentationEnabled { get; }

        public GameServices(
            IUnitService unitService,
            IProjectileService projectiles,
            IObjectiveService objectiveService,
            bool presentationEnabled = true)
        {
            UnitService = unitService ?? throw new ArgumentNullException(nameof(unitService));
            Projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
            ObjectiveService = objectiveService ?? throw new ArgumentNullException(nameof(objectiveService));
            PresentationEnabled = presentationEnabled;
        }

        public void ClearAll()
        {
            Projectiles.ReturnAllToPool();
            UnitService.Clear();
            ObjectiveService.ClearAll();
        }
    }
}
