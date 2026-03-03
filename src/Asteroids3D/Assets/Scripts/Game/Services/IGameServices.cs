namespace Game.Services
{
    public interface IGameServices
    {
        IUnitService UnitService { get; }
        IEnvironmentService EnvironmentService { get; }
        IObjectiveService ObjectiveService { get; }
        ICameraService CameraService { get; }
    }
}
