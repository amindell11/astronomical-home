using System;
using UnityEngine.InputSystem;

namespace Objectives.States
{
    /// <summary>
    /// Commits extraction on fresh input while eligible, before another simulation step can damage the player.
    /// The sector module owns disposal because closing an objective does not exit its current state.
    /// </summary>
    public sealed class ExtractionJumpState : ObjectiveState, IDisposable
    {
        private readonly IExtractionZone zone;
        private readonly Action onCommit;
        private readonly InputAction jump;
        private bool committed;

        public ExtractionJumpState(IExtractionZone zone, Action onCommit, Action onEnter = null) : base(onEnter)
        {
            this.zone = zone ?? throw new ArgumentNullException(nameof(zone));
            this.onCommit = onCommit ?? throw new ArgumentNullException(nameof(onCommit));
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/f");
            jump.performed += Commit;
        }

        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
        public override bool IsComplete => committed;
        public override string StatusLine => zone.IsPlayerInZone ? "PRESS F TO JUMP" : "REACH THE JUMP GATE";

        public override void Enter()
        {
            committed = false;
            base.Enter();
            jump.Enable();
        }

        public override void Tick(float deltaTime) { }
        public override void Exit() => jump.Disable();
        public void Dispose() => jump.Dispose();

        private void Commit(InputAction.CallbackContext context)
        {
            if (!zone.IsPlayerInZone) return;
            committed = true;
            jump.Disable();
            onCommit();
        }
    }
}
