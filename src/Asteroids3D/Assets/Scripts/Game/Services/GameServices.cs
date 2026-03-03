using System;

namespace Game.Services
{
    public class GameServices : IGameServices
    {
        public IUnitService UnitService { get; }
        public IEnvironmentService EnvironmentService { get; }
        public IObjectiveService ObjectiveService { get; }
        public ICameraService CameraService { get; }

        public GameServices(
            IUnitService unitService,
            IEnvironmentService environmentService,
            IObjectiveService objectiveService,
            ICameraService cameraService)
        {
            UnitService = unitService ?? throw new ArgumentNullException(nameof(unitService));
            EnvironmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            ObjectiveService = objectiveService ?? throw new ArgumentNullException(nameof(objectiveService));
            CameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
        }

        /// <summary>Clear all service registries between sectors.</summary>
        public void ClearAll()
        {
            UnitService.Clear();
            EnvironmentService.Clear();
            ObjectiveService.Clear();
            CameraService.Clear();
        }
    }
}
