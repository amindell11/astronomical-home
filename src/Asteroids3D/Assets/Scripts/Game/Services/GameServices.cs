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
            UnitService = unitService;
            EnvironmentService = environmentService;
            ObjectiveService = objectiveService;
            CameraService = cameraService;
        }

        public void ClearAll()
        {
            UnitService?.Clear();
            EnvironmentService?.Clear();
            ObjectiveService?.Clear();
            CameraService?.Clear();
        }
    }
}
