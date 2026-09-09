using Game.Services.Units;
using Game.Services.Projectiles;
using Game.Services.Objectives;
namespace Game.Services
{
    public interface IGameServices
    {
        IUnitService UnitService { get; }
        IProjectileService Projectiles { get; }
        IObjectiveService ObjectiveService { get; }
        /// <summary>This session's presentation policy — spawn seams apply it to what they instantiate.</summary>
        bool PresentationEnabled { get; }
    }
}
