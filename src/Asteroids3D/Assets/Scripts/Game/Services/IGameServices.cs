namespace Game.Services
{
    public interface IGameServices
    {
        IUnitService UnitService { get; }
        IProjectileService Projectiles { get; }
        IEnvironmentService EnvironmentService { get; }
        IObjectiveService ObjectiveService { get; }
        ICameraService CameraService { get; }
        IUIService UIService { get; }
        /// <summary>This session's presentation policy — spawn seams apply it to what they instantiate.</summary>
        bool PresentationEnabled { get; }
    }
}
