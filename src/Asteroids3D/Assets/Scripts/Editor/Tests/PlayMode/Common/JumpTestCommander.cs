using Ships;
using Ships.Command;
using Utils;

namespace Tests.PlayMode.Common
{
    /// <summary>A non-player hero's commander: raises its ship's jump request on cue.</summary>
    public sealed class JumpTestCommander : Commander
    {
        private IJumpDrive jump;

        /// <summary>Factory test ships never run Ship.Initialize, so the jump drive is handed over here.</summary>
        public static JumpTestCommander Fly(Ship ship)
        {
            var commander = ship.gameObject.AddComponent<JumpTestCommander>();
            ship.AdoptCommander(commander);
            commander.Initialize(new ShipControl(ship, ship.Movement, new SeedScope(0), jump: ship));
            return commander;
        }

        public override void Initialize(in ShipControl control) => jump = control.Jump;

        public void RequestJump() => jump.RequestJump();
    }
}
