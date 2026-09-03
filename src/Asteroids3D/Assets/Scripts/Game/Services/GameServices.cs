using System;

namespace Game.Services
{
    public class GameServices : IGameServices
    {
        public IUnitService UnitService { get; }
        public IProjectileService Projectiles { get; }
        public IEnvironmentService EnvironmentService { get; }
        public IObjectiveService ObjectiveService { get; }
        public ICameraService CameraService { get; }
        public IUIService UIService { get; }
        public bool PresentationEnabled { get; }

        public GameServices(
            IUnitService unitService,
            IProjectileService projectiles,
            IEnvironmentService environmentService,
            IObjectiveService objectiveService,
            ICameraService cameraService,
            IUIService uiService,
            bool presentationEnabled = true)
        {
            UnitService = unitService ?? throw new ArgumentNullException(nameof(unitService));
            Projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
            EnvironmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            ObjectiveService = objectiveService ?? throw new ArgumentNullException(nameof(objectiveService));
            CameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            UIService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            PresentationEnabled = presentationEnabled;
        }

        /// <summary>Clear all service registries between sectors.</summary>
        public void ClearAll()
        {
            Projectiles.ReturnAllToPool();
            UnitService.Clear();
            EnvironmentService.Clear();
            ObjectiveService.ClearAll();
            CameraService.Clear();
            UIService.Clear();
        }
    }
}
