using System;

namespace Game.Services
{
    public class GameServices : IGameServices
    {
        public IUnitService UnitService { get; }
        public IEnvironmentService EnvironmentService { get; }
        public IObjectiveService ObjectiveService { get; }
        public ICameraService CameraService { get; }
        public IUIService UIService { get; }
        public ArenaContext Arena { get; }

        public GameServices(
            IUnitService unitService,
            IEnvironmentService environmentService,
            IObjectiveService objectiveService,
            ICameraService cameraService,
            IUIService uiService,
            ArenaContext arena)
        {
            UnitService = unitService ?? throw new ArgumentNullException(nameof(unitService));
            EnvironmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            ObjectiveService = objectiveService ?? throw new ArgumentNullException(nameof(objectiveService));
            CameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            UIService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            Arena = arena ?? throw new ArgumentNullException(nameof(arena));
        }

        /// <summary>Clear all service registries between sectors. Arena holds refs, not owned state, so it is untouched.</summary>
        public void ClearAll()
        {
            UnitService.Clear();
            EnvironmentService.Clear();
            ObjectiveService.Clear();
            CameraService.Clear();
            UIService.Clear();
        }
    }
}
