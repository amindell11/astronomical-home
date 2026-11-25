using System;

namespace Objectives.States
{
    public class CompletedState : ObjectiveState
    {
        public CompletedState(Action onEnter = null) : base(onEnter) { }
        public override ObjectiveType StateType => ObjectiveType.Completed;
        public override void Tick(float deltaTime) { }
        public override bool IsComplete => false;
    }
}
